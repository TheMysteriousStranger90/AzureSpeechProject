using System;
using System.Threading.Tasks;
using AzureSpeechProject.Logger;
using NAudio.Wave;

namespace AzureSpeechProject.Services;

public class MicrophonePermissionService : IMicrophonePermissionService
{
    private readonly ILogger _logger;

    public MicrophonePermissionService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<bool> CheckMicrophonePermissionAsync()
    {
        try
        {
            _logger.Log("CheckMicrophonePermissionAsync called");
            if (OperatingSystem.IsWindows())
            {
                return await CheckWindowsMicrophonePermissionAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error checking microphone permission: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RequestMicrophonePermissionAsync()
    {
        try
        {
            _logger.Log("RequestMicrophonePermissionAsync called");
            return await CheckMicrophonePermissionAsync();
        }
        catch (Exception ex)
        {
            _logger.Log($"Error requesting microphone permission: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CheckWindowsMicrophonePermissionAsync()
    {
        return await Task.Run(async () =>
        {
            try
            {
                _logger.Log("Starting Windows microphone permission check...");
                var deviceCount = WaveInEvent.DeviceCount;
                _logger.Log($"Found {deviceCount} audio input devices");
                
                if (deviceCount == 0)
                {
                    _logger.Log("No microphone devices found");
                    return false;
                }

                var accessDenied = false;
                
                try
                {
                    using var waveIn = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(8000, 1),
                        DeviceNumber = 0,
                        BufferMilliseconds = 50
                    };

                    var dataReceived = false;
                    waveIn.DataAvailable += (s, e) => 
                    {
                        if (e.BytesRecorded > 0)
                        {
                            dataReceived = true;
                            _logger.Log($"Microphone data received: {e.BytesRecorded} bytes");
                        }
                    };

                    waveIn.RecordingStopped += (s, e) => 
                    {
                        if (e.Exception != null)
                        {
                            _logger.Log($"Recording stopped with exception: {e.Exception.Message}");
                            accessDenied = true;
                        }
                    };

                    _logger.Log("Starting microphone test recording...");
                    waveIn.StartRecording();
                    
                    await Task.Delay(500);
                    
                    waveIn.StopRecording();
                    
                    await Task.Delay(100);
                    
                    if (accessDenied)
                    {
                        _logger.Log("Microphone access denied during recording");
                        return false;
                    }
                    
                    if (dataReceived)
                    {
                        _logger.Log("Microphone access verified - data received");
                        return true;
                    }
                    else
                    {
                        _logger.Log("Microphone accessible but no data received - possible permission issue");
                        return false;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.Log($"Microphone access explicitly denied: {ex.Message}");
                    return false;
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    _logger.Log($"COM error accessing microphone: {ex.Message} (HResult: {ex.HResult:X})");
                    return false;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.Log($"Invalid operation accessing microphone: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error checking Windows microphone permission: {ex.Message}");
                return false;
            }
        });
    }
}