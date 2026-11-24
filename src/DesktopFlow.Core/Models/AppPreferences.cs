namespace DesktopFlow.Core.Models;

public sealed class AppPreferences
{
    public string Language { get; set; } = "en-US";

    public AppThemePreference Theme { get; set; } = AppThemePreference.Dark;

    public bool StartWithWindows { get; set; }

    public bool StartMinimized { get; set; }

    public bool RunAutomationOnStartup { get; set; }

    public bool ShowNotifications { get; set; } = true;

    public bool MinimizeToTray { get; set; } = true;

    public bool MonitorDesktop { get; set; }

    public bool MonitorDownloads { get; set; }

    public bool AutomaticOrganization { get; set; }

    public bool AutomationPaused { get; set; }

    public int FileStabilityDelayMs { get; set; } = 2000;

    public string DesktopPath { get; set; } = string.Empty;

    public string DownloadsPath { get; set; } = string.Empty;

    public bool EnableUndoHistory { get; set; } = true;

    public bool ConfirmBeforeBulk { get; set; } = true;

    public bool PreviewBeforeOrganize { get; set; } = true;

    public bool KeepShortcuts { get; set; } = true;

    public bool HasCompletedSetup { get; set; }

    public List<string> ExcludedPatterns { get; set; } = ["desktop.ini"];

    public List<ExtensionMapping> DesktopMappings { get; set; } = [];

    public List<ExtensionMapping> DownloadsMappings { get; set; } = [];

    public int ClampDelay()
    {
        if (FileStabilityDelayMs < 500)
        {
            return 500;
        }

        if (FileStabilityDelayMs > 60000)
        {
            return 60000;
        }

        return FileStabilityDelayMs;
    }

    public IReadOnlyList<ExtensionMapping> MappingsFor(MonitorScope scope)
    {
        return scope switch
        {
            MonitorScope.Desktop => DesktopMappings,
            MonitorScope.Downloads => DownloadsMappings,
            _ => throw new System.Diagnostics.UnreachableException(scope.ToString()),
        };
    }

    public string RootFor(MonitorScope scope)
    {
        return scope switch
        {
            MonitorScope.Desktop => DesktopPath,
            MonitorScope.Downloads => DownloadsPath,
            _ => throw new System.Diagnostics.UnreachableException(scope.ToString()),
        };
    }

    public bool MonitoringEnabled(MonitorScope scope)
    {
        return scope switch
        {
            MonitorScope.Desktop => MonitorDesktop,
            MonitorScope.Downloads => MonitorDownloads,
            _ => throw new System.Diagnostics.UnreachableException(scope.ToString()),
        };
    }
}
