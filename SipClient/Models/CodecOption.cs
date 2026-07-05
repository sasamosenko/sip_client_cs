using CommunityToolkit.Mvvm.ComponentModel;

namespace SipClient.Models;

public partial class CodecOption : ObservableObject
{
    [ObservableProperty] private bool _isEnabled;

    public string Name { get; }
    public string Description { get; }
    public int PayloadType { get; }

    public CodecOption(string name, string description, int payloadType, bool isEnabled)
    {
        Name = name;
        Description = description;
        PayloadType = payloadType;
        IsEnabled = isEnabled;
    }
}
