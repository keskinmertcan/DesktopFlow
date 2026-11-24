namespace DesktopFlow.Infrastructure.Persistence;

public sealed class PreferenceEntity
{
    public int Id { get; set; }

    public string Json { get; set; } = "{}";
}

public sealed class RuleEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public int Priority { get; set; }

    public string Scope { get; set; } = string.Empty;

    public string MatchType { get; set; } = string.Empty;

    public string MatchValue { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public bool IsBuiltIn { get; set; }
}

public sealed class ActivityEntity
{
    public long Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public string DestinationPath { get; set; } = string.Empty;

    public string? TempPath { get; set; }

    public DateTime TimestampUtc { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? CategoryKey { get; set; }

    public string? Scope { get; set; }

    public bool CanUndo { get; set; }

    public bool IsUndone { get; set; }

    public long? UndoOfActivityId { get; set; }
}
