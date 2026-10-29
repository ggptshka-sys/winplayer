using System.Windows;
using WinPlayer.Services;

namespace WinPlayer.Windows;

public partial class UpdateWindow : Window
{
    private readonly UpdateManifest _manifest;

    public UpdateWindow(UpdateManifest manifest, Version remoteVersion)
    {
        InitializeComponent();
        _manifest = manifest;
        TitleText.Text = $"Доступна версия {remoteVersion.Major}.{remoteVersion.Minor}.{remoteVersion.Build}";
        NotesText.Text = string.IsNullOrWhiteSpace(manifest.Notes)
            ? "Можно скачать обновление с GitHub Releases."
            : manifest.Notes!;
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        UpdateService.OpenDownload(_manifest);
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();
}
