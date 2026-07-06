# Архитектура Agent Communication System

## Обзор

Система для общения между агентами:
- **SIP Client Agent** (локальная машина) - тестирует SIP-клиент
- **Asterisk Server Agent** (удалённая машина) - управляет SIP-сервером

## Сетевая архитектура

```
┌─────────────────────────────────────────────────────────────────┐
│                        Локальная сеть                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────┐           ┌─────────────────────┐     │
│  │   SIP Client Agent  │           │  Asterisk Server    │     │
│  │   (локальная машина)│           │  (asterisk.ss.local)│     │
│  │                     │           │                     │     │
│  │  - SipClient.exe    │    HTTP   │  - PJSIP Stack     │     │
│  │  - Тестирование     │ ◄───────► │  - AMI Manager     │     │
│  │  - Анализ логов     │   :8088   │  - ARI REST API    │     │
│  │                     │   :5038   │                     │     │
│  └─────────────────────┘           └─────────────────────┘     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Протоколы коммуникации

### 1. ARI HTTP API (порт 8088)

**Назначение:** Мониторинг и получение статуса

**Аутентификация:** Basic (admin:admin123)

**Доступные endpoints:**

| Endpoint | Метод | Описание |
|----------|-------|----------|
| `/ari/endpoints` | GET | Список SIP endpoints |
| `/ari/channels` | GET | Активные каналы |
| `/ari/channels` | POST | Originate call |
| `/ari/bridges` | GET | Мосты (конференции) |

**Пример запроса:**
```http
GET /ari/endpoints
Authorization: Basic base64(admin:admin123)
Accept: application/json
```

**Пример ответа:**
```json
{
  "endpoints": [
    {
      "technology": "PJSIP",
      "resource": "testuser1 testuser2",
      "state": "online offline"
    }
  ]
}
```

### 2. AMI TCP Protocol (порт 5038)

**Назначение:** Управление и выполнение команд

**Аутентификация:** Login action

**Протокол:** текстовый (CRLF terminated)

**Пример команды:**
```
Action: Login
Username: admin
Secret: admin123
Events: off
```

**Пример ответа:**
```
Response: Success
Message: Authentication accepted
```

## Доступные команды

### Для мониторинга (ARI)

```powershell
# Статус endpoints
.\ari_client.ps1 status
.\ari_client.ps1 endpoints

# Активные каналы
.\ari_client.ps1 channels
```

### Для управления (AMI)

```powershell
# Originate call
$writer.Write("Action: Originate`r`nChannel: PJSIP/1002`r`nContext: default`r`nExten: 1002`r`nPriority: 1`r`nCallerID: 1001`r`nTimeout: 30000`r`nAsync: true`r`n`r`n")

# Hangup
$writer.Write("Action: Hangup`r`nChannel: PJSIP/1002`r`n`r`n")

# List channels
$writer.Write("Action: CoreShowChannels`r`n`r`n")
```

## Инструкции для агентов

- [Инструкции для Asterisk агента](AGENT_INSTRUCTIONS.md)
- [Инструкции для SIP Client агента](LOCAL_AGENT.md)
- [AMI Client](AMI_CLIENT.md)

## Рабочий процесс

1. **Подготовка:**
   - SIP Client Agent проверяет статус Asterisk через ARI
   - Если всё ОК - начинает тестирование

2. **Тестирование:**
   - SIP Client Agent запускает `SipClient.exe`
   - Asterisk Agent мониторит registration и channels через ARI
   - При необходимости Asterisk Agent originate call через AMI

3. **Отчёт:**
   - SIP Client Agent анализирует логи
   - Результаты записываются в CLAUDE.md
   - Агенты общаются через HTTP запросы

## Тестирование

### Быстрая проверка связи

```powershell
# Проверить ARI
.\ari_client.ps1 status

# Проверить AMI
$socket = New-Object System.Net.Sockets.TcpClient("asterisk.ss.local", 5038)
Write-Host "AMI connected: $($socket.Connected)"
$socket.Close()
```

### Полный цикл теста

```powershell
# 1. Проверить статус
.\ari_client.ps1 status

# 2. Запустить SIP клиент
dist\SipClient.exe --server=asterisk.ss.local --username=testuser1 --password=pass1001 --call=1002

# 3. Проверить каналы
.\ari_client.ps1 channels

# 4. Originate call (если нужно)
# Через AMI команду
```
