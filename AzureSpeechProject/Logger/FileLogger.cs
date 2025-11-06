using System.Globalization;
using AzureSpeechProject.Constants;
using AzureSpeechProject.Helpers;

namespace AzureSpeechProject.Logger;

internal sealed class FileLogger : ILogger, IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private string _logFilePath = string.Empty;
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

            string parentDir = Path.GetDirectoryName(FileConstants.TranscriptsDirectory) ??
                               throw new InvalidOperationException("Unable to determine parent directory.");
            string currentLogParent = Path.GetDirectoryName(Path.GetDirectoryName(_logFilePath)) ??
                                      throw new InvalidOperationException("Unable to determine current log parent directory.");

            if (parentDir != currentLogParent)
            {
                UpdateLogPath();
            }

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

    private void UpdateLogPath()
    {
        string? parentDir = Path.GetDirectoryName(FileConstants.TranscriptsDirectory);
        if (string.IsNullOrEmpty(parentDir))
        {
            parentDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        var logsDirectory = Path.Combine(parentDir, "Logs");

        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
        }

        var date = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        _logFilePath = Path.Combine(logsDirectory, $"azure_speech_{date}.log");
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
