using System.Collections.ObjectModel;
using System.Diagnostics;
using DesktopFlow.App.Localization;
using DesktopFlow.App.Services;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Storage;

namespace DesktopFlow.App.ViewModels;

public partial class StorageViewModel : ViewModelBase
{
    private readonly IStorageAnalyzer _storage;
    private readonly IShellLauncher _shell;
    private readonly IUiDispatcher _ui;
    private CancellationTokenSource? _scan;
    private IReadOnlyList<DriveSpace> _drives = [];
    private StorageReport? _report;

    public StorageViewModel(ILocalizer text, IStorageAnalyzer storage, IShellLauncher shell, IUiDispatcher ui)
        : base(text)
    {
        _storage = storage;
        _shell = shell;
        _ui = ui;
        Status = text["Storage_Ready"];
    }

    public ObservableCollection<DriveRow> Drives { get; } = [];

    public ObservableCollection<FolderRow> Folders { get; } = [];

    public ObservableCollection<CategoryRow> Categories { get; } = [];

    public ObservableCollection<DuplicateRow> Duplicates { get; } = [];

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private string _moreDuplicates = string.Empty;

    [ObservableProperty]
    private bool _hasReport;

    [ObservableProperty]
    private bool _hasFolders;

    [ObservableProperty]
    private bool _hasDuplicates;

    [ObservableProperty]
    private bool _hasCategories;

    public bool ShowScanPrompt => !HasReport && !IsBusy;

    public bool ShowNoFolders => HasReport && !HasFolders;

    public bool ShowNoDuplicates => HasReport && !HasDuplicates;

    public bool ShowNoCategories => HasReport && !HasCategories;

    public void Load()
    {
        _drives = _storage.ListDrives();
        ApplyDrives();
    }

    protected override void OnLanguageChanged()
    {
        ApplyDrives();
        ApplyReport();
        if (!IsBusy && _report is null)
        {
            Status = Text["Storage_Ready"];
        }
    }

    partial void OnHasReportChanged(bool value)
    {
        NotifyVisibility();
    }

    partial void OnHasFoldersChanged(bool value)
    {
        NotifyVisibility();
    }

    partial void OnHasDuplicatesChanged(bool value)
    {
        NotifyVisibility();
    }

    partial void OnHasCategoriesChanged(bool value)
    {
        NotifyVisibility();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        _scan?.Cancel();
        _scan = new CancellationTokenSource();
        var token = _scan.Token;
        await GuardAsync(async cancellationToken =>
        {
            NotifyVisibility();
            var roots = _storage.FixedDriveRoots();
            if (roots.Count == 0)
            {
                ErrorMessage = Text["Storage_NoDrives"];
                return;
            }

            var progress = new Progress<StorageScanProgress>(update => _ui.Enqueue(() => ApplyProgress(update)));
            try
            {
                _report = await _storage.AnalyzeAsync(roots, progress, cancellationToken);
                ApplyReport();
                Status = Text.Format(
                    "Storage_Done",
                    _report.ScannedFiles,
                    ByteSize.Format(_report.ScannedBytes),
                    _report.DuplicateGroupCount,
                    ByteSize.Format(_report.WastedBytes),
                    _report.SkippedEntries);
                CurrentPath = string.Empty;
            }
            catch (OperationCanceledException)
            {
                Status = Text["Storage_Stopped"];
            }
        }, token);
        NotifyVisibility();
    }

    [RelayCommand]
    private void Cancel()
    {
        _scan?.Cancel();
    }

    [RelayCommand]
    private void OpenLocation(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                _shell.OpenFolder(path);
                return;
            }

            _shell.Reveal(path);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ApplyProgress(StorageScanProgress update)
    {
        CurrentPath = update.CurrentPath;
        Status = update.Phase switch
        {
            StorageScanPhase.Walking => Text.Format("Storage_Progress", update.FilesSeen, ByteSize.Format(update.BytesSeen)),
            StorageScanPhase.Hashing => Text.Format("Storage_Hashing", update.FilesSeen, ByteSize.Format(update.BytesSeen)),
            _ => throw new UnreachableException(update.Phase.ToString()),
        };
    }

    private void ApplyDrives()
    {
        Drives.Clear();
        foreach (var drive in _drives)
        {
            var title = string.IsNullOrWhiteSpace(drive.Label)
                ? drive.Root
                : $"{drive.Label} ({drive.Root.TrimEnd('\\')})";
            var percent = drive.TotalBytes == 0 ? 0 : drive.UsedBytes * 100d / drive.TotalBytes;
            Drives.Add(new DriveRow(
                title,
                Text.Format("Storage_DriveDetail", ByteSize.Format(drive.UsedBytes), ByteSize.Format(drive.TotalBytes), ByteSize.Format(drive.FreeBytes)),
                percent));
        }
    }

    private void ApplyReport()
    {
        Folders.Clear();
        Categories.Clear();
        Duplicates.Clear();
        MoreDuplicates = string.Empty;
        if (_report is null)
        {
            HasReport = false;
            HasFolders = false;
            HasDuplicates = false;
            HasCategories = false;
            return;
        }

        foreach (var folder in _report.LargestFolders)
        {
            var name = Path.GetFileName(folder.Path);
            if (string.IsNullOrEmpty(name))
            {
                name = folder.Path;
            }

            Folders.Add(new FolderRow(
                name,
                folder.Path,
                ByteSize.Format(folder.Bytes),
                Text.Format("Storage_FileCount", folder.FileCount),
                OpenLocationCommand));
        }

        var largestCategory = _report.Categories.Count == 0 ? 0 : _report.Categories.Max(row => row.Bytes);
        foreach (var category in _report.Categories)
        {
            var share = largestCategory == 0 ? 0 : category.Bytes * 100d / largestCategory;
            Categories.Add(new CategoryRow(
                Text[StorageCategories.LabelKey(category.Category)],
                Text.Format("Storage_FileCount", category.FileCount),
                ByteSize.Format(category.Bytes),
                share));
        }

        foreach (var group in _report.Duplicates)
        {
            Duplicates.Add(new DuplicateRow(
                Text.Format("Storage_DuplicateSummary", group.CopyCount, ByteSize.Format(group.WastedBytes)),
                ByteSize.Format(group.Bytes),
                group.Paths.Select(path => new PathRow(path, OpenLocationCommand)).ToList()));
        }

        if (_report.DuplicateGroupCount > _report.Duplicates.Count)
        {
            MoreDuplicates = Text.Format("Storage_MoreDuplicates", _report.DuplicateGroupCount - _report.Duplicates.Count);
        }

        HasFolders = Folders.Count > 0;
        HasCategories = Categories.Count > 0;
        HasDuplicates = Duplicates.Count > 0;
        HasReport = true;
    }

    private void NotifyVisibility()
    {
        OnPropertyChanged(nameof(ShowScanPrompt));
        OnPropertyChanged(nameof(ShowNoFolders));
        OnPropertyChanged(nameof(ShowNoCategories));
        OnPropertyChanged(nameof(ShowNoDuplicates));
    }
}

public sealed class DriveRow
{
    public DriveRow(string title, string detail, double usedPercent)
    {
        Title = title;
        Detail = detail;
        UsedPercent = usedPercent;
    }

    public string Title { get; }

    public string Detail { get; }

    public double UsedPercent { get; }
}

public sealed class FolderRow
{
    public FolderRow(string name, string path, string size, string files, IRelayCommand<string?> openCommand)
    {
        Name = name;
        Path = path;
        Size = size;
        Files = files;
        OpenCommand = openCommand;
    }

    public string Name { get; }

    public string Path { get; }

    public string Size { get; }

    public string Files { get; }

    public IRelayCommand<string?> OpenCommand { get; }
}

public sealed class CategoryRow
{
    public CategoryRow(string name, string files, string size, double share)
    {
        Name = name;
        Files = files;
        Size = size;
        Share = share;
    }

    public string Name { get; }

    public string Files { get; }

    public string Size { get; }

    public double Share { get; }
}

public sealed class DuplicateRow
{
    public DuplicateRow(string summary, string size, IReadOnlyList<PathRow> paths)
    {
        Summary = summary;
        Size = size;
        Paths = paths;
    }

    public string Summary { get; }

    public string Size { get; }

    public IReadOnlyList<PathRow> Paths { get; }
}

public sealed class PathRow
{
    public PathRow(string path, IRelayCommand<string?> openCommand)
    {
        Path = path;
        OpenCommand = openCommand;
    }

    public string Path { get; }

    public IRelayCommand<string?> OpenCommand { get; }
}
