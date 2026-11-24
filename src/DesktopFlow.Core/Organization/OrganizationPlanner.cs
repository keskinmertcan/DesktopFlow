using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Safety;

namespace DesktopFlow.Core.Organization;

public sealed class OrganizationPlanner
{
    private readonly IFileClassifier _classifier;
    private readonly IRuleEngine _rules;

    public OrganizationPlanner(IFileClassifier classifier, IRuleEngine rules)
    {
        _classifier = classifier;
        _rules = rules;
    }

    public IReadOnlyList<PlannedMove> Plan(
        string root,
        IReadOnlyList<FileInspection> files,
        IReadOnlyList<ExtensionMapping> mappings,
        IReadOnlyList<AutomationRule> rules,
        SafetyOptions safety,
        MonitorScope scope,
        Func<string, bool> fileExists)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plans = new List<PlannedMove>(files.Count);
        foreach (var file in files)
        {
            var skip = FileSafetyPolicy.Evaluate(file, safety);
            if (skip is not null)
            {
                plans.Add(Skipped(file, skip.Value, null, null));
                continue;
            }

            string relative;
            string? category;
            string? ruleName;
            var selection = _rules.Select(rules, file, scope);
            if (selection is not null)
            {
                ruleName = selection.Rule.Name;
                if (selection.Rule.ActionType == RuleActionType.Skip)
                {
                    plans.Add(Skipped(file, SkipReason.RuleSkip, null, ruleName));
                    continue;
                }

                relative = selection.Rule.RelativePath;
                category = selection.Rule.Name;
            }
            else
            {
                var classification = _classifier.Classify(file.Name, mappings);
                relative = classification.RelativePath;
                category = classification.CategoryKey;
                ruleName = null;
            }

            if (!DestinationBuilder.TryCombineUnderRoot(root, relative, out var directory, out _))
            {
                plans.Add(Skipped(file, SkipReason.DestinationEscapesRoot, category, ruleName));
                continue;
            }

            var sourceDirectory = Path.GetDirectoryName(file.FullPath);
            if (sourceDirectory is not null
                && string.Equals(Path.GetFullPath(sourceDirectory), directory, StringComparison.OrdinalIgnoreCase))
            {
                plans.Add(Skipped(file, SkipReason.AlreadyInDestination, category, ruleName));
                continue;
            }

            var uniqueName = FileNameConflictResolver.Resolve(
                file.Name,
                name =>
                {
                    var candidate = Path.Combine(directory, name);
                    return reserved.Contains(candidate) || fileExists(candidate);
                });
            var destination = Path.Combine(directory, uniqueName);
            reserved.Add(destination);
            plans.Add(new PlannedMove
            {
                SourcePath = file.FullPath,
                FileName = file.Name,
                DestinationDirectory = directory,
                DestinationFileName = uniqueName,
                CategoryKey = category,
                RelativeFolder = relative.Replace('\\', '/'),
                RuleName = ruleName,
                Length = file.Length,
            });
        }

        return plans;
    }

    private static PlannedMove Skipped(FileInspection file, SkipReason reason, string? category, string? ruleName)
    {
        return new PlannedMove
        {
            SourcePath = file.FullPath,
            FileName = file.Name,
            CategoryKey = category,
            RuleName = ruleName,
            SkipReason = reason,
            Length = file.Length,
        };
    }
}
