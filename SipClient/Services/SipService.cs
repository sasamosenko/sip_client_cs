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
    private double _micVolume = 100;
    private double _speakerVolume = 100;
    private int _lastLoggedMic = -1;
    private int _lastLoggedSpeaker = -1;
    private bool _hangupInProgress;
    private bool _callEndedFired;
    private bool _manualCallInProgress;

    // Stored dialogue info for sending BYE even after agent cleanup
    private SIPDialogue? _activeDialogue;

    // Manual INVITE tracking
    private string? _outgoingCallId;
    private SIPFromHeader? _outgoingFromHeader;
    private SIPToHeader? _outgoingToHeader;
    private int _outgoingCSeq = 1;
    private SIPEndPoint? _outgoingRemoteEndPoint;
    private string? _remoteContactUri;
    private bool _outgoingCallAnswered;
    private SIPRequest? _outgoingInviteRequest;

    public bool IsRegistered => _registrar != null;
    public bool IsInCall => _manualCallInProgress || _userAgent?.IsCallActive == true;
    public string? CurrentCallId { get; private set; }

    /// <summary>
    /// Получить информацию о текущем диалоге для трансфера.
    /// </summary>
    public (SIPEndPoint? remoteEndPoint, string? callId, SIPFromHeader? fromHeader, SIPToHeader? toHeader, int cseq)? GetCallDialogInfo()
    {
        if (_manualCallInProgress && _outgoingRemoteEndPoint != null && _outgoingCallId != null)
        {
            return (_outgoingRemoteEndPoint, _outgoingCallId, _outgoingFromHeader, _outgoingToHeader, _outgoingCSeq);
        }
        else if (_userAgent?.IsCallActive == true && _userAgent?.Dialogue != null)
        {
            var d = _userAgent!.Dialogue!;
            return (
                d.RemoteSIPEndPoint,
                d.CallId,
                new SIPFromHeader(
                    d.LocalUserField?.Name ?? _config.Username,
                    d.LocalUserField?.URI ?? SIPURI.ParseSIPURIRelaxed($"sip:{_config.Username}@{_config.Server}"),
                    d.LocalTag),
                new SIPToHeader(null, d.RemoteTarget, d.RemoteTag),
                d.CSeq
            );
        }
        return null;
    }

    public event Action<int, string>? RegistrationStateChanged;
    public event Action<string, string>? IncomingCall;
    public event Action<string, int, string>? CallStateChanged;
    public event Action? CallEnded;
    public event Action<string, string>? CallFailedWithReason;
    public event Action<string>? ErrorOccurred;

    public SipService(ILogger<SipService> logger)
    {
        _logger = logger;
        _sipLogger = new SipLogger();
        _config = new SipConfig();
    }

    public void SetMicVolume(double percent)
    {
        _micVolume = percent;
        var rounded = (int)Math.Round(percent);
        if (Math.Abs(_lastLoggedMic - rounded) >= 1)
        {
            _lastLoggedMic = rounded;
            _sipLogger.LogEvent($"Mic volume set to {rounded}%");
        }
    }

    public void SetSpeakerVolume(double percent)
    {
        _speakerVolume = percent;
        var rounded = (int)Math.Round(percent);
        if (Math.Abs(_lastLoggedSpeaker - rounded) >= 1)
        {
            _lastLoggedSpeaker = rounded;
            _sipLogger.LogEvent($"Speaker volume set to {rounded}%");
        }
    }

    public void SetMuted(bool muted)
    {
        _sipLogger.LogEvent($"Microphone {(muted ? "muted" : "unmuted")}");
        try
        {
            if (_rtpSession != null)
            {
                _rtpSession.SetMediaStreamStatus(SDPMediaTypesEnum.audio,
                    muted ? MediaStreamStatusEnum.Inactive : MediaStreamStatusEnum.SendRecv);
            }
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Failed to set mute: {ex.Message}");
        }
    }

    public void SetLoggingEnabled(bool enabled)
    {
        _sipLogger.Enabled = enabled;
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

        // Cleanup old transport
        try
        {
            if (_sipTransport != null)
            {
                _sipTransport.SIPTransportRequestReceived -= OnSipRequestReceived;
                try { _sipTransport.Shutdown(); } catch { }
                _sipTransport = null;
            }
        }
        catch { }

        try
        {
            _sipTransport = new SIPTransport();

            // Try specified port, fallback to OS-assigned port
            IPEndPoint udpEndPoint;
            try
            {
                udpEndPoint = new IPEndPoint(IPAddress.Any, config.LocalPort);
                _sipTransport.AddSIPChannel(new SIPUDPChannel(udpEndPoint));
            }
            catch
            {
                udpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                _sipTransport.AddSIPChannel(new SIPUDPChannel(udpEndPoint));
            }

            _sipTransport.SIPTransportRequestReceived += OnSipRequestReceived;
            _sipTransport.SIPTransportResponseReceived += OnSipResponseReceived;

            HookSdpFiltering();

            // SIP packet dumps — trace events capture ALL traffic (sent + received)
            _sipTransport.SIPRequestInTraceEvent += (local, remote, req) =>
                _sipLogger.LogReceived(req.ToString());
            _sipTransport.SIPRequestOutTraceEvent += (local, remote, req) =>
                _sipLogger.LogSent(req.ToString());
            _sipTransport.SIPResponseInTraceEvent += (local, remote, resp) =>
                _sipLogger.LogReceived(resp.ToString());
            _sipTransport.SIPResponseOutTraceEvent += (local, remote, resp) =>
                _sipLogger.LogSent(resp.ToString());
            _sipTransport.SIPBadRequestInTraceEvent += (local, remote, msg, _, raw) =>
                _sipLogger.LogError($"Bad request from {remote}: {msg}\n{raw}");
            _sipTransport.SIPBadResponseInTraceEvent += (local, remote, msg, _, raw) =>
                _sipLogger.LogError($"Bad response from {remote}: {msg}\n{raw}");

            var actualPort = ((SIPUDPChannel)_sipTransport.GetSIPChannels().First()).Port;
            _sipLogger.LogEvent($"SIP transport started on port {actualPort}");
            _logger.LogInformation("SIP transport started on port {Port}", actualPort);
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Failed to start transport: {ex.Message}");
            _logger.LogError(ex, "Failed to start SIP transport");
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public void UpdateConfig(SipConfig config)
    {
        _config = config;
    }

    public async Task RegisterAsync()
    {
        if (_sipTransport == null)
        {
            _sipLogger.LogError("RegisterAsync: transport is null");
            return;
        }

        // Cleanup old registrar
        _registrar = null;

        try
        {
            var domain = string.IsNullOrEmpty(_config.Domain) ? _config.Server : _config.Domain;
            var authUser = string.IsNullOrEmpty(_config.AuthUsername) ? _config.Username : _config.AuthUsername;
            var sipAOR = SIPURI.ParseSIPURI($"sip:{_config.Username}@{domain}");
            var contactURI = SIPURI.ParseSIPURI($"sip:{_config.Username}@{_config.Server}:{_config.LocalPort}");
            var registrarHost = _config.Server + (_config.Port != 5060 ? $":{_config.Port}" : "");

            _sipLogger.LogEvent($"RegisterAsync: AOR={sipAOR}, Contact={contactURI}, Server={registrarHost}, AuthUser={authUser}, Domain={domain}");

            _registrar = new SIPRegistrationUserAgent(
                _sipTransport,
                null,                   // outboundProxy
                sipAOR,                 // sipAccountAOR: sip:502@127.0.0.1
                authUser,               // authUsername
                _config.Password,       // password
                domain,                 // realm
                registrarHost,          // registrarHost
                contactURI,             // contactURI: sip:502@127.0.0.1:5080
                _config.RegistrationExpiry,
                null,                   // customHeaders
                60,                     // maxRegistrationAttemptTimeout
                300,                    // registerFailureRetryInterval
                3,                      // maxRegisterAttempts
                false                   // exitOnUnequivocalFailure
            );

            _registrar.UserDisplayName = _config.DisplayName;
            _registrar.UserAgent = "SipClient/" + SipVersion.String;

            _registrar.RegistrationSuccessful += (uri, rsp) =>
            {
                _sipLogger.LogEvent($"Registration successful: {uri}");
                RegistrationStateChanged?.Invoke(200, "OK");
            };

            _registrar.RegistrationFailed += (uri, rsp, err) =>
            {
                _sipLogger.LogError($"Registration failed: {uri} - {err}");
                RegistrationStateChanged?.Invoke(403, err);
            };

            _registrar.RegistrationTemporaryFailure += (uri, rsp, msg) =>
            {
                _sipLogger.LogEvent($"Registration temp failure: {uri} - {msg}");
                RegistrationStateChanged?.Invoke(503, msg);
            };

            _registrar.Start();
            _sipLogger.LogEvent($"Registration started to {registrarHost}");
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
        var session = new VoIPMediaSession(audioEndPoint.ToMediaEndPoints());
        session.AcceptRtpFromAny = true;
        return session;
    }

    // Codec payload types: name -> payload number
    private static readonly Dictionary<string, int> CodecPayloadMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PCMU"] = 0,
        ["PCMA"] = 8,
        ["G722"] = 9,
        ["G729"] = 18,
        ["telephone-event"] = 101
    };

    private string FilterSdpCodecs(string sdp)
    {
        var enabled = _config.EnabledCodecs;
        if (enabled == null || enabled.Count == 0) return sdp;

        var enabledPayloads = new HashSet<int>();
        foreach (var codec in enabled)
        {
            if (CodecPayloadMap.TryGetValue(codec, out var pt))
                enabledPayloads.Add(pt);
        }
        // Always keep telephone-event
        enabledPayloads.Add(101);

        var lines = sdp.Split(["\r\n", "\n"], StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("m=audio", StringComparison.OrdinalIgnoreCase))
            {
                // Parse: m=audio <port> RTP/AVP <pt1> <pt2> ...
                var parts = lines[i].Split(' ');
                if (parts.Length > 3)
                {
                    var kept = new List<string> { parts[0], parts[1], parts[2] };
                    for (int j = 3; j < parts.Length; j++)
                    {
                        if (int.TryParse(parts[j], out var pt) && enabledPayloads.Contains(pt))
                            kept.Add(parts[j]);
                    }
                    lines[i] = string.Join(" ", kept);
                }
            }
            else if (lines[i].StartsWith("a=rtpmap:", StringComparison.OrdinalIgnoreCase))
            {
                // Extract payload type from "a=rtpmap:<pt> <codec>/<rate>"
                var afterColon = lines[i].Substring(9);
                var spaceIdx = afterColon.IndexOf(' ');
                if (spaceIdx > 0 && int.TryParse(afterColon.Substring(0, spaceIdx), out var pt))
                {
                    if (!enabledPayloads.Contains(pt))
                    {
                        lines[i] = ""; // remove this line
                    }
                }
            }
            else if (lines[i].StartsWith("a=fmtp:", StringComparison.OrdinalIgnoreCase))
            {
                var afterColon = lines[i].Substring(7);
                var spaceIdx = afterColon.IndexOf(' ');
                if (spaceIdx > 0 && int.TryParse(afterColon.Substring(0, spaceIdx), out var pt))
                {
                    if (!enabledPayloads.Contains(pt))
                    {
                        lines[i] = "";
                    }
                }
            }
        }

        var result = string.Join("\r\n", lines.Where(l => l != ""));
        return result;
    }

    private void HookSdpFiltering()
    {
        if (_sipTransport == null) return;

        _sipTransport.SIPRequestOutTraceEvent += (local, remote, req) =>
        {
            if (req.Body != null && req.Body.Contains("m=audio"))
            {
                req.Body = FilterSdpCodecs(req.Body);
            }
        };

        _sipTransport.SIPResponseOutTraceEvent += (local, remote, resp) =>
        {
            if (resp.Body != null && resp.Body.Contains("m=audio"))
            {
                resp.Body = FilterSdpCodecs(resp.Body);
            }
        };
    }

    public async Task MakeCallAsync(string number)
    {
        if (_sipTransport == null)
        {
            _sipLogger.LogError($"MakeCall failed: _sipTransport is null (registered={IsRegistered})");
            ErrorOccurred?.Invoke("Транспорт не запущен");
            return;
        }
        if (!IsRegistered)
        {
            _sipLogger.LogError("MakeCall failed: not registered");
            ErrorOccurred?.Invoke("Не зарегистрирован");
            return;
        }
        if (IsInCall)
        {
            ErrorOccurred?.Invoke("Уже в звонке");
            return;
        }

        try
        {
            CurrentCallId = Guid.NewGuid().ToString("N");
            _hangupInProgress = false;
            _callEndedFired = false;
            _outgoingCallAnswered = false;
            _remoteContactUri = null;

            _rtpSession = CreateMediaSession();

            // Generate SDP offer
            var sdpOffer = _rtpSession.CreateOffer(null);
            if (sdpOffer == null)
            {
                _sipLogger.LogError("Failed to create SDP offer");
                ErrorOccurred?.Invoke("Не удалось создать SDP offer");
                CleanupCall();
                return;
            }
            var sdpBody = sdpOffer.ToString();
            _sipLogger.LogEvent($"SDP offer:\n{sdpBody}");

            var domain = string.IsNullOrEmpty(_config.Domain) ? _config.Server : _config.Domain;
            var callUri = SIPURI.ParseSIPURI($"sip:{number}@{domain}");
            var fromUri = SIPURI.ParseSIPURI($"sip:{_config.Username}@{domain}");

            // Build INVITE request manually
            var invite = SIPRequest.GetRequest(SIPMethodsEnum.INVITE, callUri);

            // Set From with display name and tag
            invite.Header.From = new SIPFromHeader(
                string.IsNullOrEmpty(_config.DisplayName) ? _config.Username : _config.DisplayName,
                fromUri,
                CallProperties.CreateNewCallId());
            invite.Header.CSeqMethod = SIPMethodsEnum.INVITE;
            invite.Header.CSeq = 1;

            // Set To
            invite.Header.To = new SIPToHeader(null, callUri, null);

            // Set Contact header
            var localPort = ((SIPUDPChannel)_sipTransport.GetSIPChannels().First()).Port;
            var contactUri = SIPURI.ParseSIPURI($"sip:{_config.Username}@{localPort}");
            invite.Header.Contact = new List<SIPContactHeader>
            {
                new SIPContactHeader(
                    string.IsNullOrEmpty(_config.DisplayName) ? _config.Username : _config.DisplayName,
                    contactUri)
            };

            // Set User-Agent
            invite.Header.UserAgent = "SipClient/" + SipVersion.String;
            invite.Header.ContentType = SDP.SDP_MIME_CONTENTTYPE;
            invite.Header.ContentLength = sdpBody.Length;
            invite.Body = sdpBody;

            // Store state for response handling
            _outgoingCallId = invite.Header.CallId;
            _outgoingFromHeader = invite.Header.From;
            _outgoingToHeader = invite.Header.To;
            _outgoingCSeq = 1;
            _outgoingInviteRequest = invite;

            // Resolve server endpoint
            var addresses = await Dns.GetHostEntryAsync(_config.Server);
            if (addresses.AddressList.Length == 0)
            {
                _sipLogger.LogError($"DNS resolution failed for {_config.Server}");
                ErrorOccurred?.Invoke($"Не удалось разрешить {_config.Server}");
                CleanupCall();
                return;
            }
            _outgoingRemoteEndPoint = new SIPEndPoint(new IPEndPoint(addresses.AddressList[0], _config.Port));

            _manualCallInProgress = true;
            _sipLogger.LogEvent($"Sending INVITE to {number} via {_outgoingRemoteEndPoint}");
            CallStateChanged?.Invoke(CurrentCallId!, 2, "Trying");

            await _sipTransport.SendRequestAsync(_outgoingRemoteEndPoint, invite);
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Make call failed: {ex}\n");
            ErrorOccurred?.Invoke(ex.Message);
            CleanupCall();
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

    private void OnCallAnswered(ISIPClientUserAgent uac, SIPResponse sipResponse)
    {
        _sipLogger.LogEvent($"Call answered: {sipResponse.StatusCode} {sipResponse.ReasonPhrase}");
        _activeDialogue = _userAgent?.Dialogue;
        CallStateChanged?.Invoke(CurrentCallId!, 5, "OK");
    }

    private void OnCallFailed(ISIPClientUserAgent uac, string errorMessage, SIPResponse failureResponse)
    {
        var reason = failureResponse?.ReasonPhrase ?? errorMessage;
        _sipLogger.LogError($"Call failed: {reason}");
        CallFailedWithReason?.Invoke(CurrentCallId ?? "", reason);
        CleanupCall();
    }

    private void OnCallHungup(SIPDialogue? dialogue)
    {
        _sipLogger.LogEvent("Call hungup");
        if (_callEndedFired) return;
        _callEndedFired = true;
        CleanupCall();
        CallEnded?.Invoke();
    }

    public void AnswerCall()
    {
        if (_userAgent == null || _pendingIncomingCall == null) return;

        _sipLogger.LogEvent("Answering call");

        try
        {
            _callEndedFired = false;
            _hangupInProgress = false;
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
        if (_hangupInProgress) return;
        _hangupInProgress = true;

        _sipLogger.LogEvent("Hangup");

        if (_manualCallInProgress)
        {
            SendByeForManualCall();
            CleanupManualCall();
            if (!_callEndedFired)
            {
                _callEndedFired = true;
                CallEnded?.Invoke();
            }
            return;
        }

        if (_userAgent != null)
        {
            if (_userAgent.IsCallActive)
                _userAgent.Hangup();
            else
                _userAgent.Cancel();
        }
        else if (_activeDialogue != null && _sipTransport != null)
        {
            // Agent is gone but dialogue still exists — send BYE manually
            _sipLogger.LogEvent("Sending BYE via stored dialogue");
            var byeRequest = _activeDialogue.GetInDialogRequest(SIPMethodsEnum.BYE);
            var byeTransaction = new SIPNonInviteTransaction(_sipTransport, byeRequest, _activeDialogue.RemoteSIPEndPoint);
            byeTransaction.SendRequest();
        }

        _pendingIncomingCall?.Reject(SIPResponseStatusCodesEnum.BusyHere, null, null);

        if (!_callEndedFired)
        {
            _callEndedFired = true;
            CleanupCall();
            CallEnded?.Invoke();
        }
        else
        {
            CleanupCall();
        }
    }

    private void SendByeForManualCall()
    {
        if (_sipTransport == null || _outgoingRemoteEndPoint == null || _outgoingCallId == null) return;

        try
        {
            // Determine remote endpoint for BYE
            var byeRemoteEndPoint = _outgoingRemoteEndPoint;
            SIPURI? byeRequestUri = null;

            if (!string.IsNullOrEmpty(_remoteContactUri))
                byeRequestUri = SIPURI.ParseSIPURIRelaxed(_remoteContactUri);

            // Build BYE request
            SIPRequest byeRequest;
            if (byeRequestUri != null)
            {
                byeRequest = SIPRequest.GetRequest(SIPMethodsEnum.BYE, byeRequestUri);
            }
            else
            {
                byeRequest = SIPRequest.GetRequest(SIPMethodsEnum.BYE, SIPURI.ParseSIPURIRelaxed($"sip:{_config.Server}:{_config.Port}"));
            }

            // Copy dialog headers (swap From/To for BYE — it's from our side)
            byeRequest.Header.CallId = _outgoingCallId;
            byeRequest.Header.From = _outgoingFromHeader;
            byeRequest.Header.To = _outgoingToHeader;
            byeRequest.Header.CSeqMethod = SIPMethodsEnum.BYE;
            byeRequest.Header.CSeq = _outgoingCSeq + 2; // increment past INVITE and ACK

            var localPort = ((SIPUDPChannel)_sipTransport.GetSIPChannels().First()).Port;
            var contactUri = SIPURI.ParseSIPURI($"sip:{_config.Username}@{localPort}");
            byeRequest.Header.Contact = new List<SIPContactHeader>
            {
                new SIPContactHeader(
                    string.IsNullOrEmpty(_config.DisplayName) ? _config.Username : _config.DisplayName,
                    contactUri)
            };
            byeRequest.Header.UserAgent = "SipClient/" + SipVersion.String;
            byeRequest.Header.MaxForwards = 70;

            _sipLogger.LogEvent($"Sending BYE to {byeRemoteEndPoint}");
            _ = _sipTransport.SendRequestAsync(byeRemoteEndPoint, byeRequest);
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Failed to send BYE: {ex.Message}");
        }
    }

    private void CleanupManualCall()
    {
        _manualCallInProgress = false;
        _outgoingCallId = null;
        _outgoingFromHeader = null;
        _outgoingToHeader = null;
        _outgoingRemoteEndPoint = null;
        _remoteContactUri = null;
        _outgoingCallAnswered = false;

        try
        {
            _rtpSession?.Close("manual call cleanup");
        }
        catch { }
        _rtpSession = null;
        CurrentCallId = null;
        _hangupInProgress = false;
    }

    private Task OnSipResponseReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPResponse sipResponse)
    {
        _sipLogger.LogEvent($"Response from {remoteEndPoint}: {sipResponse.StatusCode} {sipResponse.ReasonPhrase}");

        // Handle responses to our manual outgoing INVITE
        if (_manualCallInProgress && !string.IsNullOrEmpty(_outgoingCallId)
            && sipResponse.Header.CallId == _outgoingCallId)
        {
            HandleManualCallResponse(sipResponse, remoteEndPoint);
        }

        return Task.CompletedTask;
    }

    private void HandleManualCallResponse(SIPResponse sipResponse, SIPEndPoint remoteEndPoint)
    {
        var statusCode = sipResponse.StatusCode;

        switch (statusCode)
        {
            case 100: // Trying
                _sipLogger.LogEvent("Call trying (100)");
                CallStateChanged?.Invoke(CurrentCallId!, 1, "Trying");
                break;

            case 180: // Ringing
                _sipLogger.LogEvent("Call ringing (180)");
                CallStateChanged?.Invoke(CurrentCallId!, 3, "Ringing");
                break;

            case 183: // Session Progress with early media — handle as ringing, NOT cancel
                _sipLogger.LogEvent("Session progress (183) — treating as ringing");
                CallStateChanged?.Invoke(CurrentCallId!, 3, "Ringing");
                break;

            case 401: // Unauthorized — retry with digest auth
                _sipLogger.LogEvent("Call unauthorized (401) — retrying with digest auth");
                SendAckForResponse(sipResponse, remoteEndPoint);
                ResendInviteWithAuth(sipResponse, remoteEndPoint);
                break;

            case >= 200 and < 300: // 200 OK — call answered
                _sipLogger.LogEvent($"Call answered ({statusCode})");
                HandleManualCallAnswered(sipResponse, remoteEndPoint);
                break;

            case >= 300: // Error/failure
                _sipLogger.LogEvent($"Call failed ({statusCode} {sipResponse.ReasonPhrase})");
                SendAckForResponse(sipResponse, remoteEndPoint);
                CallFailedWithReason?.Invoke(CurrentCallId ?? "", sipResponse.ReasonPhrase ?? $"Error {statusCode}");
                CleanupManualCall();
                break;
        }
    }

    private void HandleManualCallAnswered(SIPResponse sipResponse, SIPEndPoint remoteEndPoint)
    {
        if (_outgoingCallAnswered) return; // prevent double-ACK
        _outgoingCallAnswered = true;

        try
        {
            // Store remote Contact for BYE
            if (sipResponse.Header.Contact?.Count > 0)
                _remoteContactUri = sipResponse.Header.Contact[0].ContactURI.ToString();

            // Send ACK for 200 OK (separate transaction per RFC 3261)
            SendAckForResponse(sipResponse, remoteEndPoint);

            // Parse and apply remote SDP answer
            if (!string.IsNullOrEmpty(sipResponse.Body) && _rtpSession != null)
            {
                var remoteSdp = SDP.ParseSDPDescription(sipResponse.Body);
                _ = _rtpSession.SetRemoteDescription(SdpType.answer, remoteSdp);
                _ = _rtpSession.Start();
                _sipLogger.LogEvent("Media session started");
            }

            CallStateChanged?.Invoke(CurrentCallId!, 5, "OK");
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Error handling 200 OK: {ex.Message}");
            CallFailedWithReason?.Invoke(CurrentCallId ?? "", ex.Message);
            CleanupManualCall();
        }
    }

    private void SendAckForResponse(SIPResponse sipResponse, SIPEndPoint remoteEndPoint)
    {
        if (_sipTransport == null || remoteEndPoint == null) return;

        try
        {
            // Build ACK for 2xx response (new transaction per RFC 3261 §13.2.2.4)
            var requestUri = sipResponse.Header.To?.ToURI;
            if (requestUri == null) return;

            var ack = SIPRequest.GetRequest(SIPMethodsEnum.ACK, requestUri);
            ack.Header.CallId = sipResponse.Header.CallId;
            ack.Header.From = _outgoingFromHeader;
            ack.Header.To = sipResponse.Header.To;
            ack.Header.CSeqMethod = SIPMethodsEnum.ACK;
            ack.Header.CSeq = _outgoingCSeq + 1; // ACK CSeq is separate from INVITE

            var localPort = ((SIPUDPChannel)_sipTransport.GetSIPChannels().First()).Port;
            var contactUri = SIPURI.ParseSIPURI($"sip:{_config.Username}@{localPort}");
            ack.Header.Contact = new List<SIPContactHeader>
            {
                new SIPContactHeader(
                    string.IsNullOrEmpty(_config.DisplayName) ? _config.Username : _config.DisplayName,
                    contactUri)
            };
            ack.Header.UserAgent = "SipClient/" + SipVersion.String;
            ack.Header.MaxForwards = 70;

            _sipLogger.LogEvent($"Sending ACK to {remoteEndPoint}");
            _ = _sipTransport.SendRequestAsync(remoteEndPoint, ack);
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Failed to send ACK: {ex.Message}");
        }
    }

    private void ResendInviteWithAuth(SIPResponse sipResponse, SIPEndPoint remoteEndPoint)
    {
        if (_sipTransport == null || _outgoingInviteRequest == null || remoteEndPoint == null) return;

        try
        {
            // Parse WWW-Authenticate header
            var wwwAuth = sipResponse.Header.AuthenticationHeaders?.FirstOrDefault();
            if (wwwAuth == null)
            {
                _sipLogger.LogError("No WWW-Authenticate header in 401 response");
                CallFailedWithReason?.Invoke(CurrentCallId ?? "", "No authentication challenge");
                CleanupManualCall();
                return;
            }

            // Copy original INVITE
            var authInvite = _outgoingInviteRequest.Copy();
            _outgoingCSeq++;
            authInvite.Header.CSeq = _outgoingCSeq;

            // Compute digest auth
            var authHeaderStr = ComputeDigestAuth(authInvite, wwwAuth);
            authInvite.Header.UnknownHeaders ??= new List<string>();
            authInvite.Header.UnknownHeaders.Add($"Authorization: {authHeaderStr}");

            _sipLogger.LogEvent($"Resending INVITE with Authorization to {remoteEndPoint}");
            _ = _sipTransport.SendRequestAsync(remoteEndPoint, authInvite);
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"Failed to resend INVITE with auth: {ex.Message}");
            CallFailedWithReason?.Invoke(CurrentCallId ?? "", ex.Message);
            CleanupManualCall();
        }
    }

    private string ComputeDigestAuth(SIPRequest request, SIPAuthenticationHeader challenge)
    {
        var username = string.IsNullOrEmpty(_config.AuthUsername) ? _config.Username : _config.AuthUsername;
        var uri = request.URI.ToString();
        var method = request.Method.ToString();
        var realm = challenge.SIPDigest.Realm;
        var nonce = challenge.SIPDigest.Nonce;
        var qop = "auth";

        // HA1 = MD5(username:realm:password)
        var ha1 = Md5Hex($"{username}:{realm}:{_config.Password}");
        // HA2 = MD5(method:uri)
        var ha2 = Md5Hex($"{method}:{uri}");
        var nc = "00000001";
        var cnonce = Guid.NewGuid().ToString("N").Substring(0, 8);
        // response = MD5(HA1:nonce:nc:cnonce:qop:HA2)
        var response = Md5Hex($"{ha1}:{nonce}:{nc}:{cnonce}:{qop}:{ha2}");

        var header = $"Digest username=\"{username}\",realm=\"{realm}\",nonce=\"{nonce}\",uri=\"{uri}\",response=\"{response}\",algorithm=MD5,cnonce=\"{cnonce}\",nc={nc},qop={qop}";
        _sipLogger.LogEvent($"Computed digest: user={username}, realm={realm}, response={response.Substring(0, 8)}...");
        return header;
    }

    private static string Md5Hex(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = System.Security.Cryptography.MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private Task OnSipRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
    {
        if (sipRequest.Method == SIPMethodsEnum.INVITE)
        {
            var replacesHeader = sipRequest.Header.Replaces;
            var fromName = sipRequest.Header.From?.FromName;
            var fromUser = sipRequest.Header.From?.FromURI?.User;
            var remoteUri = !string.IsNullOrEmpty(fromUser) && fromUser != "anonymous"
                ? fromUser
                : fromName ?? remoteEndPoint.ToString();
            var callId = sipRequest.Header.CallId ?? Guid.NewGuid().ToString("N");

            // Replaces = transfer: drop old call, accept new one
            if (replacesHeader != null)
            {
                _sipLogger.LogEvent($"INVITE with Replaces — transfer from {remoteUri}");

                // Close old media session first
                try { _rtpSession?.Close("replaces"); } catch { }
                _rtpSession = null;

                // Accept the new call
                if (_userAgent == null)
                    _userAgent = new SIPUserAgent(_sipTransport!, null);

                _pendingIncomingCall = _userAgent.AcceptCall(sipRequest);
                CurrentCallId = callId;

                _sipLogger.LogEvent($"Transfer call accepted: {remoteUri}");
                IncomingCall?.Invoke(callId, remoteUri);
                return Task.CompletedTask;
            }

            if (IsInCall)
            {
                var busyResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.BusyHere, null);
                _ = _sipTransport!.SendResponseAsync(busyResponse);
                return Task.CompletedTask;
            }

            _sipLogger.LogEvent($"Incoming call from {remoteUri}");

            CleanupCall();

            if (_userAgent == null)
                _userAgent = new SIPUserAgent(_sipTransport!, null);

            _pendingIncomingCall = _userAgent.AcceptCall(sipRequest);
            CurrentCallId = callId;

            _sipLogger.LogEvent($"Incoming call accepted for answering: {remoteUri}");
            IncomingCall?.Invoke(callId, remoteUri);
        }
        else if (sipRequest.Method == SIPMethodsEnum.BYE)
        {
            _sipLogger.LogEvent("BYE received from remote");

            if (_manualCallInProgress)
            {
                // Manual call — BYE from remote, send 200 OK then clean up
                _sipLogger.LogEvent("Remote BYE for manual call — sending 200 OK");
                var byeOkResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                _ = _sipTransport!.SendResponseAsync(byeOkResponse);

                if (!_callEndedFired)
                {
                    _callEndedFired = true;
                    CleanupManualCall();
                    CallEnded?.Invoke();
                }
                return Task.CompletedTask;
            }

            // SIPUserAgent handles BYE internally (sends 200 OK) but OnCallHungup
            // doesn't always fire for outgoing calls. Poll IsCallActive to clean up UI.
            _ = Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(200);
                    if (_callEndedFired) return;
                    var agent = _userAgent;
                    if (agent == null || !agent.IsCallActive)
                    {
                        _sipLogger.LogEvent("Remote BYE processed — cleaning up UI");
                        if (!_callEndedFired)
                        {
                            _callEndedFired = true;
                            CleanupCall();
                            CallEnded?.Invoke();
                        }
                        return;
                    }
                }
                if (!_callEndedFired)
                {
                    _sipLogger.LogEvent("Remote BYE: agent still active after 2s, forcing cleanup");
                    _callEndedFired = true;
                    CleanupCall();
                    CallEnded?.Invoke();
                }
            });
        }
        else if (sipRequest.Method == SIPMethodsEnum.CANCEL)
        {
            _sipLogger.LogEvent("CANCEL received from remote");

            // Send 200 OK for the CANCEL itself
            var cancelOk = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
            _ = _sipTransport!.SendResponseAsync(cancelOk);

            // Send 487 Request Terminated for the original INVITE
            var inviteTerminated = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.RequestTerminated, null);
            _ = _sipTransport!.SendResponseAsync(inviteTerminated);

            // Reject pending incoming call if any
            if (_pendingIncomingCall != null)
            {
                _pendingIncomingCall.Reject(SIPResponseStatusCodesEnum.RequestTerminated, null, null);
            }

            if (!_callEndedFired)
            {
                _callEndedFired = true;
                CleanupCall();
                CallEnded?.Invoke();
            }
            else
            {
                CleanupCall();
            }
        }
        else if (sipRequest.Method == SIPMethodsEnum.OPTIONS || sipRequest.Method == SIPMethodsEnum.REGISTER || sipRequest.Method == SIPMethodsEnum.NOTIFY)
        {
            _sipLogger.LogEvent($"{sipRequest.Method} received — sending 200 OK");
            var okResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
            _ = _sipTransport!.SendResponseAsync(okResponse);
        }

        return Task.CompletedTask;
    }

    private void CleanupCall()
    {
        try
        {
            _rtpSession?.Close("cleanup");
        }
        catch { }
        _rtpSession = null;
        _userAgent = null;
        _pendingIncomingCall = null;
        _hangupInProgress = false;
        _activeDialogue = null;
        CurrentCallId = null;
    }

    public string GetLogPath() => _sipLogger.GetLogPath();

    public SipLogger GetSipLogger() => _sipLogger;
}

public class AudioDeviceInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}
