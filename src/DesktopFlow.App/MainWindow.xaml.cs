using DesktopFlow.App.Localization;
using DesktopFlow.App.Services;
using DesktopFlow.App.ViewModels;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace DesktopFlow.App;

public sealed partial class MainWindow : Window, IThemeApplier
{
    private readonly NavigationService _navigation;
    private readonly ISettingsStore _settings;
    private readonly ILocalizer _text;
    private readonly IUserPrompt _prompt;
    private readonly IFolderBrowser _folders;
    private readonly AppNotificationService _notifications;
    private readonly IMonitorController _monitor;
    private bool _allowExit;
    private H.NotifyIcon.TaskbarIcon? _tray;

    public MainWindow(
        ShellViewModel shell,
        NavigationService navigation,
        ISettingsStore settings,
        ILocalizer text,
        IUserPrompt prompt,
        IFolderBrowser folders,
        AppNotificationService notifications,
        IMonitorController monitor)
    {
        _navigation = navigation;
        _settings = settings;
        _text = text;
        _prompt = prompt;
        _folders = folders;
        _notifications = notifications;
        _monitor = monitor;
        InitializeComponent();
        Root.DataContext = shell;
        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(icon))
        {
            AppWindow.SetIcon(icon);
        }

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 960;
            presenter.PreferredMinimumHeight = 640;
        }

        AppWindow.Resize(new SizeInt32(1200, 800));
        _navigation.Attach(ContentFrame, Nav);
        Root.Loaded += (_, _) =>
        {
            AttachUi();
            EnsureTray();
        };
        Closed += OnClosed;
        AppWindow.Closing += OnClosing;
        Activated += OnActivated;

        if (!_settings.Current.HasCompletedSetup)
        {
            _navigation.SetPaneVisible(false);
            _navigation.Navigate("setup");
        }
        else
        {
            _navigation.Navigate("overview");
        }
    }

    public void Apply(AppThemePreference theme)
    {
        Root.RequestedTheme = theme switch
        {
            AppThemePreference.Dark => ElementTheme.Dark,
            AppThemePreference.Light => ElementTheme.Light,
            AppThemePreference.System => ElementTheme.Default,
            _ => throw new System.Diagnostics.UnreachableException(theme.ToString()),
        };
    }

    public void BringToFront()
    {
        AppWindow.Show();
        Activate();
    }

    public void HideToTray()
    {
        AppWindow.Hide();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        if (Content is FrameworkElement { XamlRoot: { } root })
        {
            root.Changed += (_, _) => AttachUi();
        }

        AttachUi();
    }

    private void AttachUi()
    {
        if (Content?.XamlRoot is null)
        {
            return;
        }

        _prompt.Attach(Content.XamlRoot);
        _folders.Attach(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    private void EnsureTray()
    {
        if (_tray is not null)
        {
            return;
        }

        try
        {
            _tray = new H.NotifyIcon.TaskbarIcon
            {
                ToolTipText = "DesktopFlow",
                IconSource = new H.NotifyIcon.GeneratedIconSource { Text = "DF" },
            };
            _tray.ForceCreate(false);
            _tray.LeftClickCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(BringToFront);
            _notifications.TrayFallback = (title, body) => _tray.ShowNotification(title, body);
            _text.LanguageChanged += (_, _) => BuildTrayMenu();
            BuildTrayMenu();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            _tray?.Dispose();
            _tray = null;
        }
    }

    private void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is string tag)
        {
            _navigation.Navigate(tag);
        }
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        var setupFinished = _settings.Current.HasCompletedSetup;
        if (_allowExit || !setupFinished || !_settings.Current.MinimizeToTray)
        {
            return;
        }

        args.Cancel = true;
        HideToTray();
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        _tray?.Dispose();
        if (Application.Current is App app)
        {
            await app.ShutdownAsync();
        }

        Environment.Exit(0);
    }

    private void BuildTrayMenu()
    {
        var menu = new MenuFlyout();
        menu.Items.Add(Item(_text["Tray_Open"], BringToFront));
        menu.Items.Add(Item(_monitor.IsPaused ? _text["Tray_Resume"] : _text["Tray_Pause"], TogglePause));
        menu.Items.Add(Item(_text["Tray_Organize"], OrganizeNow));
        menu.Items.Add(Item(_text["Tray_Settings"], OpenSettings));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item(_text["Tray_Exit"], ExitApplication));
        if (_tray is not null)
        {
            _tray.ContextFlyout = menu;
        }
    }

    private MenuFlyoutItem Item(string label, Action action)
    {
        var item = new MenuFlyoutItem { Text = label };
        item.Click += (_, _) => action();
        return item;
    }

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

        BuildTrayMenu();
    }

    private async void OrganizeNow()
    {
        try
        {
            if (_settings.Current.MonitorDesktop || _settings.Current.HasCompletedSetup)
            {
                await _monitor.OrganizeNowAsync(MonitorScope.Desktop, CancellationToken.None);
            }

            if (_settings.Current.MonitorDownloads)
            {
                await _monitor.OrganizeNowAsync(MonitorScope.Downloads, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
        }
    }

    private void OpenSettings()
    {
        BringToFront();
        _navigation.SetPaneVisible(true);
        _navigation.Navigate("settings");
    }

    private void ExitApplication()
    {
        _allowExit = true;
        Close();
    }
}

