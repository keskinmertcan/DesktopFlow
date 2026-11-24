using System.Diagnostics;
using DesktopFlow.Core.Models;

namespace DesktopFlow.Core.Safety;

public static class FileSafetyPolicy
{
    public static SkipReason? Evaluate(FileInspection file, SafetyOptions options)
    {
        if (file.IsDirectory)
        {
            return SkipReason.Directory;
        }

        if (file.IsReparsePoint)
        {
            return SkipReason.ReparsePoint;
        }

        if (IsDesktopIni(file.Name))
        {
            return SkipReason.DesktopIni;
        }

        if (file.IsHidden || file.IsSystem)
        {
            return SkipReason.HiddenOrSystem;
        }

        if (file.IsCloudPlaceholder)
        {
            return SkipReason.CloudPlaceholder;
        }

        if (options.KeepShortcuts && file.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return SkipReason.Shortcut;
        }

        if (PartialDownloadDetector.IsPartial(file.Name))
        {
            return SkipReason.PartialDownload;
        }

        if (IsExcluded(file.Name, options.ExcludedPatterns))
        {
            return SkipReason.Excluded;
        }

        return null;
    }

    public static bool IsDesktopIni(string fileName)
    {
        return fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExcluded(string fileName, IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (WildcardMatcher.IsMatch(fileName, pattern.Trim()))
            {
                return true;
            }
        }

        return false;
    }

    public static string Describe(SkipReason reason)
    {
        return reason switch
        {
            SkipReason.Directory => "Folders are left where they are.",
            SkipReason.Shortcut => "Shortcuts are kept on the desktop.",
            SkipReason.HiddenOrSystem => "Hidden and system files are left untouched.",
            SkipReason.DesktopIni => "desktop.ini is left untouched.",
            SkipReason.Excluded => "The file matches an exclusion.",
            SkipReason.ReparsePoint => "Links and junctions are not followed.",
            SkipReason.RuleSkip => "A rule says to leave this file in place.",
            SkipReason.PartialDownload => "The download is still in progress.",
            SkipReason.CloudPlaceholder => "The cloud file is not downloaded locally.",
            SkipReason.AlreadyInDestination => "The file is already in its destination.",
            SkipReason.DestinationEscapesRoot => "The destination is outside the monitored folder.",
            _ => throw new UnreachableException(reason.ToString()),
        };
    }
}
