using System.Runtime.InteropServices;
using AzureSpeechProject.Logger;
using NAudio.Wave;

namespace AzureSpeechProject.Services;

public sealed class MicrophonePermissionService : IMicrophonePermissionService
{
    private readonly ILogger _logger;

    public MicrophonePermissionService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<bool> CheckMicrophonePermissionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.Log("CheckMicrophonePermissionAsync called");
            if (OperatingSystem.IsWindows())
            {
                return await CheckWindowsMicrophonePermissionAsync(cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Microphone permission check was cancelled");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Log($"Unauthorized access to microphone: {ex.Message}");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Log($"Invalid operation checking microphone: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RequestMicrophonePermissionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.Log("RequestMicrophonePermissionAsync called");
            return await CheckMicrophonePermissionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Microphone permission request was cancelled");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Log($"Unauthorized access requesting microphone: {ex.Message}");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Log($"Invalid operation requesting microphone: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CheckWindowsMicrophonePermissionAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.Log("Starting Windows microphone permission check...");
                var deviceCount = WaveInEvent.DeviceCount;
                _logger.Log($"Found {deviceCount} audio input devices");

                if (deviceCount == 0)
                {
                    _logger.Log("No microphone devices found");
                    return false;
                }

                for (int i = 0; i < deviceCount; i++)
                {
                    var capabilities = WaveInEvent.GetCapabilities(i);
                    _logger.Log($"Device {i}: {capabilities.ProductName}");
                }

                cancellationToken.ThrowIfCancellationRequested();

                var accessDenied = false;
                var initializationError = false;

                try
                {
                    using var waveIn = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(16000, 1),
                        DeviceNumber = 0,
                        BufferMilliseconds = 100
                    };

                    var dataReceived = false;
                    var dataReceivedEvent = new TaskCompletionSource<bool>();

                    waveIn.DataAvailable += (s, e) =>
                    {
                        if (e.BytesRecorded > 0)
                        {
                            dataReceived = true;
                            _logger.Log($"Microphone data received: {e.BytesRecorded} bytes");
                            dataReceivedEvent.TrySetResult(true);
                        }
                    };

                    waveIn.RecordingStopped += (s, e) =>
                    {
                        if (e.Exception != null)
                        {
                            _logger.Log($"Recording stopped with exception: {e.Exception.Message}");
                            if (e.Exception is UnauthorizedAccessException)
                            {
                                accessDenied = true;
                            }
                            else
                            {
                                initializationError = true;
                            }

                            dataReceivedEvent.TrySetResult(false);
                        }
                        else if (!dataReceived)
                        {
                            dataReceivedEvent.TrySetResult(false);
                        }
                    };

                    _logger.Log("Starting microphone test recording...");

                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        waveIn.StartRecording();

                        var receivedDataTask = dataReceivedEvent.Task;
                        var timeoutTask = Task.Delay(3000, cancellationToken);
                        var completedTask = await Task.WhenAny(receivedDataTask, timeoutTask).ConfigureAwait(false);

                        waveIn.StopRecording();

                        if (completedTask == timeoutTask && !dataReceived)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                _logger.Log("Microphone test cancelled");
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                            _logger.Log("Microphone test timed out - no data received within 3 seconds");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        waveIn.StopRecording();
                        _logger.Log("Microphone test was cancelled");
                        throw;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.Log($"Unauthorized access during recording test: {ex.Message}");
                        return false;
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.Log($"Invalid operation during recording test: {ex.Message}");
                        return false;
                    }

                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);

                    if (accessDenied)
                    {
                        _logger.Log("Microphone access explicitly denied");
                        return false;
                    }

                    if (initializationError)
                    {
                        _logger.Log("Microphone initialization error occurred");
                        if (deviceCount > 1)
                        {
                            _logger.Log("Trying alternative microphone device...");
                        }

                        return false;
                    }

                    if (dataReceived)
                    {
                        _logger.Log("Microphone access verified - data received");
                        return true;
                    }
                    else
                    {
                        _logger.Log("Microphone accessible but no data received - check if microphone is muted");
                        return true;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.Log("Microphone permission check was cancelled");
                    throw;
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.Log($"Microphone access explicitly denied: {ex.Message}");
                    return false;
                }
                catch (COMException ex)
                {
                    _logger.Log($"COM error accessing microphone: {ex.Message} (HResult: {ex.HResult:X})");
                    if (ex.HResult == -2147023728)
                    {
                        _logger.Log("This appears to be a Windows permission issue");
                    }

                    return false;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.Log($"Invalid operation accessing microphone: {ex.Message}");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Log("Windows microphone check was cancelled");
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Log($"Unauthorized access checking Windows microphone: {ex.Message}");
                return false;
            }
            catch (COMException ex)
            {
                _logger.Log($"COM error checking Windows microphone: {ex.Message}");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                _logger.Log($"Invalid operation checking Windows microphone: {ex.Message}");
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}
