namespace DesktopFlow.Infrastructure.Persistence;

public sealed class SqliteGate
{
    public SemaphoreSlim Lock { get; } = new(1, 1);
}
