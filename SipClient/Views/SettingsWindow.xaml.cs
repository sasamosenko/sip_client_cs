using System.Windows;
using System.Windows.Controls;
using SipClient.Models;

namespace SipClient.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
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

    private void CodecMoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm && sender is Button btn && btn.Tag is CodecOption codec)
        {
            var idx = vm.CodecOptions.IndexOf(codec);
            if (idx > 0)
                vm.CodecOptions.Move(idx, idx - 1);
        }
    }

    private void CodecMoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm && sender is Button btn && btn.Tag is CodecOption codec)
        {
            var idx = vm.CodecOptions.IndexOf(codec);
            if (idx >= 0 && idx < vm.CodecOptions.Count - 1)
                vm.CodecOptions.Move(idx, idx + 1);
        }
    }

    private void PasswordToggle_Click(object sender, RoutedEventArgs e)
    {
        if (PasswordToggle.IsChecked == true)
        {
            var pwd = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;

            var tb = new TextBox
            {
                Text = pwd,
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 14,
                Background = (System.Windows.Media.Brush)FindResource("SurfaceCardBrush"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x58, 0x58, 0x78)),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = "PasswordBoxReplacement"
            };
            tb.TextChanged += (s, args) =>
            {
                if (DataContext is ViewModels.MainViewModel vm2)
                    vm2.Password = tb.Text;
            };

            var grid = (Grid)PasswordBox.Parent;
            grid.Children.Add(tb);
            Grid.SetColumn(tb, 0);
        }
        else
        {
            var grid = (Grid)PasswordBox.Parent;
            var tb = grid.Children
                .OfType<Control>()
                .FirstOrDefault(c => c.Tag?.ToString() == "PasswordBoxReplacement") as TextBox;

            if (tb != null)
            {
                PasswordBox.Password = tb.Text;
                grid.Children.Remove(tb);
            }

            PasswordBox.Visibility = Visibility.Visible;
        }
    }
}
