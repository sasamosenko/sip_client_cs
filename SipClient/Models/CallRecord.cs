using System.Windows;
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
        "completed" => "Завершён",
        "failed" => "Ошибка",
        "busy" => "Занят",
        "no_answer" => "Без ответа",
        "rejected" => "Отклонён",
        _ => Status
    };

    public string DirectionIcon => Direction == "in" ? "IN" : "OUT";

    public string DirectionColor => Direction == "in"
        ? "#4CAF50"
        : "#6C63FF";

    public SolidColorBrush StatusBrush => Status switch
    {
        "missed" => new SolidColorBrush(Color.FromRgb(255, 82, 82)),
        "rejected" => new SolidColorBrush(Color.FromRgb(255, 82, 82)),
        "busy" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
        "failed" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
        "no_answer" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
        "transfer" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
        "completed" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
        _ => new SolidColorBrush(Color.FromRgb(160, 160, 176))
    };

    public string ToCsvLine() =>
        $"{Timestamp:yyyy-MM-dd HH:mm:ss},{Direction},{Number},{Status},{DurationStr}";

    public static string CsvHeader() =>
        "Timestamp,Direction,Number,Status,Duration";

    public void CopyToClipboard()
    {
        Clipboard.SetText(ToCsvLine());
    }
}
