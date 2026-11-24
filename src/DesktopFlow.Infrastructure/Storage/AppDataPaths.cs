namespace DesktopFlow.Infrastructure.Storage;

public sealed class AppDataPaths
{
    public AppDataPaths(string root)
    {
        Root = root;
        DatabaseFile = Path.Combine(root, "desktopflow.db");
        LogFile = Path.Combine(root, "logs", "desktopflow.log");
    }

    public string Root { get; }

    public string DatabaseFile { get; }

    public string LogFile { get; }

    public static AppDataPaths ForUser()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopFlow");
        return new AppDataPaths(root);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        var logDirectory = Path.GetDirectoryName(LogFile);
        if (!string.IsNullOrEmpty(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
    }
}
