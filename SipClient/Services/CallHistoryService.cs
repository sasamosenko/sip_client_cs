using System.IO;
using System.Text;
using SipClient.Models;

namespace SipClient.Services;

public class CallHistoryService
{
    private readonly string _filePath;
    
    public CallHistoryService()
    {
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "call_history.csv");
    }
    
    public List<CallRecord> Load()
    {
        var records = new List<CallRecord>();
        if (!File.Exists(_filePath)) return records;
        
        var lines = File.ReadAllLines(_filePath);
        foreach (var line in lines.Skip(1)) // skip header
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');
            if (parts.Length < 5) continue;
            
            records.Add(new CallRecord
            {
                Timestamp = DateTime.Parse(parts[0]),
                Number = parts[1],
                Direction = parts[2],
                Duration = int.TryParse(parts[3], out var d) ? d : 0,
                Status = parts[4]
            });
        }
        return records;
    }
    
    public void Add(CallRecord record)
    {
        var records = Load();
        records.Insert(0, record);
        Save(records);
    }

    public void Clear()
    {
        Save(new List<CallRecord>());
    }
    
    public void Save(List<CallRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Number,Direction,Duration,Status");
        foreach (var r in records)
        {
            sb.AppendLine($"{r.Timestamp:yyyy-MM-dd HH:mm:ss},{Escape(r.Number)},{r.Direction},{r.Duration},{r.Status}");
        }
        File.WriteAllText(_filePath, sb.ToString());
    }
    
    private string Escape(string s) => s.Contains(',') ? $"\"{s}\"" : s;
}
