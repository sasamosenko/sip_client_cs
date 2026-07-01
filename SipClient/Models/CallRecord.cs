namespace SipClient.Models;

public class CallRecord
{
    public string Number { get; set; } = "";
    public string Direction { get; set; } = "out"; // "in" or "out"
    public string Status { get; set; } = "completed"; // "completed", "missed", "transfer"
    public int Duration { get; set; } // seconds
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string DurationStr
    {
        get
        {
            var mins = Duration / 60;
            var secs = Duration % 60;
            return $"{mins:D2}:{secs:D2}";
        }
    }
}
