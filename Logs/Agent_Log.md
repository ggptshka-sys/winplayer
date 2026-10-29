# Agent Log

## 2026-10-29 — MVP scaffold
- На машине не было `dotnet` / winget → установлен .NET SDK **8.0.427**.
- Вместо WinUI 3 выбран **WPF + Windows 10 TFM** для доступа к SMTC без App SDK packaging.
- Реализованы `MediaSessionService` (SMTC) и `SystemMuteService` (Core Audio mute).
- UI: frameless dark neumorphism, always-on-top.
- `dotnet build -c Release` — **успех**, 0 ошибок.

## 2026-10-29 — Brow / notch UI
- Окно закреплено сверху по центру (`Topmost`, без таскбара).
- Collapsed ~52px: только кнопки; hover → ~138px: title/artist, seek, время, close.
- Timeline/seek через SMTC `GetTimelineProperties` / `TryChangePlaybackPositionAsync`.
- Collapse с задержкой, чтобы не мигало при работе со слайдером.
- Release build OK.

## 2026-10-29 — Glass style
- Референс: frosted dark glass (крупный radius, тонкий светлый stroke, круглые chip-кнопки).
- Neumorphism убран → `GlassButton` / полупрозрачный fill `#CC1C1C1E`.
- `AcrylicHelper` (DWM acrylic) best-effort; при `AllowsTransparency` часто без blur, визуально стекло всё равно держится.
- Brow логика hover/seek сохранена.

## 2026-10-29 — Fix brow clip / substrate
- Убран `AcrylicHelper` на HWND (давал прямоугольную подложку).
- Flat-top: `CornerRadius=0,0,22,22`, `Top=0`.
- Высота/паддинги подогнаны — кнопки больше не обрезаются.

## 2026-10-29 — Tray + autostart
- `NotifyIcon` в системном трее (ПКМ: Показать / Автозапуск / Выход).
- Автозапуск: `HKCU\...\Run\WinPlayer`.
- Закрытие UI → в трей; полный выход из трея или «Выход» в меню.

## 2026-10-29 — Update feed
- `UpdateService` читает `version.json` по URL из `update-feed.url`.
- При старте + пункт трея «Проверить обновления» → balloon; клик открывает download URL.
- Инструкция: `UPDATES.md`. Placeholder `OWNER/REPO` пока не заменён — проверка тихо пропускается.
