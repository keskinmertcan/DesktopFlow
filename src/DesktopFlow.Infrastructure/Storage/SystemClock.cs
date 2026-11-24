using DesktopFlow.Core.Abstractions;

namespace DesktopFlow.Infrastructure.Storage;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
