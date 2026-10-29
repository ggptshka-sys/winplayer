using System.Windows;
using WinPlayer.Services;

namespace WinPlayer.Windows;

public partial class SettingsWindow : Window
{
    private bool _syncing;
    public event EventHandler? HideToTrayRequested;

    public SettingsWindow()
    {
        InitializeComponent();
        _syncing = true;
        AutostartCheck.IsChecked = AutostartService.IsEnabled();
        _syncing = false;
        VersionText.Text = $"Версия {UpdateService.GetCurrentVersionDisplay()}";
    }

    private void Autostart_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        AutostartService.SetEnabled(AutostartCheck.IsChecked == true);
    }

    private void HideToTray_Click(object sender, RoutedEventArgs e)
    {
        HideToTrayRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
