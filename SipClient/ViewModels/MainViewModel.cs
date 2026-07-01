using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SipClient.Models;
using SipClient.Services;
using System.Collections.ObjectModel;

namespace SipClient.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SipService _sipService;
    private readonly ConfigService _configService;
    private SipConfig _config;
    private DateTime? _callStartTime;
    
    [ObservableProperty] private string _statusText = "Offline";
    [ObservableProperty] private string _statusColor = "Red";
    [ObservableProperty] private string _phoneNumber = "";
    [ObservableProperty] private string _callDuration = "00:00";
    [ObservableProperty] private bool _isInCall;
    [ObservableProperty] private bool _isRegistered;
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
        foreach (var record in _configService.LoadHistory())
        {
            CallHistory.Add(record);
        }
        
        // Create SIP service
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory()
            .CreateLogger<SipService>();
        _sipService = new SipService(logger);
        
        // Subscribe to SIP events
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
            StatusText = "Enter server and username";
            return;
        }
        
        _config.Server = Server;
        _config.Port = Port;
        _config.Username = Username;
        _config.Password = Password;
        _configService.SaveConfig(_config);
        
        StatusText = "Connecting...";
        
        await _sipService.StartAsync(_config);
        await _sipService.RegisterAsync();
    }
    
    [RelayCommand]
    private async Task CallAsync()
    {
        if (string.IsNullOrEmpty(PhoneNumber)) return;
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
        _sipService.AnswerCall();
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
    }
    
    private void OnRegistrationStateChanged(int code, string reason)
    {
        IsRegistered = (code >= 200 && code < 300);
        StatusText = IsRegistered ? "Registered" : $"Offline ({code} {reason})";
        StatusColor = IsRegistered ? "Green" : "Red";
    }
    
    private void OnIncomingCall(string callId, string remoteUri)
    {
        StatusText = $"Incoming: {remoteUri}";
        
        if (AutoAnswerEnabled)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(AutoAnswerDelay * 1000);
                _sipService.AnswerCall();
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
        }
    }
    
    private void OnCallEnded()
    {
        IsInCall = false;
        CallDuration = "00:00";
        
        if (_callStartTime.HasValue)
        {
            var duration = (int)(DateTime.Now - _callStartTime.Value).TotalSeconds;
            var record = new CallRecord
            {
                Number = PhoneNumber,
                Direction = "out",
                Status = "completed",
                Duration = duration,
                Timestamp = DateTime.Now
            };
            
            _configService.AddToHistory(record);
            CallHistory.Insert(0, record);
            
            _callStartTime = null;
        }
    }
    
    private void OnErrorOccurred(string error)
    {
        StatusText = $"Error: {error}";
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
}
