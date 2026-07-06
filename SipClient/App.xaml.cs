using System.Windows;

namespace SipClient;

public partial class App : Application
{
    public static string? AutoCallNumber { get; private set; }
    public static string? TestServer { get; private set; }
    public static string? TestUsername { get; private set; }
    public static string? TestPassword { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        foreach (var arg in e.Args)
        {
            if (arg.StartsWith("--call=", StringComparison.OrdinalIgnoreCase))
                AutoCallNumber = arg.Substring("--call=".Length);
            else if (arg.StartsWith("--server=", StringComparison.OrdinalIgnoreCase))
                TestServer = arg.Substring("--server=".Length);
            else if (arg.StartsWith("--username=", StringComparison.OrdinalIgnoreCase))
                TestUsername = arg.Substring("--username=".Length);
            else if (arg.StartsWith("--password=", StringComparison.OrdinalIgnoreCase))
                TestPassword = arg.Substring("--password=".Length);
        }
    }
}
