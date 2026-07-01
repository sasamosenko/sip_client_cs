using Newtonsoft.Json;
using SipClient.Models;

namespace SipClient.Services;

public class ConfigService
{
    private readonly string _configPath;
    private readonly string _historyPath;
    
    public ConfigService()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        _historyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "call_history.json");
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
    
    public List<CallRecord> LoadHistory()
    {
        if (File.Exists(_historyPath))
        {
            var json = File.ReadAllText(_historyPath);
            return JsonConvert.DeserializeObject<List<CallRecord>>(json) ?? new List<CallRecord>();
        }
        return new List<CallRecord>();
    }
    
    public void SaveHistory(List<CallRecord> history)
    {
        var json = JsonConvert.SerializeObject(history, Formatting.Indented);
        File.WriteAllText(_historyPath, json);
    }
    
    public void AddToHistory(CallRecord record)
    {
        var history = LoadHistory();
        history.Insert(0, record);
        SaveHistory(history);
    }
}
