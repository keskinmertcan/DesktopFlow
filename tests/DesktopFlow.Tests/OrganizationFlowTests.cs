using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Safety;

namespace DesktopFlow.Tests;

public class OrganizationFlowTests
{
    [Fact]
    public async Task Preview_and_organize_move_only_eligible_files()
    {
        using var app = new TestApp();
        await app.SaveWorkspaceAsync();
        await File.WriteAllTextAsync(Path.Combine(app.Desktop, "brief.pdf"), "pdf");
        await File.WriteAllTextAsync(Path.Combine(app.Desktop, "photo.png"), "png");
        await File.WriteAllTextAsync(Path.Combine(app.Desktop, "App.lnk"), "shortcut");
        await File.WriteAllTextAsync(Path.Combine(app.Desktop, "desktop.ini"), "ini");
        await File.WriteAllTextAsync(Path.Combine(app.Desktop, "keep-me.txt"), "keep");
        Directory.CreateDirectory(Path.Combine(app.Desktop, "Projects"));

        var organizer = app.Provider.GetRequiredService<IOrganizationService>();
        var preview = await organizer.PreviewAsync(MonitorScope.Desktop, CancellationToken.None);
        Assert.Contains(preview, plan => plan.FileName == "brief.pdf" && plan.WillMove && plan.RelativeFolder == "Documents/PDF");
        Assert.Contains(preview, plan => plan.FileName == "photo.png" && plan.RelativeFolder == "Images/PNG");
        Assert.Contains(preview, plan => plan.FileName == "App.lnk" && plan.SkipReason == SkipReason.Shortcut);
        Assert.Contains(preview, plan => plan.FileName == "desktop.ini" && plan.SkipReason == SkipReason.DesktopIni);
        Assert.Contains(preview, plan => plan.FileName == "keep-me.txt" && plan.SkipReason == SkipReason.Excluded);
        Assert.Contains(preview, plan => plan.FileName == "Projects" && plan.SkipReason == SkipReason.Directory);

        var result = await organizer.OrganizeAsync(MonitorScope.Desktop, null, CancellationToken.None);
        Assert.Equal(2, result.Moved);
        Assert.True(File.Exists(Path.Combine(app.Desktop, "Documents", "PDF", "brief.pdf")));
        Assert.True(File.Exists(Path.Combine(app.Desktop, "Images", "PNG", "photo.png")));
        Assert.True(File.Exists(Path.Combine(app.Desktop, "App.lnk")));
        Assert.True(Directory.Exists(Path.Combine(app.Desktop, "Projects")));
    }

    [Fact]
    public async Task Custom_rule_overrides_the_extension_folder()
    {
        using var app = new TestApp();
        await app.SaveWorkspaceAsync();
        await app.Provider.GetRequiredService<IRuleStore>().SaveAsync(new AutomationRule
        {
            Name = "Invoices",
            Priority = 1,
            Scope = MonitorScope.Desktop,
            MatchType = RuleMatchType.NameContains,
            MatchValue = "invoice",
            ActionType = RuleActionType.MoveToRelativeFolder,
            RelativePath = "Invoices",
            IsEnabled = true,
        }, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(app.Desktop, "Invoice-9.pdf"), "invoice");

        var result = await app.Provider.GetRequiredService<IOrganizationService>().OrganizeAsync(
            MonitorScope.Desktop,
            null,
            CancellationToken.None);

        Assert.Equal(1, result.Moved);
        Assert.True(File.Exists(Path.Combine(app.Desktop, "Invoices", "Invoice-9.pdf")));
    }

    [Fact]
    public async Task Skip_rule_leaves_the_file_in_place()
    {
        using var app = new TestApp();
        await app.SaveWorkspaceAsync();
        await app.Provider.GetRequiredService<IRuleStore>().SaveAsync(new AutomationRule
        {
            Name = "Keep installers",
            Priority = 1,
            Scope = MonitorScope.Downloads,
            MatchType = RuleMatchType.ExtensionEquals,
            MatchValue = ".exe",
            ActionType = RuleActionType.Skip,
            IsEnabled = true,
        }, CancellationToken.None);
        var installer = Path.Combine(app.Downloads, "setup.exe");
        await File.WriteAllBytesAsync(installer, [1]);

        var preview = await app.Provider.GetRequiredService<IOrganizationService>().PreviewAsync(
            MonitorScope.Downloads,
            CancellationToken.None);

        Assert.Contains(preview, plan => plan.FileName == "setup.exe" && plan.SkipReason == SkipReason.RuleSkip);
        Assert.True(File.Exists(installer));
    }

    [Fact]
    public async Task Undo_restores_the_previous_path_and_refuses_to_overwrite()
    {
        using var app = new TestApp();
        await app.SaveWorkspaceAsync();
        var source = Path.Combine(app.Desktop, "notes.txt");
        await File.WriteAllTextAsync(source, "body");
        var organizer = app.Provider.GetRequiredService<IOrganizationService>();
        var undo = app.Provider.GetRequiredService<IUndoService>();
        var activities = app.Provider.GetRequiredService<IActivityStore>();
        await organizer.OrganizeAsync(MonitorScope.Desktop, null, CancellationToken.None);
        var moved = (await activities.GetRecentAsync(10, CancellationToken.None)).Single(record => record.CanUndo);
        Assert.False(File.Exists(source));

        await File.WriteAllTextAsync(source, "occupied");
        var blocked = await undo.UndoAsync(moved.Id, false, CancellationToken.None);
        Assert.Equal(UndoStatus.NeedsAlternateName, blocked.Status);
        Assert.Equal("occupied", await File.ReadAllTextAsync(source));

        var restored = await undo.UndoAsync(moved.Id, true, CancellationToken.None);
        Assert.Equal(UndoStatus.Restored, restored.Status);
        Assert.Equal("occupied", await File.ReadAllTextAsync(source));
        Assert.Equal("body", await File.ReadAllTextAsync(restored.RestoredPath!));
        var history = await activities.GetRecentAsync(10, CancellationToken.None);
        Assert.Contains(history, record => record.Kind == ActivityKind.Undo && record.Status == ActivityStatus.Succeeded);
    }

    [Fact]
    public async Task Interrupted_move_is_recovered_without_deleting_the_original()
    {
        using var app = new TestApp();
        var activities = app.Provider.GetRequiredService<IActivityStore>();
        var source = Path.Combine(app.Desktop, "half.txt");
        await File.WriteAllTextAsync(source, "original");
        var destination = Path.Combine(app.Desktop, "Other", "half.txt");
        await activities.AddAsync(new ActivityRecord
        {
            FileName = "half.txt",
            SourcePath = source,
            DestinationPath = destination,
            Timestamp = DateTimeOffset.UtcNow,
            Kind = ActivityKind.Move,
            Status = ActivityStatus.Pending,
            Scope = MonitorScope.Desktop,
        }, CancellationToken.None);

        var recovered = await app.Provider.GetRequiredService<IRecoveryService>().RecoverAsync(CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Equal("original", await File.ReadAllTextAsync(source));
        Assert.False(File.Exists(destination));
        var pending = await activities.GetPendingAsync(CancellationToken.None);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task Downloads_mapping_is_independent_from_the_desktop()
    {
        using var app = new TestApp();
        await app.SaveWorkspaceAsync();
        await File.WriteAllTextAsync(Path.Combine(app.Downloads, "paper.pdf"), "pdf");
        var result = await app.Provider.GetRequiredService<IOrganizationService>().OrganizeAsync(
            MonitorScope.Downloads,
            null,
            CancellationToken.None);

        Assert.Equal(1, result.Moved);
        Assert.True(File.Exists(Path.Combine(app.Downloads, "PDF", "paper.pdf")));
        Assert.False(Directory.Exists(Path.Combine(app.Downloads, "Documents")));
    }
}
