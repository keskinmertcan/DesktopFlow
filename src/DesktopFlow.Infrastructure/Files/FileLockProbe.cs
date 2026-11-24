using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;

namespace DesktopFlow.Infrastructure.Files;

public sealed class FileLockProbe : IFileLockProbe
{
    public FileLockState Probe(string path)
    {
        if (!File.Exists(path))
        {
            return FileLockState.NotFound;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return FileLockState.Available;
        }
        catch (UnauthorizedAccessException)
        {
            return FileLockState.AccessDenied;
        }
        catch (IOException)
        {
            return FileLockState.Locked;
        }
    }
}
