using System.Collections.ObjectModel;
using DesktopFlow.App.Localization;
using DesktopFlow.App.Services;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;

namespace DesktopFlow.App.ViewModels;

public partial class FileRowViewModel : ObservableObject
{
    public required string Name { get; init; }
    public required string Size { get; init; }
    public required string Destination { get; init; }
    public required string Status { get; init; }
    public required string SourcePath { get; init; }
    public bool WillMove { get; init; }
}

public partial class FolderViewModel : ViewModelBase
{
    private readonly IOrganizationService _organization;
    private readonly ISettingsStore _settings;
    private readonly IUserPrompt _prompt;
    private readonly INotificationService _notifications;
    private readonly IMonitorController _monitor;
    private MonitorScope _scope = MonitorScope.Desktop;

    public FolderViewModel(
        ILocalizer text,
        IOrganizationService organization,
        ISettingsStore settings,
        IUserPrompt prompt,
        INotificationService notifications,
        IMonitorController monitor)
        : base(text)
    {
        _organization = organization;
        _settings = settings;
        _prompt = prompt;
        _notifications = notifications;
        _monitor = monitor;
    }

    public ObservableCollection<FileRowViewModel> Files { get; } = [];

    public List<string> SelectedPaths { get; } = [];

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private bool _monitoring;

    public bool HasFiles => Files.Count > 0;

    public void Initialize(MonitorScope scope)
    {
        _scope = scope;
        ApplyLabels();
        Monitoring = _settings.Current.MonitoringEnabled(scope);
    }

    public void SetSelection(IEnumerable<FileRowViewModel> rows)
    {
        SelectedPaths.Clear();
        SelectedPaths.AddRange(rows.Select(row => row.SourcePath));
    }

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        return GuardAsync(async token =>
        {
            var plans = await _organization.PreviewAsync(_scope, token);
            Files.Clear();
            foreach (var plan in plans.OrderByDescending(item => item.WillMove).ThenBy(item => item.FileName, StringComparer.CurrentCultureIgnoreCase))
            {
                Files.Add(new FileRowViewModel
                {
                    Name = plan.FileName,
                    Size = DisplayText.Bytes(plan.Length),
                    Destination = plan.WillMove ? plan.RelativeFolder ?? string.Empty : string.Empty,
                    Status = plan.SkipReason is null ? Text["Status_WillMove"] : TextMaps.Skip(Text, plan.SkipReason.Value),
                    SourcePath = plan.SourcePath,
                    WillMove = plan.WillMove,
                });
            }

            OnPropertyChanged(nameof(HasFiles));
        }, cancellationToken);
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        return LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private Task OrganizeAllAsync()
    {
        return OrganizeAsync(null);
    }

    [RelayCommand]
    private Task OrganizeSelectedAsync()
    {
        return OrganizeAsync(SelectedPaths.ToArray());
    }

    [RelayCommand]
    private async Task ToggleMonitoringAsync()
    {
        var preferences = await _settings.GetAsync(CancellationToken.None);
        switch (_scope)
        {
            case MonitorScope.Desktop:
                preferences.MonitorDesktop = Monitoring;
                break;
            case MonitorScope.Downloads:
                preferences.MonitorDownloads = Monitoring;
                break;
            default:
                throw new System.Diagnostics.UnreachableException(_scope.ToString());
        }

        await _settings.SaveAsync(preferences, CancellationToken.None);
        _monitor.Reload();
    }

    protected override void OnLanguageChanged()
    {
        ApplyLabels();
        _ = LoadAsync(CancellationToken.None);
    }

    private void ApplyLabels()
    {
        Title = _scope == MonitorScope.Desktop ? Text["Folder_DesktopTitle"] : Text["Folder_DownloadsTitle"];
        Subtitle = _scope == MonitorScope.Desktop ? Text["Folder_DesktopSubtitle"] : Text["Folder_DownloadsSubtitle"];
    }

    private async Task OrganizeAsync(IReadOnlyCollection<string>? paths)
    {
        await GuardAsync(async cancellationToken =>
        {
            var preview = await _organization.PreviewAsync(_scope, cancellationToken);
            if (paths is not null)
            {
                preview = preview.Where(plan => paths.Contains(plan.SourcePath, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            if (!preview.Any(plan => plan.WillMove))
            {
                await _prompt.ShowAsync(Title, paths is null ? Text["Dialog_NoMoves"] : Text["Dialog_SelectFiles"]);
                return;
            }

            var preferences = _settings.Current;
            if (preferences.PreviewBeforeOrganize || preferences.ConfirmBeforeBulk)
            {
                if (!await _prompt.ConfirmMovesAsync(preview))
                {
                    return;
                }
            }

            var result = await _organization.OrganizeAsync(_scope, paths, cancellationToken);
            var message = Text.Format("Dialog_Result", result.Moved, result.Skipped, result.Failed);
            _notifications.Show(Text["Notify_BatchTitle"], message);
            await _prompt.ShowAsync(Title, message);
            await LoadAsync(cancellationToken);
        }, CancellationToken.None);
    }
}
