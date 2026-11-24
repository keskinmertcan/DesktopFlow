namespace DesktopFlow.Core.Organization;

public static class DestinationBuilder
{
    public static bool TryCombineUnderRoot(string root, string relativeFolder, out string fullDirectory, out string? error)
    {
        fullDirectory = string.Empty;
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
        {
            error = "The monitored folder is not available.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(relativeFolder))
        {
            error = "The destination folder is empty.";
            return false;
        }

        var normalized = relativeFolder.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim();
        if (Path.IsPathRooted(normalized))
        {
            error = "The destination must be a folder inside the monitored directory.";
            return false;
        }

        var rootFull = Path.GetFullPath(root);
        var combined = Path.GetFullPath(Path.Combine(rootFull, normalized));
        var rootPrefix = rootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            error = "The destination leaves the monitored directory.";
            return false;
        }

        if (string.Equals(combined, rootFull, StringComparison.OrdinalIgnoreCase))
        {
            error = "Files stay in a subfolder so they are not organized again.";
            return false;
        }

        fullDirectory = combined;
        error = null;
        return true;
    }
}
