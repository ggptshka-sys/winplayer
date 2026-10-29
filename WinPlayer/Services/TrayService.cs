using System.Drawing;
using System.Windows.Forms;

namespace WinPlayer.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autostartItem;
    private bool _disposed;
    private bool _syncingAutostart;
    private UpdateManifest? _pendingUpdate;

    public event EventHandler? ShowRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? AutostartChanged;
    public event EventHandler? CheckUpdatesRequested;

    public TrayService()
    {
        _autostartItem = new ToolStripMenuItem("Автозапуск")
        {
            Checked = AutostartService.IsEnabled(),
            CheckOnClick = true
        };
        _autostartItem.CheckedChanged += (_, _) =>
        {
            if (_syncingAutostart) return;
            AutostartService.SetEnabled(_autostartItem.Checked);
            AutostartChanged?.Invoke(this, EventArgs.Empty);
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Показать", null, (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(_autostartItem);
        menu.Items.Add("Проверить обновления", null, (_, _) => CheckUpdatesRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateAppIcon(),
            Text = "WinPlayer",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.BalloonTipClicked += (_, _) =>
        {
            if (_pendingUpdate is not null)
            {
                UpdateService.OpenDownload(_pendingUpdate);
            }
        };
    }

    public void RefreshAutostartState()
    {
        _syncingAutostart = true;
        try
        {
            _autostartItem.Checked = AutostartService.IsEnabled();
        }
        finally
        {
            _syncingAutostart = false;
        }
    }

    public void NotifyUpdateAvailable(UpdateManifest manifest, Version remoteVersion)
    {
        _pendingUpdate = manifest;
        var notes = string.IsNullOrWhiteSpace(manifest.Notes) ? "Нажмите, чтобы скачать." : manifest.Notes!;
        _notifyIcon.BalloonTipTitle = $"WinPlayer {remoteVersion.Major}.{remoteVersion.Minor}.{remoteVersion.Build}";
        _notifyIcon.BalloonTipText = $"Доступно обновление.\n{notes}";
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(8000);
    }

    public void NotifyNoUpdate(string currentDisplay)
    {
        _pendingUpdate = null;
        _notifyIcon.BalloonTipTitle = "WinPlayer";
        _notifyIcon.BalloonTipText = $"У вас актуальная версия ({currentDisplay}).";
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(4000);
    }

    public void NotifyUpdateError(string message)
    {
        _pendingUpdate = null;
        _notifyIcon.BalloonTipTitle = "WinPlayer";
        _notifyIcon.BalloonTipText = $"Не удалось проверить обновления.\n{message}";
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
        _notifyIcon.ShowBalloonTip(5000);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static Icon CreateAppIcon()
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var bg = new SolidBrush(Color.FromArgb(32, 32, 32));
            g.FillEllipse(bg, 1, 1, 30, 30);

            using var ring = new Pen(Color.FromArgb(180, 255, 255, 255), 1.5f);
            g.DrawEllipse(ring, 2, 2, 28, 28);

            var points = new[]
            {
                new Point(12, 9),
                new Point(12, 23),
                new Point(23, 16)
            };
            g.FillPolygon(Brushes.White, points);
        }

        var hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        bmp.Dispose();
        return icon;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
