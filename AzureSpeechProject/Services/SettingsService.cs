using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AzureSpeechProject.Interfaces;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;

namespace AzureSpeechProject.Services;

internal sealed class SettingsService : ISettingsService
{
    private readonly ILogger _logger;
    private readonly string _settingsFilePath;
    private readonly byte[] _entropy;

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public SettingsService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "AzureSpeechProject");

        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }

        _settingsFilePath = Path.Combine(appFolder, "settings.json");
        _entropy = Encoding.UTF8.GetBytes("AzureSpeechProject_v1.0_Entropy_Azure");
    }

    public string SettingsFilePath => _settingsFilePath;

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.Log($"🔐 Loading Azure Speech Service settings from: {_settingsFilePath}");
            _logger.Log($"Settings file exists: {File.Exists(_settingsFilePath)}");

            if (!File.Exists(_settingsFilePath))
            {
                _logger.Log("📋 Settings file not found, creating default settings");
                var defaultSettings = CreateDefaultSettings();
                await SaveSettingsAsync(defaultSettings, cancellationToken).ConfigureAwait(false);
                return defaultSettings;
            }

            var jsonContent = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken).ConfigureAwait(false);
            _logger.Log(
                $"📄 Read settings file content (length: {jsonContent.Length.ToString(CultureInfo.InvariantCulture)} chars)");

            cancellationToken.ThrowIfCancellationRequested();

            var settingsData = JsonSerializer.Deserialize<SettingsData>(jsonContent, JsonReadOptions);

            if (settingsData == null)
            {
                _logger.Log("❌ Failed to deserialize settings, using defaults");
                return CreateDefaultSettings();
            }

            _logger.Log(
                $"📊 Deserialized settings - Region: '{settingsData.Region}', HasEncryptedKey: {!string.IsNullOrEmpty(settingsData.EncryptedKey)}");

            var settings = new AppSettings
            {
                Region = settingsData.Region ?? string.Empty,
                SpeechLanguage = settingsData.SpeechLanguage ?? "en-US",
                SampleRate = settingsData.SampleRate,
                BitsPerSample = settingsData.BitsPerSample,
                Channels = settingsData.Channels,
                OutputDirectory = settingsData.OutputDirectory ?? string.Empty,
                Key = string.Empty
            };

            if (!string.IsNullOrEmpty(settingsData.EncryptedKey))
            {
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        var encryptedBytes = Convert.FromBase64String(settingsData.EncryptedKey);
                        var decryptedBytes = DecryptData(encryptedBytes);
                        settings.Key = Encoding.UTF8.GetString(decryptedBytes);
                        _logger.Log("🔓 Azure Speech Service key decrypted successfully");
                    }
                    catch (CryptographicException ex)
                    {
                        _logger.Log($"❌ Failed to decrypt Azure Speech Service key: {ex.Message}");
                        settings.Key = string.Empty;
                    }
                    catch (FormatException ex)
                    {
                        _logger.Log($"❌ Invalid encrypted key format: {ex.Message}");
                        settings.Key = string.Empty;
                    }
                }
                else
                {
                    _logger.Log("⚠️ Encryption not supported on this platform, key not decrypted");
                }
            }
            else
            {
                _logger.Log("⚠️ No encrypted Azure Speech Service key found in settings");
            }

            _logger.Log(
                $"✅ Final settings loaded - OutputDirectory: '{settings.OutputDirectory}', KeyConfigured: {!string.IsNullOrEmpty(settings.Key)}");
            return settings;
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Settings loading was cancelled");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.Log($"❌ JSON error loading Azure Speech Service settings: {ex.Message}");
            return CreateDefaultSettings();
        }
        catch (IOException ex)
        {
            _logger.Log($"❌ IO error loading Azure Speech Service settings: {ex.Message}");
            return CreateDefaultSettings();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Log($"❌ Access denied loading Azure Speech Service settings: {ex.Message}");
            return CreateDefaultSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.Log($"💾 Saving Azure Speech Service settings to: {_settingsFilePath}");
            _logger.Log(
                $"Settings to save - Region: '{settings.Region}', OutputDirectory: '{settings.OutputDirectory}', KeyProvided: {!string.IsNullOrEmpty(settings.Key)}");

            var settingsDirectory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(settingsDirectory) && !Directory.Exists(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
                _logger.Log($"📁 Created settings directory: {settingsDirectory}");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var settingsData = new SettingsData
            {
                Region = settings.Region ?? string.Empty,
                SpeechLanguage = settings.SpeechLanguage ?? "en-US",
                SampleRate = settings.SampleRate,
                BitsPerSample = settings.BitsPerSample,
                Channels = settings.Channels,
                OutputDirectory = settings.OutputDirectory ?? string.Empty,
                EncryptedKey = string.Empty
            };

            if (!string.IsNullOrEmpty(settings.Key))
            {
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        var keyBytes = Encoding.UTF8.GetBytes(settings.Key);
                        var encryptedKeyBytes = EncryptData(keyBytes);
                        settingsData.EncryptedKey = Convert.ToBase64String(encryptedKeyBytes);
                        _logger.Log("🔐 Azure Speech Service key encrypted successfully");
                    }
                    catch (CryptographicException ex)
                    {
                        _logger.Log($"❌ Failed to encrypt Azure Speech Service key: {ex.Message}");
                        throw new InvalidOperationException("Failed to encrypt Azure Speech Service credentials", ex);
                    }
                }
                else
                {
                    _logger.Log("⚠️ Encryption not supported on this platform, key not encrypted");
                }
            }
            else
            {
                _logger.Log("⚠️ No Azure Speech Service key provided, saving without encryption");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var json = JsonSerializer.Serialize(settingsData, JsonWriteOptions);
            _logger.Log("📝 Serialized settings data for Azure Speech Service");

            await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken).ConfigureAwait(false);
            _logger.Log($"✅ Azure Speech Service settings file written to disk: {_settingsFilePath}");

            if (File.Exists(_settingsFilePath))
            {
                var fileInfo = new FileInfo(_settingsFilePath);
                _logger.Log(
                    $"📊 Settings file confirmed - Size: {fileInfo.Length.ToString(CultureInfo.InvariantCulture)} bytes, LastWrite: {fileInfo.LastWriteTime.ToString(CultureInfo.CurrentCulture)}");
            }
            else
            {
                _logger.Log("❌ WARNING: Settings file was not created!");
            }

            if (!string.IsNullOrEmpty(settings.OutputDirectory) && !Directory.Exists(settings.OutputDirectory))
            {
                Directory.CreateDirectory(settings.OutputDirectory);
                _logger.Log($"📁 Created Azure Speech Service output directory: {settings.OutputDirectory}");
            }

            _logger.Log("✅ Azure Speech Service settings saved successfully with encryption");
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Settings saving was cancelled");
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Log($"❌ Error saving Azure Speech Service settings: {ex.Message}");
            throw;
        }
    }

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.Log("🔄 Resetting Azure Speech Service settings to defaults");
            var defaultSettings = CreateDefaultSettings();
            await SaveSettingsAsync(defaultSettings, cancellationToken).ConfigureAwait(false);
            _logger.Log("✅ Azure Speech Service settings reset to defaults");
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Settings reset was cancelled");
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.Log($"❌ Error resetting Azure Speech Service settings: {ex.Message}");
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    private byte[] EncryptData(byte[] data)
    {
        return ProtectedData.Protect(data, _entropy, DataProtectionScope.CurrentUser);
    }

    [SupportedOSPlatform("windows")]
    private byte[] DecryptData(byte[] encryptedData)
    {
        return ProtectedData.Unprotect(encryptedData, _entropy, DataProtectionScope.CurrentUser);
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            Region = string.Empty,
            Key = string.Empty,
            SpeechLanguage = "en-US",
            SampleRate = 16000,
            BitsPerSample = 16,
            Channels = 1,
            OutputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Azure Speech Services",
                "Transcripts")
        };
    }

    private sealed class SettingsData
    {
        public string Region { get; set; } = string.Empty;
        public string EncryptedKey { get; set; } = string.Empty;
        public string SpeechLanguage { get; set; } = string.Empty;
        public int SampleRate { get; set; }
        public int BitsPerSample { get; set; }
        public int Channels { get; set; }
        public string OutputDirectory { get; set; } = string.Empty;
    }
}
