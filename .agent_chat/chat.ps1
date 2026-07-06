# Agent Chat Helper Script
# Использование: .\chat.ps1 send asterisk "Проверь регистрацию"
# Использование: .\chat.ps1 poll sip-client

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("send", "poll", "read", "clear")]
    [string]$Action,
    
    [string]$To,
    [string]$Message,
    [string]$From = "sip-client"
)

$ChatDir = "$PSScriptRoot"
$ProcessedDir = "$ChatDir\processed"

# Создать директорию для обработанных
if (-not (Test-Path $ProcessedDir)) {
    New-Item -ItemType Directory -Path $ProcessedDir | Out-Null
}

function Send-ChatMessage {
    param([string]$To, [string]$Message, [string]$From)
    
    $timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    $id = "msg-" + (Get-Date).ToString("yyyyMMddHHmmss")
    
    $content = @"
---
from: $From
to: $To
timestamp: $timestamp
type: chat
id: $id
---

$Message
"@
    
    $filePath = "$ChatDir\$id.md"
    Set-Content -Path $filePath -Value $content
    Write-Host "Sent: $id -> $To"
}

function Get-ChatMessages {
    param([string]$For)
    
    $messages = Get-ChildItem -Path $ChatDir -Filter "msg-*.md" | 
        Where-Object { $_.Name -notlike "*.processed.*" } |
        Sort-Object LastWriteTime
    
    foreach ($msg in $messages) {
        $content = Get-Content $msg.FullName -Raw
        if ($content -match "to:\s*$For") {
            Write-Host "=== $($msg.Name) ==="
            Write-Host $content
            Write-Host ""
        }
    }
}

function Read-ChatMessage {
    param([string]$Path)
    
    $content = Get-Content $Path -Raw
    Write-Host $content
    
    # Переместить в обработанные
    $destPath = "$ProcessedDir\$($Path | Split-Path -Leaf)"
    Move-Item -Path $Path -Destination $destPath -Force
}

switch ($Action) {
    "send" {
        if (-not $To -or -not $Message) {
            Write-Host "Usage: .\chat.ps1 send <to> <message>"
            exit 1
        }
        Send-ChatMessage -To $To -Message $Message -From $From
    }
    "poll" {
        if (-not $To) {
            $To = $From
        }
        Get-ChatMessages -For $To
    }
    "read" {
        # Прочитать все непрочитанные
        Get-ChildItem -Path $ChatDir -Filter "msg-*.md" | 
            Where-Object { $_.Name -notlike "*.processed.*" } |
            ForEach-Object {
                $content = Get-Content $_.FullName -Raw
                if ($content -match "to:\s*$From") {
                    Write-Host "=== $($_.Name) ==="
                    Write-Host $content
                    Write-Host ""
                    Move-Item -Path $_.FullName -Destination "$ProcessedDir\$($_.Name)" -Force
                }
            }
    }
    "clear" {
        Remove-Item "$ChatDir\msg-*.md" -Force -ErrorAction SilentlyContinue
        Write-Host "Chat cleared"
    }
}
