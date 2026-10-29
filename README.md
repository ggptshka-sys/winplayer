# WinPlayer

Плавающий always-on-top виджет управления активной SMTC-сессией (WPF / .NET 8).

## Запуск

```powershell
dotnet run --project WinPlayer -c Release
```

Или: `WinPlayer\bin\Release\net8.0-windows10.0.19041.0\WinPlayer.exe`

## Возможности

- Бровь сверху экрана: play / pause / next / prev / mute
- Hover → title, seek, время
- Трей: автозапуск, проверка обновлений, выход
- Обновления через публичный [`version.json`](version.json) — см. [UPDATES.md](UPDATES.md)

## Тест

1. Включи музыку в браузере / плеере.
2. Запусти WinPlayer.
3. Проверь кнопки и раскрытие по наведению.
