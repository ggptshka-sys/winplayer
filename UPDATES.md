# Как выкатывать обновления

1. Подними публичный GitHub-репозиторий.
2. В `WinPlayer/update-feed.url` замени URL на свой, например:
   `https://raw.githubusercontent.com/ТВОЙ_НИК/winplayer/main/version.json`
3. Собери Release и залей zip/exe в GitHub Releases.
4. Обнови корневой `version.json`:
   - `version` — новая версия (должна совпадать с `<Version>` в `.csproj`)
   - `url` — ссылка на скачивание из Releases
   - `notes` — короткий текст для balloon в трее
5. Закоммить и запушь. У пользователей при следующем запуске (или «Проверить обновления» в трее) прилетит уведомление.

Пересборка для пользователей:
- в `.csproj` подними `<Version>1.0.1</Version>`
- собери `dotnet publish` / `dotnet build -c Release`
- обнови `version.json` и сделай Release
