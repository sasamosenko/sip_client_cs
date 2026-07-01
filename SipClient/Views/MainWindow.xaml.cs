using System.Windows;
using System.Windows.Controls;

namespace SipClient.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.Password = PasswordBox.Password;
        }
    }
}
