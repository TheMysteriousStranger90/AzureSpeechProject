using DotNetEnv;

namespace AzureSpeechToTextApp.Configurations;

public class EnvironmentConfiguration : IEnvironmentConfiguration
{
    private bool _loaded = false;
    private readonly object _lock = new object();
    private readonly string[] _possiblePaths;
    
    public EnvironmentConfiguration(string[]? customPaths = null)
    {
        _possiblePaths = customPaths ?? new[]
        {
            ".env",
            Path.Combine(Directory.GetCurrentDirectory(), ".env")
        };
        
        Load();
    }

    private void Load()
    {
        if (_loaded)
        {
            return;
        }
        
        lock (_lock)
        {
            if (_loaded)
            {
                return;
            }
            
            foreach (var path in _possiblePaths)
            {
                if (File.Exists(path))
                {
                    Env.Load(path);
                    _loaded = true;
                    return;
                }
            }
        }
    }
    
    public void Reload()
    {
        lock (_lock)
        {
            _loaded = false;
            Load();
        }
    }
}