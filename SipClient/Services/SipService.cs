using System.Net;
using System.Net.Sockets;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using Microsoft.Extensions.Logging;
using SipClient.Models;

namespace SipClient.Services;

public class SipService
{
    private readonly ILogger<SipService> _logger;
    private SIPTransport? _sipTransport;
    private SIPRegistrarUserAgent? _registrar;
    private SIPCallUserAgent? _currentCall;
    private SipConfig _config;
    
    public bool IsRegistered { get; private set; }
    public bool IsInCall => _currentCall != null;
    public string? CurrentCallId { get; private set; }
    
    // Events
    public event Action<int, string>? RegistrationStateChanged; // code, reason
    public event Action<string, string>? IncomingCall; // callId, remoteUri
    public event Action<string, int, string>? CallStateChanged; // callId, state, reason
    public event Action? CallEnded;
    public event Action<string>? ErrorOccurred;

    public SipService(ILogger<SipService> logger)
    {
        _logger = logger;
        _config = new SipConfig();
    }

    public async Task StartAsync(SipConfig config)
    {
        _config = config;
        
        try
        {
            _sipTransport = new SIPTransport();
            
            // Create UDP endpoint
            var udpEndPoint = new IPEndPoint(IPAddress.Any, config.Port);
            _sipTransport.AddSIPChannel(new SIPUDPChannel(udpEndPoint));
            
            _logger.LogInformation("SIP transport started on port {Port}", config.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SIP transport");
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public async Task RegisterAsync()
    {
        if (_sipTransport == null) return;
        
        try
        {
            var userAgent = Guid.NewGuid().ToString("N").Substring(0, 16);
            var contactUri = SIPURI.ParseSIPURI($"sip:{_config.Username}@{_config.Server}:{_config.Port}");
            
            _registrar = new SIPRegistrarUserAgent(
                _sipTransport,
                null,
                contactUri,
                _config.Server,
                _config.Username,
                _config.Password,
                userAgent,
                300
            );
            
            _registrar.RegistrationStateChanged += (state, reason) =>
            {
                IsRegistered = (state == SIPRegistrationStatesEnum.Registered);
                RegistrationStateChanged?.Invoke((int)state, reason);
            };
            
            _registrar.Start();
            
            await Task.Run(async () =>
            {
                while (!_registrar.IsRegistered && _registrar != null)
                {
                    await Task.Delay(100);
                }
            });
            
            _logger.LogInformation("Registration initiated to {Server}:{Port}", _config.Server, _config.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed");
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public async Task MakeCallAsync(string number)
    {
        if (_sipTransport == null || !IsRegistered)
        {
            ErrorOccurred?.Invoke("Not registered");
            return;
        }
        
        if (IsInCall)
        {
            ErrorOccurred?.Invoke("Already in a call");
            return;
        }
        
        try
        {
            var callUri = SIPURI.ParseSIPURI($"sip:{number}@{_config.Server}");
            
            _currentCall = new SIPCallUserAgent(
                _sipTransport,
                null,
                callUri,
                null,
                null,
                null
            );
            
            CurrentCallId = Guid.NewGuid().ToString("N");
            
            _currentCall.Answered += () =>
            {
                CallStateChanged?.Invoke(CurrentCallId!, "established", "OK");
            };
            
            _currentCall.RemoteHangup += () =>
            {
                CallEnded?.Invoke();
                _currentCall = null;
                CurrentCallId = null;
            };
            
            _currentCall.NoAnswer += () =>
            {
                CallEnded?.Invoke();
                _currentCall = null;
                CurrentCallId = null;
            };
            
            _currentCall.Failed += (reason) =>
            {
                CallEnded?.Invoke();
                _currentCall = null;
                CurrentCallId = null;
                ErrorOccurred?.Invoke(reason);
            };
            
            _currentCall.CallReady += (audio) =>
            {
                // Call is ready, media is flowing
            };
            
            _currentCall.Start();
            
            _logger.LogInformation("Calling {Number}", number);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to make call");
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public void AnswerCall()
    {
        _currentCall?.Answer();
    }

    public void HangupCall()
    {
        _currentCall?.Hangup();
        _currentCall = null;
        CurrentCallId = null;
    }

    public async Task StopAsync()
    {
        _currentCall?.Hangup();
        _registrar?.Stop();
        
        if (_sipTransport != null)
        {
            _sipTransport.Shutdown();
            _sipTransport = null;
        }
        
        IsRegistered = false;
        _logger.LogInformation("SIP service stopped");
    }
}
