using DesktopFlow.App.Localization;
using DesktopFlow.App.Services;
using DesktopFlow.Core;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;

namespace DesktopFlow.App.ViewModels;

public sealed class Choice
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public override string ToString() => Label;
}

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private readonly IStartupRegistration _startup;
    private readonly IMonitorController _monitor;
    private readonly IThemeApplier _theme;
    private readonly IFolderBrowser _folders;
    private readonly IShellLauncher _shell;
    private readonly IUserPrompt _prompt;

    public SettingsViewModel(
        ILocalizer text,
        ISettingsStore settings,
        IStartupRegistration startup,
        IMonitorController monitor,
        IThemeApplier theme,
        IFolderBrowser folders,
        IShellLauncher shell,
        IUserPrompt prompt)
        : base(text)
    {
        _settings = settings;
        _startup = startup;
        _monitor = monitor;
        _theme = theme;
        _folders = folders;
        _shell = shell;
        _prompt = prompt;
        Languages.Add(new Choice { Id = "en-US", Label = text["Language_English"] });
        Languages.Add(new Choice { Id = "tr-TR", Label = text["Language_Turkish"] });
        Themes.Add(new Choice { Id = nameof(AppThemePreference.Dark), Label = text["Theme_Dark"] });
        Themes.Add(new Choice { Id = nameof(AppThemePreference.Light), Label = text["Theme_Light"] });
        Themes.Add(new Choice { Id = nameof(AppThemePreference.System), Label = text["Theme_System"] });
        VersionLabel = text.Format("Footer_Version", typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");
    }

    public IList<Choice> Languages { get; } = [];
    public IList<Choice> Themes { get; } = [];
    public string VersionLabel { get; }
    public string Repository => ProductInfo.RepositoryUrl;
    public string License => ProductInfo.LicenseName;

    [ObservableProperty] private Choice? _language;
    [ObservableProperty] private Choice? _themeChoice;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _minimizeToTray = true;
    [ObservableProperty] private bool _monitorDesktop;
    [ObservableProperty] private bool _monitorDownloads;
    [ObservableProperty] private bool _automatic;
    [ObservableProperty] private double _delay = 2000;
    [ObservableProperty] private bool _notifications = true;
    [ObservableProperty] private bool _runOnStartup;
    [ObservableProperty] private string _desktopPath = string.Empty;
    [ObservableProperty] private string _downloadsPath = string.Empty;
    [ObservableProperty] private string _exclusions = string.Empty;
    [ObservableProperty] private bool _undoHistory = true;
    [ObservableProperty] private bool _confirmBulk = true;
    [ObservableProperty] private bool _preview = true;
    [ObservableProperty] private bool _keepShortcuts = true;
    [ObservableProperty] private string _savedMessage = string.Empty;

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        return GuardAsync(async token =>
        {
            var preferences = await _settings.GetAsync(token);
            Language = Languages.FirstOrDefault(item => item.Id == preferences.Language) ?? Languages[0];
            ThemeChoice = Themes.FirstOrDefault(item => item.Id == preferences.Theme.ToString()) ?? Themes[0];
            StartWithWindows = await _startup.GetEnabledAsync(token);
            StartMinimized = preferences.StartMinimized;
            MinimizeToTray = preferences.MinimizeToTray;
            MonitorDesktop = preferences.MonitorDesktop;
            MonitorDownloads = preferences.MonitorDownloads;
            Automatic = preferences.AutomaticOrganization;
            Delay = preferences.ClampDelay();
            Notifications = preferences.ShowNotifications;
            RunOnStartup = preferences.RunAutomationOnStartup;
            DesktopPath = preferences.DesktopPath;
            DownloadsPath = preferences.DownloadsPath;
            Exclusions = string.Join(Environment.NewLine, preferences.ExcludedPatterns);
            UndoHistory = preferences.EnableUndoHistory;
            ConfirmBulk = preferences.ConfirmBeforeBulk;
            Preview = preferences.PreviewBeforeOrganize;
            KeepShortcuts = preferences.KeepShortcuts;
            if (!string.IsNullOrWhiteSpace(_startup.LastMessage))
            {
                ErrorMessage = Text["Message_StartupBlocked"];
            }
        }, cancellationToken);
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

    [RelayCommand]
    private void OpenRepository()
    {
        _shell.OpenUrl(ProductInfo.RepositoryUrl);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await GuardAsync(async cancellationToken =>
        {
            if (!Directory.Exists(DesktopPath) || !Directory.Exists(DownloadsPath))
            {
                ErrorMessage = Text["Validation_Path"];
                return;
            }

            var preferences = await _settings.GetAsync(cancellationToken);
            preferences.Language = Language?.Id ?? "en-US";
            preferences.Theme = Enum.Parse<AppThemePreference>(ThemeChoice?.Id ?? nameof(AppThemePreference.Dark));
            preferences.StartMinimized = StartMinimized;
            preferences.MinimizeToTray = MinimizeToTray;
            preferences.MonitorDesktop = MonitorDesktop;
            preferences.MonitorDownloads = MonitorDownloads;
            preferences.AutomaticOrganization = Automatic;
            preferences.FileStabilityDelayMs = (int)Math.Clamp(double.IsNaN(Delay) ? 2000 : Delay, 500, 60000);
            preferences.ShowNotifications = Notifications;
            preferences.RunAutomationOnStartup = RunOnStartup;
            preferences.DesktopPath = DesktopPath;
            preferences.DownloadsPath = DownloadsPath;
            preferences.ExcludedPatterns = Exclusions
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (preferences.ExcludedPatterns.Count == 0)
            {
                preferences.ExcludedPatterns.Add("desktop.ini");
            }

            preferences.EnableUndoHistory = UndoHistory;
            preferences.ConfirmBeforeBulk = ConfirmBulk;
            preferences.PreviewBeforeOrganize = Preview;
            preferences.KeepShortcuts = KeepShortcuts;
            await _settings.SaveAsync(preferences, cancellationToken);
            Text.SetLanguage(preferences.Language);
            _theme.Apply(preferences.Theme);
            var enabled = await _startup.SetEnabledAsync(StartWithWindows, StartMinimized, cancellationToken);
            StartWithWindows = enabled && StartWithWindows;
            if (!enabled && StartWithWindows)
            {
                ErrorMessage = Text["Message_StartupBlocked"];
                return;
            }

            _monitor.Reload();
            SavedMessage = Text["Settings_Saved"];
            await _prompt.ShowAsync(Text["Settings_Title"], Text["Settings_Saved"]);
        }, CancellationToken.None);
    }
}
