using System.Diagnostics;
using System.Globalization;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Classification;
using DesktopFlow.Core.Models;

namespace DesktopFlow.Core.Rules;

public sealed class RuleEngine : IRuleEngine
{
    public bool Matches(AutomationRule rule, FileInspection file)
    {
        if (!rule.IsEnabled || file.IsDirectory)
        {
            return false;
        }

        var value = rule.MatchValue.Trim();
        switch (rule.MatchType)
        {
            case RuleMatchType.ExtensionEquals:
                return string.Equals(
                    FileClassifier.NormalizeExtension(value),
                    FileClassifier.NormalizeExtension(file.Extension),
                    StringComparison.OrdinalIgnoreCase);
            case RuleMatchType.ExtensionIn:
                return SplitExtensions(value).Contains(FileClassifier.NormalizeExtension(file.Extension));
            case RuleMatchType.NameContains:
                return value.Length > 0 && file.Name.Contains(value, StringComparison.OrdinalIgnoreCase);
            case RuleMatchType.NameStartsWith:
                return value.Length > 0 && file.Name.StartsWith(value, StringComparison.OrdinalIgnoreCase);
            case RuleMatchType.NameEquals:
                return file.Name.Equals(value, StringComparison.OrdinalIgnoreCase);
            case RuleMatchType.SizeGreaterThan:
                return TryParseSize(value, out var minimum) && file.Length > minimum;
            case RuleMatchType.SizeLessThan:
                return TryParseSize(value, out var maximum) && file.Length < maximum;
            case RuleMatchType.Always:
                return true;
            default:
                throw new UnreachableException(rule.MatchType.ToString());
        }
    }

    public RuleSelection? Select(IReadOnlyList<AutomationRule> rules, FileInspection file, MonitorScope scope)
    {
        var matches = rules.Where(rule => rule.Scope == scope && Matches(rule, file)).ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        var best = matches
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => Specificity(rule.MatchType))
            .ThenBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase)
            .First();

        return new RuleSelection(best, matches.Count > 1);
    }

    public IReadOnlyList<RuleConflict> FindConflicts(
        IReadOnlyList<AutomationRule> rules,
        IReadOnlyList<ExtensionMapping> mappings)
    {
        var conflicts = new List<RuleConflict>();
        var enabled = rules.Where(rule => rule.IsEnabled).ToList();
        for (var leftIndex = 0; leftIndex < enabled.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < enabled.Count; rightIndex++)
            {
                var left = enabled[leftIndex];
                var right = enabled[rightIndex];
                if (left.Scope != right.Scope || !PatternsCanOverlap(left, right) || SameOutcome(left, right))
                {
                    continue;
                }

                var samePriority = left.Priority == right.Priority;
                var description = samePriority
                    ? $"'{left.Name}' and '{right.Name}' can match the same file and have the same priority."
                    : $"'{Preferred(left, right).Name}' overrides '{Other(left, right).Name}' when both match.";
                var winner = samePriority ? left : Preferred(left, right);
                var loser = samePriority ? right : Other(left, right);
                conflicts.Add(new RuleConflict(winner.Name, loser.Name, description, samePriority));
            }
        }

        foreach (var rule in enabled.Where(rule => rule.ActionType == RuleActionType.MoveToRelativeFolder && IsExtension(rule.MatchType)))
        {
            foreach (var extension in ExtensionSet(rule))
            {
                var mapping = mappings.FirstOrDefault(item =>
                    string.Equals(FileClassifier.NormalizeExtension(item.Extension), extension, StringComparison.OrdinalIgnoreCase));
                if (mapping is null)
                {
                    continue;
                }

                if (string.Equals(NormalizePath(mapping.RelativePath), NormalizePath(rule.RelativePath), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                conflicts.Add(new RuleConflict(
                    rule.Name,
                    mapping.RelativePath,
                    $"'{rule.Name}' overrides the {extension} folder mapping ({mapping.RelativePath}).",
                    false,
                    extension));
            }
        }

        return conflicts
            .OrderByDescending(conflict => conflict.SamePriority)
            .ThenBy(conflict => conflict.FirstRule, StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToList();
    }

    public static int Specificity(RuleMatchType matchType)
    {
        return matchType switch
        {
            RuleMatchType.NameEquals => 0,
            RuleMatchType.NameStartsWith => 1,
            RuleMatchType.NameContains => 2,
            RuleMatchType.ExtensionEquals => 3,
            RuleMatchType.ExtensionIn => 4,
            RuleMatchType.SizeGreaterThan => 5,
            RuleMatchType.SizeLessThan => 5,
            RuleMatchType.Always => 6,
            _ => throw new UnreachableException(matchType.ToString()),
        };
    }

    private static AutomationRule Preferred(AutomationRule left, AutomationRule right)
    {
        if (left.Priority != right.Priority)
        {
            return left.Priority < right.Priority ? left : right;
        }

        return Specificity(left.MatchType) <= Specificity(right.MatchType) ? left : right;
    }

    private static AutomationRule Other(AutomationRule left, AutomationRule right)
    {
        var preferred = Preferred(left, right);
        return ReferenceEquals(preferred, left) ? right : left;
    }

    private static bool SameOutcome(AutomationRule left, AutomationRule right)
    {
        if (left.ActionType != right.ActionType)
        {
            return false;
        }

        if (left.ActionType == RuleActionType.Skip)
        {
            return true;
        }

        return string.Equals(NormalizePath(left.RelativePath), NormalizePath(right.RelativePath), StringComparison.OrdinalIgnoreCase);
    }

    private static bool PatternsCanOverlap(AutomationRule left, AutomationRule right)
    {
        if (left.MatchType == RuleMatchType.Always || right.MatchType == RuleMatchType.Always)
        {
            return true;
        }

        if (IsExtension(left.MatchType) && IsExtension(right.MatchType))
        {
            return ExtensionSet(left).Overlaps(ExtensionSet(right));
        }

        if (IsName(left.MatchType) && IsName(right.MatchType))
        {
            return NamePatternsOverlap(left, right);
        }

        if (IsSize(left.MatchType) && IsSize(right.MatchType))
        {
            return SizeRangesOverlap(left, right);
        }

        return true;
    }

    private static bool NamePatternsOverlap(AutomationRule left, AutomationRule right)
    {
        var a = left.MatchValue.Trim();
        var b = right.MatchValue.Trim();
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        if (left.MatchType == RuleMatchType.NameEquals && right.MatchType == RuleMatchType.NameEquals)
        {
            return a.Equals(b, StringComparison.OrdinalIgnoreCase);
        }

        if (left.MatchType == RuleMatchType.NameEquals)
        {
            return NameMatches(right, a);
        }

        if (right.MatchType == RuleMatchType.NameEquals)
        {
            return NameMatches(left, b);
        }

        if (left.MatchType == RuleMatchType.NameStartsWith && right.MatchType == RuleMatchType.NameStartsWith)
        {
            return a.StartsWith(b, StringComparison.OrdinalIgnoreCase) || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
        }

        if (left.MatchType == RuleMatchType.NameContains && right.MatchType == RuleMatchType.NameContains)
        {
            return a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool NameMatches(AutomationRule rule, string name)
    {
        var value = rule.MatchValue.Trim();
        switch (rule.MatchType)
        {
            case RuleMatchType.NameContains:
                return name.Contains(value, StringComparison.OrdinalIgnoreCase);
            case RuleMatchType.NameStartsWith:
                return name.StartsWith(value, StringComparison.OrdinalIgnoreCase);
            case RuleMatchType.NameEquals:
                return name.Equals(value, StringComparison.OrdinalIgnoreCase);
            case RuleMatchType.ExtensionEquals:
            case RuleMatchType.ExtensionIn:
            case RuleMatchType.SizeGreaterThan:
            case RuleMatchType.SizeLessThan:
            case RuleMatchType.Always:
                return false;
            default:
                throw new UnreachableException(rule.MatchType.ToString());
        }
    }

    private static bool SizeRangesOverlap(AutomationRule left, AutomationRule right)
    {
        var leftRange = SizeRange(left);
        var rightRange = SizeRange(right);
        return leftRange.Minimum < rightRange.Maximum && rightRange.Minimum < leftRange.Maximum;
    }

    private static (long Minimum, long Maximum) SizeRange(AutomationRule rule)
    {
        var parsed = TryParseSize(rule.MatchValue, out var size) ? size : 0;
        return rule.MatchType switch
        {
            RuleMatchType.SizeGreaterThan => (parsed + 1, long.MaxValue),
            RuleMatchType.SizeLessThan => (0, parsed),
            _ => (0, long.MaxValue),
        };
    }

    private static bool IsExtension(RuleMatchType matchType)
    {
        return matchType is RuleMatchType.ExtensionEquals or RuleMatchType.ExtensionIn;
    }

    private static bool IsName(RuleMatchType matchType)
    {
        return matchType is RuleMatchType.NameContains or RuleMatchType.NameStartsWith or RuleMatchType.NameEquals;
    }

    private static bool IsSize(RuleMatchType matchType)
    {
        return matchType is RuleMatchType.SizeGreaterThan or RuleMatchType.SizeLessThan;
    }

    private static HashSet<string> ExtensionSet(AutomationRule rule)
    {
        return rule.MatchType switch
        {
            RuleMatchType.ExtensionEquals => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                FileClassifier.NormalizeExtension(rule.MatchValue),
            },
            RuleMatchType.ExtensionIn => SplitExtensions(rule.MatchValue),
            _ => [],
        };
    }

    private static HashSet<string> SplitExtensions(string value)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var extension = FileClassifier.NormalizeExtension(part);
            if (extension.Length > 0)
            {
                set.Add(extension);
            }
        }

        return set;
    }

    private static bool TryParseSize(string value, out long size)
    {
        return long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim().Trim('/');
    }
}
