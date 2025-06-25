using System;
using AzureSpeechProject.Configurations;
using AzureSpeechProject.Constants;

namespace AzureSpeechProject.Services;

public class SecretsService
{
    private readonly IEnvironmentConfiguration _envConfig;
    
    public SecretsService(IEnvironmentConfiguration envConfig)
    {
        _envConfig = envConfig ?? throw new ArgumentNullException(nameof(envConfig));
    }
    
    public (string region, string key) GetAzureSpeechCredentials()
    {
        _envConfig.Reload();
        
        var region = Environment.GetEnvironmentVariable(SecretConstants.AzureSpeechRegion);
        var key = Environment.GetEnvironmentVariable(SecretConstants.AzureSpeechKey);
        
        if (string.IsNullOrEmpty(region))
        {
            throw new InvalidOperationException($"{SecretConstants.AzureSpeechRegion} not found in environment variables");
        }
        
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException($"{SecretConstants.AzureSpeechKey} not found in environment variables");
        }
        
        return (region, key);
    }
}