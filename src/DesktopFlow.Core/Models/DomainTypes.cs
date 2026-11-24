namespace DesktopFlow.Core.Models;

public enum MonitorScope
{
    Desktop = 0,
    Downloads = 1,
}

public enum ActivityKind
{
    Move = 0,
    Undo = 1,
    Skip = 2,
    Recovery = 3,
}

public enum ActivityStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Skipped = 3,
}

public enum RuleMatchType
{
    ExtensionEquals = 0,
    ExtensionIn = 1,
    NameContains = 2,
    NameStartsWith = 3,
    NameEquals = 4,
    SizeGreaterThan = 5,
    SizeLessThan = 6,
    Always = 7,
}

public enum RuleActionType
{
    MoveToRelativeFolder = 0,
    Skip = 1,
}

public enum SkipReason
{
    Directory = 0,
    Shortcut = 1,
    HiddenOrSystem = 2,
    DesktopIni = 3,
    Excluded = 4,
    ReparsePoint = 5,
    RuleSkip = 6,
    PartialDownload = 7,
    CloudPlaceholder = 8,
    AlreadyInDestination = 9,
    DestinationEscapesRoot = 10,
}

public enum FileLockState
{
    Available = 0,
    Locked = 1,
    NotFound = 2,
    AccessDenied = 3,
}

public enum StabilityState
{
    Stable = 0,
    Retry = 1,
    Gone = 2,
    AccessDenied = 3,
}

public enum MonitorRunState
{
    Off = 0,
    Paused = 1,
    Watching = 2,
    Error = 3,
}

public enum AppThemePreference
{
    Dark = 0,
    Light = 1,
    System = 2,
}

public enum UndoStatus
{
    Restored = 0,
    NeedsAlternateName = 1,
    Failed = 2,
    NotAllowed = 3,
}

public enum RecoveryAction
{
    MarkSucceeded = 0,
    MarkFailed = 1,
    DeleteTempAndFail = 2,
    PromoteTempAndSucceed = 3,
}

public sealed class ExtensionMapping
{
    public string Extension { get; set; } = string.Empty;

    public string CategoryKey { get; set; } = string.Empty;

    public string RelativePath { get; set; } = "Other";
}

public sealed class AutomationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public int Priority { get; set; } = 100;

    public MonitorScope Scope { get; set; } = MonitorScope.Desktop;

    public RuleMatchType MatchType { get; set; } = RuleMatchType.ExtensionEquals;

    public string MatchValue { get; set; } = string.Empty;

    public RuleActionType ActionType { get; set; } = RuleActionType.MoveToRelativeFolder;

    public string RelativePath { get; set; } = string.Empty;

    public bool IsBuiltIn { get; set; }
}

public sealed class FileInspection
{
    public required string FullPath { get; init; }

    public required string Name { get; init; }

    public required string Extension { get; init; }

    public long Length { get; init; }

    public bool IsDirectory { get; init; }

    public bool IsHidden { get; init; }

    public bool IsSystem { get; init; }

    public bool IsReparsePoint { get; init; }

    public bool IsCloudPlaceholder { get; init; }
}

public sealed class PlannedMove
{
    public required string SourcePath { get; init; }

    public required string FileName { get; init; }

    public string DestinationDirectory { get; init; } = string.Empty;

    public string DestinationFileName { get; init; } = string.Empty;

    public string? CategoryKey { get; init; }

    public string? RelativeFolder { get; init; }

    public string? RuleName { get; init; }

    public SkipReason? SkipReason { get; init; }

    public long Length { get; init; }

    public bool WillMove => SkipReason is null;

    public string DestinationPath =>
        string.IsNullOrEmpty(DestinationDirectory)
            ? string.Empty
            : Path.Combine(DestinationDirectory, DestinationFileName);
}

public sealed class ActivityRecord
{
    public long Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public string DestinationPath { get; set; } = string.Empty;

    public string? TempPath { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public ActivityKind Kind { get; set; }

    public ActivityStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    public string? CategoryKey { get; set; }

    public MonitorScope? Scope { get; set; }

    public bool CanUndo { get; set; }

    public bool IsUndone { get; set; }

    public long? UndoOfActivityId { get; set; }
}

public sealed record RuleConflict(string FirstRule, string SecondRule, string Description, bool SamePriority, string? Detail = null);

public sealed record RuleSelection(AutomationRule Rule, bool HasCompetitors);

public readonly record struct FileClassification(string CategoryKey, string RelativePath);

public sealed record MoveExecution(bool Succeeded, string? DestinationPath, string? ErrorMessage, string? TempPath);

public sealed record UndoResult(UndoStatus Status, string? RestoredPath, string? ErrorMessage);

public sealed record StabilityResult(StabilityState State, string? Detail);

public sealed record StorageSlice(string Name, long Bytes, int FileCount);

public sealed record UpdateCheckResult(bool ReachedNetwork, bool UpdateAvailable, string? LatestVersion, string? Message);

public sealed record RecoveryDecision(RecoveryAction Action, string? Message);

public sealed class ActivityQuery
{
    public string? FileNameContains { get; init; }

    public string? CategoryKey { get; init; }

    public ActivityKind? Kind { get; init; }

    public ActivityStatus? Status { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public int Limit { get; init; } = 500;
}

public sealed class OrganizeBatchResult
{
    public int Moved { get; init; }

    public int Skipped { get; init; }

    public int Failed { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<PlannedMove> Plans { get; init; } = [];
}

public sealed class OrganizeFileResult
{
    public static OrganizeFileResult Ignored { get; } = new() { WasIgnored = true };

    public bool WasIgnored { get; init; }

    public bool ShouldRetry { get; init; }

    public ActivityRecord? Activity { get; init; }
}

public sealed class DashboardSnapshot
{
    public int DesktopFileCount { get; init; }

    public int PendingDesktop { get; init; }

    public int PendingDownloads { get; init; }

    public int OrganizedCount { get; init; }

    public int ActiveRuleCount { get; init; }

    public DateTimeOffset? LastOrganizedAt { get; init; }

    public MonitorRunState DesktopMonitor { get; init; }

    public MonitorRunState DownloadsMonitor { get; init; }

    public bool AutomationPaused { get; init; }

    public IReadOnlyList<ActivityRecord> Recent { get; init; } = [];

    public IReadOnlyList<StorageSlice> Storage { get; init; } = [];
}

public sealed record SafetyOptions(bool KeepShortcuts, IReadOnlyList<string> ExcludedPatterns);
