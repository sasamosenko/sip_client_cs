using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SipClient.Models;

namespace SipClient.Services;

/// <summary>
/// Blind transfer module —独立 от основного SipService.
/// Использует SIP REFER для перевода вызова.
/// НЕ изменяет существующий функционал.
/// </summary>
public class TransferService
{
    private readonly SipLogger _sipLogger;
    private SIPTransport? _sipTransport;
    private SipConfig _config;

    public event Action<string>? ErrorOccurred;
    public event Action<string>? LogEvent;

    public TransferService(SipLogger sipLogger)
    {
        _sipLogger = sipLogger;
    }

    public void Init(SIPTransport transport, SipConfig config)
    {
        _sipTransport = transport;
        _config = config;
    }

    /// <summary>
    /// Выполнить blind transfer. Возвращает true еслиREFER отправлен.
    /// </summary>
    public async Task<bool> BlindTransferAsync(string destination)
    {
        if (_sipTransport == null)
        {
            ErrorOccurred?.Invoke("Трансфер: транспорт не инициализирован");
            return false;
        }

        try
        {
            var referTarget = $"sip:{destination}@{_config.Server}";
            var referRequest = SIPRequest.GetRequest(SIPMethodsEnum.REFER, SIPURI.ParseSIPURIRelaxed(referTarget));

            var localPort = ((SIPUDPChannel)_sipTransport.GetSIPChannels().First()).Port;
            referRequest.Header.ReferTo = $"<{referTarget}>";
            referRequest.Header.ReferredBy = $"<sip:{_config.Username}@{localPort}>";

            var contactUri = SIPURI.ParseSIPURI($"sip:{_config.Username}@{localPort}");
            referRequest.Header.Contact = new List<SIPContactHeader>
            {
                new SIPContactHeader(
                    string.IsNullOrEmpty(_config.DisplayName) ? _config.Username : _config.DisplayName,
                    contactUri)
            };
            referRequest.Header.UserAgent = "SipClient/" + SipVersion.String;
            referRequest.Header.MaxForwards = 70;

            _sipLogger.LogEvent($"[Transfer] REFER → {referTarget}");
            return true;
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"[Transfer] Ошибка: {ex.Message}");
            ErrorOccurred?.Invoke($"Ошибка трансфера: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Отправить REFER с полными in-dialog заголовками.
    /// Вызывается из SipService при наличии информации о диалоге.
    /// </summary>
    public async Task<bool> SendReferInDialog(
        string destination,
        SIPEndPoint remoteEndPoint,
        string callId,
        SIPFromHeader fromHeader,
        SIPToHeader toHeader,
        int currentCSeq)
    {
        if (_sipTransport == null) return false;

        try
        {
            var referTarget = $"sip:{destination}@{_config.Server}";
            var referRequest = SIPRequest.GetRequest(SIPMethodsEnum.REFER, SIPURI.ParseSIPURIRelaxed(referTarget));

            referRequest.Header.CallId = callId;
            referRequest.Header.From = fromHeader;
            referRequest.Header.To = toHeader;
            referRequest.Header.CSeqMethod = SIPMethodsEnum.REFER;
            referRequest.Header.CSeq = currentCSeq + 2;
            referRequest.Header.ReferTo = $"<{referTarget}>";

            var localPort = ((SIPUDPChannel)_sipTransport.GetSIPChannels().First()).Port;
            referRequest.Header.ReferredBy = $"<sip:{_config.Username}@{localPort}>";

            var contactUri = SIPURI.ParseSIPURI($"sip:{_config.Username}@{localPort}");
            referRequest.Header.Contact = new List<SIPContactHeader>
            {
                new SIPContactHeader(
                    string.IsNullOrEmpty(_config.DisplayName) ? _config.Username : _config.DisplayName,
                    contactUri)
            };
            referRequest.Header.UserAgent = "SipClient/" + SipVersion.String;
            referRequest.Header.MaxForwards = 70;

            _sipLogger.LogEvent($"[Transfer] REFER → {remoteEndPoint} ({referTarget})");
            _ = _sipTransport.SendRequestAsync(remoteEndPoint, referRequest);

            await Task.Delay(1000);
            return true;
        }
        catch (Exception ex)
        {
            _sipLogger.LogError($"[Transfer] Ошибка: {ex.Message}");
            ErrorOccurred?.Invoke($"Ошибка трансфера: {ex.Message}");
            return false;
        }
    }
}
