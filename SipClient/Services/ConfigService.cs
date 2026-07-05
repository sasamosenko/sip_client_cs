using System.IO;
using Newtonsoft.Json;
using SipClient.Models;

namespace SipClient.Services;

public class ConfigService
{
    private readonly string _configPath;

    public ConfigService()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
    }

    public SipConfig LoadConfig()
    {
        if (File.Exists(_configPath))
        {
            var json = File.ReadAllText(_configPath);
            return JsonConvert.DeserializeObject<SipConfig>(json) ?? new SipConfig();
        }
        return new SipConfig();
    }

    public void SaveConfig(SipConfig config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(_configPath, json);
    }
}
