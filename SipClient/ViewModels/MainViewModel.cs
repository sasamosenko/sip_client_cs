using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SipClient.Models;
using SipClient.Services;
using System.Collections.ObjectModel;

namespace SipClient.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SipService _sipService;
    private readonly TransferService _transferService;
    private readonly CallHistoryService _historyService;
    private readonly NotificationService _notificationService;
    private readonly ConfigService _configService;
    private SipConfig _config;
    private DateTime? _callStartTime;

    [ObservableProperty] private string _statusText = "Не подключено";
    [ObservableProperty] private string _statusBrush = "#FF5252";
    [ObservableProperty] private string _phoneNumber = "";
    [ObservableProperty] private string _callDuration = "00:00";
    [ObservableProperty] private string _activeCallNumber = "";
    [ObservableProperty] private bool _isInCall;
    [ObservableProperty] private bool _isIncomingCall;
    [ObservableProperty] private bool _isRegistered;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private bool _isHistoryEmpty = true;
    [ObservableProperty] private bool _showTransferDialog;

    [ObservableProperty] private string _server = "";
    [ObservableProperty] private string _proxy = "";
    [ObservableProperty] private int _port = 5060;
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _displayName = "SIP Client";
    [ObservableProperty] private string _domain = "";
    [ObservableProperty] private string _authUsername = "";
    [ObservableProperty] private string _userAgent = "SipClient/1.0";
    [ObservableProperty] private bool _autoAnswerEnabled;
    [ObservableProperty] private int _autoAnswerDelay = 3;
    [ObservableProperty] private double _micVolume = 100;
    [ObservableProperty] private double _speakerVolume = 100;
    [ObservableProperty] private string _transferNumber = "";
    [ObservableProperty] private int _localPort = 5080;
    [ObservableProperty] private int _rtpPortMin = 10000;
    [ObservableProperty] private int _rtpPortMax = 20000;
    [ObservableProperty] private int _registrationExpiry = 600;
    [ObservableProperty] private double _ringVolume = 80;
    [ObservableProperty] private bool _sipLoggingEnabled;

    [ObservableProperty] private AudioDeviceInfo? _selectedPlaybackDevice;
    [ObservableProperty] private AudioDeviceInfo? _selectedCaptureDevice;

    public ObservableCollection<CodecOption> CodecOptions { get; } = new();

    public ObservableCollection<CallRecord> CallHistory { get; } = new();
    public ObservableCollection<AudioDeviceInfo> PlaybackDevices { get; } = new();
    public ObservableCollection<AudioDeviceInfo> CaptureDevices { get; } = new();

    public MainViewModel()
    {
        _configService = new ConfigService();
        _historyService = new CallHistoryService();
        _notificationService = new NotificationService();
        _config = _configService.LoadConfig();

        // Load settings
        Server = _config.Server;
        Proxy = _config.Proxy;
        Port = _config.Port;
        Username = _config.Username;
        Password = _config.Password;
        DisplayName = _config.DisplayName;
        Domain = _config.Domain;
        AuthUsername = _config.AuthUsername;
        UserAgent = _config.UserAgent;
        AutoAnswerEnabled = _config.AutoAnswerEnabled;
        AutoAnswerDelay = _config.AutoAnswerDelaySeconds;
        MicVolume = _config.MicVolume;
        SpeakerVolume = _config.SpeakerVolume;
        RtpPortMin = _config.RtpPortMin;
        RtpPortMax = _config.RtpPortMax;
        RegistrationExpiry = _config.RegistrationExpiry;
        RingVolume = _config.RingVolume;
        _notificationService.SetRingVolume(RingVolume);
        SipLoggingEnabled = _config.SipLoggingEnabled;
        LocalPort = _config.LocalPort;

        // Load codecs
        var allCodecs = new (string name, string desc, int pt)[]
        {
            ("G722", "G.722 (широкополосный)", 9),
            ("PCMU", "G.711 μ-law", 0),
            ("PCMA", "G.711 A-law", 8),
            ("G729", "G.729 (сжатый)", 18),
        };
        var enabledSet = new HashSet<string>(_config.EnabledCodecs, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, desc, pt) in allCodecs)
            CodecOptions.Add(new CodecOption(name, desc, pt, enabledSet.Contains(name)));

        // Load history
        foreach (var record in _historyService.Load())
            CallHistory.Add(record);
        IsHistoryEmpty = CallHistory.Count == 0;

        // Load audio devices
        LoadAudioDevices();

        // Create SIP service
        ILogger<SipService> logger = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<SipService>();
        _sipService = new SipService(logger);
        _sipService.SetLoggingEnabled(SipLoggingEnabled);

        // Create transfer service (separate module)
        _transferService = new TransferService(_sipService.GetSipLogger());

        _sipService.RegistrationStateChanged += OnRegistrationStateChanged;
        _sipService.IncomingCall += OnIncomingCall;
        _sipService.CallStateChanged += OnCallStateChanged;
        _sipService.CallEnded += OnCallEnded;
        _sipService.CallFailedWithReason += OnCallFailedWithReason;
        _sipService.ErrorOccurred += OnErrorOccurred;

        // Auto-connect on startup if credentials are configured
        // Also support --call, --server, --username, --password command-line args for testing
        var autoCall = App.AutoCallNumber;
        if (!string.IsNullOrEmpty(App.TestServer)) Server = App.TestServer;
        if (!string.IsNullOrEmpty(App.TestUsername)) Username = App.TestUsername;
        if (!string.IsNullOrEmpty(App.TestPassword)) Password = App.TestPassword;

        if (!string.IsNullOrEmpty(Server) && !string.IsNullOrEmpty(Username))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(500); // let the UI render first
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await ConnectAsync();

                    // Auto-call after successful registration
                    if (!string.IsNullOrEmpty(autoCall))
                    {
                        // Wait for registration
                        for (int i = 0; i < 30 && !IsRegistered; i++)
                            await Task.Delay(1000);

                        if (IsRegistered)
                        {
                            PhoneNumber = autoCall;
                            await CallAsync();
                        }
                    }
                });
            });
        }
    }

    private void LoadAudioDevices()
    {
        PlaybackDevices.Clear();
        CaptureDevices.Clear();

        foreach (var device in SipService.GetPlaybackDevices())
            PlaybackDevices.Add(device);

        foreach (var device in SipService.GetCaptureDevices())
            CaptureDevices.Add(device);

        // Restore saved device selection
        if (_config.PlaybackDeviceId >= 0 && _config.PlaybackDeviceId < PlaybackDevices.Count)
            SelectedPlaybackDevice = PlaybackDevices.FirstOrDefault(d => d.Index == _config.PlaybackDeviceId);
        else if (PlaybackDevices.Count > 0)
            SelectedPlaybackDevice = PlaybackDevices[0];

        if (_config.CaptureDeviceId >= 0 && _config.CaptureDeviceId < CaptureDevices.Count)
            SelectedCaptureDevice = CaptureDevices.FirstOrDefault(d => d.Index == _config.CaptureDeviceId);
        else if (CaptureDevices.Count > 0)
            SelectedCaptureDevice = CaptureDevices[0];
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrEmpty(Server) || string.IsNullOrEmpty(Username))
        {
            _notificationService.ShowNotification("Ошибка", "Введите сервер и логин", NotificationType.Warning);
            return;
        }

        SaveConfigToService();

        StatusText = "Подключение...";
        StatusBrush = "#FFC107";

        await _sipService.StartAsync(_config);
        await _sipService.RegisterAsync();
    }

    [RelayCommand]
    private async Task ReregisterAsync()
    {
        if (!IsRegistered) return;

        SaveConfigToService();
        await _sipService.RegisterAsync();
        _notificationService.ShowNotification("Перерегистрация", "Запрошена повторная регистрация", NotificationType.Info);
    }

    private void SaveConfigToService()
    {
        _config.Server = Server;
        _config.Proxy = Proxy;
        _config.Port = Port;
        _config.Username = Username;
        _config.Password = Password;
        _config.DisplayName = DisplayName;
        _config.Domain = Domain;
        _config.AuthUsername = AuthUsername;
        _config.UserAgent = UserAgent;
        _config.AutoAnswerEnabled = AutoAnswerEnabled;
        _config.AutoAnswerDelaySeconds = AutoAnswerDelay;
        _config.MicVolume = MicVolume;
        _config.SpeakerVolume = SpeakerVolume;
        _config.RtpPortMin = RtpPortMin;
        _config.RtpPortMax = RtpPortMax;
        _config.RegistrationExpiry = RegistrationExpiry;
        _config.RingVolume = RingVolume;
        _config.SipLoggingEnabled = SipLoggingEnabled;
        _config.LocalPort = LocalPort;
        if (SelectedPlaybackDevice != null)
            _config.PlaybackDeviceId = SelectedPlaybackDevice.Index;
        if (SelectedCaptureDevice != null)
            _config.CaptureDeviceId = SelectedCaptureDevice.Index;
        _config.EnabledCodecs = CodecOptions.Where(c => c.IsEnabled).Select(c => c.Name).ToList();
        _sipService.UpdateConfig(_config);
    }

    [RelayCommand]
    private async Task CallAsync()
    {
        if (string.IsNullOrEmpty(PhoneNumber)) return;
        ActiveCallNumber = PhoneNumber;
        _lastCallDirection = "out";
        await _sipService.MakeCallAsync(PhoneNumber);
    }

    [RelayCommand]
    private void Hangup()
    {
        if (IsIncomingCall)
        {
            _notificationService.CloseIncomingCallNotification();
            IsIncomingCall = false;
        }
        _sipService.HangupCall();
    }

    [RelayCommand]
    private void Answer()
    {
        _notificationService.CloseIncomingCallNotification();
        IsIncomingCall = false;
        _sipService.AnswerCall();
        _notificationService.PlayConnected();
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        _sipService?.SetMuted(IsMuted);
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        var window = new Views.SettingsWindow
        {
            DataContext = this,
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        SaveConfigToService();
        _configService.SaveConfig(_config);
        _notificationService.SetRingVolume(RingVolume);
        _sipService.SetLoggingEnabled(SipLoggingEnabled);

        _notificationService.ShowNotification("Сохранено", "Настройки обновлены", NotificationType.Success);

        // Close settings window
        var settingsWindow = System.Windows.Application.Current.Windows
            .OfType<Views.SettingsWindow>()
            .FirstOrDefault();
        settingsWindow?.Close();
    }

    [RelayCommand]
    private void OpenTransferDialog()
    {
        if (!IsInCall) return;
        TransferNumber = "";
        ShowTransferDialog = true;
    }

    [RelayCommand]
    private void ShowAbout()
    {
        var about = new Views.AboutWindow
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        about.ShowDialog();
    }

    [RelayCommand]
    private async Task ExecuteTransferAsync()
    {
        if (string.IsNullOrEmpty(TransferNumber)) return;

        var dialogInfo = _sipService.GetCallDialogInfo();
        if (dialogInfo == null)
        {
            _notificationService.ShowNotification("Ошибка", "Нет активного звонка для трансфера", NotificationType.Error);
            return;
        }

        ShowTransferDialog = false;
        StatusText = $"Трансфер на {TransferNumber}...";
        StatusBrush = "#FFC107";

        var (remoteEP, callId, fromHeader, toHeader, cseq) = dialogInfo.Value;
        var result = await _transferService.SendReferInDialog(TransferNumber, remoteEP, callId, fromHeader, toHeader, cseq);

        if (result)
        {
            StatusText = "Трансфер отправлен";
            StatusBrush = "#4CAF50";
            _notificationService.ShowNotification("Трансфер", $"REFER отправлен на {TransferNumber}", NotificationType.Success);
        }
        else
        {
            StatusText = "Ошибка трансфера";
            StatusBrush = "#FF5252";
        }
    }

    [RelayCommand]
    private void CancelTransfer()
    {
        ShowTransferDialog = false;
    }

    [RelayCommand]
    private void Redial(string? number)
    {
        if (string.IsNullOrEmpty(number) || IsInCall) return;
        PhoneNumber = number;
        _ = CallAsync();
    }

    [RelayCommand]
    private void ClearCallHistory()
    {
        CallHistory.Clear();
        IsHistoryEmpty = true;
        _historyService.Clear();
    }

    private void OnRegistrationStateChanged(int code, string reason)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsRegistered = (code >= 200 && code < 300);
            StatusText = IsRegistered ? "Зарегистрирован" : $"Ошибка: {code} {reason}";
            StatusBrush = IsRegistered ? "#4CAF50" : "#FF5252";

            if (IsRegistered)
                _notificationService.ShowNotification("Подключено", $"Регистрация на {Server} успешна", NotificationType.Success);
        });
    }

    private void OnIncomingCall(string callId, string remoteUri)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsIncomingCall = true;
            ActiveCallNumber = remoteUri;
            _lastCallDirection = "in";
            StatusText = $"Входящий: {remoteUri}";

            _notificationService.ShowIncomingCallNotification(
                remoteUri,
                onAnswer: () =>
                {
                    IsIncomingCall = false;
                    _sipService.AnswerCall();
                    IsInCall = true;
                    _callStartTime = DateTime.Now;
                    _ = UpdateCallDuration();
                },
                onReject: () =>
                {
                    IsIncomingCall = false;
                    _sipService.HangupCall();
                    RecordCall(remoteUri, "in", "missed", 0);
                }
            );

            if (AutoAnswerEnabled)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(AutoAnswerDelay * 1000);
                    if (IsIncomingCall)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            _notificationService.CloseIncomingCallNotification();
                            IsIncomingCall = false;
                            _sipService.AnswerCall();
                            IsInCall = true;
                            StatusText = $"Звонок: {ActiveCallNumber}";
                            StatusBrush = "#4CAF50";
                            _callStartTime = DateTime.Now;
                            _ = UpdateCallDuration();
                        });
                    }
                });
            }
        });
    }

    private void OnCallStateChanged(string callId, int state, string reason)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (state == 5)
            {
                IsInCall = true;
                _callStartTime = DateTime.Now;
                _ = UpdateCallDuration();
                _notificationService.PlayConnected();
            }
        });
    }

    private void OnCallEnded()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _notificationService.CloseIncomingCallNotification();
            IsIncomingCall = false;

            if (_callStartTime.HasValue)
            {
                var duration = (int)(DateTime.Now - _callStartTime.Value).TotalSeconds;
                RecordCall(ActiveCallNumber, _lastCallDirection, "completed", duration);
                _callStartTime = null;
            }

            IsInCall = false;
            IsMuted = false;
            CallDuration = "00:00";
            StatusText = IsRegistered ? "Зарегистрирован" : "Не зарегистрирован";
            StatusBrush = IsRegistered ? "#4CAF50" : "#FF5252";
        });
    }

    private void OnCallFailedWithReason(string callId, string reason)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _notificationService.CloseIncomingCallNotification();
            IsIncomingCall = false;

            var status = MapSipReason(reason);
            if (_callStartTime.HasValue)
            {
                var duration = (int)(DateTime.Now - _callStartTime.Value).TotalSeconds;
                RecordCall(ActiveCallNumber, _lastCallDirection, status, duration);
                _callStartTime = null;
            }
            else
            {
                RecordCall(ActiveCallNumber, "out", status, 0);
            }

            IsInCall = false;
            IsMuted = false;
            CallDuration = "00:00";
            StatusText = IsRegistered ? "Зарегистрирован" : "Не зарегистрирован";
            StatusBrush = IsRegistered ? "#4CAF50" : "#FF5252";
        });
    }

    private void RecordCall(string number, string direction, string status, int duration)
    {
        var record = new CallRecord
        {
            Number = number,
            Direction = direction,
            Status = status,
            Duration = duration,
            Timestamp = DateTime.Now
        };
        _historyService.Add(record);
        CallHistory.Insert(0, record);
        IsHistoryEmpty = false;
    }

    private static string MapSipReason(string reason)
    {
        return reason.ToLowerInvariant() switch
        {
            var r when r.Contains("busy") => "busy",
            var r when r.Contains("not found") => "failed",
            var r when r.Contains("decline") || r.Contains("reject") => "rejected",
            var r when r.Contains("timeout") || r.Contains("no response") => "no_answer",
            var r when r.Contains("cancel") => "no_answer",
            var r when r.Contains("terminated") => "completed",
            _ => "failed"
        };
    }

    private string _lastCallDirection = "out";

    private void OnErrorOccurred(string error)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            StatusText = $"Ошибка: {error}";
            StatusBrush = "#FF5252";
            _notificationService.ShowNotification("Ошибка", error, NotificationType.Error);
        });
    }

    private async Task UpdateCallDuration()
    {
        while (IsInCall && _callStartTime.HasValue)
        {
            var elapsed = DateTime.Now - _callStartTime.Value;
            CallDuration = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
            await Task.Delay(1000);
        }
    }

    partial void OnMicVolumeChanged(double value)
    {
        _config.MicVolume = value;
        _sipService?.SetMicVolume(value);
    }

    partial void OnSpeakerVolumeChanged(double value)
    {
        _config.SpeakerVolume = value;
        _sipService?.SetSpeakerVolume(value);
    }
}
