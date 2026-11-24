using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;

namespace DesktopFlow.Infrastructure.Files;

public sealed class FileScanner : IFileScanner
{
    private const FileAttributes Offline = (FileAttributes)0x1000;
    private const FileAttributes RecallOnOpen = (FileAttributes)0x40000;
    private const FileAttributes RecallOnDataAccess = (FileAttributes)0x400000;

    private readonly IAppLogger _logger;

    public FileScanner(IAppLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<FileInspection> ListTopLevel(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return [];
        }

        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(root).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error($"Could not read '{root}'.", ex);
            return [];
        }

        var files = new List<FileInspection>();
        foreach (var entry in entries)
        {
            var inspection = Inspect(entry);
            if (inspection is not null)
            {
                files.Add(inspection);
            }
        }

        return files;
    }

    public FileInspection? Inspect(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return null;
            }

            var attributes = File.GetAttributes(path);
            var isDirectory = attributes.HasFlag(FileAttributes.Directory);
            long length = 0;
            if (!isDirectory)
            {
                length = new FileInfo(path).Length;
            }

            return new FileInspection
            {
                FullPath = Path.GetFullPath(path),
                Name = Path.GetFileName(path),
                Extension = isDirectory ? string.Empty : Path.GetExtension(path),
                Length = length,
                IsDirectory = isDirectory,
                IsHidden = attributes.HasFlag(FileAttributes.Hidden),
                IsSystem = attributes.HasFlag(FileAttributes.System),
                IsReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint),
                IsCloudPlaceholder = attributes.HasFlag(Offline)
                    || attributes.HasFlag(RecallOnOpen)
                    || attributes.HasFlag(RecallOnDataAccess),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error($"Could not inspect '{path}'.", ex);
            return null;
        }
    }

    public IReadOnlyList<StorageSlice> Summarize(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return [];
        }

        var slices = new List<StorageSlice>();
        long looseBytes = 0;
        var looseCount = 0;
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(root).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error($"Could not summarize '{root}'.", ex);
            return [];
        }

        foreach (var entry in entries)
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(entry);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            if (attributes.HasFlag(FileAttributes.Directory))
            {
                var measured = Measure(entry);
                slices.Add(new StorageSlice(Path.GetFileName(entry), measured.Bytes, measured.Count));
                continue;
            }

            try
            {
                looseBytes += new FileInfo(entry).Length;
                looseCount++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        slices.Add(new StorageSlice("Loose files", looseBytes, looseCount));
        return slices.OrderByDescending(slice => slice.Bytes).ToList();
    }

    private static (long Bytes, int Count) Measure(string directory)
    {
        long bytes = 0;
        var count = 0;
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };
            foreach (var file in Directory.EnumerateFiles(directory, "*", options))
            {
                try
                {
                    bytes += new FileInfo(file).Length;
                    count++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return (bytes, count);
    }
}
