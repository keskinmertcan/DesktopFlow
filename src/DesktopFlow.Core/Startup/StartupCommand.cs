namespace DesktopFlow.Core.Startup;

public static class StartupCommand
{
    public static string Build(string executablePath, bool minimized)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var arguments = minimized ? "--startup --minimized" : "--startup";
        return $"\"{executablePath}\" {arguments}";
    }

    public static bool IsStartupArgument(string argument)
    {
        return argument.Equals("--startup", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMinimizedArgument(string argument)
    {
        return argument.Equals("--minimized", StringComparison.OrdinalIgnoreCase);
    }
}
