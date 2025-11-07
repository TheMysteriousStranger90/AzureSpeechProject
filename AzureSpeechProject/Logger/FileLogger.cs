using System.Globalization;
using AzureSpeechProject.Helpers;

namespace AzureSpeechProject.Logger;

internal sealed class FileLogger : ILogger, IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private string _logFilePath = string.Empty;
    private string _cachedOutputDirectory = string.Empty;
    private bool _disposed;

    public FileLogger()
    {
        UpdateLogPath();
    }

    public void Log(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _lock.EnterWriteLock();

            var logEntry = $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}] {message}";

            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);

            Console.WriteLine(logEntry);

            try
            {
                MainThreadHelper.InvokeOnMainThread(() => { });
            }
            catch (InvalidOperationException)
            {
                // Main thread not available, ignore
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"IO Error writing to log: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Access denied writing to log: {ex.Message}");
        }
        finally
        {
            if (_lock.IsWriteLockHeld)
            {
                _lock.ExitWriteLock();
            }
        }
    }

    public void UpdateLogPathFromSettings(string outputDirectory)
    {
        if (_cachedOutputDirectory != outputDirectory)
        {
            _cachedOutputDirectory = outputDirectory;
            UpdateLogPath();
        }
    }

    private void UpdateLogPath()
    {
        try
        {
            string? parentDir;

            if (!string.IsNullOrWhiteSpace(_cachedOutputDirectory))
            {
                parentDir = Path.GetDirectoryName(_cachedOutputDirectory);
                if (string.IsNullOrEmpty(parentDir))
                {
                    parentDir = _cachedOutputDirectory;
                }
            }
            else
            {
                parentDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Azure Speech Services");
            }

            var logsDirectory = Path.Combine(parentDir, "Logs");

            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            var date = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            _logFilePath = Path.Combine(logsDirectory, $"azure_speech_{date}.log");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating log path: {ex.Message}");
            var tempLogsDir = Path.Combine(Path.GetTempPath(), "AzureSpeechLogs");
            if (!Directory.Exists(tempLogsDir))
            {
                Directory.CreateDirectory(tempLogsDir);
            }
            var date = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            _logFilePath = Path.Combine(tempLogsDir, $"azure_speech_{date}.log");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Dispose();
        _disposed = true;
    }
}
