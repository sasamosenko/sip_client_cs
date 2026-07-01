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
    private readonly SipLogger _sipLogger;
    private SIPTransport? _sipTransport;
    private SIPRegistrarUserAgent? _registrar;
    private SIPCallUserAgent? _currentCall;
    private SipConfig _config;

    public bool IsRegistered { get; private set; }
    public bool IsInCall => _currentCall != null;
    public string? CurrentCallId { get; private set; }

    public event Action<int, string>? RegistrationStateChanged;
    public event Action<string, string>? IncomingCall;
    public event Action<string, int, string>? CallStateChanged;
    public event Action? CallEnded;
    public event Action<string>? ErrorOccurred;

    public SipService(ILogger<SipService> logger)
    {
        _logger = logger;
        _sipLogger = new SipLogger();
        _config = new SipConfig();
    }

    public async Task StartAsync(SipConfig config)
    {
        _config = config;

        try
        {
            _sipTransport = new SIPTransport();
            var udpEndPoint = new IPEndPoint(IPAddress.Any, config.Port);
            _sipTransport.AddSIPChannel(new SIPUDPChannel(udpEndPoint));

            _sipLogger.LogEvent($"SIP transport started on port {config.Port}");
            _logger.LogInformation("SIP transport started on port {Port}", config.Port);
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Failed to start transport: {ex.Message}");
            _logger.LogError(ex, "Failed to start SIP transport");
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public async Task RegisterAsync()
    {
        if (_sipTransport == null) return;

        try
        {
            var contactUri = SIPURI.ParseSIPURI($"sip:{_config.Username}@{_config.Server}:{_config.Port}");

            _registrar = new SIPRegistrarUserAgent(
                _sipTransport, null, contactUri,
                _config.Server, _config.Username, _config.Password,
                Guid.NewGuid().ToString("N").Substring(0, 16), 300
            );

            _registrar.RegistrationStateChanged += (state, reason) =>
            {
                IsRegistered = (state == SIPRegistrationStatesEnum.Registered);
                _sipLogger.LogEvent($"Registration: {state} - {reason}");
                RegistrationStateChanged?.Invoke((int)state, reason);
            };

            _registrar.Start();
            _logger.LogInformation("Registration initiated to {Server}:{Port}", _config.Server, _config.Port);
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Registration failed: {ex.Message}");
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public async Task MakeCallAsync(string number)
    {
        if (_sipTransport == null || !IsRegistered) { ErrorOccurred?.Invoke("Не зарегистрирован"); return; }
        if (IsInCall) { ErrorOccurred?.Invoke("Уже в звонке"); return; }

        try
        {
            var callUri = SIPURI.ParseSIPURI($"sip:{number}@{_config.Server}");
            _currentCall = new SIPCallUserAgent(_sipTransport, null, callUri, null, null, null);
            CurrentCallId = Guid.NewGuid().ToString("N");

            _sipLogger.LogEvent($"Calling {number}");

            _currentCall.Answered += () =>
            {
                _sipLogger.LogEvent("Call answered");
                CallStateChanged?.Invoke(CurrentCallId!, 5, "OK");
            };

            _currentCall.RemoteHangup += () =>
            {
                _sipLogger.LogEvent("Remote hangup");
                CallEnded?.Invoke();
                _currentCall = null;
                CurrentCallId = null;
            };

            _currentCall.NoAnswer += () =>
            {
                _sipLogger.LogEvent("No answer");
                CallEnded?.Invoke();
                _currentCall = null;
                CurrentCallId = null;
            };

            _currentCall.Failed += (reason) =>
            {
                _sipLogger.LogError($"Call failed: {reason}");
                CallEnded?.Invoke();
                _currentCall = null;
                CurrentCallId = null;
                ErrorOccurred?.Invoke(reason);
            };

            _currentCall.Start();
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Make call failed: {ex.Message}");
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public void AnswerCall()
    {
        _sipLogger.LogEvent("Answering call");
        _currentCall?.Answer();
    }

    public void HangupCall()
    {
        _sipLogger.LogEvent("Hangup");
        _currentCall?.Hangup();
        _currentCall = null;
        CurrentCallId = null;
    }

    public string GetLogPath() => _sipLogger.GetLogPath();
}
