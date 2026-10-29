# WinPlayer — контекст

## Цель
MVP: «бровь» сверху экрана (как notch) для SMTC: play/pause, next/prev, mute; по hover — title/artist, seek, время.

## Стек
- C# / **WPF** на `net8.0-windows10.0.19041.0` (WinUI 3 отложен)
- SMTC: `GlobalSystemMediaTransportControlsSessionManager` + timeline/seek
- Mute: Core Audio (`IAudioEndpointVolume`)
- UI: taskbar-like acrylic brow, Topmost, expand on hover
- Tray icon + autostart (`HKCU\...\Run`)
- Updates: poll public `version.json` (GitHub raw) → balloon in tray → open download URL

## Структура
```
winplayer/
  WinPlayer.sln
  WinPlayer/
    MainWindow.xaml(.cs)
    Services/MediaSessionService.cs
    Services/SystemMuteService.cs
    Models/MediaSessionInfo.cs
```

## Ограничения MVP
- Одна сессия (current SMTC session)
- Нет настроек, трея, плейлиста, seek
- Mute = системный mute устройства воспроизведения
