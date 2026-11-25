namespace DesktopFlow.Core.Storage;

public static class SystemLocationPolicy
{
    private static readonly HashSet<string> DriveRootDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows",
        "Windows.old",
        "$Recycle.Bin",
        "System Volume Information",
        "Recovery",
        "PerfLogs",
        "Config.Msi",
        "$WinREAgent",
        "$SysReset",
        "$Windows.~BT",
        "$Windows.~WS",
        "Documents and Settings",
    };

    private static readonly HashSet<string> DriveRootFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "pagefile.sys",
        "hiberfil.sys",
        "swapfile.sys",
        "DumpStack.log.tmp",
    };

    private static readonly string[] BlockedRelativePrefixes =
    [
        @"Program Files\WindowsApps",
        @"Program Files\Windows Defender",
        @"Program Files\ModifiableWindowsApps",
        @"Program Files (x86)\WindowsApps",
        @"Program Files (x86)\Windows Defender",
        @"ProgramData\Microsoft\Windows",
        @"ProgramData\Microsoft\Windows Defender",
        @"ProgramData\Microsoft\Diagnosis",
        @"ProgramData\Microsoft\Search",
        @"ProgramData\Microsoft\Crypto",
        @"ProgramData\Microsoft\Provisioning",
        @"ProgramData\USOShared",
        @"ProgramData\Microsoft\Network",
    ];

    private static readonly string[] BlockedUserPrefixes =
    [
        @"AppData\Local\Microsoft\Windows",
        @"AppData\Local\Microsoft\WindowsApps",
        @"AppData\Local\Microsoft\Windows Defender",
        @"AppData\Roaming\Microsoft\Windows",
    ];

    private static readonly HashSet<string> BlockedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop.ini",
        "thumbs.db",
        "ehthumbs.db",
        "ehthumbs_vista.db",
        "IconCache.db",
        "NTUSER.DAT",
        "ntuser.dat.LOG1",
        "ntuser.dat.LOG2",
        "ntuser.ini",
        "UsrClass.dat",
        "UsrClass.dat.LOG1",
        "UsrClass.dat.LOG2",
    };

    public static bool IsExcluded(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root))
        {
            return false;
        }

        var relative = Path.GetRelativePath(root, full);
        if (relative is "." or "")
        {
            return false;
        }

        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (DriveRootDirectories.Contains(parts[0]))
        {
            return true;
        }

        if (parts.Length == 1 && DriveRootFiles.Contains(parts[0]))
        {
            return true;
        }

        if (StartsWithAny(relative, BlockedRelativePrefixes))
        {
            return true;
        }

        if (parts.Length >= 2
            && parts[0].Equals("Users", StringComparison.OrdinalIgnoreCase)
            && IsTemplateProfile(parts[1]))
        {
            return true;
        }

        if (parts.Length >= 3
            && parts[0].Equals("Users", StringComparison.OrdinalIgnoreCase)
            && StartsWithAny(string.Join('\\', parts.Skip(2)), BlockedUserPrefixes))
        {
            return true;
        }

        return BlockedFileNames.Contains(parts[^1]);
    }

    private static bool IsTemplateProfile(string name)
    {
        return name.Equals("Default", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Default User", StringComparison.OrdinalIgnoreCase)
            || name.Equals("All Users", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWithAny(string relative, IEnumerable<string> prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (relative.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
