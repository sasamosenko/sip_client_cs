using System.Windows.Media;

namespace SipClient.Models;

public class CallRecord
{
    public string Number { get; set; } = "";
    public string Direction { get; set; } = "out";
    public string Status { get; set; } = "completed";
    public int Duration { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string DurationStr => Duration > 0
        ? $"{Duration / 60:D2}:{Duration % 60:D2}"
        : "—";

    public string TimeDisplay => Timestamp.ToString("dd.MM.yyyy HH:mm");

    public string StatusDisplay => Status switch
    {
        "missed" => "Пропущенный",
        "transfer" => "Трансфер",
        _ => ""
    };

    public string DirectionIcon => Direction == "in" ? "📥" : "📤";

    public string DirectionColor => Direction == "in"
        ? "#4CAF50"
        : "#6C63FF";

    public SolidColorBrush StatusBrush => Status switch
    {
        "missed" => new SolidColorBrush(Color.FromRgb(255, 82, 82)),
        "transfer" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
        _ => new SolidColorBrush(Colors.Transparent)
    };
}
