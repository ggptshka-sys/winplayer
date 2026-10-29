using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinPlayer.Services;

namespace WinPlayer;

public partial class MainWindow : Window
{
    // 8 top + 38 button + 16 bottom = 62; +6 so round-region / stroke don't clip circles
    private const double CollapsedHeight = 68;
    private static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(480);
    private static readonly Duration ExpandDuration = new(TimeSpan.FromMilliseconds(520));
    private static readonly Duration CollapseDuration = new(TimeSpan.FromMilliseconds(440));

    private readonly MediaSessionService _media = new();
    private readonly SystemMuteService _mute = new();
    private readonly TrayService _tray = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly DispatcherTimer _timelineTimer;
    private readonly DispatcherTimer _collapseTimer;

    private bool _closing;
    private bool _forceExit;
    private bool _expanded;
    private bool _seeking;
    private bool _menuOpen;
    private bool _canSeek;
    private TimeSpan _duration;

    public MainWindow()
    {
        InitializeComponent();

        _timelineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timelineTimer.Tick += async (_, _) => await RefreshUiAsync(timelineOnly: true);

        _collapseTimer = new DispatcherTimer { Interval = CollapseDelay };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (!IsMouseOver && !_seeking && !_menuOpen)
            {
                SetExpanded(false);
            }
        };

        _tray.ShowRequested += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _tray.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApp);
        _tray.AutostartChanged += (_, _) => Dispatcher.Invoke(RefreshAutostartMenuHeader);
        _tray.CheckUpdatesRequested += (_, _) => Dispatcher.InvokeAsync(() => CheckForUpdatesAsync(userInitiated: true));

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        SourceInitialized += (_, _) => PinToTopCenter();
    }

    private void PinToTopCenter()
    {
        // Flush to the physical top of the primary work area (brow / notch).
        var work = SystemParameters.WorkArea;
        Left = work.Left + (work.Width - Width) / 2;
        Top = 0;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        PinToTopCenter();
        Height = CollapsedHeight;
        RefreshAutostartMenuHeader();

        try
        {
            await _media.InitializeAsync();
            _media.SessionChanged += OnSessionChanged;
            await RefreshUiAsync();
            UpdateMuteGlyph();
            _timelineTimer.Start();
            _ = CheckForUpdatesAsync(userInitiated: false);
        }
        catch (Exception ex)
        {
            TitleText.Text = "SMTC недоступен";
            ArtistText.Text = ex.Message;
            SetExpanded(true);
        }
    }

    private async Task CheckForUpdatesAsync(bool userInitiated)
    {
        // Небольшая пауза после старта, чтобы не мешать UI.
        if (!userInitiated)
        {
            await Task.Delay(2500);
        }

        var result = await UpdateService.CheckAsync();
        if (_closing) return;

        if (result.Error is not null)
        {
            if (userInitiated)
            {
                _tray.NotifyUpdateError(result.Error);
            }
            return;
        }

        if (result.UpdateAvailable && result.Manifest is not null && result.RemoteVersion is not null)
        {
            _tray.NotifyUpdateAvailable(result.Manifest, result.RemoteVersion);
            return;
        }

        if (userInitiated)
        {
            _tray.NotifyNoUpdate(UpdateService.GetCurrentVersionDisplay());
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_forceExit) return;
        e.Cancel = true;
        HideToTray();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closing = true;
        _timelineTimer.Stop();
        _collapseTimer.Stop();
        _media.SessionChanged -= OnSessionChanged;
        _media.Dispose();
        _mute.Dispose();
        _tray.Dispose();
        _refreshLock.Dispose();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        PinToTopCenter();
    }

    private void HideToTray()
    {
        Hide();
    }

    private void ExitApp()
    {
        _forceExit = true;
        Close();
    }

    private void RefreshAutostartMenuHeader()
    {
        var on = AutostartService.IsEnabled();
        AutostartMenuItem.Header = on ? "Автозапуск  ✓" : "Автозапуск";
        _tray.RefreshAutostartState();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (_closing) return;
        Dispatcher.InvokeAsync(async () => await RefreshUiAsync());
    }

    private async Task RefreshUiAsync(bool timelineOnly = false)
    {
        if (_closing) return;
        await _refreshLock.WaitAsync();
        try
        {
            var info = await _media.GetInfoAsync();

            if (!timelineOnly)
            {
                TitleText.Text = info.Title;
                ArtistText.Text = string.IsNullOrWhiteSpace(info.Artist)
                    ? (info.HasSession ? ShortAppName(info.AppName) : "Наведи · запусти музыку")
                    : info.Artist;

                PlayPauseButton.Content = info.IsPlaying ? "\uE769" : "\uE768";
                PlayPauseButton.IsEnabled = info.HasSession && info.CanPlayPause;
                PrevButton.IsEnabled = info.HasSession && info.CanSkipPrevious;
                NextButton.IsEnabled = info.HasSession && info.CanSkipNext;
            }

            _canSeek = info.CanSeek;
            _duration = info.Duration;
            SeekSlider.IsEnabled = _canSeek;

            if (!_seeking)
            {
                if (_duration > TimeSpan.Zero)
                {
                    SeekSlider.Maximum = _duration.TotalSeconds;
                    SeekSlider.Value = Math.Clamp(info.Position.TotalSeconds, 0, SeekSlider.Maximum);
                }
                else
                {
                    SeekSlider.Maximum = 1;
                    SeekSlider.Value = 0;
                }
            }

            PositionText.Text = FormatTime(info.Position);
            DurationText.Text = _duration > TimeSpan.Zero ? FormatTime(_duration) : "--:--";
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static string ShortAppName(string appUserModelId)
    {
        if (string.IsNullOrWhiteSpace(appUserModelId)) return string.Empty;
        var leaf = appUserModelId.Split('!').LastOrDefault() ?? appUserModelId;
        return leaf.Split('.').LastOrDefault() ?? leaf;
    }

    private static string FormatTime(TimeSpan t)
    {
        if (t.TotalHours >= 1)
        {
            return $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";
        }

        return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }

    private void UpdateMuteGlyph()
    {
        try
        {
            MuteButton.Content = _mute.IsMuted ? "\uE74F" : "\uE767";
        }
        catch
        {
            MuteButton.Content = "\uE767";
        }
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _collapseTimer.Stop();
        SetExpanded(true);
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_seeking || _menuOpen) return;
        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private void SetExpanded(bool expand)
    {
        if (_expanded == expand) return;
        _expanded = expand;

        var duration = expand ? ExpandDuration : CollapseDuration;
        IEasingFunction easing = new QuinticEase
        {
            EasingMode = EasingMode.EaseInOut
        };

        if (expand)
        {
            ExpandedHeader.Visibility = Visibility.Visible;
            ExpandedSeek.Visibility = Visibility.Visible;
            ExpandedHeader.Opacity = 0;
            ExpandedSeek.Opacity = 0;
            HeaderSlide.Y = -10;
            SeekSlide.Y = -8;
            Shell.UpdateLayout();
        }

        var fromHeight = ActualHeight > 0 ? ActualHeight : Height;
        // Clear any stuck Height animation so From reads true visual height.
        if (expand)
        {
            BeginAnimation(HeightProperty, null);
            fromHeight = ActualHeight > 0 ? ActualHeight : Height;
        }

        var targetHeight = expand ? MeasureExpandedHeight() : CollapsedHeight;
        var heightAnim = new DoubleAnimation
        {
            From = fromHeight,
            To = targetHeight,
            Duration = duration,
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
        BeginAnimation(HeightProperty, heightAnim, HandoffBehavior.SnapshotAndReplace);

        if (expand)
        {
            Fade(ExpandedHeader, 0, 1, 420, beginMs: 70);
            Fade(ExpandedSeek, 0, 1, 440, beginMs: 130);
            Slide(HeaderSlide, -10, 0, 460, beginMs: 50);
            Slide(SeekSlide, -8, 0, 480, beginMs: 110);
            _ = RefreshUiAsync();
        }
        else
        {
            Fade(ExpandedHeader, ExpandedHeader.Opacity, 0, 260, beginMs: 0,
                onDone: () => { if (!_expanded) ExpandedHeader.Visibility = Visibility.Collapsed; });
            Fade(ExpandedSeek, ExpandedSeek.Opacity, 0, 240, beginMs: 0,
                onDone: () => { if (!_expanded) ExpandedSeek.Visibility = Visibility.Collapsed; });
            Slide(HeaderSlide, HeaderSlide.Y, -6, 280, beginMs: 0);
            Slide(SeekSlide, SeekSlide.Y, -6, 260, beginMs: 0);
        }
    }

    private double MeasureExpandedHeight()
    {
        Shell.Measure(new System.Windows.Size(Width, double.PositiveInfinity));
        var h = Math.Ceiling(Shell.DesiredSize.Height);
        return Math.Max(CollapsedHeight + 40, h + 1);
    }

    private static void Fade(
        UIElement element,
        double from,
        double to,
        int ms,
        int beginMs = 0,
        Action? onDone = null)
    {
        var anim = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(ms),
            BeginTime = TimeSpan.FromMilliseconds(beginMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.HoldEnd
        };

        if (onDone is not null)
        {
            anim.Completed += (_, _) => onDone();
        }

        element.BeginAnimation(UIElement.OpacityProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    private static void Slide(
        TranslateTransform transform,
        double from,
        double to,
        int ms,
        int beginMs = 0)
    {
        var anim = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(ms),
            BeginTime = TimeSpan.FromMilliseconds(beginMs),
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        transform.BeginAnimation(TranslateTransform.YProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    private void SeekSlider_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => _seeking = true;

    private async void SeekSlider_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            if (_canSeek)
            {
                await _media.SeekAsync(TimeSpan.FromSeconds(SeekSlider.Value));
            }
        }
        finally
        {
            _seeking = false;
            if (!IsMouseOver)
            {
                _collapseTimer.Stop();
                _collapseTimer.Start();
            }
        }
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_seeking)
        {
            PositionText.Text = FormatTime(TimeSpan.FromSeconds(e.NewValue));
        }
    }

    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        await _media.PlayPauseAsync();
        await RefreshUiAsync();
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        await _media.SkipNextAsync();
        await RefreshUiAsync();
    }

    private async void Prev_Click(object sender, RoutedEventArgs e)
    {
        await _media.SkipPreviousAsync();
        await RefreshUiAsync();
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _mute.ToggleMute();
            UpdateMuteGlyph();
        }
        catch (Exception ex)
        {
            ArtistText.Text = $"Mute: {ex.Message}";
            SetExpanded(true);
        }
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (MoreButton.ContextMenu is null) return;
        MoreButton.ContextMenu.PlacementTarget = MoreButton;
        MoreButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        MoreButton.ContextMenu.IsOpen = true;
    }

    private void MoreMenu_Opened(object sender, RoutedEventArgs e)
    {
        _menuOpen = true;
        _collapseTimer.Stop();
        SetExpanded(true);
    }

    private void MoreMenu_Closed(object sender, RoutedEventArgs e)
    {
        _menuOpen = false;
        if (!IsMouseOver)
        {
            _collapseTimer.Stop();
            _collapseTimer.Start();
        }
    }

    private void HideToTray_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApp();

    private void Autostart_Click(object sender, RoutedEventArgs e)
    {
        AutostartService.Toggle();
        RefreshAutostartMenuHeader();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            "WinPlayer — компактный SMTC-виджет.\nMVP для управления медиа с рабочего стола.",
            "Об авторе",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
