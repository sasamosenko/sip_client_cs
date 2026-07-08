# SIP Client

Десктопный SIP-клиент для Windows. Self-contained `.exe` — не требует установки .NET.

## Возможности

- SIP регистрация на АТС (Digest auth)
- Исходящие и входящие звонки
- Blind transfer (REFER)
- Автоответ с настраиваемой задержкой
- Настройка кодеков (G.722, G.711 μ/A-law, G.729) с приоритетами
- Выбор микрофона/динамика
- Регулировка громкости (микрофон, динамик, звонок) 0–150%
- История звонков с копированием в CSV (кнопка + Ctrl+C)
- SIP-логирование (SENT/RECEIVED дампы)
- Окно "О приложении" (версия, автор, компания)
- Тёмная тема
- PerMonitorV2 DPI (масштабирование под разрешение экрана)

## Требования

- Windows 10+ (x64)
- .NET 10 SDK (для сборки)

## Сборка

```bash
dotnet publish SipClient/SipClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

## Установка

1. Скачать `SipClient.exe` из [Releases](https://github.com/sasamosenko/sip_client_cs/releases)
2. Создать `config.json` рядом (см. пример ниже)
3. Запустить

## CLI-параметры

Приложение поддерживает запуск из командной строки:

```bash
# Регистрация + звонок
SipClient.exe --server=asterisk.ss.local --username=testuser1 --password=pass1001 --call=1002

# Только регистрация
SipClient.exe --server=asterisk.ss.local --username=testuser1 --password=pass1001
```

| Параметр | Описание |
|----------|----------|
| `--server` | SIP-сервер (IP или домен) |
| `--port` | Порт SIP (по умолчанию 5060) |
| `--username` | SIP username |
| `--password` | SIP пароль |
| `--call` | Номер для автоматического вызова после регистрации |

## Конфигурация (config.json)

```json
{
  "Server": "sip.example.com",
  "Port": 5060,
  "Username": "1001",
  "Password": "secret",
  "DisplayName": "Иван Иванов",
  "Domain": "",
  "AuthUsername": "",
  "LocalPort": 5080,
  "CaptureDeviceId": -1,
  "PlaybackDeviceId": -1,
  "MicVolume": 100,
  "SpeakerVolume": 100,
  "RingVolume": 80,
  "AutoAnswerEnabled": false,
  "AutoAnswerDelaySeconds": 3,
  "SipLoggingEnabled": true,
  "RegistrationExpiry": 600,
  "EnabledCodecs": ["G722", "PCMU", "PCMA", "G729"]
}
```

| Параметр | Описание |
|----------|----------|
| `Server` | SIP-сервер (IP или домен) |
| `Port` | Порт SIP (по умолчанию 5060) |
| `LocalPort` | Локальный порт привязки |
| `EnabledCodecs` | Упорядоченный список кодеков (первый = предпочтительный) |
| `AutoAnswerEnabled` | Автоответ на входящие |
| `AutoAnswerDelaySeconds` | Задержка автоответа (сек) |
| `SipLoggingEnabled` | Логирование SIP-пакетов в `logs/` |

## Кодеки

| Кодек | Описание | Payload Type |
|-------|----------|-------------|
| G722 | Широкополосный (7 kHz) | 9 |
| PCMU | G.711 μ-law (8 kHz) | 0 |
| PCMA | G.711 A-law (8 kHz) | 8 |
| G729 | Сжатый (8 kHz, 8 kbps) | 18 |

## Структура проекта

```
SipClient/
├── Models/          — SipConfig, CallRecord, CodecOption
├── Services/        — SipService, TransferService, ConfigService, NotificationService, SipLogger
├── ViewModels/      — MainViewModel (CommunityToolkit.Mvvm)
├── Views/           — MainWindow, SettingsWindow, AboutWindow
├── Converters/      — WPF value converters
├── Resources/       — Styles.xaml, phone.ico
└── App.xaml
```

## Стек

- C# / .NET 10
- WPF (dark theme)
- SIPSorcery 8.0.7 (SIP stack)
- NAudio 2.2.1 (аудио)
- CommunityToolkit.Mvvm 8.2.2
- Newtonsoft.Json 13.0.3

## Разработка

Разработка велась компанией **ООО «Декодика»** при поддержке AI-агента **MiMoCode** (Xiaomi MiMo Team).
