# SIP Client — Контекст проекта

## TL;DR
Десктопный SIP-клиент для Windows. Стек: C# / .NET 8 + WPF + SIPSorcery. Автосборка через GitHub Actions → готовый `.exe` для сотрудников.

## Репозитории
- **Python версия (заброшена):** `https://github.com/sasamosenko/sip_client` — pjsua2 + PySide6, не удалось собрать на Windows
- **C# версия (активная):** `https://github.com/sasamosenko/sip_client_cs` — .NET 8 + WPF + SIPSorcery

## Где работаем
- Разработка на **macOS** (нет dotnet CLI, нет Visual Studio)
- Сборка на **Windows** через GitHub Actions
- Код пишем здесь, ошибки ловим через GitHub Actions build logs

## Текущее состояние (2026-07-01)
Build падает с ошибкой:
```
The type or namespace name 'SIPCallUserAgent' could not be found
The type or namespace name 'SIPRegistrarUserAgent' could not be found
```
Скорее всего API пакета SIPSorcery изменился. Нужно проверить доступные классы в最新 версии.

## Структура проекта
```
sip_client_cs/
├── SipClient/
│   ├── SipClient.csproj        # .NET 8, SIPSorcery 5.5.0, CommunityToolkit.Mvvm
│   ├── Models/
│   │   ├── SipConfig.cs        # Конфиг: server, port, user, pass, devices, auto-answer
│   │   └── CallRecord.cs       # CSV формат: timestamp, number, direction, duration, status
│   ├── Services/
│   │   ├── SipService.cs       # SIP: регистрация, звонки (ИСПРАВИТЬ API)
│   │   ├── ConfigService.cs    # JSON config load/save
│   │   ├── CallHistoryService.cs # CSV history load/save
│   │   ├── SipLogger.cs        # SIP пакеты в logs/sip_YYYY-MM-DD.log
│   │   └── NotificationService.cs # Toast уведомления + звуки
│   ├── ViewModels/
│   │   └── MainViewModel.cs    # MVVM: connect, call, hangup, answer, settings
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
1. **Исправить SipService.cs** — найти правильные классы SIPSorcery для:
   - SIP транспорт (UDP)
   - Регистрация на сервер
   - Исходящие звонки
   - Входящие звонки
2. **Проверить API SIPSorcery** — зайти на `https://github.com/sipsorcery/sipsorcery` и посмотреть примеры
3. **Собрать и протестировать** — push → GitHub Actions → скачать `.exe`
4. **Доделать UI** — возможно нужны дополнительные экраны

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
- **SIP:** SIPSorcery (NuGet пакет)
- **MVVM:** CommunityToolkit.Mvvm
- **JSON:** Newtonsoft.Json
- **Сборка:** GitHub Actions + dotnet publish
