# Инструкции для разработки

## Правила работы

### 1. Восстановление работоспособности
Приоритет — сделать приложение компилируемым и запускаемым. Каждое изменение должно проходить `dotnet build` без ошибок.

### 2. Атомарное тестирование модулей
Тестируем каждый модуль по отдельности, совместно с UI:

1. **Регистрация** — подключение к серверу, статус "Зарегистрирован"
2. **Исходящий звонок** — дозвон, 180/183, 200 OK, аудио
3. **Входящий звонок** — приём, автоответ
4. **Завершение звонка** — BYE по обе стороны
5. **Трансфер** — blind transfer (REFER)
6. **Настройки** — сохранение/загрузка config.json
7. **История звонков** — запись, отображение, очистка
8. **Аудио** — громкость, выбор устройств

### 3. Порядок работы
- Берём один модуль
- Тестируем в UI на реальном сервере
- Если работает — коммитим
- Помечаем модуль как "locked" — больше не трогаем без явного запроса
- Переходим к следующему модулю

### 4. Юнит-тесты
Каждый модуль должен иметь юнит-тесты:
- Тесты пишутся ПОСЛЕ проверки в UI (тесты фиксируют работоспособное поведение)
- Тесты покрывают ключевую логику (SIP-сообщения, парсинг, состояние)
- Тесты запускаются через `dotnet test`

### 5. Запреты
- НЕ трогаем модуль, помеченный как "locked"
- НЕ делаем больших переписываний без явного запроса
- НЕ коммитим непроверенный код
- Каждый коммит = один работающий модуль

## Структура модулей

| # | Модуль | Файлы | Статус |
|---|--------|-------|--------|
| 1 | Регистрация | SipService.RegisterAsync | ✅ tested, locked |
| 2 | Исходящий звонок | SipService.MakeCallAsync | 🔒 pending |
| 3 | Входящий звонок | SipService.AnswerCall | 🔒 pending |
| 4 | Завершение | SipService.HangupCall | 🔒 pending |
| 5 | Трансфер | SipService.BlindTransferAsync | 🔒 pending |
| 6 | Настройки | ConfigService, SettingsWindow | 🔒 pending |
| 7 | История | CallHistoryService | 🔒 pending |
| 8 | Аудио | Volume, device selection | 🔒 pending |

## SIPSorcery API (верифицировано)

- `SIPRequest.GetRequest(SIPMethodsEnum.INVITE, SIPURI)` — создаёт INVITE
- `IMediaSession.CreateOffer(IPAddress)` → `SDP` (вызывать `.ToString()` для Body)
- `SDP.ParseSDPDescription(string)` → `SDP`
- `SdpType.answer` (lowercase)
- `SIPContactHeader(string, SIPURI)` — конструктор
- `SIPHeader.Contact` — `List<SIPContactHeader>`
- `SIPFromHeader(string, SIPURI, string)` — (name, uri, tag)
- `SIPToHeader(SIPUserField, SIPURI, string)` — (userField, uri, tag)

## Сервер для тестов

- asterisk.ss.local:5060 (SIP), :5038 (AMI), :8088 (ARI)
- Extension 1001: testuser1 / pass1001
- Extension 1002: testuser2 / pass1002

## Тестирование через CLI

```bash
# Запуск с авто-звонком после регистрации
SipClient.exe --server=asterisk.ss.local --username=testuser1 --password=pass1001 --call=1002

# Только регистрация (без звонка)
SipClient.exe --server=asterisk.ss.local --username=testuser1 --password=pass1001
```
