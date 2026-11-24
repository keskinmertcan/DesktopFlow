using DesktopFlow.App.Localization;
using DesktopFlow.App.Services;
using DesktopFlow.App.ViewModels;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace DesktopFlow.App;

public partial class App : Application
{
    private IHost? _host;
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private CancellationTokenSource? _showLoop;
    private int _shutdownStarted;

    public App()
    {
        UnhandledException += (_, args) =>
        {
            CrashLog.Write(args.Exception);
            args.Handled = true;
        };
        InitializeComponent();
    }

    public static T Get<T>()
        where T : notnull
    {
        var host = ((App)Current)._host ?? throw new InvalidOperationException("DesktopFlow is not ready.");
        return host.Services.GetRequiredService<T>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var ui = DispatcherQueue.GetForCurrentThread();
        _ = Task.Run(() => StartAsync(ui));
    }

    public async Task ShutdownAsync()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        {
            return;
        }

        _showLoop?.Cancel();
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
            _host = null;
        }

        _showEvent?.Dispose();
        if (_mutex is not null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    private async Task StartAsync(DispatcherQueue ui)
    {
        try
        {
            if (!SingleInstance.TryBecomePrimary(out _mutex))
            {
                Exit();
                return;
            }

            _showEvent = SingleInstance.CreateShowEvent();
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddDesktopFlowInfrastructure();
            builder.Services.AddSingleton<TranslationSource>();
            builder.Services.AddSingleton<ILocalizer>(provider => provider.GetRequiredService<TranslationSource>());
            builder.Services.AddSingleton<WinUiDispatcher>();
            builder.Services.AddSingleton<IUiDispatcher>(provider => provider.GetRequiredService<WinUiDispatcher>());
            builder.Services.AddSingleton<DialogService>();
            builder.Services.AddSingleton<IUserPrompt>(provider => provider.GetRequiredService<DialogService>());
            builder.Services.AddSingleton<FolderBrowser>();
            builder.Services.AddSingleton<IFolderBrowser>(provider => provider.GetRequiredService<FolderBrowser>());
            builder.Services.AddSingleton<WindowsStartupService>();
            builder.Services.AddSingleton<IStartupRegistration>(provider => provider.GetRequiredService<WindowsStartupService>());
            builder.Services.AddSingleton<AppNotificationService>();
            builder.Services.AddSingleton<INotificationService>(provider => provider.GetRequiredService<AppNotificationService>());
            builder.Services.AddSingleton<NavigationService>();
            builder.Services.AddSingleton<ShellViewModel>();
            builder.Services.AddTransient<OverviewViewModel>();
            builder.Services.AddTransient<FolderViewModel>();
            builder.Services.AddTransient<OrganizerViewModel>();
            builder.Services.AddTransient<RulesViewModel>();
            builder.Services.AddTransient<ActivityViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<SetupViewModel>();
            builder.Services.AddSingleton<MainWindow>();
            builder.Services.AddSingleton<IThemeApplier>(provider => provider.GetRequiredService<MainWindow>());
            builder.Services.AddHostedService<ActivityNotifier>();
            _host = builder.Build();
            await _host.StartAsync().ConfigureAwait(false);

            var settings = await _host.Services.GetRequiredService<ISettingsStore>().GetAsync(CancellationToken.None).ConfigureAwait(false);
            var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!ui.TryEnqueue(() => OpenWindow(settings, shown)))
            {
                throw new InvalidOperationException("DesktopFlow could not open its window.");
            }

            await shown.Task;
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            Exit();
        }
    }

    private void OpenWindow(Core.Models.AppPreferences settings, TaskCompletionSource shown)
    {
        try
        {
            var localizer = _host!.Services.GetRequiredService<TranslationSource>();
            localizer.SetLanguage(settings.Language);
            Resources["Loc"] = localizer;

            var window = _host.Services.GetRequiredService<MainWindow>();
            window.Apply(settings.Theme);
            _host.Services.GetRequiredService<IUiDispatcher>().Attach(window.DispatcherQueue);
            var startMinimized = settings.HasCompletedSetup
                && Environment.GetCommandLineArgs().Any(Core.Startup.StartupCommand.IsMinimizedArgument);
            if (startMinimized)
            {
                window.HideToTray();
            }
            else
            {
                window.Activate();
            }

            _showLoop = new CancellationTokenSource();
            var loop = _showLoop.Token;
            var showEvent = _showEvent;
            _ = Task.Run(() =>
            {
                while (!loop.IsCancellationRequested && showEvent is not null)
                {
                    if (showEvent.WaitOne(500))
                    {
                        window.DispatcherQueue.TryEnqueue(window.BringToFront);
                    }
                }
            }, loop);
            shown.SetResult();
        }
        catch (Exception ex)
        {
            shown.SetException(ex);
        }
    }

}
