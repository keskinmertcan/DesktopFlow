using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Classification;
using DesktopFlow.Core.Storage;
using DesktopFlow.Infrastructure.Files;

namespace DesktopFlow.Tests;

public class StorageAnalysisTests
{
    [Theory]
    [InlineData(@"C:\Windows\System32\ntdll.dll")]
    [InlineData(@"C:\WINDOWS\WinSxS\backup")]
    [InlineData(@"C:\pagefile.sys")]
    [InlineData(@"C:\hiberfil.sys")]
    [InlineData(@"D:\System Volume Information\tracking.log")]
    [InlineData(@"C:\Program Files\WindowsApps\Package")]
    [InlineData(@"C:\Users\Default\NTUSER.DAT")]
    [InlineData(@"C:\Users\Mertcan\AppData\Local\Microsoft\Windows\Explorer\thumbcache.db")]
    [InlineData(@"C:\Users\Mertcan\Documents\desktop.ini")]
    [InlineData(@"C:\$Recycle.Bin\S-1-5-21\file.txt")]
    public void Windows_system_locations_are_excluded(string path)
    {
        Assert.True(SystemLocationPolicy.IsExcluded(path));
    }

    [Theory]
    [InlineData(@"C:\Users\Mertcan\Documents\report.pdf")]
    [InlineData(@"C:\Users\Mertcan\Documents\Windows\notes.txt")]
    [InlineData(@"C:\Program Files\Games\setup.exe")]
    [InlineData(@"C:\Users\Mertcan\AppData\Local\Temp\download.bin")]
    [InlineData(@"D:\Videos\movie.mp4")]
    [InlineData(@"C:\Users\Public\Pictures\photo.jpg")]
    public void Personal_files_and_installed_programs_stay_visible(string path)
    {
        Assert.False(SystemLocationPolicy.IsExcluded(path));
    }

    [Fact]
    public void Byte_sizes_use_binary_units()
    {
        Assert.Equal("5 B", ByteSize.Format(5));
        Assert.Equal("1 KB", ByteSize.Format(1024));
        Assert.Equal("1.5 KB", ByteSize.Format(1536));
    }

    [Fact]
    public async Task Scan_reports_largest_folders_types_and_exact_copies()
    {
        var root = Path.Combine(Path.GetTempPath(), "DesktopFlow.Storage." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var keep = Path.Combine(root, "keep");
            var big = Path.Combine(root, "big");
            Directory.CreateDirectory(keep);
            Directory.CreateDirectory(big);
            await File.WriteAllTextAsync(Path.Combine(keep, "a.txt"), "hello");
            await File.WriteAllTextAsync(Path.Combine(keep, "b.txt"), "hello");
            await File.WriteAllBytesAsync(Path.Combine(keep, "photo.png"), [1, 2, 3, 4]);
            await File.WriteAllBytesAsync(Path.Combine(big, "movie.bin"), new byte[1000]);
            await File.WriteAllBytesAsync(Path.Combine(keep, "empty.dat"), []);

            var analyzer = new StorageAnalyzer(new FileClassifier(), new QuietLog());
            var report = await analyzer.AnalyzeAsync([root], progress: null, CancellationToken.None);

            Assert.Equal(5, report.ScannedFiles);
            Assert.Equal(1014, report.ScannedBytes);
            var largest = Assert.Single(report.LargestFolders, folder => folder.Path.Equals(big, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1000, largest.Bytes);
            Assert.Contains(report.LargestFolders, folder => folder.Path.Equals(keep, StringComparison.OrdinalIgnoreCase) && folder.FileCount == 4);
            Assert.Contains(report.Categories, category => category.Category == StorageCategory.Images && category.FileCount == 1);
            var duplicate = Assert.Single(report.Duplicates);
            Assert.Equal(2, duplicate.CopyCount);
            Assert.Equal(5, duplicate.WastedBytes);
            Assert.Equal(5, report.WastedBytes);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class QuietLog : IAppLogger
    {
        public void Info(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
