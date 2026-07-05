using System.Media;
using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SipClient.Services;

public class NotificationService
{
    private readonly Dispatcher _dispatcher;
    private WaveOutEvent? _ringOutput;
    private bool _ringing;
    private double _ringVolumePercent = 80;
    private Window? _incomingCallWindow;

    public NotificationService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public void SetRingVolume(double percent)
    {
        _ringVolumePercent = percent;
    }

    public void PlayRing()
    {
        if (_ringing) return;
        _ringing = true;

        _dispatcher.Invoke(() =>
        {
            try
            {
                var ring = new RingPatternProvider(44100, _ringVolumePercent / 100.0);

                _ringOutput = new WaveOutEvent();
                _ringOutput.Init(ring);
                _ringOutput.Play();
            }
            catch
            {
                _ringing = false;
            }
        });
    }

    public void PlayBusy()
    {
        StopSound();
        _dispatcher.Invoke(() =>
        {
            try
            {
                using var player = new System.Media.SoundPlayer();
                SystemSounds.Exclamation.Play();
            }
            catch { }
        });
    }

    public void PlayConnected()
    {
        StopSound();
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
        _ringing = false;
        try
        {
            _ringOutput?.Stop();
            _ringOutput?.Dispose();
            _ringOutput = null;
        }
        catch { }
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
                NotificationType.Success => "\u2713",
                NotificationType.Error => "\u2715",
                NotificationType.Warning => "\u26A0",
                _ => "\u2139"
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

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                notification.Close();
            };
            timer.Start();

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
                Content = "\U0001F4DE \u041E\u0442\u0432\u0435\u0442\u0438\u0442\u044C",
                FontSize = 13,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(76, 175, 80)),
                Padding = new System.Windows.Thickness(16, 8, 16, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new System.Windows.Thickness(0, 0, 8, 0)
            };
            answerBtn.Click += (s, e) =>
            {
                StopSound();
                onAnswer();
                notification.Close();
            };

            var rejectBtn = new System.Windows.Controls.Button
            {
                Content = "\u2715 \u041E\u0442\u043A\u043B\u043E\u043D\u0438\u0442\u044C",
                FontSize = 13,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 82, 82)),
                Padding = new System.Windows.Thickness(16, 8, 16, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            rejectBtn.Click += (s, e) =>
            {
                StopSound();
                onReject();
                notification.Close();
            };

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
                            Text = "\U0001F4DE \u0412\u0445\u043E\u0434\u044F\u0449\u0438\u0439 \u0437\u0432\u043E\u043D\u043E\u043A",
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

            notification.Closed += (s, e) => { StopSound(); _incomingCallWindow = null; };

            _incomingCallWindow = notification;
            PlayRing();
            notification.Show();
        });
    }

    public void CloseIncomingCallNotification()
    {
        StopSound();
        try
        {
            if (_incomingCallWindow != null)
            {
                _incomingCallWindow.Close();
                _incomingCallWindow = null;
            }
        }
        catch { }
    }
}

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Generates a repeating ring pattern: 1s tone, 2s silence
/// </summary>
internal class RingPatternProvider : ISampleProvider
{
    private readonly float _sampleRate;
    private readonly float _volume;
    private int _position;
    private const int ToneSamples = 44100;     // 1 second of tone
    private const int SilenceSamples = 88200;   // 2 seconds of silence
    private const int CycleSamples = ToneSamples + SilenceSamples; // 3 seconds total

    public WaveFormat WaveFormat { get; }

    public RingPatternProvider(float sampleRate, double volume01)
    {
        _sampleRate = sampleRate;
        _volume = (float)Math.Clamp(volume01, 0.0, 1.0);
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat((int)sampleRate, 1);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int posInCycle = (_position + i) % CycleSamples;

            if (posInCycle < ToneSamples)
            {
                // During tone: 440Hz sine with slight amplitude modulation
                double t = (double)posInCycle / _sampleRate;
                buffer[offset + i] = (float)(0.3 * _volume * Math.Sin(2 * Math.PI * 440 * t)
                                           * (0.7 + 0.3 * Math.Sin(2 * Math.PI * 0.5 * t)));
            }
            else
            {
                // During silence
                buffer[offset + i] = 0f;
            }
        }

        _position += count;
        return count;
    }
}
