using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Organization;

namespace DesktopFlow.Infrastructure.Files;

public sealed class OrganizationService : IOrganizationService
{
    private readonly ISettingsStore _settings;
    private readonly IRuleStore _rules;
    private readonly IActivityStore _activities;
    private readonly IFileScanner _scanner;
    private readonly IFileMover _mover;
    private readonly IFileStabilityWaiter _stability;
    private readonly OrganizationPlanner _planner;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;

    public OrganizationService(
        ISettingsStore settings,
        IRuleStore rules,
        IActivityStore activities,
        IFileScanner scanner,
        IFileMover mover,
        IFileStabilityWaiter stability,
        OrganizationPlanner planner,
        IClock clock,
        IAppLogger logger)
    {
        _settings = settings;
        _rules = rules;
        _activities = activities;
        _scanner = scanner;
        _mover = mover;
        _stability = stability;
        _planner = planner;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlannedMove>> PreviewAsync(MonitorScope scope, CancellationToken cancellationToken)
    {
        var preferences = await _settings.GetAsync(cancellationToken);
        var root = preferences.RootFor(scope);
        var files = _scanner.ListTopLevel(root);
        var rules = await _rules.GetEnabledAsync(scope, cancellationToken);
        return _planner.Plan(
            root,
            files,
            preferences.MappingsFor(scope),
            rules,
            Safety(preferences),
            scope,
            File.Exists);
    }

    public async Task<OrganizeBatchResult> OrganizeAsync(
        MonitorScope scope,
        IReadOnlyCollection<string>? sourcePaths,
        CancellationToken cancellationToken)
    {
        var preferences = await _settings.GetAsync(cancellationToken);
        var plans = await PreviewAsync(scope, cancellationToken);
        var selected = plans.Where(plan => sourcePaths is null || sourcePaths.Contains(plan.SourcePath, StringComparer.OrdinalIgnoreCase)).ToList();
        var moved = 0;
        var skipped = 0;
        var failed = 0;
        var errors = new List<string>();
        foreach (var plan in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!plan.WillMove)
            {
                skipped++;
                continue;
            }

            var activity = await MovePlanAsync(scope, plan, preferences, waitForStability: false, cancellationToken);
            if (activity is null)
            {
                skipped++;
                continue;
            }

            if (activity.Status == ActivityStatus.Succeeded)
            {
                moved++;
            }
            else
            {
                failed++;
                if (!string.IsNullOrWhiteSpace(activity.ErrorMessage))
                {
                    errors.Add($"{plan.FileName}: {activity.ErrorMessage}");
                }
            }
        }

        return new OrganizeBatchResult
        {
            Moved = moved,
            Skipped = skipped,
            Failed = failed,
            Errors = errors,
            Plans = plans,
        };
    }

    public async Task<OrganizeFileResult> OrganizeFileIfNeededAsync(string path, CancellationToken cancellationToken)
    {
        var preferences = await _settings.GetAsync(cancellationToken);
        if (!preferences.HasCompletedSetup || !preferences.AutomaticOrganization)
        {
            return OrganizeFileResult.Ignored;
        }

        var scope = ResolveScope(path, preferences);
        if (scope is null || !preferences.MonitoringEnabled(scope.Value))
        {
            return OrganizeFileResult.Ignored;
        }

        var root = preferences.RootFor(scope.Value);
        var parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (parent is null || !string.Equals(parent, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
        {
            return OrganizeFileResult.Ignored;
        }

        var stability = await _stability.WaitAsync(path, TimeSpan.FromMilliseconds(Math.Min(preferences.ClampDelay(), 1500)), cancellationToken);
        if (stability.State == StabilityState.Retry)
        {
            return new OrganizeFileResult { ShouldRetry = true };
        }

        if (stability.State != StabilityState.Stable)
        {
            return OrganizeFileResult.Ignored;
        }

        var inspection = _scanner.Inspect(path);
        if (inspection is null || inspection.IsDirectory)
        {
            return OrganizeFileResult.Ignored;
        }

        var rules = await _rules.GetEnabledAsync(scope.Value, cancellationToken);
        var plan = _planner.Plan(
            root,
            [inspection],
            preferences.MappingsFor(scope.Value),
            rules,
            Safety(preferences),
            scope.Value,
            File.Exists).SingleOrDefault();
        if (plan is null || !plan.WillMove)
        {
            return OrganizeFileResult.Ignored;
        }

        var activity = await MovePlanAsync(scope.Value, plan, preferences, waitForStability: false, cancellationToken);
        return new OrganizeFileResult { Activity = activity };
    }

    private async Task<ActivityRecord?> MovePlanAsync(
        MonitorScope scope,
        PlannedMove plan,
        AppPreferences preferences,
        bool waitForStability,
        CancellationToken cancellationToken)
    {
        if (waitForStability)
        {
            var stability = await _stability.WaitAsync(plan.SourcePath, TimeSpan.FromMilliseconds(preferences.ClampDelay()), cancellationToken);
            if (stability.State != StabilityState.Stable)
            {
                return null;
            }
        }

        if (!File.Exists(plan.SourcePath))
        {
            return null;
        }

        var record = new ActivityRecord
        {
            FileName = plan.FileName,
            SourcePath = plan.SourcePath,
            DestinationPath = plan.DestinationPath,
            Timestamp = _clock.UtcNow,
            Kind = ActivityKind.Move,
            Status = ActivityStatus.Pending,
            CategoryKey = plan.CategoryKey,
            Scope = scope,
            CanUndo = false,
        };

        try
        {
            record = await _activities.AddAsync(record, cancellationToken);
            var execution = await _mover.MoveAsync(
                plan.SourcePath,
                plan.DestinationDirectory,
                plan.DestinationFileName,
                async (temp, token) =>
                {
                    record.TempPath = temp;
                    await _activities.UpdateAsync(record, token);
                },
                cancellationToken);

            record.DestinationPath = execution.DestinationPath ?? record.DestinationPath;
            record.Status = execution.Succeeded ? ActivityStatus.Succeeded : ActivityStatus.Failed;
            record.ErrorMessage = execution.ErrorMessage;
            record.CanUndo = execution.Succeeded && preferences.EnableUndoHistory;
            record.Timestamp = _clock.UtcNow;
            await _activities.UpdateAsync(record, cancellationToken);
            if (execution.Succeeded)
            {
                _logger.Info($"Moved '{plan.SourcePath}' to '{record.DestinationPath}'.");
            }
            else
            {
                _logger.Error($"Failed to move '{plan.SourcePath}': {execution.ErrorMessage}");
            }

            return record;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to move '{plan.SourcePath}'.", ex);
            record.Status = ActivityStatus.Failed;
            record.ErrorMessage = ex.Message;
            record.CanUndo = false;
            if (record.Id != 0)
            {
                await _activities.UpdateAsync(record, CancellationToken.None);
            }

            return record;
        }
    }

    private static SafetyOptions Safety(AppPreferences preferences)
    {
        return new SafetyOptions(preferences.KeepShortcuts, preferences.ExcludedPatterns);
    }

    private static MonitorScope? ResolveScope(string path, AppPreferences preferences)
    {
        string? directory;
        try
        {
            directory = Path.GetDirectoryName(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return null;
        }

        if (directory is null)
        {
            return null;
        }

        if (Matches(directory, preferences.DesktopPath))
        {
            return MonitorScope.Desktop;
        }

        if (Matches(directory, preferences.DownloadsPath))
        {
            return MonitorScope.Downloads;
        }

        return null;
    }

    private static bool Matches(string directory, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        try
        {
            return string.Equals(directory, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
