using DesktopFlow.Core.Models;
using DesktopFlow.Core.Storage;

namespace DesktopFlow.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IAppLogger
{
    void Info(string message);

    void Error(string message, Exception? exception = null);
}

public interface ISettingsStore
{
    AppPreferences Current { get; }

    Task<AppPreferences> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken);
}

public interface IActivityStore
{
    Task<ActivityRecord> AddAsync(ActivityRecord record, CancellationToken cancellationToken);

    Task UpdateAsync(ActivityRecord record, CancellationToken cancellationToken);

    Task<ActivityRecord?> GetAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ActivityRecord>> QueryAsync(ActivityQuery query, CancellationToken cancellationToken);

    Task<int> CountSucceededMovesAsync(CancellationToken cancellationToken);

    Task<DateTimeOffset?> LastSucceededMoveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ActivityRecord>> GetPendingAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ActivityRecord>> GetRecentAsync(int count, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken);
}

public interface IRuleStore
{
    Task<IReadOnlyList<AutomationRule>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AutomationRule>> GetEnabledAsync(MonitorScope scope, CancellationToken cancellationToken);

    Task SaveAsync(AutomationRule rule, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface IFileScanner
{
    IReadOnlyList<FileInspection> ListTopLevel(string root);

    FileInspection? Inspect(string path);

    IReadOnlyList<StorageSlice> Summarize(string root);
}

public interface IFileLockProbe
{
    FileLockState Probe(string path);
}

public interface IFileStabilityWaiter
{
    Task<StabilityResult> WaitAsync(string path, TimeSpan settle, CancellationToken cancellationToken);
}

public interface IFileMover
{
    Task<MoveExecution> MoveAsync(
        string sourcePath,
        string destinationDirectory,
        string destinationFileName,
        Func<string, CancellationToken, Task>? onTempCreated,
        CancellationToken cancellationToken);
}

public interface IFileClassifier
{
    FileClassification Classify(string fileName, IReadOnlyList<ExtensionMapping> mappings);
}

public interface IRuleEngine
{
    bool Matches(AutomationRule rule, FileInspection file);

    RuleSelection? Select(IReadOnlyList<AutomationRule> rules, FileInspection file, MonitorScope scope);

    IReadOnlyList<RuleConflict> FindConflicts(
        IReadOnlyList<AutomationRule> rules,
        IReadOnlyList<ExtensionMapping> mappings);
}

public interface IOrganizationService
{
    Task<IReadOnlyList<PlannedMove>> PreviewAsync(MonitorScope scope, CancellationToken cancellationToken);

    Task<OrganizeBatchResult> OrganizeAsync(
        MonitorScope scope,
        IReadOnlyCollection<string>? sourcePaths,
        CancellationToken cancellationToken);

    Task<OrganizeFileResult> OrganizeFileIfNeededAsync(string path, CancellationToken cancellationToken);
}

public interface IUndoService
{
    Task<UndoResult> UndoAsync(long activityId, bool useAlternateName, CancellationToken cancellationToken);
}

public interface IRecoveryService
{
    Task<int> RecoverAsync(CancellationToken cancellationToken);
}

public interface IMonitorController
{
    bool IsPaused { get; }

    MonitorRunState DesktopState { get; }

    MonitorRunState DownloadsState { get; }

    string? LastError { get; }

    event EventHandler? StateChanged;

    event EventHandler<ActivityRecord>? ActivityRecorded;

    void Pause();

    void Resume();

    Task OrganizeNowAsync(MonitorScope scope, CancellationToken cancellationToken);

    void Reload();
}

public interface IStartupRegistration
{
    string? LastMessage { get; }

    Task<bool> GetEnabledAsync(CancellationToken cancellationToken);

    Task<bool> SetEnabledAsync(bool enabled, bool minimized, CancellationToken cancellationToken);
}

public interface INotificationService
{
    void Show(string title, string body);
}

public interface IUpdateChecker
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken);
}

public interface IShellLauncher
{
    void OpenFolder(string path);

    void Reveal(string path);

    void OpenUrl(string url);
}

public interface IStorageAnalyzer
{
    IReadOnlyList<DriveSpace> ListDrives();

    IReadOnlyList<string> FixedDriveRoots();

    Task<StorageReport> AnalyzeAsync(
        IReadOnlyList<string> roots,
        IProgress<StorageScanProgress>? progress,
        CancellationToken cancellationToken);
}
