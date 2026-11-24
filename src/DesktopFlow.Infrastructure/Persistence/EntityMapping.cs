using DesktopFlow.Core.Models;

namespace DesktopFlow.Infrastructure.Persistence;

internal static class EntityMapping
{
    public static ActivityRecord ToModel(ActivityEntity entity)
    {
        return new ActivityRecord
        {
            Id = entity.Id,
            FileName = entity.FileName,
            SourcePath = entity.SourcePath,
            DestinationPath = entity.DestinationPath,
            TempPath = entity.TempPath,
            Timestamp = new DateTimeOffset(DateTime.SpecifyKind(entity.TimestampUtc, DateTimeKind.Utc)),
            Kind = Parse<ActivityKind>(entity.Kind),
            Status = Parse<ActivityStatus>(entity.Status),
            ErrorMessage = entity.ErrorMessage,
            CategoryKey = entity.CategoryKey,
            Scope = string.IsNullOrWhiteSpace(entity.Scope) ? null : Parse<MonitorScope>(entity.Scope),
            CanUndo = entity.CanUndo,
            IsUndone = entity.IsUndone,
            UndoOfActivityId = entity.UndoOfActivityId,
        };
    }

    public static void CopyTo(ActivityRecord model, ActivityEntity entity)
    {
        entity.FileName = model.FileName;
        entity.SourcePath = model.SourcePath;
        entity.DestinationPath = model.DestinationPath;
        entity.TempPath = model.TempPath;
        entity.TimestampUtc = model.Timestamp.UtcDateTime;
        entity.Kind = model.Kind.ToString();
        entity.Status = model.Status.ToString();
        entity.ErrorMessage = model.ErrorMessage;
        entity.CategoryKey = model.CategoryKey;
        entity.Scope = model.Scope?.ToString();
        entity.CanUndo = model.CanUndo;
        entity.IsUndone = model.IsUndone;
        entity.UndoOfActivityId = model.UndoOfActivityId;
    }

    public static AutomationRule ToModel(RuleEntity entity)
    {
        return new AutomationRule
        {
            Id = entity.Id,
            Name = entity.Name,
            IsEnabled = entity.IsEnabled,
            Priority = entity.Priority,
            Scope = Parse<MonitorScope>(entity.Scope),
            MatchType = Parse<RuleMatchType>(entity.MatchType),
            MatchValue = entity.MatchValue,
            ActionType = Parse<RuleActionType>(entity.ActionType),
            RelativePath = entity.RelativePath,
            IsBuiltIn = entity.IsBuiltIn,
        };
    }

    public static RuleEntity ToEntity(AutomationRule model)
    {
        return new RuleEntity
        {
            Id = model.Id,
            Name = model.Name,
            IsEnabled = model.IsEnabled,
            Priority = model.Priority,
            Scope = model.Scope.ToString(),
            MatchType = model.MatchType.ToString(),
            MatchValue = model.MatchValue,
            ActionType = model.ActionType.ToString(),
            RelativePath = model.RelativePath,
            IsBuiltIn = model.IsBuiltIn,
        };
    }

    public static void CopyTo(AutomationRule model, RuleEntity entity)
    {
        entity.Name = model.Name;
        entity.IsEnabled = model.IsEnabled;
        entity.Priority = model.Priority;
        entity.Scope = model.Scope.ToString();
        entity.MatchType = model.MatchType.ToString();
        entity.MatchValue = model.MatchValue;
        entity.ActionType = model.ActionType.ToString();
        entity.RelativePath = model.RelativePath;
        entity.IsBuiltIn = model.IsBuiltIn;
    }

    private static TEnum Parse<TEnum>(string value)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Unknown {typeof(TEnum).Name} value '{value}'.");
    }
}
