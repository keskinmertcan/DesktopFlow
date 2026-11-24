using DesktopFlow.Core.Abstractions;

namespace DesktopFlow.Infrastructure.SystemServices;

public sealed class NullNotificationService : INotificationService
{
    public void Show(string title, string body)
    {
    }
}
