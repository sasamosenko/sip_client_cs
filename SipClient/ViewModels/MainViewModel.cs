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
    [ObservableProperty] private bool _showSettings;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private bool _isHistoryEmpty = true;

    [ObservableProperty] private string _server = "";
    [ObservableProperty] private int _port = 5060;
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _autoAnswerEnabled;
    [ObservableProperty] private int _autoAnswerDelay = 3;
    [ObservableProperty] private int _micVolume = 80;
    [ObservableProperty] private int _speakerVolume = 80;

    public ObservableCollection<CallRecord> CallHistory { get; } = new();

    public MainViewModel()
    {
        _configService = new ConfigService();
        _historyService = new CallHistoryService();
        _notificationService = new NotificationService();
        _config = _configService.LoadConfig();

        // Load settings
        Server = _config.Server;
        Port = _config.Port;
        Username = _config.Username;
        Password = _config.Password;
        AutoAnswerEnabled = _config.AutoAnswerEnabled;
        AutoAnswerDelay = _config.AutoAnswerDelaySeconds;
        MicVolume = _config.MicVolume;
        SpeakerVolume = _config.SpeakerVolume;

        // Load history
        foreach (var record in _historyService.Load())
            CallHistory.Add(record);
        IsHistoryEmpty = CallHistory.Count == 0;

        // Create SIP service
        ILogger<SipService> logger = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<SipService>();
        _sipService = new SipService(logger);

        _sipService.RegistrationStateChanged += OnRegistrationStateChanged;
        _sipService.IncomingCall += OnIncomingCall;
        _sipService.CallStateChanged += OnCallStateChanged;
        _sipService.CallEnded += OnCallEnded;
        _sipService.ErrorOccurred += OnErrorOccurred;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrEmpty(Server) || string.IsNullOrEmpty(Username))
        {
            _notificationService.ShowNotification("Ошибка", "Введите сервер и логин", NotificationType.Warning);
            return;
        }

        _config.Server = Server;
        _config.Port = Port;
        _config.Username = Username;
        _config.Password = Password;
        _configService.SaveConfig(_config);

        StatusText = "Подключение...";
        StatusBrush = "#FFC107";

        await _sipService.StartAsync(_config);
        await _sipService.RegisterAsync();
    }

    [RelayCommand]
    private async Task CallAsync()
    {
        if (string.IsNullOrEmpty(PhoneNumber)) return;
        ActiveCallNumber = PhoneNumber;
        await _sipService.MakeCallAsync(PhoneNumber);
    }

    [RelayCommand]
    private void Hangup()
    {
        _sipService.HangupCall();
    }

    [RelayCommand]
    private void Answer()
    {
        IsIncomingCall = false;
        _sipService.AnswerCall();
        _notificationService.PlayConnected();
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _config.Server = Server;
        _config.Port = Port;
        _config.Username = Username;
        _config.Password = Password;
        _config.AutoAnswerEnabled = AutoAnswerEnabled;
        _config.AutoAnswerDelaySeconds = AutoAnswerDelay;
        _config.MicVolume = MicVolume;
        _config.SpeakerVolume = SpeakerVolume;
        _configService.SaveConfig(_config);
        _notificationService.ShowNotification("Сохранено", "Настройки обновлены", NotificationType.Success);
        ShowSettings = false;
    }

    private void OnRegistrationStateChanged(int code, string reason)
    {
        IsRegistered = (code >= 200 && code < 300);
        StatusText = IsRegistered ? "Зарегистрирован" : $"Ошибка: {code} {reason}";
        StatusBrush = IsRegistered ? "#4CAF50" : "#FF5252";

        if (IsRegistered)
            _notificationService.ShowNotification("Подключено", $"Регистрация на {Server} успешна", NotificationType.Success);
    }

    private void OnIncomingCall(string callId, string remoteUri)
    {
        IsIncomingCall = true;
        ActiveCallNumber = remoteUri;
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
                var record = new CallRecord
                {
                    Number = remoteUri,
                    Direction = "in",
                    Status = "missed",
                    Duration = 0,
                    Timestamp = DateTime.Now
                };
                _historyService.Add(record);
                CallHistory.Insert(0, record);
                IsHistoryEmpty = false;
            }
        );

        if (AutoAnswerEnabled)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(AutoAnswerDelay * 1000);
                if (IsIncomingCall)
                {
                    IsIncomingCall = false;
                    _sipService.AnswerCall();
                    IsInCall = true;
                    _callStartTime = DateTime.Now;
                    _ = UpdateCallDuration();
                }
            });
        }
    }

    private void OnCallStateChanged(string callId, int state, string reason)
    {
        if (state == 5) // Established
        {
            IsInCall = true;
            _callStartTime = DateTime.Now;
            _ = UpdateCallDuration();
            _notificationService.PlayConnected();
        }
    }

    private void OnCallEnded()
    {
        IsInCall = false;
        IsMuted = false;
        CallDuration = "00:00";
        _notificationService.StopSound();

        if (_callStartTime.HasValue)
        {
            var duration = (int)(DateTime.Now - _callStartTime.Value).TotalSeconds;
            var record = new CallRecord
            {
                Number = ActiveCallNumber,
                Direction = "out",
                Status = "completed",
                Duration = duration,
                Timestamp = DateTime.Now
            };
            _historyService.Add(record);
            CallHistory.Insert(0, record);
            IsHistoryEmpty = false;
            _callStartTime = null;
        }
    }

    private void OnErrorOccurred(string error)
    {
        StatusText = $"Ошибка: {error}";
        StatusBrush = "#FF5252";
        _notificationService.ShowNotification("Ошибка", error, NotificationType.Error);
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

    partial void OnMicVolumeChanged(int value)
    {
        _config.MicVolume = value;
    }

    partial void OnSpeakerVolumeChanged(int value)
    {
        _config.SpeakerVolume = value;
    }
}
