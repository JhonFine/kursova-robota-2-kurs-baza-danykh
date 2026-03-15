using System.IO;
using System.Text;

namespace CarRental.Desktop.Services.Logging;

public sealed class FileAppLogger(string logDirectoryPath) : IAppLogger
{
    private static readonly object WriteLock = new();

    public void Info(string message)
        => Write("INFO", message);

    public void Error(string message, Exception? exception = null)
    {
        var payload = exception is null
            ? message
            : $"{message}{Environment.NewLine}{exception}";
        Write("ERROR", payload);
    }

    private void Write(string level, string message)
    {
        Directory.CreateDirectory(logDirectoryPath);
        var path = Path.Combine(logDirectoryPath, $"app_{DateTime.UtcNow:yyyyMMdd}.log");
        var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

        lock (WriteLock)
        {
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }
    }
}
