namespace DesktopFlow.Core.Storage;

public enum StorageCategory
{
    Images,
    Video,
    Audio,
    Documents,
    Archives,
    Installers,
    DiskImages,
    Code,
    Other,
}

public enum StorageScanPhase
{
    Walking,
    Hashing,
}

public sealed record DriveSpace(string Root, string Label, long TotalBytes, long FreeBytes)
{
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
}

public sealed record FolderUsage(string Path, long Bytes, int FileCount);

public sealed record CategoryUsage(StorageCategory Category, long Bytes, int FileCount);

public sealed record DuplicateGroup(string ContentHash, long Bytes, IReadOnlyList<string> Paths)
{
    public int CopyCount => Paths.Count;

    public long WastedBytes => Bytes * Math.Max(0, Paths.Count - 1);
}

public sealed record StorageScanProgress(StorageScanPhase Phase, string CurrentPath, int FilesSeen, long BytesSeen);

public sealed record StorageReport(
    IReadOnlyList<FolderUsage> LargestFolders,
    IReadOnlyList<CategoryUsage> Categories,
    IReadOnlyList<DuplicateGroup> Duplicates,
    int DuplicateGroupCount,
    long WastedBytes,
    long ScannedBytes,
    int ScannedFiles,
    int SkippedEntries);
