using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Organization;

namespace DesktopFlow.Infrastructure.Files;

public sealed class UndoService : IUndoService
{
    private readonly IActivityStore _activities;
    private readonly IFileMover _mover;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;

    public UndoService(IActivityStore activities, IFileMover mover, IClock clock, IAppLogger logger)
    {
        _activities = activities;
        _mover = mover;
        _clock = clock;
        _logger = logger;
    }

    public async Task<UndoResult> UndoAsync(long activityId, bool useAlternateName, CancellationToken cancellationToken)
    {
        var record = await _activities.GetAsync(activityId, cancellationToken);
        if (record is null
            || record.Kind != ActivityKind.Move
            || record.Status != ActivityStatus.Succeeded
            || !record.CanUndo
            || record.IsUndone)
        {
            return new UndoResult(UndoStatus.NotAllowed, null, "This action cannot be undone.");
        }

        if (!File.Exists(record.DestinationPath))
        {
            return new UndoResult(UndoStatus.Failed, null, "The file is no longer at the organized location.");
        }

        var originalDirectory = Path.GetDirectoryName(record.SourcePath);
        if (string.IsNullOrWhiteSpace(originalDirectory))
        {
            return new UndoResult(UndoStatus.Failed, null, "The original folder is not available.");
        }

        try
        {
            Directory.CreateDirectory(originalDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new UndoResult(UndoStatus.Failed, null, ex.Message);
        }

        var targetName = Path.GetFileName(record.SourcePath);
        var originalPath = Path.Combine(originalDirectory, targetName);
        if (File.Exists(originalPath))
        {
            if (!useAlternateName)
            {
                return new UndoResult(UndoStatus.NeedsAlternateName, null, "The original name is already used.");
            }

            targetName = FileNameConflictResolver.Resolve(
                targetName,
                name => File.Exists(Path.Combine(originalDirectory, name)));
        }

        var execution = await _mover.MoveAsync(
            record.DestinationPath,
            originalDirectory,
            targetName,
            null,
            cancellationToken);

        var undo = new ActivityRecord
        {
            FileName = record.FileName,
            SourcePath = record.DestinationPath,
            DestinationPath = execution.DestinationPath ?? Path.Combine(originalDirectory, targetName),
            Timestamp = _clock.UtcNow,
            Kind = ActivityKind.Undo,
            Status = execution.Succeeded ? ActivityStatus.Succeeded : ActivityStatus.Failed,
            ErrorMessage = execution.ErrorMessage,
            CategoryKey = record.CategoryKey,
            Scope = record.Scope,
            CanUndo = false,
            UndoOfActivityId = record.Id,
        };
        await _activities.AddAsync(undo, cancellationToken);

        if (!execution.Succeeded)
        {
            _logger.Error($"Undo failed for '{record.FileName}': {execution.ErrorMessage}");
            return new UndoResult(UndoStatus.Failed, null, execution.ErrorMessage);
        }

        record.IsUndone = true;
        record.CanUndo = false;
        await _activities.UpdateAsync(record, cancellationToken);
        _logger.Info($"Restored '{record.FileName}' to '{undo.DestinationPath}'.");
        return new UndoResult(UndoStatus.Restored, undo.DestinationPath, null);
    }
}
