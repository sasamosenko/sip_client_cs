namespace SipClient.Models;

public class SipConfig
{
    public string Server { get; set; } = "";
    public int Port { get; set; } = 5060;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "SIP Client";
    public string Domain { get; set; } = "";
    public string AuthUsername { get; set; } = "";
    public string UserAgent { get; set; } = "SipClient/1.0";
    public int CaptureDeviceId { get; set; } = -1;
    public int PlaybackDeviceId { get; set; } = -1;
    public double MicVolume { get; set; } = 100;
    public double SpeakerVolume { get; set; } = 100;
    public bool AutoAnswerEnabled { get; set; } = false;
    public int AutoAnswerDelaySeconds { get; set; } = 3;
    public int RtpPortMin { get; set; } = 10000;
    public int RtpPortMax { get; set; } = 20000;
    public int RegistrationExpiry { get; set; } = 600;
    public double RingVolume { get; set; } = 80;
    public bool SipLoggingEnabled { get; set; } = true;
    public int LocalPort { get; set; } = 5080;
    public List<string> EnabledCodecs { get; set; } = new() { "G722", "PCMU", "PCMA", "G729" };
}
