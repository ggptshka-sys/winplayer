using Windows.Media.Control;
using WinPlayer.Models;

namespace WinPlayer.Services;

public sealed class MediaSessionService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool _disposed;

    public event EventHandler? SessionChanged;

    public async Task InitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += OnCurrentSessionChanged;
        BindSession(_manager.GetCurrentSession());
        RaiseChanged();
    }

    public async Task<MediaSessionInfo> GetInfoAsync()
    {
        if (_session is null)
        {
            return new MediaSessionInfo();
        }

        try
        {
            var props = await _session.TryGetMediaPropertiesAsync();
            var playback = _session.GetPlaybackInfo();
            var timeline = _session.GetTimelineProperties();
            var status = playback.PlaybackStatus;
            var isPlaying = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            var duration = timeline.EndTime > timeline.StartTime
                ? timeline.EndTime - timeline.StartTime
                : TimeSpan.Zero;
            var position = timeline.Position - timeline.StartTime;
            if (position < TimeSpan.Zero) position = TimeSpan.Zero;
            if (duration > TimeSpan.Zero && position > duration) position = duration;

            return new MediaSessionInfo
            {
                HasSession = true,
                Title = string.IsNullOrWhiteSpace(props.Title) ? "Без названия" : props.Title,
                Artist = props.Artist ?? string.Empty,
                AppName = _session.SourceAppUserModelId ?? string.Empty,
                IsPlaying = isPlaying,
                CanPlayPause = playback.Controls.IsPlayEnabled || playback.Controls.IsPauseEnabled,
                CanSkipNext = playback.Controls.IsNextEnabled,
                CanSkipPrevious = playback.Controls.IsPreviousEnabled,
                CanSeek = playback.Controls.IsPlaybackPositionEnabled && duration > TimeSpan.Zero,
                Position = position,
                Duration = duration
            };
        }
        catch
        {
            return new MediaSessionInfo();
        }
    }

    public async Task PlayPauseAsync()
    {
        if (_session is null) return;

        var playback = _session.GetPlaybackInfo();
        if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
        {
            await _session.TryPauseAsync();
        }
        else
        {
            await _session.TryPlayAsync();
        }
    }

    public Task SkipNextAsync() =>
        _session is null ? Task.CompletedTask : _session.TrySkipNextAsync().AsTask();

    public Task SkipPreviousAsync() =>
        _session is null ? Task.CompletedTask : _session.TrySkipPreviousAsync().AsTask();

    public async Task SeekAsync(TimeSpan position)
    {
        if (_session is null) return;
        await _session.TryChangePlaybackPositionAsync(position.Ticks);
    }

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        BindSession(sender.GetCurrentSession());
        RaiseChanged();
    }

    private void BindSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_session is not null)
        {
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }

        _session = session;

        if (_session is not null)
        {
            _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        }
    }

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) => RaiseChanged();

    private void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) => RaiseChanged();

    private void OnTimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args) => RaiseChanged();

    private void RaiseChanged() => SessionChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }

        BindSession(null);
        _manager = null;
    }
}
