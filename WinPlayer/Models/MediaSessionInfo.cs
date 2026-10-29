namespace WinPlayer.Models;

public sealed class MediaSessionInfo
{
    public string Title { get; init; } = "Нет активного медиа";
    public string Artist { get; init; } = string.Empty;
    public string AppName { get; init; } = string.Empty;
    public bool IsPlaying { get; init; }
    public bool HasSession { get; init; }
    public bool CanPlayPause { get; init; }
    public bool CanSkipNext { get; init; }
    public bool CanSkipPrevious { get; init; }
    public bool CanSeek { get; init; }
    public TimeSpan Position { get; init; }
    public TimeSpan Duration { get; init; }
}
