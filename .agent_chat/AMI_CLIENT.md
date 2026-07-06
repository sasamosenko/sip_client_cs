# AMI Client для Agent Communication

## Подключение

```powershell
# Параметры подключения
$AMI_HOST = "asterisk.ss.local"
$AMI_PORT = 5038
$AMI_USER = "admin"
$AMI_PASS = "admin123"

# Создание подключения
$socket = New-Object System.Net.Sockets.TcpClient($AMI_HOST, $AMI_PORT)
$stream = $socket.GetStream()
$writer = New-Object System.IO.StreamWriter($stream)
$reader = New-Object System.IO.StreamReader($stream)
```

## Команды

### Login
```
Action: Login
Username: admin
Secret: admin123
Events: off
```

### Originate Call
```
Action: Originate
Channel: PJSIP/1002
Context: default
Exten: 1002
Priority: 1
CallerID: 1001
Timeout: 30000
Async: true
```

### Hangup Channel
```
Action: Hangup
Channel: PJSIP/1002
```

### Get Channel Status
```
Action: ChannelStatus
Channel: PJSIP/1002
```

### List Channels
```
Action: CoreShowChannels
```

### Queue Status
```
Action: QueueStatus
Queue: support
```

## Пример использования

```powershell
function Send-AMICommand {
    param([string]$Command)
    
    $writer.Write($Command + "`r`n`r`n")
    $writer.Flush()
    Start-Sleep -Milliseconds 500
    
    $buffer = New-Object byte[] 4096
    $bytesRead = $stream.Read($buffer, 0, 4096)
    return [System.Text.Encoding]::ASCII.GetString($buffer, 0, $bytesRead)
}

# Login
Send-AMICommand "Action: Login`r`nUsername: admin`r`nSecret: admin123`r`nEvents: off"

# Originate call
Send-AMICommand "Action: Originate`r`nChannel: PJSIP/1002`r`nContext: default`r`nExten: 1002`r`nPriority: 1`r`nCallerID: 1001"
```
