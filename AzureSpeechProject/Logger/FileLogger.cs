using System;
using System.IO;
using System.Threading;
using AzureSpeechProject.Constants;
using AzureSpeechProject.Helpers;

namespace AzureSpeechProject.Logger;

public class FileLogger : ILogger
{
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    private string _logFilePath;

    public FileLogger()
    {
        UpdateLogPath();
    }

    public void Log(string message)
    {
        try
        {
            _lock.EnterWriteLock();

            string parentDir = Path.GetDirectoryName(FileConstants.TranscriptsDirectory) ??
                               throw new InvalidOperationException();
            string currentLogParent = Path.GetDirectoryName(Path.GetDirectoryName(_logFilePath)) ??
                                      throw new InvalidOperationException();

            if (parentDir != currentLogParent)
            {
                UpdateLogPath();
            }

            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);

            Console.WriteLine(logEntry);

            try
            {
                MainThreadHelper.InvokeOnMainThread(() => { });
            }
            catch
            {
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to log: {ex.Message}");
        }
        finally
        {
            _lock.ExitWriteLock();
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

        var date = DateTime.Now.ToString("yyyyMMdd");
        _logFilePath = Path.Combine(logsDirectory, $"azure_speech_{date}.log");
    }
}