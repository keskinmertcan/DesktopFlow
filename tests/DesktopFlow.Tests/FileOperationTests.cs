using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using DesktopFlow.Infrastructure.Files;

namespace DesktopFlow.Tests;

public class FileOperationTests
{
    [Fact]
    public async Task Move_creates_a_missing_folder_and_does_not_overwrite()
    {
        using var app = new TestApp();
        var mover = app.Provider.GetRequiredService<IFileMover>();
        var source = Path.Combine(app.Desktop, "report.pdf");
        await File.WriteAllTextAsync(source, "new");
        var destination = Path.Combine(app.Desktop, "Documents", "PDF");
        await File.WriteAllTextAsync(Path.Combine(app.Desktop, "placeholder"), "x");
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, "report.pdf"), "keep");

        var execution = await mover.MoveAsync(source, destination, "report.pdf", null, CancellationToken.None);

        Assert.True(execution.Succeeded);
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(destination, "report.pdf")));
        Assert.Equal("new", await File.ReadAllTextAsync(execution.DestinationPath!));
        Assert.False(File.Exists(source));
        Assert.EndsWith("report (1).pdf", execution.DestinationPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Locked_file_is_detected_and_not_moved()
    {
        using var app = new TestApp();
        var path = Path.Combine(app.Desktop, "busy.txt");
        await File.WriteAllTextAsync(path, "busy");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var probe = app.Provider.GetRequiredService<IFileLockProbe>();
        Assert.Equal(FileLockState.Locked, probe.Probe(path));

        var mover = app.Provider.GetRequiredService<IFileMover>();
        var execution = await mover.MoveAsync(path, Path.Combine(app.Desktop, "Other"), "busy.txt", null, CancellationToken.None);
        Assert.False(execution.Succeeded);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Missing_destination_parent_is_created()
    {
        using var app = new TestApp();
        var source = Path.Combine(app.Desktop, "song.mp3");
        await File.WriteAllBytesAsync(source, [1, 2, 3]);
        var destination = Path.Combine(app.Desktop, "Audio", "Nested");
        Assert.False(Directory.Exists(destination));

        var execution = await app.Provider.GetRequiredService<IFileMover>().MoveAsync(
            source,
            destination,
            "song.mp3",
            null,
            CancellationToken.None);

        Assert.True(execution.Succeeded);
        Assert.True(File.Exists(Path.Combine(destination, "song.mp3")));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Access_denied_leaves_the_source_in_place()
    {
        using var app = new TestApp();
        var source = Path.Combine(app.Desktop, "blocked.txt");
        await File.WriteAllTextAsync(source, "safe");
        var lockedFolder = Path.Combine(app.Desktop, "NoWrite");
        Directory.CreateDirectory(lockedFolder);
        var directory = new DirectoryInfo(lockedFolder);
        var security = directory.GetAccessControl();
        var user = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("No user identity.");
        security.AddAccessRule(new FileSystemAccessRule(
            user,
            FileSystemRights.CreateFiles | FileSystemRights.WriteData,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Deny));
        directory.SetAccessControl(security);

        var execution = await app.Provider.GetRequiredService<IFileMover>().MoveAsync(
            source,
            lockedFolder,
            "blocked.txt",
            null,
            CancellationToken.None);

        Assert.False(execution.Succeeded);
        Assert.Equal("safe", await File.ReadAllTextAsync(source));
        Assert.False(File.Exists(Path.Combine(lockedFolder, "blocked.txt")));
    }

    [Fact]
    public async Task Stable_file_is_accepted_and_partial_name_is_not()
    {
        using var app = new TestApp();
        var waiter = app.Provider.GetRequiredService<IFileStabilityWaiter>();
        var ready = Path.Combine(app.Desktop, "ready.txt");
        await File.WriteAllTextAsync(ready, "done");
        var stable = await waiter.WaitAsync(ready, TimeSpan.FromMilliseconds(300), CancellationToken.None);
        Assert.Equal(StabilityState.Stable, stable.State);

        var partial = Path.Combine(app.Desktop, "book.pdf.crdownload");
        await File.WriteAllTextAsync(partial, "partial");
        var waiting = await waiter.WaitAsync(partial, TimeSpan.FromMilliseconds(300), CancellationToken.None);
        Assert.Equal(StabilityState.Retry, waiting.State);
    }
}
