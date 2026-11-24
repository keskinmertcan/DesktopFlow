namespace DesktopFlow.Core.Safety;

public static class PartialDownloadDetector
{
    private static readonly string[] PartialExtensions =
    [
        ".crdownload",
        ".partial",
        ".part",
        ".opdownload",
        ".download",
        ".!ut",
    ];

    public static bool IsPartial(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (PartialExtensions.Any(partial => extension.Equals(partial, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Browsers often write "report.pdf.tmp" and rename it when the download finishes.
        var withoutTmp = Path.GetFileNameWithoutExtension(fileName);
        return extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase)
            && Path.GetExtension(withoutTmp).Length > 0;
    }
}
