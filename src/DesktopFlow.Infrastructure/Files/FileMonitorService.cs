using System.Collections.Concurrent;
using System.Threading.Channels;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Startup;
using Microsoft.Extensions.Hosting;

namespace DesktopFlow.Infrastructure.Files;

public sealed class FileMonitorService : BackgroundService, IMonitorController
{
    private readonly ISettingsStore _settings;
    private readonly IOrganizationService _organization;
    private readonly IRecoveryService _recovery;
    private readonly IFileScanner _scanner;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _retries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Channel<bool> _signal = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions { SingleReader = true });
    private readonly object _watcherGate = new();
    private bool _userPaused;
    private bool _startupPaused;

    public FileMonitorService(
        ISettingsStore settings,
        IOrganizationService organization,
        IRecoveryService recovery,
        IFileScanner scanner,
        IClock clock,
        IAppLogger logger)
    {
        _settings = settings;
        _organization = organization;
        _recovery = recovery;
        _scanner = scanner;
        _clock = clock;
        _logger = logger;
    }

    public bool IsPaused => _userPaused || _startupPaused;

    public MonitorRunState DesktopState { get; private set; } = MonitorRunState.Off;

    public MonitorRunState DownloadsState { get; private set; } = MonitorRunState.Off;

    public string? LastError { get; private set; }

    public event EventHandler? StateChanged;

    public event EventHandler<ActivityRecord>? ActivityRecorded;

    public void Pause()
    {
        _userPaused = true;
        var preferences = _settings.Current;
        preferences.AutomationPaused = true;
        _ = PersistAsync(preferences);
        Reload();
    }

    public void Resume()
    {
        _userPaused = false;
        _startupPaused = false;
        var preferences = _settings.Current;
        preferences.AutomationPaused = false;
        _ = PersistAsync(preferences);
        Reload();
    }

    public async Task OrganizeNowAsync(MonitorScope scope, CancellationToken cancellationToken)
    {
        var result = await _organization.OrganizeAsync(scope, null, cancellationToken);
        _logger.Info($"Organize now ({scope}): moved {result.Moved}, skipped {result.Skipped}, failed {result.Failed}.");
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reload()
    {
        var preferences = _settings.Current;
        _userPaused = preferences.AutomationPaused;
        SyncWatchers(preferences);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _recovery.RecoverAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error("Recovery failed.", ex);
            LastError = ex.Message;
        }

        var preferences = await _settings.GetAsync(stoppingToken);
        _userPaused = preferences.AutomationPaused;
        if (IsStartupLaunch() && !preferences.RunAutomationOnStartup)
        {
            _startupPaused = true;
        }

        SyncWatchers(preferences);
        StateChanged?.Invoke(this, EventArgs.Empty);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _signal.Reader.WaitToReadAsync(stoppingToken);
                while (_signal.Reader.TryRead(out _))
                {
                }

                await Task.Delay(350, stoppingToken);
                await ProcessReadyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("The file monitor loop failed.", ex);
                LastError = ex.Message;
                try
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public override void Dispose()
    {
        DisposeWatchers();
        base.Dispose();
    }

    private async Task ProcessReadyAsync(CancellationToken cancellationToken)
    {
        if (IsPaused || !_settings.Current.AutomaticOrganization)
        {
            _pending.Clear();
            return;
        }

        var delay = TimeSpan.FromMilliseconds(_settings.Current.ClampDelay());
        var ready = _pending
            .Where(pair => _clock.UtcNow - pair.Value >= delay)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var path in ready)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_pending.TryRemove(path, out _))
            {
                continue;
            }

            if (!_inFlight.TryAdd(path, 1))
            {
                continue;
            }

            try
            {
                var result = await _organization.OrganizeFileIfNeededAsync(path, cancellationToken);
                if (result.ShouldRetry)
                {
                    var count = _retries.AddOrUpdate(path, 1, (_, current) => current + 1);
                    if (count <= 40)
                    {
                        _pending[path] = _clock.UtcNow;
                        _signal.Writer.TryWrite(true);
                    }
                    else
                    {
                        _retries.TryRemove(path, out _);
                        _logger.Info($"Stopped waiting for '{path}'.");
                    }

                    continue;
                }

                _retries.TryRemove(path, out _);
                if (result.Activity is not null)
                {
                    ActivityRecorded?.Invoke(this, result.Activity);
                }
            }
            finally
            {
                _inFlight.TryRemove(path, out _);
            }
        }
    }

    private void SyncWatchers(AppPreferences preferences)
    {
        lock (_watcherGate)
        {
            DisposeWatchers();
            DesktopState = CreateWatcher(preferences, MonitorScope.Desktop);
            DownloadsState = CreateWatcher(preferences, MonitorScope.Downloads);
        }
    }

    private MonitorRunState CreateWatcher(AppPreferences preferences, MonitorScope scope)
    {
        if (!preferences.HasCompletedSetup || !preferences.MonitoringEnabled(scope))
        {
            return MonitorRunState.Off;
        }

        var root = preferences.RootFor(scope);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            LastError = $"The folder is not available: {root}";
            return MonitorRunState.Error;
        }

        if (IsPaused)
        {
            return MonitorRunState.Paused;
        }

        try
        {
            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
                EnableRaisingEvents = true,
            };
            watcher.Created += (_, args) => Enqueue(args.FullPath);
            watcher.Renamed += (_, args) => Enqueue(args.FullPath);
            watcher.Changed += (_, args) => TouchIfPending(args.FullPath);
            watcher.Error += (_, args) => OnWatcherError(args, root);
            _watchers.Add(watcher);
            return MonitorRunState.Watching;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            LastError = ex.Message;
            _logger.Error($"Could not watch '{root}'.", ex);
            return MonitorRunState.Error;
        }
    }

    private void OnWatcherError(ErrorEventArgs args, string root)
    {
        var exception = args.GetException();
        LastError = exception.Message;
        _logger.Error("The file watcher missed events. DesktopFlow is scanning the folder again.", exception);
        try
        {
            foreach (var file in _scanner.ListTopLevel(root))
            {
                if (!file.IsDirectory)
                {
                    Enqueue(file.FullPath);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("The recovery scan failed.", ex);
        }

        Reload();
    }

    private void Enqueue(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _pending[path] = _clock.UtcNow;
        _signal.Writer.TryWrite(true);
    }

    private void TouchIfPending(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !_pending.ContainsKey(path))
        {
            return;
        }

        _pending[path] = _clock.UtcNow;
    }

    private async Task PersistAsync(AppPreferences preferences)
    {
        try
        {
            await _settings.SaveAsync(preferences, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Error("Could not save the automation pause state.", ex);
        }
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    private static bool IsStartupLaunch()
    {
        return Environment.GetCommandLineArgs().Any(StartupCommand.IsStartupArgument);
    }
}
