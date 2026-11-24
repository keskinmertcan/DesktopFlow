namespace DesktopFlow.Core.Organization;

public static class FileNameConflictResolver
{
    public const int MaxAttempts = 9999;

    public static string Resolve(string desiredFileName, Func<string, bool> fileNameExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(desiredFileName);
        if (!fileNameExists(desiredFileName))
        {
            return desiredFileName;
        }

        var extension = Path.GetExtension(desiredFileName);
        var baseName = Path.GetFileNameWithoutExtension(desiredFileName);
        for (var index = 1; index <= MaxAttempts; index++)
        {
            var candidate = $"{baseName} ({index}){extension}";
            if (!fileNameExists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not find a free name for '{desiredFileName}'.");
    }
}
