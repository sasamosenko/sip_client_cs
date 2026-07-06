# Asterisk ARI HTTP Client for Agent Communication
# Использование:
#   .\ari_client.ps1 status
#   .\ari_client.ps1 endpoints
#   .\ari_client.ps1 channels
#   .\ari_client.ps1 originate 1001 1002
#   .\ari_client.ps1 command "pjsip show contacts"
#   .\ari_client.ps1 log

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("status", "endpoints", "channels", "originate", "command", "log", "dialplan")]
    [string]$Action,
    
    [string]$Arg1,
    [string]$Arg2
)

$ARI_URL = "http://asterisk.ss.local:8088"
$ARI_USER = "admin"
$ARI_PASS = "admin123"

function Invoke-ARI {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null
    )
    
    $uri = "$ARI_URL$Path"
    $headers = @{
        "Accept" = "application/json"
    }
    
    $params = @{
        Uri = $uri
        Method = $Method
        Headers = $headers
        Credential = New-Object System.Management.Automation.PSCredential($ARI_USER, (ConvertTo-SecureString $ARI_PASS -AsPlainText -Force))
        ContentType = "application/json"
    }
    
    if ($Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 10)
    }
    
    try {
        $response = Invoke-RestMethod @params
        return $response
    } catch {
        Write-Host "ARI Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            Write-Host $reader.ReadToEnd() -ForegroundColor Yellow
        }
        return $null
    }
}

switch ($Action) {
    "status" {
        Write-Host "=== Asterisk Server Status ===" -ForegroundColor Cyan
        
        # Получить endpoints для определения статуса
        $endpoints = Invoke-ARI -Path "/ari/endpoints"
        if ($endpoints) {
            $online = ($endpoints | Where-Object { $_.state -eq "online" }).Count
            $total = $endpoints.Count
            Write-Host "Endpoints: $online/$total online"
            
            foreach ($ep in $endpoints) {
                $state = if ($ep.state -eq "online") { "[ONLINE]" } else { "[OFFLINE]" }
                Write-Host "  $state $($ep.technology) - $($ep.resource)"
            }
        }
        
        # Получить активные каналы
        $channels = Invoke-ARI -Path "/ari/channels"
        if ($channels) {
            Write-Host "Active channels: $($channels.Count)"
        }
        
        # Получить bridges
        $bridges = Invoke-ARI -Path "/ari/bridges"
        if ($bridges) {
            Write-Host "Bridges: $($bridges.Count)"
        }
    }
    
    "endpoints" {
        Write-Host "=== SIP Endpoints ===" -ForegroundColor Cyan
        $endpoints = Invoke-ARI -Path "/ari/endpoints"
        if ($endpoints) {
            foreach ($ep in $endpoints) {
                $state = if ($ep.state -eq "online") { "[ONLINE]" } else { "[OFFLINE]" }
                Write-Host "$state $($ep.resource) ($($ep.technology))"
            }
        }
    }
    
    "channels" {
        Write-Host "=== Active Channels ===" -ForegroundColor Cyan
        $channels = Invoke-ARI -Path "/ari/channels"
        if ($channels) {
            if ($channels.Count -eq 0) {
                Write-Host "No active channels"
            } else {
                foreach ($ch in $channels) {
                    Write-Host "[$($ch.id)] $($ch.name) - State: $($ch.state)"
                }
            }
        }
    }
    
    "originate" {
        if (-not $Arg1 -or -not $Arg2) {
            Write-Host "Usage: .\ari_client.ps1 originate <from_ext> <to_ext>"
            exit 1
        }
        Write-Host "=== Originate Call: $Arg1 -> $Arg2 ===" -ForegroundColor Cyan
        $body = @{
            endpoint = "sip:$Arg2"
            extension = $Arg2
            context = "default"
            callerId = $Arg1
            timeout = 30
        }
        $result = Invoke-ARI -Method "POST" -Path "/ari/channels" -Body $body
        if ($result) {
            Write-Host "Channel created: $($result.id)" -ForegroundColor Green
            Write-Host "State: $($result.state)"
        }
    }
    
    "command" {
        if (-not $Arg1) {
            Write-Host "Usage: .\ari_client.ps1 command <asterisk-cli-command>"
            exit 1
        }
        Write-Host "=== Executing: $Arg1 ===" -ForegroundColor Cyan
        # ARI doesn't directly execute CLI commands, but we can use it indirectly
        # For direct CLI access, we'd need SSH or AMI
        Write-Host "Note: ARI doesn't support direct CLI commands." -ForegroundColor Yellow
        Write-Host "Use AMI (port 5038) or SSH for CLI commands." -ForegroundColor Yellow
    }
    
    "log" {
        Write-Host "=== Recent Events ===" -ForegroundColor Cyan
        $events = Invoke-ARI -Path "/ari/events?count=20"
        if ($events) {
            foreach ($event in $events) {
                Write-Host "[$($event.timestamp)] $($event.type): $($event.message)"
            }
        }
    }
    
    "dialplan" {
        Write-Host "=== Dialplan Contexts ===" -ForegroundColor Cyan
        $contexts = Invoke-ARI -Path "/ari/dialplans"
        if ($contexts) {
            foreach ($ctx in $contexts) {
                Write-Host "Context: $($ctx.name)"
            }
        }
    }
}
