using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SipClient.Models;

namespace SipClient.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadIcon();
    }

    private void LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/SipClient;component/Resources/phone.ico", UriKind.Absolute);
            Icon = new BitmapImage(uri);
        }
        catch
        {
            try
            {
                var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "phone.ico");
                if (System.IO.File.Exists(path))
                    Icon = new BitmapImage(new Uri(path, UriKind.Absolute));
            }
            catch { }
        }
    }

    private void PhoneNumber_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ViewModels.MainViewModel vm)
        {
            vm.CallCommand.Execute(null);
        }
    }

    private void CopyCallRecord_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is CallRecord record)
            record.CopyToClipboard();
    }

    private void Redial_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string number
            && DataContext is ViewModels.MainViewModel vm)
        {
            vm.RedialCommand.Execute(number);
        }
    }

    private void CallHistory_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
            && sender is ListBox lb && lb.SelectedItem is CallRecord record)
        {
            record.CopyToClipboard();
            e.Handled = true;
        }
    }
}
