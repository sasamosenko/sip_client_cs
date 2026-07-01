using System.Media;
using System.Windows;
using System.Windows.Threading;

namespace SipClient.Services;

public class NotificationService
{
    private readonly Dispatcher _dispatcher;
    private SoundPlayer? _ringPlayer;
    private SoundPlayer? _busyPlayer;
    
    public NotificationService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        LoadSounds();
    }
    
    private void LoadSounds()
    {
        try
        {
            // Use system sounds as fallback
            _ringPlayer = null;
            _busyPlayer = null;
        }
        catch { }
    }
    
    public void PlayRing()
    {
        _dispatcher.Invoke(() =>
        {
            try
            {
                SystemSounds.Beep.Play();
            }
            catch { }
        });
    }
    
    public void PlayBusy()
    {
        _dispatcher.Invoke(() =>
        {
            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch { }
        });
    }
    
    public void PlayConnected()
    {
        _dispatcher.Invoke(() =>
        {
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch { }
        });
    }
    
    public void StopSound()
    {
        // Stop any playing sound
    }
    
    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        _dispatcher.Invoke(() =>
        {
            var notification = new Window
            {
                Title = "",
                Width = 320,
                Height = 90,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Left = SystemParameters.PrimaryScreenWidth - 340,
                Top = 40
            };
            
            var bg = type switch
            {
                NotificationType.Success => "#4CAF50",
                NotificationType.Error => "#FF5252",
                NotificationType.Warning => "#FFC107",
                _ => "#6C63FF"
            };
            
            var icon = type switch
            {
                NotificationType.Success => "✓",
                NotificationType.Error => "✕",
                NotificationType.Warning => "⚠",
                _ => "ℹ"
            };
            
            notification.Content = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bg)),
                CornerRadius = new System.Windows.CornerRadius(10),
                Padding = new System.Windows.Thickness(16, 12, 16, 12),
                Child = new System.Windows.Controls.StackPanel
                {
                    Children =
                    {
                        new System.Windows.Controls.TextBlock
                        {
                            Text = $"{icon} {title}",
                            FontSize = 14,
                            FontWeight = System.Windows.FontWeights.SemiBold,
                            Foreground = System.Windows.Media.Brushes.White
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = message,
                            FontSize = 12,
                            Foreground = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromArgb(200, 255, 255, 255)),
                            TextWrapping = System.Windows.TextWrapping.Wrap,
                            Margin = new System.Windows.Thickness(0, 4, 0, 0)
                        }
                    }
                }
            };
            
            // Auto-close after 3 seconds
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                notification.Close();
            };
            timer.Start();
            
            // Click to close
            notification.MouseLeftButtonDown += (s, e) => notification.Close();
            
            notification.Show();
        });
    }
    
    public void ShowIncomingCallNotification(string callerNumber, Action onAnswer, Action onReject)
    {
        _dispatcher.Invoke(() =>
        {
            var notification = new Window
            {
                Title = "",
                Width = 360,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Left = SystemParameters.PrimaryScreenWidth - 380,
                Top = 40
            };
            
            var answerBtn = new System.Windows.Controls.Button
            {
                Content = "📞 Ответить",
                FontSize = 13,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(76, 175, 80)),
                Padding = new System.Windows.Thickness(16, 8, 16, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new System.Windows.Thickness(0, 0, 8, 0)
            };
            answerBtn.Click += (s, e) => { onAnswer(); notification.Close(); };
            
            var rejectBtn = new System.Windows.Controls.Button
            {
                Content = "✕ Отклонить",
                FontSize = 13,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 82, 82)),
                Padding = new System.Windows.Thickness(16, 8, 16, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            rejectBtn.Click += (s, e) => { onReject(); notification.Close(); };
            
            notification.Content = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(22, 33, 62)),
                CornerRadius = new System.Windows.CornerRadius(12),
                Padding = new System.Windows.Thickness(20, 16, 20, 16),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(108, 99, 255)),
                BorderThickness = new System.Windows.Thickness(1),
                Child = new System.Windows.Controls.StackPanel
                {
                    Children =
                    {
                        new System.Windows.Controls.TextBlock
                        {
                            Text = "📞 Входящий звонок",
                            FontSize = 14,
                            FontWeight = System.Windows.FontWeights.SemiBold,
                            Foreground = System.Windows.Media.Brushes.White,
                            Margin = new System.Windows.Thickness(0, 0, 0, 4)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = callerNumber,
                            FontSize = 20,
                            FontWeight = System.Windows.FontWeights.Bold,
                            Foreground = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(108, 99, 255)),
                            Margin = new System.Windows.Thickness(0, 0, 0, 16)
                        },
                        new System.Windows.Controls.StackPanel
                        {
                            Orientation = System.Windows.Controls.Orientation.Horizontal,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            Children = { answerBtn, rejectBtn }
                        }
                    }
                }
            };
            
            PlayRing();
            
            notification.Show();
        });
    }
}

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}
