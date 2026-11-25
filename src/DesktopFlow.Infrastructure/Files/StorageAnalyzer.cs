using System.Security.Cryptography;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Classification;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Storage;

namespace DesktopFlow.Infrastructure.Files;

public sealed class StorageAnalyzer : IStorageAnalyzer
{
    public const int LargestFolderLimit = 40;
    public const int DuplicateGroupLimit = 80;

    private readonly IFileClassifier _classifier;
    private readonly IAppLogger _logger;
    private readonly IReadOnlyList<ExtensionMapping> _mappings;

    public StorageAnalyzer(IFileClassifier classifier, IAppLogger logger)
    {
        _classifier = classifier;
        _logger = logger;
        _mappings = DefaultTaxonomy.CreateDesktopMappings();
    }

    public IReadOnlyList<DriveSpace> ListDrives()
    {
        var drives = new List<DriveSpace>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
                {
                    continue;
                }

                drives.Add(new DriveSpace(drive.Name, drive.VolumeLabel, drive.TotalSize, drive.AvailableFreeSpace));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return drives;
    }

    public IReadOnlyList<string> FixedDriveRoots()
    {
        return ListDrives().Select(drive => drive.Root).ToList();
    }

    public Task<StorageReport> AnalyzeAsync(
        IReadOnlyList<string> roots,
        IProgress<StorageScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => Analyze(roots, progress, cancellationToken), cancellationToken);
    }

    private StorageReport Analyze(
        IReadOnlyList<string> roots,
        IProgress<StorageScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var scanRoots = roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var folderBytes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var folderFiles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var categories = Enum.GetValues<StorageCategory>().ToDictionary(category => category, _ => (Bytes: 0L, Count: 0));
        var firstOfSize = new Dictionary<long, string>();
        var sameSize = new Dictionary<long, List<string>>();
        long scannedBytes = 0;
        var scannedFiles = 0;
        var skipped = 0;
        var directoriesSeen = 0;

        var pending = new Stack<string>();
        for (var index = scanRoots.Count - 1; index >= 0; index--)
        {
            pending.Push(scanRoots[index]);
        }

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            if (SystemLocationPolicy.IsExcluded(directory))
            {
                continue;
            }

            directoriesSeen++;
            if (directoriesSeen % 25 == 0)
            {
                progress?.Report(new StorageScanProgress(StorageScanPhase.Walking, directory, scannedFiles, scannedBytes));
            }

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                skipped++;
                continue;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    skipped++;
                    continue;
                }

                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    skipped++;
                    continue;
                }

                if (SystemLocationPolicy.IsExcluded(entry))
                {
                    continue;
                }

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    pending.Push(entry);
                    continue;
                }

                long length;
                try
                {
                    length = new FileInfo(entry).Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    skipped++;
                    continue;
                }

                scannedBytes += length;
                scannedFiles++;
                AddToAncestors(folderBytes, folderFiles, entry, length, scanRoots);
                var category = StorageCategories.ForFile(Path.GetFileName(entry), _classifier, _mappings);
                var current = categories[category];
                categories[category] = (current.Bytes + length, current.Count + 1);
                if (length > 0)
                {
                    RememberSize(firstOfSize, sameSize, length, entry);
                }
            }
        }

        var duplicates = HashDuplicates(sameSize, progress, scannedFiles, scannedBytes, ref skipped, cancellationToken);
        var largest = folderBytes
            .Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(LargestFolderLimit)
            .Select(pair => new FolderUsage(pair.Key, pair.Value, folderFiles.GetValueOrDefault(pair.Key)))
            .ToList();
        var categoryUsage = categories
            .Where(pair => pair.Value.Bytes > 0 || pair.Value.Count > 0)
            .Select(pair => new CategoryUsage(pair.Key, pair.Value.Bytes, pair.Value.Count))
            .OrderByDescending(row => row.Bytes)
            .ToList();
        var wasted = duplicates.Sum(group => group.WastedBytes);
        var shown = duplicates.Take(DuplicateGroupLimit).ToList();
        _logger.Info($"Storage scan finished. Files={scannedFiles}, bytes={scannedBytes}, duplicates={duplicates.Count}, skipped={skipped}.");
        return new StorageReport(largest, categoryUsage, shown, duplicates.Count, wasted, scannedBytes, scannedFiles, skipped);
    }

    private List<DuplicateGroup> HashDuplicates(
        Dictionary<long, List<string>> sameSize,
        IProgress<StorageScanProgress>? progress,
        int scannedFiles,
        long scannedBytes,
        ref int skipped,
        CancellationToken cancellationToken)
    {
        var byHash = new Dictionary<string, (long Size, List<string> Paths)>(StringComparer.Ordinal);
        var hashed = 0;
        foreach (var (size, paths) in sameSize)
        {
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hashed++;
                if (hashed % 20 == 0)
                {
                    progress?.Report(new StorageScanProgress(StorageScanPhase.Hashing, path, scannedFiles, scannedBytes));
                }

                var hash = TryHash(path);
                if (hash is null)
                {
                    skipped++;
                    continue;
                }

                if (!byHash.TryGetValue(hash, out var group))
                {
                    group = (size, []);
                }

                group.Paths.Add(path);
                byHash[hash] = group;
            }
        }

        return byHash
            .Where(pair => pair.Value.Paths.Count >= 2)
            .Select(pair => new DuplicateGroup(
                pair.Key,
                pair.Value.Size,
                pair.Value.Paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderByDescending(group => group.WastedBytes)
            .ThenBy(group => group.Paths[0], StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void RememberSize(
        Dictionary<long, string> firstOfSize,
        Dictionary<long, List<string>> sameSize,
        long length,
        string path)
    {
        if (sameSize.TryGetValue(length, out var paths))
        {
            paths.Add(path);
            return;
        }

        if (firstOfSize.TryGetValue(length, out var first))
        {
            firstOfSize.Remove(length);
            sameSize[length] = [first, path];
            return;
        }

        firstOfSize[length] = path;
    }

    private static void AddToAncestors(
        Dictionary<string, long> folderBytes,
        Dictionary<string, int> folderFiles,
        string file,
        long length,
        IReadOnlyList<string> scanRoots)
    {
        var directory = Path.GetDirectoryName(file);
        while (!string.IsNullOrEmpty(directory) && !IsScanRoot(directory, scanRoots))
        {
            folderBytes[directory] = folderBytes.GetValueOrDefault(directory) + length;
            folderFiles[directory] = folderFiles.GetValueOrDefault(directory) + 1;
            directory = Path.GetDirectoryName(directory);
        }
    }

    private static bool IsScanRoot(string directory, IReadOnlyList<string> scanRoots)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(directory);
        foreach (var root in scanRoots)
        {
            if (string.Equals(trimmed, Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? TryHash(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }
}
