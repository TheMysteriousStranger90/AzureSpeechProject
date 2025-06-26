using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;

namespace AzureSpeechProject.Services;

public class SettingsService : ISettingsService
{
    private readonly ILogger _logger;
    private readonly string _settingsFilePath;
    private readonly byte[] _entropy;

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

        _entropy = Encoding.UTF8.GetBytes("AzureSpeechProject_v1.0_Entropy");
    }

    public string GetSettingsFilePath() => _settingsFilePath;

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            _logger.Log($"Attempting to load settings from: {_settingsFilePath}");
            _logger.Log($"Settings file exists: {File.Exists(_settingsFilePath)}");

            if (!File.Exists(_settingsFilePath))
            {
                _logger.Log("Settings file not found, creating default settings");
                var defaultSettings = CreateDefaultSettings();
                await SaveSettingsAsync(defaultSettings);
                return defaultSettings;
            }

            var encryptedJson = await File.ReadAllTextAsync(_settingsFilePath);
            _logger.Log($"Read settings file content: {encryptedJson}");

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var settingsData = JsonSerializer.Deserialize<SettingsData>(encryptedJson, jsonOptions);

            if (settingsData == null)
            {
                _logger.Log("Failed to deserialize settings, using defaults");
                return CreateDefaultSettings();
            }

            _logger.Log(
                $"Deserialized settings - Region: {settingsData.Region}, OutputDirectory: {settingsData.OutputDirectory}");

            var settings = new AppSettings
            {
                Region = settingsData.Region,
                SpeechLanguage = settingsData.SpeechLanguage,
                SampleRate = settingsData.SampleRate,
                BitsPerSample = settingsData.BitsPerSample,
                Channels = settingsData.Channels,
                OutputDirectory = settingsData.OutputDirectory
            };

            if (!string.IsNullOrEmpty(settingsData.EncryptedKey))
            {
                try
                {
                    var encryptedBytes = Convert.FromBase64String(settingsData.EncryptedKey);
                    var decryptedBytes =
                        ProtectedData.Unprotect(encryptedBytes, _entropy, DataProtectionScope.CurrentUser);
                    settings.Key = Encoding.UTF8.GetString(decryptedBytes);
                    _logger.Log("Azure Speech Key decrypted successfully");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Failed to decrypt Azure Speech Key: {ex.Message}");
                    settings.Key = string.Empty;
                }
            }

            _logger.Log($"Final settings loaded - OutputDirectory: {settings.OutputDirectory}");
            return settings;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error loading settings: {ex.Message}");
            _logger.Log($"Stack trace: {ex.StackTrace}");
            return CreateDefaultSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            _logger.Log($"Starting to save settings to: {_settingsFilePath}");
            _logger.Log(
                $"Settings to save - Region: {settings.Region}, OutputDirectory: {settings.OutputDirectory}, SpeechLanguage: {settings.SpeechLanguage}");

            var settingsDirectory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(settingsDirectory) && !Directory.Exists(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
                _logger.Log($"Created settings directory: {settingsDirectory}");
            }

            var settingsData = new SettingsData
            {
                Region = settings.Region,
                SpeechLanguage = settings.SpeechLanguage,
                SampleRate = settings.SampleRate,
                BitsPerSample = settings.BitsPerSample,
                Channels = settings.Channels,
                OutputDirectory = settings.OutputDirectory
            };

            if (!string.IsNullOrEmpty(settings.Key))
            {
                try
                {
                    var keyBytes = Encoding.UTF8.GetBytes(settings.Key);
                    var encryptedKeyBytes = ProtectedData.Protect(keyBytes, _entropy, DataProtectionScope.CurrentUser);
                    settingsData.EncryptedKey = Convert.ToBase64String(encryptedKeyBytes);
                    _logger.Log("Azure Speech Key encrypted successfully");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Failed to encrypt Azure Speech Key: {ex.Message}");
                    throw;
                }
            }

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var json = JsonSerializer.Serialize(settingsData, jsonOptions);
            _logger.Log($"Serialized settings JSON: {json}");

            await File.WriteAllTextAsync(_settingsFilePath, json);
            _logger.Log($"Settings file written to disk: {_settingsFilePath}");

            if (File.Exists(_settingsFilePath))
            {
                var fileInfo = new FileInfo(_settingsFilePath);
                _logger.Log(
                    $"Settings file confirmed - Size: {fileInfo.Length} bytes, LastWrite: {fileInfo.LastWriteTime}");
            }
            else
            {
                _logger.Log("WARNING: Settings file was not created!");
            }

            if (!string.IsNullOrEmpty(settings.OutputDirectory) && !Directory.Exists(settings.OutputDirectory))
            {
                Directory.CreateDirectory(settings.OutputDirectory);
                _logger.Log($"Created output directory: {settings.OutputDirectory}");
            }

            _logger.Log("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error saving settings: {ex.Message}");
            _logger.Log($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task ResetToDefaultsAsync()
    {
        try
        {
            var defaultSettings = CreateDefaultSettings();
            await SaveSettingsAsync(defaultSettings);
            _logger.Log("Settings reset to defaults");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error resetting settings: {ex.Message}");
            throw;
        }
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            Region = "westeurope",
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

    private class SettingsData
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