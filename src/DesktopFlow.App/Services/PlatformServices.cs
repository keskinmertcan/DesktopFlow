using System.Diagnostics;
using System.Runtime.InteropServices;
using DesktopFlow.App.Localization;
using DesktopFlow.Core;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Startup;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Windows.ApplicationModel;

namespace DesktopFlow.App.Services;

public sealed class WindowsStartupService : IStartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DesktopFlow";

    public string? LastMessage { get; private set; }

    public async Task<bool> GetEnabledAsync(CancellationToken cancellationToken)
    {
        if (PackageIdentity.IsPackaged)
        {
            var task = await StartupTask.GetAsync(ProductInfo.StartupTaskId);
            return task.State == StartupTaskState.Enabled;
        }

        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    public async Task<bool> SetEnabledAsync(bool enabled, bool minimized, CancellationToken cancellationToken)
    {
        LastMessage = null;
        if (PackageIdentity.IsPackaged)
        {
            return await SetPackagedAsync(enabled);
        }

        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (!enabled)
        {
            key.DeleteValue(ValueName, false);
            return true;
        }

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            LastMessage = "The application path is not available.";
            return false;
        }

        key.SetValue(ValueName, StartupCommand.Build(executable, minimized));
        return true;
    }

    private async Task<bool> SetPackagedAsync(bool enabled)
    {
        var task = await StartupTask.GetAsync(ProductInfo.StartupTaskId);
        if (!enabled)
        {
            task.Disable();
            return true;
        }

        switch (task.State)
        {
            case StartupTaskState.Enabled:
            case StartupTaskState.EnabledByPolicy:
                return true;
            case StartupTaskState.Disabled:
                var result = await task.RequestEnableAsync();
                var accepted = result == StartupTaskState.Enabled;
                if (!accepted)
                {
                    LastMessage = "Windows did not enable startup.";
                }

                return accepted;
            case StartupTaskState.DisabledByUser:
            case StartupTaskState.DisabledByPolicy:
                LastMessage = "Windows did not enable startup.";
                return false;
            default:
                throw new UnreachableException(task.State.ToString());
        }
    }
}

public sealed class AppNotificationService : INotificationService
{
    private readonly ISettingsStore _settings;
    private readonly IAppLogger _logger;
    private int _registered;

    public AppNotificationService(ISettingsStore settings, IAppLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public Action<string, string>? TrayFallback { get; set; }

    public void Show(string title, string body)
    {
        if (!_settings.Current.ShowNotifications)
        {
            return;
        }

        try
        {
            if (Interlocked.Exchange(ref _registered, 1) == 0)
            {
                AppNotificationManager.Default.Register();
            }

            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body)
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            _logger.Error("Windows notification failed. Using the tray notification.", ex);
            TrayFallback?.Invoke(title, body);
        }
    }
}

public sealed class ActivityNotifier : IHostedService
{
    private readonly IMonitorController _monitor;
    private readonly INotificationService _notifications;
    private readonly ISettingsStore _settings;
    private readonly ILocalizer _text;
    private readonly IUiDispatcher _dispatcher;

    public ActivityNotifier(
        IMonitorController monitor,
        INotificationService notifications,
        ISettingsStore settings,
        ILocalizer text,
        IUiDispatcher dispatcher)
    {
        _monitor = monitor;
        _notifications = notifications;
        _settings = settings;
        _text = text;
        _dispatcher = dispatcher;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _monitor.ActivityRecorded += OnActivity;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _monitor.ActivityRecorded -= OnActivity;
        return Task.CompletedTask;
    }

    private void OnActivity(object? sender, ActivityRecord record)
    {
        if (!_settings.Current.ShowNotifications || record.Kind != ActivityKind.Move)
        {
            return;
        }

        _dispatcher.Enqueue(() =>
        {
            if (record.Status == ActivityStatus.Succeeded)
            {
                _notifications.Show(_text["Notify_MovedTitle"], _text.Format("Notify_MovedBody", record.FileName));
                return;
            }

            if (record.Status == ActivityStatus.Failed)
            {
                _notifications.Show(_text["Notify_FailedTitle"], string.IsNullOrWhiteSpace(record.ErrorMessage) ? record.FileName : record.ErrorMessage);
            }
        });
    }
}

internal static class PackageIdentity
{
    public static bool IsPackaged
    {
        get
        {
            try
            {
                var length = 0u;
                var result = GetCurrentPackageFullName(ref length, null);
                return result != 15700;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                return false;
            }
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, System.Text.StringBuilder? packageFullName);
}

public interface IThemeApplier
{
    void Apply(AppThemePreference theme);
}

public interface IFolderBrowser
{
    void Attach(nint windowHandle);

    Task<string?> PickAsync();
}

public sealed class FolderBrowser : IFolderBrowser
{
    private nint _windowHandle;

    public void Attach(nint windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public async Task<string?> PickAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowHandle);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
