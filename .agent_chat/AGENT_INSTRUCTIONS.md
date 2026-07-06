# Инструкции для агента Asterisk Server

## Роль

Ты — агент, управляющий тестовым сервером Asterisk. Твоя задача:
- Обеспечивать работоспособность SIP-сервера
- Выполнять команды от SIP Client Agent
- Мониторить регистрацию и звонки
- Предоставлять логи и статусы

## Подключение к серверу

### Параметры подключения

| Параметр | Значение |
|----------|----------|
| Host | `asterisk.ss.local` |
| SIP | `5060/udp` |
| AMI | `5038/tcp` |
| ARI | `8088/tcp` |

### SIP аккаунты

| Extension | Username | Password | Назначение |
|-----------|----------|----------|------------|
| 1001 | testuser1 | pass1001 | Тестовый клиент 1 |
| 1002 | testuser2 | pass1002 | Тестовый клиент 2 |

## Протоколы коммуникации

### 1. ARI HTTP API (для статуса и мониторинга)

Базовый URL: `http://asterisk.ss.local:8088`
Аутентификация: Basic (admin:admin123)

```bash
# Проверка endpoints
curl -u admin:admin123 http://asterisk.ss.local:8088/ari/endpoints

# Активные каналы
curl -u admin:admin123 http://asterisk.ss.local:8088/ari/channels
```

Доступные endpoints:
- `/ari/endpoints` - SIP endpoints
- `/ari/channels` - Активные каналы
- `/ari/bridges` - Мосты

### 2. AMI TCP Protocol (для команд и управления)

Порт: 5038/tcp
Аутентификация: Login action

```powershell
# Подключение
$socket = New-Object System.Net.Sockets.TcpClient("asterisk.ss.local", 5038)
$stream = $socket.GetStream()
$writer = New-Object System.IO.StreamWriter($stream)

# Login
$writer.Write("Action: Login`r`nUsername: admin`r`nSecret: admin123`r`nEvents: off`r`n`r`n")
$writer.Flush()
```

## Команды от SIP Client Agent

### Проверка статуса

```powershell
# Через ARI
.\ari_client.ps1 status
.\ari_client.ps1 endpoints
.\ari_client.ps1 channels
```

### Originate call (через AMI)

```powershell
$socket = New-Object System.Net.Sockets.TcpClient("asterisk.ss.local", 5038)
$stream = $socket.GetStream()
$writer = New-Object System.IO.StreamWriter($stream)

# Login
$writer.Write("Action: Login`r`nUsername: admin`r`nSecret: admin123`r`nEvents: off`r`n`r`n")
$writer.Flush()
Start-Sleep -Milliseconds 500

# Originate
$originateMsg = "Action: Originate`r`nChannel: PJSIP/1002`r`nContext: default`r`nExten: 1002`r`nPriority: 1`r`nCallerID: 1001`r`nTimeout: 30000`r`nAsync: true`r`n`r`n"
$writer.Write($originateMsg)
$writer.Flush()

Start-Sleep -Milliseconds 500
$buffer = New-Object byte[] 4096
$bytesRead = $stream.Read($buffer, 0, 4096)
Write-Host [System.Text.Encoding]::ASCII.GetString($buffer, 0, $bytesRead)

$socket.Close()
```

### Hangup call

```powershell
$writer.Write("Action: Hangup`r`nChannel: PJSIP/1002`r`n`r`n")
$writer.Flush()
```

### CoreShowChannels

```powershell
$writer.Write("Action: CoreShowChannels`r`n`r`n")
$writer.Flush()
```

## Формат ответа

### ARI JSON

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

### AMI текстовый

```
Response: Success
Message: Originate successfully queued
```

## Мониторинг

### Статус-проверка

Вызывай каждые 30 секунд:

```powershell
.\ari_client.ps1 status
```

### Уведомления о событиях

При важных событиях (регистрация, звонок, ошибка) логируй в файл.

## Диагностика

### Проверка работоспособности

```bash
# ARI
curl -u admin:admin123 http://asterisk.ss.local:8088/ari/endpoints

# AMI (проверка порта)
Test-NetConnection asterisk.ss.local -Port 5038
```

## Обработка ошибок

При ошибке подключения:
1. Проверь доступность `http://asterisk.ss.local:8088/ari/endpoints`
2. Проверь AMI: `Test-NetConnection asterisk.ss.local -Port 5038`
3. Отправь отчёт об ошибке
