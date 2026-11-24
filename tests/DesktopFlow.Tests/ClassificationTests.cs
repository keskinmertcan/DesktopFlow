using DesktopFlow.Core.Classification;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Organization;
using DesktopFlow.Core.Rules;
using DesktopFlow.Core.Safety;
using DesktopFlow.Core.Startup;

namespace DesktopFlow.Tests;

public class ClassificationTests
{
    private readonly FileClassifier _classifier = new();
    private readonly List<ExtensionMapping> _desktop = DefaultTaxonomy.CreateDesktopMappings();
    private readonly List<ExtensionMapping> _downloads = DefaultTaxonomy.CreateDownloadsMappings();

    [Theory]
    [InlineData("brief.pdf", "Documents/PDF")]
    [InlineData("notes.docx", "Documents/Word")]
    [InlineData("budget.xlsx", "Documents/Excel")]
    [InlineData("deck.pptx", "Documents/PowerPoint")]
    [InlineData("readme.txt", "Documents/Text")]
    [InlineData("photo.png", "Images/PNG")]
    [InlineData("photo.jpg", "Images/JPG")]
    [InlineData("photo.jpeg", "Images/JPG")]
    [InlineData("icon.svg", "Images/SVG")]
    [InlineData("picture.webp", "Images/WEBP")]
    [InlineData("anim.gif", "Images/Other")]
    [InlineData("clip.mp4", "Videos")]
    [InlineData("song.mp3", "Audio")]
    [InlineData("bundle.zip", "Archives")]
    [InlineData("setup.exe", "Installers")]
    [InlineData("app.cs", "Development/CSharp")]
    [InlineData("script.py", "Development/Python")]
    [InlineData("site.js", "Development/JavaScript")]
    [InlineData("site.ts", "Development/JavaScript")]
    [InlineData("page.html", "Development/Other")]
    [InlineData("mystery.bin", "Other")]
    public void Desktop_extensions_use_the_documented_folders(string fileName, string expected)
    {
        var result = _classifier.Classify(fileName, _desktop);
        Assert.Equal(expected, result.RelativePath);
    }

    [Theory]
    [InlineData("brief.pdf", "PDF")]
    [InlineData("photo.png", "Images")]
    [InlineData("setup.msi", "Installers")]
    [InlineData("bundle.rar", "Archives")]
    [InlineData("clip.mkv", "Videos")]
    [InlineData("notes.docx", "Documents")]
    public void Downloads_extensions_use_their_own_folders(string fileName, string expected)
    {
        var result = _classifier.Classify(fileName, _downloads);
        Assert.Equal(expected, result.RelativePath);
    }

    [Theory]
    [InlineData("file.pdf.crdownload", true)]
    [InlineData("file.pdf.part", true)]
    [InlineData("file.pdf.tmp", true)]
    [InlineData("notes.tmp", false)]
    [InlineData("setup.exe", false)]
    public void Partial_downloads_are_detected(string fileName, bool expected)
    {
        Assert.Equal(expected, PartialDownloadDetector.IsPartial(fileName));
    }
}

public class FileNameConflictTests
{
    [Fact]
    public void Resolver_appends_a_number_and_never_returns_an_existing_name()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "report.pdf", "report (1).pdf" };
        var resolved = FileNameConflictResolver.Resolve("report.pdf", used.Contains);
        Assert.Equal("report (2).pdf", resolved);
        Assert.DoesNotContain(used, name => name.Equals(resolved, StringComparison.OrdinalIgnoreCase));
    }
}

public class DestinationBuilderTests
{
    [Fact]
    public void Destination_must_stay_inside_the_monitored_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "DesktopFlowRoot");
        Assert.True(DestinationBuilder.TryCombineUnderRoot(root, "Documents/PDF", out var inside, out _));
        Assert.StartsWith(Path.GetFullPath(root), inside, StringComparison.OrdinalIgnoreCase);

        Assert.False(DestinationBuilder.TryCombineUnderRoot(root, "../Outside", out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.False(DestinationBuilder.TryCombineUnderRoot(root, ".", out _, out _));
    }
}

public class RuleEngineTests
{
    private readonly RuleEngine _engine = new();

    [Fact]
    public void Higher_priority_rule_wins_when_several_rules_match()
    {
        var file = File("Invoice-march.pdf", ".pdf", 100);
        var rules = new List<AutomationRule>
        {
            Rule("PDF", 100, RuleMatchType.ExtensionEquals, ".pdf", "Documents/PDF"),
            Rule("Invoices", 10, RuleMatchType.NameContains, "invoice", "Invoices"),
        };

        var selected = _engine.Select(rules, file, MonitorScope.Desktop);
        Assert.NotNull(selected);
        Assert.Equal("Invoices", selected.Rule.RelativePath);
        Assert.True(selected.HasCompetitors);
    }

    [Fact]
    public void Disabled_and_other_scope_rules_do_not_match()
    {
        var file = File("notes.pdf", ".pdf", 20);
        var rules = new List<AutomationRule>
        {
            Rule("Off", 1, RuleMatchType.ExtensionEquals, ".pdf", "Hidden", enabled: false),
            Rule("Downloads only", 1, RuleMatchType.ExtensionEquals, ".pdf", "PDF", scope: MonitorScope.Downloads),
        };

        Assert.Null(_engine.Select(rules, file, MonitorScope.Desktop));
    }

    [Fact]
    public void Same_priority_extension_rules_are_reported_as_conflicts()
    {
        var rules = new List<AutomationRule>
        {
            Rule("A", 10, RuleMatchType.ExtensionEquals, ".pdf", "Documents/PDF"),
            Rule("B", 10, RuleMatchType.ExtensionEquals, "pdf", "Invoices"),
        };

        var conflicts = _engine.FindConflicts(rules, []);
        Assert.Contains(conflicts, conflict => conflict.SamePriority);
    }

    [Fact]
    public void A_rule_that_changes_a_mapped_folder_is_shown()
    {
        var rules = new List<AutomationRule>
        {
            Rule("Design", 20, RuleMatchType.ExtensionEquals, ".psd", "Design"),
        };
        var mappings = new List<ExtensionMapping>
        {
            new() { Extension = ".psd", CategoryKey = "other", RelativePath = "Other" },
        };

        var conflicts = _engine.FindConflicts(rules, mappings);
        Assert.Contains(conflicts, conflict => conflict.Description.Contains(".psd", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Size_rule_matches_only_above_the_threshold()
    {
        var rule = Rule("Large", 5, RuleMatchType.SizeGreaterThan, "500", "Large Files");
        Assert.True(_engine.Matches(rule, File("big.bin", ".bin", 501)));
        Assert.False(_engine.Matches(rule, File("small.bin", ".bin", 500)));
    }

    private static AutomationRule Rule(
        string name,
        int priority,
        RuleMatchType matchType,
        string value,
        string relative,
        bool enabled = true,
        MonitorScope scope = MonitorScope.Desktop)
    {
        return new AutomationRule
        {
            Name = name,
            Priority = priority,
            MatchType = matchType,
            MatchValue = value,
            RelativePath = relative,
            IsEnabled = enabled,
            Scope = scope,
            ActionType = RuleActionType.MoveToRelativeFolder,
        };
    }

    private static FileInspection File(string name, string extension, long length)
    {
        return new FileInspection
        {
            FullPath = Path.Combine(@"C:\Desktop", name),
            Name = name,
            Extension = extension,
            Length = length,
        };
    }
}

public class SafetyPolicyTests
{
    private readonly SafetyOptions _options = new(true, ["desktop.ini", "keep-me.*"]);

    [Fact]
    public void Protected_items_are_not_movable()
    {
        Assert.Equal(SkipReason.Directory, FileSafetyPolicy.Evaluate(Item("Projects", isDirectory: true), _options));
        Assert.Equal(SkipReason.Shortcut, FileSafetyPolicy.Evaluate(Item("App.lnk"), _options));
        Assert.Equal(SkipReason.DesktopIni, FileSafetyPolicy.Evaluate(Item("desktop.ini"), _options));
        Assert.Equal(SkipReason.HiddenOrSystem, FileSafetyPolicy.Evaluate(Item("secret.txt", hidden: true), _options));
        Assert.Equal(SkipReason.ReparsePoint, FileSafetyPolicy.Evaluate(Item("link.txt", reparse: true), _options));
        Assert.Equal(SkipReason.PartialDownload, FileSafetyPolicy.Evaluate(Item("book.pdf.crdownload"), _options));
        Assert.Equal(SkipReason.CloudPlaceholder, FileSafetyPolicy.Evaluate(Item("cloud.docx", cloud: true), _options));
        Assert.Equal(SkipReason.Excluded, FileSafetyPolicy.Evaluate(Item("keep-me.txt"), _options));
        Assert.Null(FileSafetyPolicy.Evaluate(Item("notes.pdf"), _options));
    }

    private static FileInspection Item(
        string name,
        bool isDirectory = false,
        bool hidden = false,
        bool reparse = false,
        bool cloud = false)
    {
        return new FileInspection
        {
            FullPath = Path.Combine(@"C:\Desktop", name),
            Name = name,
            Extension = isDirectory ? string.Empty : Path.GetExtension(name),
            IsDirectory = isDirectory,
            IsHidden = hidden,
            IsReparsePoint = reparse,
            IsCloudPlaceholder = cloud,
        };
    }
}

public class RecoveryPlannerTests
{
    [Fact]
    public void Interrupted_states_keep_user_files()
    {
        Assert.Equal(RecoveryAction.MarkFailed, RecoveryPlanner.Decide(true, false, false).Action);
        Assert.Equal(RecoveryAction.MarkSucceeded, RecoveryPlanner.Decide(false, true, false).Action);
        Assert.Equal(RecoveryAction.MarkFailed, RecoveryPlanner.Decide(true, true, false).Action);
        Assert.Equal(RecoveryAction.DeleteTempAndFail, RecoveryPlanner.Decide(true, false, true).Action);
        Assert.Equal(RecoveryAction.PromoteTempAndSucceed, RecoveryPlanner.Decide(false, false, true).Action);
    }
}

public class StartupCommandTests
{
    [Fact]
    public void Startup_command_quotes_the_executable_and_honors_minimized()
    {
        Assert.Equal("\"C:\\Apps\\DesktopFlow.exe\" --startup", StartupCommand.Build(@"C:\Apps\DesktopFlow.exe", false));
        Assert.Contains("--minimized", StartupCommand.Build(@"C:\Apps\DesktopFlow.exe", true), StringComparison.Ordinal);
    }
}
