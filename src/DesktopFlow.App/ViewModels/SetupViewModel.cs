using System.Text;
using DesktopFlow.App.Localization;
using DesktopFlow.App.Services;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;

namespace DesktopFlow.App.ViewModels;

public partial class SetupViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private readonly IOrganizationService _organization;
    private readonly IStartupRegistration _startup;
    private readonly IMonitorController _monitor;
    private readonly IFolderBrowser _folders;
    private readonly IUserPrompt _prompt;
    private readonly NavigationService _navigation;
    private readonly INotificationService _notificationService;

    public SetupViewModel(
        ILocalizer text,
        ISettingsStore settings,
        IOrganizationService organization,
        IStartupRegistration startup,
        IMonitorController monitor,
        IFolderBrowser folders,
        IUserPrompt prompt,
        NavigationService navigation,
        INotificationService notifications)
        : base(text)
    {
        _settings = settings;
        _organization = organization;
        _startup = startup;
        _monitor = monitor;
        _folders = folders;
        _prompt = prompt;
        _navigation = navigation;
        _notificationService = notifications;
    }

    [ObservableProperty] private int _step;
    [ObservableProperty] private bool _watchDesktop = true;
    [ObservableProperty] private bool _watchDownloads;
    [ObservableProperty] private bool _keepShortcuts = true;
    [ObservableProperty] private bool _automatic;
    [ObservableProperty] private double _delay = 2000;
    [ObservableProperty] private bool _notifications = true;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _organizeNow;
    [ObservableProperty] private string _desktopPath = string.Empty;
    [ObservableProperty] private string _downloadsPath = string.Empty;
    [ObservableProperty] private string _previewText = string.Empty;

    public string StepLabel => Text.Format("Setup_Step", Step + 1);
    public bool IsWelcome => Step == 0;
    public bool IsFolders => Step == 1;
    public bool IsPreferences => Step == 2;
    public bool IsBehavior => Step == 3;
    public bool IsPreview => Step == 4;
    public bool IsFinish => Step == 5;
    public bool CanGoBack => Step > 0;
    public string ForwardText => IsFinish ? Text["Setup_Finish"] : Text["Setup_Next"];

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var preferences = await _settings.GetAsync(cancellationToken);
        DesktopPath = preferences.DesktopPath;
        DownloadsPath = preferences.DownloadsPath;
        KeepShortcuts = preferences.KeepShortcuts;
        OnPropertyChanged(nameof(StepLabel));
    }

    [RelayCommand]
    private async Task ForwardAsync()
    {
        if (Step < 5)
        {
            Step++;
            RaiseSteps();
            if (Step == 4)
            {
                await BuildPreviewAsync();
            }

            return;
        }

        await FinishAsync();
    }

    [RelayCommand]
    private void Back()
    {
        if (Step == 0)
        {
            return;
        }

        Step--;
        RaiseSteps();
    }

    [RelayCommand]
    private async Task BrowseDesktopAsync()
    {
        var path = await _folders.PickAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            DesktopPath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseDownloadsAsync()
    {
        var path = await _folders.PickAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            DownloadsPath = path;
        }
    }

    protected override void OnLanguageChanged()
    {
        RaiseSteps();
    }

    private async Task BuildPreviewAsync()
    {
        await GuardAsync(async cancellationToken =>
        {
            var preferences = await _settings.GetAsync(cancellationToken);
            preferences.DesktopPath = DesktopPath;
            preferences.DownloadsPath = DownloadsPath;
            preferences.KeepShortcuts = KeepShortcuts;
            await _settings.SaveAsync(preferences, cancellationToken);
            var desktop = await _organization.PreviewAsync(MonitorScope.Desktop, cancellationToken);
            var builder = new StringBuilder();
            builder.AppendLine(Text["Setup_PreviewBody"]);
            foreach (var group in desktop.Where(plan => plan.WillMove).GroupBy(plan => plan.RelativeFolder).OrderBy(group => group.Key))
            {
                builder.AppendLine($"{group.Key}: {group.Count()}");
            }

            var staying = desktop.Count(plan => !plan.WillMove);
            builder.Append(Text.Format("Dialog_OrganizeBody", desktop.Count(plan => plan.WillMove), staying));
            PreviewText = builder.ToString();
        }, CancellationToken.None);
    }

    private async Task FinishAsync()
    {
        await GuardAsync(async cancellationToken =>
        {
            if (!Directory.Exists(DesktopPath) || !Directory.Exists(DownloadsPath))
            {
                ErrorMessage = Text["Validation_Path"];
                return;
            }

            var preferences = await _settings.GetAsync(cancellationToken);
            preferences.DesktopPath = DesktopPath;
            preferences.DownloadsPath = DownloadsPath;
            preferences.KeepShortcuts = KeepShortcuts;
            preferences.MonitorDesktop = WatchDesktop;
            preferences.MonitorDownloads = WatchDownloads;
            preferences.AutomaticOrganization = Automatic;
            preferences.FileStabilityDelayMs = (int)Math.Clamp(double.IsNaN(Delay) ? 2000 : Delay, 500, 60000);
            preferences.ShowNotifications = Notifications;
            preferences.StartWithWindows = StartWithWindows;
            preferences.StartMinimized = StartMinimized;
            preferences.HasCompletedSetup = true;
            await _settings.SaveAsync(preferences, cancellationToken);
            if (StartWithWindows)
            {
                await _startup.SetEnabledAsync(true, StartMinimized, cancellationToken);
            }

            _monitor.Reload();
            if (OrganizeNow)
            {
                var preview = await _organization.PreviewAsync(MonitorScope.Desktop, cancellationToken);
                if (preview.Any(plan => plan.WillMove) && await _prompt.ConfirmMovesAsync(preview))
                {
                    var result = await _organization.OrganizeAsync(MonitorScope.Desktop, null, cancellationToken);
                    var message = Text.Format("Dialog_Result", result.Moved, result.Skipped, result.Failed);
                    _notificationService.Show(Text["Notify_BatchTitle"], message);
                    await _prompt.ShowAsync(Text["Setup_DoneTitle"], message);
                }
            }

            _navigation.SetPaneVisible(true);
            _navigation.Navigate("overview");
        }, CancellationToken.None);
    }

    private void RaiseSteps()
    {
        OnPropertyChanged(nameof(StepLabel));
        OnPropertyChanged(nameof(IsWelcome));
        OnPropertyChanged(nameof(IsFolders));
        OnPropertyChanged(nameof(IsPreferences));
        OnPropertyChanged(nameof(IsBehavior));
        OnPropertyChanged(nameof(IsPreview));
        OnPropertyChanged(nameof(IsFinish));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(ForwardText));
    }
}
