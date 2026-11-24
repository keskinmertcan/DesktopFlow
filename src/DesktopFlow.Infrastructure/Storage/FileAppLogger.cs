using DesktopFlow.Core.Abstractions;

namespace DesktopFlow.Infrastructure.Storage;

public sealed class FileAppLogger : IAppLogger
{
    private readonly string _path;
    private readonly object _gate = new();

    public FileAppLogger(string path)
    {
        _path = path;
    }

    public void Info(string message)
    {
        Write("INFO", message, null);
    }

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message, exception);
    }

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            lock (_gate)
            {
                var info = new FileInfo(_path);
                if (info.Exists && info.Length > 1_000_000)
                {
                    var backup = _path + ".1";
                    File.Copy(_path, backup, true);
                    File.WriteAllText(_path, string.Empty);
                }

                File.AppendAllText(_path, line + Environment.NewLine);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
