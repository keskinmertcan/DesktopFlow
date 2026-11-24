using System.Diagnostics;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Safety;

namespace DesktopFlow.Infrastructure.Files;

public sealed class FileStabilityWaiter : IFileStabilityWaiter
{
    private readonly IFileLockProbe _probe;
    private readonly IClock _clock;

    public FileStabilityWaiter(IFileLockProbe probe, IClock clock)
    {
        _probe = probe;
        _clock = clock;
    }

    public async Task<StabilityResult> WaitAsync(string path, TimeSpan settle, CancellationToken cancellationToken)
    {
        if (PartialDownloadDetector.IsPartial(Path.GetFileName(path)))
        {
            return new StabilityResult(StabilityState.Retry, "The download is still in progress.");
        }

        var deadline = _clock.UtcNow + TimeSpan.FromMilliseconds(Math.Clamp(settle.TotalMilliseconds, 300, 2000));
        long? lastLength = null;
        var stableSamples = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PartialDownloadDetector.IsPartial(Path.GetFileName(path)))
            {
                return new StabilityResult(StabilityState.Retry, "The download is still in progress.");
            }

            var probe = _probe.Probe(path);
            switch (probe)
            {
                case FileLockState.Available:
                    break;
                case FileLockState.Locked:
                    return new StabilityResult(StabilityState.Retry, "The file is in use.");
                case FileLockState.NotFound:
                    return new StabilityResult(StabilityState.Gone, "The file disappeared.");
                case FileLockState.AccessDenied:
                    return new StabilityResult(StabilityState.AccessDenied, "Access to the file was denied.");
                default:
                    throw new UnreachableException(probe.ToString());
            }

            long length;
            try
            {
                length = new FileInfo(path).Length;
            }
            catch (IOException ex)
            {
                return new StabilityResult(StabilityState.Retry, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new StabilityResult(StabilityState.AccessDenied, ex.Message);
            }

            if (lastLength == length)
            {
                stableSamples++;
            }
            else
            {
                lastLength = length;
                stableSamples = 1;
            }

            if (stableSamples >= 2)
            {
                return new StabilityResult(StabilityState.Stable, null);
            }

            if (_clock.UtcNow >= deadline)
            {
                return new StabilityResult(StabilityState.Retry, "The file was still changing.");
            }

            await Task.Delay(200, cancellationToken);
        }
    }
}
