# SIP Client — Контекст проекта

## TL;DR
Десктопный SIP-клиент для Windows. Стек: C# / .NET 8 + WPF + SIPSorcery. Автосборка через GitHub Actions → готовый `.exe` для сотрудников.

## Репозитории
- **Python версия (заброшена):** `https://github.com/sasamosenko/sip_client` — pjsua2 + PySide6, не удалось собрать на Windows
- **C# версия (активная):** `https://github.com/sasamosenko/sip_client_cs` — .NET 8 + WPF + SIPSorcery

## Где работаем
- Разработка на **Windows** (dotnet CLI доступен, .NET 10 SDK установлен)
- CI: GitHub Actions → `.exe` для сотрудников

## Текущее состояние (2026-07-02)
SIP-слой полностью переписан под SIPSorcery 6.x API. Проект собирается (0 ошибок).

### Что было исправлено
- `SipService.cs` — переписан с нуля: `SIPClientUserAgent`/`SIPServerUserAgent` → `SIPUserAgent`, `VoIPMediaSession` получил `MediaEndPoints`, исправлены делегаты событий
- `ConfigService.cs`, `SipLogger.cs`, `CallHistoryService.cs` — добавлен `using System.IO`
- `MainViewModel.cs` — исправлен NullLoggerFactory (добавлен `using Microsoft.Extensions.Logging`)
- `SipClient.csproj` — обновлены NuGet-версии: SIPSorcery 6.0.2, SIPSorceryMedia.Windows 6.0.4, SIPSorceryMedia.Encoders 8.0.7
- `Resources/phone.ico` — удалена невалидная заглушка

## Структура проекта
```pf
sip_client_cs/
├── SipClient/
│   ├── SipClient.csproj        # .NET 8, SIPSorcery 6.0.2, CommunityToolkit.Mvvm
│   ├── Models/
│   │   ├── SipConfig.cs        # Конфиг: server, port, user, pass, devices, auto-answer
│   │   └── CallRecord.cs       # CSV формат: timestamp, number, direction, duration, status
│   ├── Services/
│   │   ├── SipService.cs       # SIP: SIPUserAgent, регистрация, звонки, blind transfer, устройства
│   │   ├── ConfigService.cs    # JSON config load/save
│   │   ├── CallHistoryService.cs # CSV history load/save
│   │   ├── SipLogger.cs        # SIP пакеты в logs/sip_YYYY-MM-DD.log
│   │   └── NotificationService.cs # Toast уведомления + звуки
│   ├── ViewModels/
│   │   └── MainViewModel.cs    # MVVM: connect, call, hangup, answer, transfer, devices, settings
│   ├── Views/
│   │   ├── MainWindow.xaml     # Красивый dark theme UI
│   │   └── MainWindow.xaml.cs
│   ├── Converters/
│   │   └── Converters.cs       # InvertedBool, DirectionToColor, DirectionToIcon
│   ├── Resources/
│   │   └── Styles.xaml         # Кастомные стили: TextBox, Button, Slider, ListBox
│   ├── App.xaml                # Ресурсы + конвертеры
│   └── App.xaml.cs
├── .github/workflows/build.yml # GitHub Actions: dotnet publish → upload artifact
├── config.json                 # Конфиг по умолчанию
└── README.md
```

## ТЗ (из сессии)
- SIP клиент для Windows
- Регистрация на АТС Infinity 1.9.8 (Digest auth)
- Входящие + исходящие звонки
- Blind transfer (Replaces header)
- История звонков (CSV)
- Выбор микрофона/динамика
- Настройка громкости
- Автоответ с задержкой
- Красивый UI, уведомления, звуки
- SIP логирование в файл
- Сборка: `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true`

## Что нужно сделать дальше
1. **Пушить и проверить CI** — push → GitHub Actions → скачать `.exe`
2. **Протестировать на реальном сервере** — регистрация на Infinity 1.9.8, звонки
3. **Доделать UI** — возможно нужны дополнительные экраны

## Как запустить на Windows
```bash
# Установить .NET 8 SDK
# Открыть в Visual Studio 2022
dotnet restore SipClient/SipClient.csproj
dotnet build SipClient/SipClient.csproj
# Или Publish:
dotnet publish SipClient/SipClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Стек
- **Язык:** C# / .NET 8
- **UI:** WPF (dark theme, кастомные стили)
- **SIP:** SIPSorcery 6.0.2 (NuGet)
- **Audio:** NAudio 2.2.1 (устройства), SIPSorceryMedia.Windows 6.0.4
- **MVVM:** CommunityToolkit.Mvvm
- **JSON:** Newtonsoft.Json
- **Сборка:** GitHub Actions + dotnet publish
