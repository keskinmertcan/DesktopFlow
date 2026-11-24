using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Organization;

namespace DesktopFlow.Infrastructure.Files;

public sealed class SafeFileMover : IFileMover
{
    private readonly IFileLockProbe _probe;
    private readonly IAppLogger _logger;

    public SafeFileMover(IFileLockProbe probe, IAppLogger logger)
    {
        _probe = probe;
        _logger = logger;
    }

    public async Task<MoveExecution> MoveAsync(
        string sourcePath,
        string destinationDirectory,
        string destinationFileName,
        Func<string, CancellationToken, Task>? onTempCreated,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
        {
            return new MoveExecution(false, null, "The source file is missing.", null);
        }

        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            return new MoveExecution(false, null, "The destination folder is empty.", null);
        }

        try
        {
            Directory.CreateDirectory(destinationDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error($"Could not create '{destinationDirectory}'.", ex);
            return new MoveExecution(false, null, ex.Message, null);
        }

        string finalName;
        try
        {
            finalName = FileNameConflictResolver.Resolve(
                destinationFileName,
                name => File.Exists(Path.Combine(destinationDirectory, name)));
        }
        catch (IOException ex)
        {
            return new MoveExecution(false, null, ex.Message, null);
        }

        var destination = Path.GetFullPath(Path.Combine(destinationDirectory, finalName));
        var source = Path.GetFullPath(sourcePath);
        if (File.Exists(destination))
        {
            return new MoveExecution(false, null, "Refusing to overwrite an existing file.", null);
        }

        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            return new MoveExecution(false, null, "The source and destination are the same file.", null);
        }

        var probe = _probe.Probe(source);
        switch (probe)
        {
            case FileLockState.Available:
                break;
            case FileLockState.Locked:
                return new MoveExecution(false, null, "The file is in use.", null);
            case FileLockState.AccessDenied:
                return new MoveExecution(false, null, "Access to the file was denied.", null);
            case FileLockState.NotFound:
                return new MoveExecution(false, null, "The source file is missing.", null);
            default:
                throw new System.Diagnostics.UnreachableException(probe.ToString());
        }

        string? tempPath = null;
        try
        {
            if (SameVolume(source, destination))
            {
                File.Move(source, destination);
                return new MoveExecution(true, destination, null, null);
            }

            tempPath = destination + ".desktopflow-partial";
            if (File.Exists(tempPath))
            {
                tempPath = destination + "." + Guid.NewGuid().ToString("N") + ".desktopflow-partial";
            }

            if (onTempCreated is not null)
            {
                await onTempCreated(tempPath, cancellationToken);
            }

            await CopyVerifiedAsync(source, tempPath, cancellationToken);
            if (File.Exists(destination))
            {
                DeleteOurTemp(tempPath);
                return new MoveExecution(false, null, "Refusing to overwrite an existing file.", null);
            }

            File.Move(tempPath, destination);
            tempPath = null;
            try
            {
                File.Delete(source);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.Error($"Copied '{source}' but could not remove the original.", ex);
                return new MoveExecution(false, destination, "The file was copied, but the original could not be removed.", null);
            }

            return new MoveExecution(true, destination, null, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (tempPath is not null)
            {
                DeleteOurTemp(tempPath);
            }

            _logger.Error($"Could not move '{source}'.", ex);
            return new MoveExecution(false, null, ex.Message, tempPath);
        }
    }

    private static async Task CopyVerifiedAsync(string source, string tempPath, CancellationToken cancellationToken)
    {
        var sourceLength = new FileInfo(source).Length;
        await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
        await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await input.CopyToAsync(output, cancellationToken);
        }

        var copiedLength = new FileInfo(tempPath).Length;
        if (copiedLength != sourceLength)
        {
            DeleteOurTemp(tempPath);
            throw new IOException("The copied file size did not match. The original was kept.");
        }
    }

    private static bool SameVolume(string left, string right)
    {
        var leftRoot = Path.GetPathRoot(left);
        var rightRoot = Path.GetPathRoot(right);
        return string.Equals(leftRoot, rightRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteOurTemp(string? path)
    {
        if (path is null || !path.EndsWith(".desktopflow-partial", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
