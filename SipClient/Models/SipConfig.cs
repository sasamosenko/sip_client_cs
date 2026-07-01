namespace SipClient.Models;

public class SipConfig
{
    public string Server { get; set; } = "";
    public int Port { get; set; } = 5060;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int CaptureDeviceId { get; set; } = -1;
    public int PlaybackDeviceId { get; set; } = -1;
    public int MicVolume { get; set; } = 80;
    public int SpeakerVolume { get; set; } = 80;
    public bool AutoAnswerEnabled { get; set; } = false;
    public int AutoAnswerDelaySeconds { get; set; } = 3;
}
