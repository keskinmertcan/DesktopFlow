using System.Collections.ObjectModel;
using DesktopFlow.App.Localization;
using DesktopFlow.App.Services;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;

namespace DesktopFlow.App.ViewModels;

public partial class OverviewViewModel : ViewModelBase
{
    private readonly IOrganizationService _organization;
    private readonly IActivityStore _activities;
    private readonly IRuleStore _rules;
    private readonly ISettingsStore _settings;
    private readonly IMonitorController _monitor;
    private readonly IFileScanner _scanner;
    private readonly IShellLauncher _shell;
    private readonly IUserPrompt _prompt;
    private readonly INotificationService _notifications;

    public OverviewViewModel(
        ILocalizer text,
        IOrganizationService organization,
        IActivityStore activities,
        IRuleStore rules,
        ISettingsStore settings,
        IMonitorController monitor,
        IFileScanner scanner,
        IShellLauncher shell,
        IUserPrompt prompt,
        INotificationService notifications)
        : base(text)
    {
        _organization = organization;
        _activities = activities;
        _rules = rules;
        _settings = settings;
        _monitor = monitor;
        _scanner = scanner;
        _shell = shell;
        _prompt = prompt;
        _notifications = notifications;
        _monitor.StateChanged += (_, _) => RefreshMonitor();
    }

    public ObservableCollection<string> Recent { get; } = [];

    public ObservableCollection<string> Storage { get; } = [];

    [ObservableProperty] private string _organized = "0";
    [ObservableProperty] private string _pending = "0";
    [ObservableProperty] private string _activeRules = "0";
    [ObservableProperty] private string _desktopFiles = "0";
    [ObservableProperty] private string _lastRun = string.Empty;
    [ObservableProperty] private string _monitorLine = string.Empty;
    [ObservableProperty] private string _pauseText = string.Empty;

    public bool HasRecent => Recent.Count > 0;
    public bool HasStorage => Storage.Count > 0;

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        return GuardAsync(LoadCoreAsync, cancellationToken);
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        return LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private Task OrganizeDesktopAsync()
    {
        return OrganizeAsync(MonitorScope.Desktop);
    }

    [RelayCommand]
    private Task OrganizeDownloadsAsync()
    {
        return OrganizeAsync(MonitorScope.Downloads);
    }

    [RelayCommand]
    private void TogglePause()
    {
        if (_monitor.IsPaused)
        {
            _monitor.Resume();
        }
        else
        {
            _monitor.Pause();
        }

        RefreshMonitor();
    }

    [RelayCommand]
    private void OpenDesktop()
    {
        try
        {
            _shell.OpenFolder(_settings.Current.DesktopPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    protected override void OnLanguageChanged()
    {
        RefreshMonitor();
        _ = LoadAsync(CancellationToken.None);
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        var preferences = await _settings.GetAsync(cancellationToken);
        var desktop = await _organization.PreviewAsync(MonitorScope.Desktop, cancellationToken);
        var downloads = await _organization.PreviewAsync(MonitorScope.Downloads, cancellationToken);
        var rules = await _rules.GetAllAsync(cancellationToken);
        var organized = await _activities.CountSucceededMovesAsync(cancellationToken);
        var last = await _activities.LastSucceededMoveAsync(cancellationToken);
        var recent = await _activities.GetRecentAsync(8, cancellationToken);
        var slices = await Task.Run(() => _scanner.Summarize(preferences.DesktopPath), cancellationToken);

        DesktopFiles = desktop.Count(plan => plan.SkipReason != SkipReason.Directory).ToString();
        Pending = (desktop.Count(plan => plan.WillMove) + downloads.Count(plan => plan.WillMove)).ToString();
        ActiveRules = rules.Count(rule => rule.IsEnabled).ToString();
        Organized = organized.ToString();
        LastRun = last is null ? Text["Overview_Never"] : DisplayText.When(last.Value);
        Recent.Clear();
        foreach (var record in recent)
        {
            Recent.Add($"{DisplayText.When(record.Timestamp)}  {record.FileName}  {TextMaps.Status(Text, record.Status)}");
        }

        Storage.Clear();
        foreach (var slice in slices.Take(8))
        {
            var name = slice.Name == "Loose files" ? Text["Storage_Loose"] : slice.Name;
            Storage.Add($"{name}   {DisplayText.Bytes(slice.Bytes)}");
        }

        RefreshMonitor();
        OnPropertyChanged(nameof(HasRecent));
        OnPropertyChanged(nameof(HasStorage));
    }

    private async Task OrganizeAsync(MonitorScope scope)
    {
        await GuardAsync(async cancellationToken =>
        {
            var preview = await _organization.PreviewAsync(scope, cancellationToken);
            var moving = preview.Count(plan => plan.WillMove);
            if (moving == 0)
            {
                await _prompt.ShowAsync(Text["Quick_Title"], Text["Dialog_NoMoves"]);
                return;
            }

            var preferences = _settings.Current;
            if (preferences.PreviewBeforeOrganize || preferences.ConfirmBeforeBulk)
            {
                var accepted = await _prompt.ConfirmMovesAsync(preview);
                if (!accepted)
                {
                    return;
                }
            }

            var result = await _organization.OrganizeAsync(scope, null, cancellationToken);
            var message = Text.Format("Dialog_Result", result.Moved, result.Skipped, result.Failed);
            if (result.Errors.Count > 0)
            {
                message += Environment.NewLine + result.Errors[0];
            }

            _notifications.Show(Text["Notify_BatchTitle"], message);
            await _prompt.ShowAsync(Text["Quick_Title"], message);
            await LoadCoreAsync(cancellationToken);
        }, CancellationToken.None);
    }

    private void RefreshMonitor()
    {
        PauseText = _monitor.IsPaused ? Text["Quick_Resume"] : Text["Quick_Pause"];
        MonitorLine = $"{Text["Monitor_Desktop"]}: {TextMaps.State(Text, _monitor.DesktopState)}    {Text["Monitor_Downloads"]}: {TextMaps.State(Text, _monitor.DownloadsState)}";
        if (!string.IsNullOrWhiteSpace(_monitor.LastError) && _monitor.DesktopState == MonitorRunState.Error)
        {
            ErrorMessage = _monitor.LastError;
        }
    }
}
