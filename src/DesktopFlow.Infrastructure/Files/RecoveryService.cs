using System.Diagnostics;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Safety;

namespace DesktopFlow.Infrastructure.Files;

public sealed class RecoveryService : IRecoveryService
{
    private readonly IActivityStore _activities;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;

    public RecoveryService(IActivityStore activities, IClock clock, IAppLogger logger)
    {
        _activities = activities;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> RecoverAsync(CancellationToken cancellationToken)
    {
        var pending = await _activities.GetPendingAsync(cancellationToken);
        var recovered = 0;
        foreach (var record in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceExists = File.Exists(record.SourcePath);
            var destinationExists = !string.IsNullOrWhiteSpace(record.DestinationPath) && File.Exists(record.DestinationPath);
            var tempExists = IsOurTemp(record.TempPath) && File.Exists(record.TempPath);
            var decision = RecoveryPlanner.Decide(sourceExists, destinationExists, tempExists);
            switch (decision.Action)
            {
                case RecoveryAction.DeleteTempAndFail:
                    DeleteOurTemp(record.TempPath);
                    record.Status = ActivityStatus.Failed;
                    record.CanUndo = false;
                    record.ErrorMessage = decision.Message;
                    break;
                case RecoveryAction.PromoteTempAndSucceed:
                    if (!PromoteTemp(record))
                    {
                        record.Status = ActivityStatus.Failed;
                        record.CanUndo = false;
                        record.ErrorMessage = "The temporary file could not be restored. The original was not deleted by DesktopFlow.";
                        break;
                    }

                    record.Status = ActivityStatus.Succeeded;
                    record.CanUndo = true;
                    record.ErrorMessage = decision.Message;
                    break;
                case RecoveryAction.MarkSucceeded:
                    record.Status = ActivityStatus.Succeeded;
                    record.CanUndo = true;
                    record.ErrorMessage = decision.Message;
                    break;
                case RecoveryAction.MarkFailed:
                    record.Status = ActivityStatus.Failed;
                    record.CanUndo = false;
                    record.ErrorMessage = decision.Message;
                    break;
                default:
                    throw new UnreachableException(decision.Action.ToString());
            }

            record.Timestamp = _clock.UtcNow;
            await _activities.UpdateAsync(record, cancellationToken);
            await _activities.AddAsync(new ActivityRecord
            {
                FileName = record.FileName,
                SourcePath = record.SourcePath,
                DestinationPath = record.DestinationPath,
                Timestamp = _clock.UtcNow,
                Kind = DesktopFlow.Core.Models.ActivityKind.Recovery,
                Status = record.Status,
                ErrorMessage = record.ErrorMessage,
                CategoryKey = record.CategoryKey,
                Scope = record.Scope,
                CanUndo = false,
            }, cancellationToken);
            _logger.Info($"Recovered interrupted move for '{record.FileName}': {record.Status}.");
            recovered++;
        }

        return recovered;
    }

    private static bool PromoteTemp(ActivityRecord record)
    {
        if (!IsOurTemp(record.TempPath) || string.IsNullOrWhiteSpace(record.DestinationPath) || File.Exists(record.DestinationPath))
        {
            return false;
        }

        try
        {
            var directory = Path.GetDirectoryName(record.DestinationPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            Directory.CreateDirectory(directory);
            File.Move(record.TempPath!, record.DestinationPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsOurTemp(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && path.EndsWith(".desktopflow-partial", StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteOurTemp(string? path)
    {
        if (!IsOurTemp(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
