# Git-based Agent Messenger
# Отправка и получение сообщений через Git

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("send", "check", "log")]
    [string]$Action,
    
    [string]$Command,
    [string]$Message,
    [string]$From = "sip-client",
    [string]$To = "asterisk"
)

$AgentChatDir = "$PSScriptRoot"
$CommandsDir = "$AgentChatDir\COMMANDS"
$ResponsesDir = "$AgentChatDir\RESPONSES"
$LogDir = "$AgentChatDir\LOG"

# Создать директории
foreach ($dir in @($CommandsDir, $ResponsesDir, $LogDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

function Send-AgentMessage {
    param([string]$Command, [string]$Message, [string]$From, [string]$To)
    
    $timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    $filename = "$Command-$(Get-Date -Format 'yyyyMMdd-HHmmss').md"
    
    $content = @"
---
command: $Command
from: $From
to: $To
timestamp: $timestamp
status: pending
---

$Message
"@
    
    $targetDir = if ($From -eq "sip-client") { $CommandsDir } else { $ResponsesDir }
    Set-Content -Path "$targetDir\$filename" -Value $content
    
    Write-Host "Message sent: $filename" -ForegroundColor Green
    Write-Host "  From: $From"
    Write-Host "  To: $To"
    Write-Host "  Command: $Command"
    
    # Auto-commit
    Set-Location (Split-Path $PSScriptRoot)
    git add ".agent_chat/"
    git commit -m "agent: $From -> $To : $Command"
    Write-Host "Committed to git" -ForegroundColor Yellow
}

function Get-AgentMessages {
    param([string]$For)
    
    $dir = if ($For -eq "sip-client") { $ResponsesDir } else { $CommandsDir }
    
    if (-not (Test-Path $dir)) {
        Write-Host "No messages found" -ForegroundColor Yellow
        return
    }
    
    $files = Get-ChildItem -Path $dir -Filter "*.md" | Sort-Object LastWriteTime
    
    if ($files.Count -eq 0) {
        Write-Host "No messages found" -ForegroundColor Yellow
        return
    }
    
    Write-Host "=== Messages for $For ===" -ForegroundColor Cyan
    foreach ($file in $files) {
        $content = Get-Content $file.FullName -Raw
        Write-Host ""
        Write-Host "--- $($file.Name) ---" -ForegroundColor Green
        Write-Host $content
    }
}

function Show-CommunicationLog {
    $logFile = "$LogDir\communication.md"
    if (Test-Path $logFile) {
        Write-Host "=== Communication Log ===" -ForegroundColor Cyan
        Get-Content $logFile
    } else {
        Write-Host "No communication log found" -ForegroundColor Yellow
    }
}

switch ($Action) {
    "send" {
        if (-not $Command -or -not $Message) {
            Write-Host "Usage: .\git_messenger.ps1 send -Command <command> -Message <message>"
            exit 1
        }
        Send-AgentMessage -Command $Command -Message $Message -From $From -To $To
    }
    "check" {
        Get-AgentMessages -For $From
    }
    "log" {
        Show-CommunicationLog
    }
}
