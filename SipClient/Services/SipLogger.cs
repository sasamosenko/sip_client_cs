using System.IO;

namespace SipClient.Services;

public class SipLogger
{
    private readonly string _logDir;
    private readonly object _lock = new();
    private bool _enabled;

    public SipLogger()
    {
        _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(_logDir);
        ClearTodayLog();
    }

    private void ClearTodayLog()
    {
        try
        {
            var file = Path.Combine(_logDir, $"sip_{DateTime.Now:yyyy-MM-dd}.log");
            if (File.Exists(file))
                File.WriteAllText(file, string.Empty);
        }
        catch { }
    }

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public void LogSent(string message) => WriteLog("SENT", message);
    public void LogReceived(string message) => WriteLog("RECEIVED", message);
    public void LogEvent(string message) => WriteLog("EVENT", message);
    public void LogError(string message) => WriteLog("ERROR", message);

    private void WriteLog(string direction, string message)
    {
        // Always log errors; other events only when enabled
        if (direction != "ERROR" && !_enabled) return;

        var file = Path.Combine(_logDir, $"sip_{DateTime.Now:yyyy-MM-dd}.log");
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var header = $"= {timestamp} [{direction}] =";

        lock (_lock)
        {
            using var writer = new StreamWriter(file, append: true);
            writer.WriteLine(header);
            writer.WriteLine(message);
            writer.WriteLine();
        }
    }

    public string GetLogPath() => _logDir;
}
