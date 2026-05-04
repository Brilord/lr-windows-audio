using System.IO;

namespace BalanceDock.Services;

public sealed class LogService
{
    private readonly object _syncRoot = new();
    private readonly string _logPath;

    public LogService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BalanceDock",
            "logs");
        Directory.CreateDirectory(directory);
        _logPath = Path.Combine(directory, "balancedock.log");
    }

    public string LogPath => _logPath;

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception is null ? message : $"{message} {exception}");
    }

    private void Write(string level, string message)
    {
        lock (_syncRoot)
        {
            File.AppendAllText(_logPath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}");
        }
    }
}
