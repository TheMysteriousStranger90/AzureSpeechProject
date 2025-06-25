using System;
using System.IO;
using System.Threading;
using AzureSpeechProject.Helpers;

namespace AzureSpeechProject.Logger;

public class FileLogger : ILogger
{
    private readonly string _logFilePath;
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    
    public FileLogger()
    {
        var logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        
        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
        }
        
        var date = DateTime.Now.ToString("yyyyMMdd");
        _logFilePath = Path.Combine(logsDirectory, $"app_log_{date}.txt");
    }
    
    public void Log(string message)
    {
        try
        {
            _lock.EnterWriteLock();
            
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            
            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            
            Console.WriteLine(logEntry);
            
            try
            {
                MainThreadHelper.InvokeOnMainThread(() => 
                {
                });
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
}