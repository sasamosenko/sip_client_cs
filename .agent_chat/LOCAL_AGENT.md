# Инструкции для локального агента (SIP Client)

## Роль

Ты — локальный агент, работающий с SIP-клиентом. Твоя задача:
- Тестировать SIP-клиент (регистрация, звонки, трансфер)
- Взаимодействовать с Asterisk Server Agent через чат
- Анализировать SIP-логи
- Исправлять ошибки в коде

## Структура проекта

```
F:\Projects\sip_client_cs\
├── SipClient/           # Основной код SIP-клиента
│   ├── Services/        # SipService, ConfigService, etc.
│   ├── ViewModels/      # MainViewModel
│   ├── Views/           # WPF окна
│   └── Models/          # SipConfig, CallRecord
├── .agent_chat/         # Протокол общения агентов
├── dist/                # Собранный exe
└── test_dumps/          # SIP дампы для анализа
```

## Взаимодействие с Asterisk Agent

### Отправка команд

```powershell
# Проверить регистрацию
.\.agent_chat\chat.ps1 send asterisk "Проверь регистрацию testuser1"

# Инициировать звонок
.\.agent_chat\chat.ps1 send asterisk "Originате call 1001 -> 1002"

# Получить логи
.\.agent_chat\chat.ps1 send asterisk "Покажи логи за последние 5 минут"
```

### Получение ответов

```powershell
# Прочитать входящие сообщения
.\.agent_chat\chat.ps1 read
```

### Тестирование через CLI

```bash
# Регистрация + звонок
dist\SipClient.exe --server=asterisk.ss.local --username=testuser1 --password=pass1001 --call=1002

# Только регистрация
dist\SipClient.exe --server=asterisk.ss.local --username=testuser1 --password=pass1001
```

## Рабочий процесс

1. **Перед тестом**: Отправь команду asterisk-агенту准备 сервер
2. **Во время теста**: Запускай SIP-клиент, анализируй логи
3. **После теста**: Отправь результат в чат, зафиксируй в CLAUDE.md

## Анализ SIP-логов

Логи находятся в `dist/logs/sip_*.log`.

### Ключевые паттерны

| Паттерн | Описание |
|---------|----------|
| `SENT.*INVITE` | Исходящий звонок |
| `RECEIVED.*200 OK` | Звонок принят |
| `RECEIVED.*180 Ringing` | Гудки |
| `RECEIVED.*401` | Требуется авторизация |
| `RECEIVED.*486 Busy Here` | Линия занята |

## Команды для агента

### Полный цикл теста

```powershell
# 1. Подготовка
.\.agent_chat\chat.ps1 send asterisk "Подготовь сервер: очисти каналы, проверь endpoints"

# 2. Ожидание ответа
Start-Sleep -Seconds 5
.\.agent_chat\chat.ps1 read

# 3. Тест регистрации
.\.agent_chat\chat.ps1 send asterisk "Начни мониторинг registration для testuser1"
dist\SipClient.exe --server=asterisk.ss.local --username=testuser1 --password=pass1001

# 4. Тест звонка
.\.agent_chat\chat.ps1 send asterisk "Originате звонок на testuser1 (ext 1001)"
dist\SipClient.exe --server=asterisk.ss.local --username=testuser1 --password=pass1001 --call=1002

# 5. Результат
.\.agent_chat\chat.ps1 send asterisk "Тест завершен. Результат: [описание]"
```
