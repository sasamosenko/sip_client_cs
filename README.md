# SIP Client (C# / .NET 8)

Десктопный SIP-клиент для Windows на C# + WPF + SIPSorcery.

## Требования

- .NET 8 SDK (для сборки)
- Windows 10/11

## Сборка

```bash
dotnet publish SipClient/SipClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Установка

1. Скачать `SipClient.exe` из артефактов сборки
2. Поместить рядом `config.json`
3. Запустить

## Функции

- SIP регистрация на АТС (Digest auth)
- Исходящие и входящие звонки
- Blind transfer (Replaces)
- История звонков (JSON)
- Выбор микрофона/динамика
- Настройка громкости
- Автоответ с задержкой
