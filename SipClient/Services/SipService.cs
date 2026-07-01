using System.Net;
using System.Net.Sockets;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Encoders;
using SIPSorceryMedia.Windows;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using SipClient.Models;

namespace SipClient.Services;

public class SipService
{
    private readonly ILogger<SipService> _logger;
    private readonly SipLogger _sipLogger;
    private SIPTransport? _sipTransport;
    private SIPRegistrationUserAgent? _registrar;
    private SIPUserAgent? _userAgent;
    private SIPServerUserAgent? _pendingIncomingCall;
    private VoIPMediaSession? _rtpSession;
    private SipConfig _config;

    public bool IsRegistered => _registrar != null;
    public bool IsInCall => _userAgent?.IsCallActive == true;
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

    public static List<AudioDeviceInfo> GetPlaybackDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            devices.Add(new AudioDeviceInfo { Index = i, Name = caps.ProductName });
        }
        return devices;
    }

    public static List<AudioDeviceInfo> GetCaptureDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            devices.Add(new AudioDeviceInfo { Index = i, Name = caps.ProductName });
        }
        return devices;
    }

    public async Task StartAsync(SipConfig config)
    {
        _config = config;

        try
        {
            _sipTransport = new SIPTransport();
            var udpEndPoint = new IPEndPoint(IPAddress.Any, config.Port);
            _sipTransport.AddSIPChannel(new SIPUDPChannel(udpEndPoint));

            _sipTransport.SIPTransportRequestReceived += OnSipRequestReceived;

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
            _registrar = new SIPRegistrationUserAgent(
                _sipTransport,
                _config.Username,
                _config.Password,
                _config.Server,
                _config.RegistrationExpiry,
                maxRegistrationAttemptTimeout: 15,
                registerFailureRetryInterval: 10,
                maxRegisterAttempts: 0,
                exitOnUnequivocalFailure: false
            );

            _registrar.RegistrationSuccessful += (uri) =>
            {
                _sipLogger.LogEvent($"Registration successful: {uri}");
                RegistrationStateChanged?.Invoke(200, "OK");
            };

            _registrar.RegistrationFailed += (uri, err) =>
            {
                _sipLogger.LogError($"Registration failed: {uri} - {err}");
                RegistrationStateChanged?.Invoke(403, err);
            };

            _registrar.RegistrationTemporaryFailure += (uri, msg) =>
            {
                _sipLogger.LogEvent($"Registration temp failure: {uri} - {msg}");
                RegistrationStateChanged?.Invoke(503, msg);
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

    private VoIPMediaSession CreateMediaSession()
    {
        var outIdx = _config.PlaybackDeviceId >= 0 ? _config.PlaybackDeviceId : 0;
        var inIdx = _config.CaptureDeviceId >= 0 ? _config.CaptureDeviceId : 0;

        var audioEndPoint = new WindowsAudioEndPoint(new AudioEncoder(), outIdx, inIdx, false, false);
        var mediaEndPoints = new MediaEndPoints
        {
            AudioSink = audioEndPoint,
            AudioSource = audioEndPoint,
        };

        var session = new VoIPMediaSession(mediaEndPoints);
        session.AcceptRtpFromAny = true;
        return session;
    }

    public async Task MakeCallAsync(string number)
    {
        if (_sipTransport == null) { ErrorOccurred?.Invoke("Транспорт не запущен"); return; }
        if (!IsRegistered) { ErrorOccurred?.Invoke("Не зарегистрирован"); return; }
        if (IsInCall) { ErrorOccurred?.Invoke("Уже в звонке"); return; }

        try
        {
            var callUri = SIPURI.ParseSIPURIRelaxed($"{number}@{_config.Server}");
            CurrentCallId = Guid.NewGuid().ToString("N");

            _rtpSession = CreateMediaSession();

            _userAgent = new SIPUserAgent(_sipTransport, null);
            _userAgent.ClientCallTrying += OnCallTrying;
            _userAgent.ClientCallRinging += OnCallRinging;
            _userAgent.ClientCallAnswered += OnCallAnswered;
            _userAgent.ClientCallFailed += OnCallFailed;
            _userAgent.OnCallHungup += OnCallHungup;

            var fromHeader = new SIPFromHeader(null, new SIPURI(_config.Username, _config.Server, null), null);
            var callDescriptor = new SIPCallDescriptor(
                _config.Username,
                _config.Password,
                callUri.ToString(),
                fromHeader.ToString(),
                null,
                null,
                null,
                null,
                SIPCallDirection.Out,
                SDP.SDP_MIME_CONTENTTYPE,
                null,
                null
            );

            _sipLogger.LogEvent($"Calling {number}");
            await _userAgent.InitiateCallAsync(callDescriptor, _rtpSession, 120);
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Make call failed: {ex.Message}");
            ErrorOccurred?.Invoke(ex.Message);
            CleanupCall();
        }
    }

    public async Task<bool> BlindTransferAsync(string destination)
    {
        if (_userAgent == null || !_userAgent.IsCallActive)
        {
            ErrorOccurred?.Invoke("Нет активного звонка для трансфера");
            return false;
        }

        try
        {
            if (!SIPURI.TryParse(destination, out var uri))
            {
                uri = SIPURI.ParseSIPURIRelaxed($"{destination}@{_config.Server}");
            }

            _sipLogger.LogEvent($"Blind transfer to {destination}");
            var result = await _userAgent.BlindTransfer(uri, TimeSpan.FromSeconds(10), CancellationToken.None);

            if (result)
            {
                _sipLogger.LogEvent("Transfer accepted by remote");
            }
            else
            {
                _sipLogger.LogEvent("Transfer rejected by remote");
                ErrorOccurred?.Invoke("Трансфер отклонён");
            }

            return result;
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Transfer failed: {ex.Message}");
            ErrorOccurred?.Invoke($"Ошибка трансфера: {ex.Message}");
            return false;
        }
    }

    private void OnCallTrying(ISIPClientUserAgent uac, SIPResponse sipResponse)
    {
        _sipLogger.LogEvent($"Call trying: {sipResponse.StatusCode} {sipResponse.ReasonPhrase}");
        CallStateChanged?.Invoke(CurrentCallId!, 2, "Trying");
    }

    private void OnCallRinging(ISIPClientUserAgent uac, SIPResponse sipResponse)
    {
        _sipLogger.LogEvent($"Call ringing: {sipResponse.StatusCode} {sipResponse.ReasonPhrase}");
        CallStateChanged?.Invoke(CurrentCallId!, 3, "Ringing");
    }

    private async void OnCallAnswered(ISIPClientUserAgent uac, SIPResponse sipResponse)
    {
        _sipLogger.LogEvent($"Call answered: {sipResponse.StatusCode} {sipResponse.ReasonPhrase}");
        CallStateChanged?.Invoke(CurrentCallId!, 5, "OK");
    }

    private void OnCallFailed(ISIPClientUserAgent uac, string errorMessage, SIPResponse failureResponse)
    {
        _sipLogger.LogError($"Call failed: {errorMessage}");
        ErrorOccurred?.Invoke(errorMessage);
        CleanupCall();
        CallEnded?.Invoke();
    }

    private void OnCallHungup(SIPDialogue? dialogue)
    {
        _sipLogger.LogEvent("Call hungup");
        CleanupCall();
        CallEnded?.Invoke();
    }

    public void AnswerCall()
    {
        if (_userAgent == null || _pendingIncomingCall == null) return;

        _sipLogger.LogEvent("Answering call");

        try
        {
            _rtpSession = CreateMediaSession();
            _userAgent.OnCallHungup += OnCallHungup;
            _ = _userAgent.Answer(_pendingIncomingCall, _rtpSession);
            _pendingIncomingCall = null;
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Answer call failed: {ex.Message}");
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public void HangupCall()
    {
        _sipLogger.LogEvent("Hangup");

        if (_userAgent != null)
        {
            if (_userAgent.IsCallActive)
                _userAgent.Hangup();
            else
                _userAgent.Cancel();
        }

        _pendingIncomingCall?.Reject(SIPResponseStatusCodesEnum.BusyHere, null, null);
        CleanupCall();
    }

    private Task OnSipRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
    {
        if (sipRequest.Method == SIPMethodsEnum.INVITE)
        {
            if (IsInCall)
            {
                var busyResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.BusyHere, null);
                _ = _sipTransport!.SendResponseAsync(busyResponse);
                return Task.CompletedTask;
            }

            var remoteUri = sipRequest.Header.From?.FromURI?.User ?? remoteEndPoint.ToString();
            var callId = sipRequest.Header.CallId ?? Guid.NewGuid().ToString("N");

            _sipLogger.LogEvent($"Incoming call from {remoteUri}");

            if (_userAgent == null)
                _userAgent = new SIPUserAgent(_sipTransport!, null);

            _pendingIncomingCall = _userAgent.AcceptCall(sipRequest);
            CurrentCallId = callId;

            _sipLogger.LogEvent($"Incoming call accepted for answering: {remoteUri}");
            IncomingCall?.Invoke(callId, remoteUri);
        }
        else if (sipRequest.Method == SIPMethodsEnum.BYE)
        {
            var okResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
            _ = _sipTransport!.SendResponseAsync(okResponse);

            _sipLogger.LogEvent("Call hungup by remote");
            CleanupCall();
            CallEnded?.Invoke();
        }
        else if (sipRequest.Method == SIPMethodsEnum.OPTIONS || sipRequest.Method == SIPMethodsEnum.REGISTER)
        {
            var okResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
            _ = _sipTransport!.SendResponseAsync(okResponse);
        }

        return Task.CompletedTask;
    }

    private void CleanupCall()
    {
        _rtpSession?.Close("hangup");
        _rtpSession = null;
        _userAgent = null;
        _pendingIncomingCall = null;
        CurrentCallId = null;
    }

    public string GetLogPath() => _sipLogger.GetLogPath();
}

public class AudioDeviceInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}
