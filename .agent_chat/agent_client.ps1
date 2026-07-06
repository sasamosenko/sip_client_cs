# Agent Communication Client
# Общение с удалённым агентом на asterisk.ss.local

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("health", "status", "build", "start", "stop", "containers", "sip-accounts", "restart", "logs")]
    [string]$Action,
    
    [string]$ApiKey = "asterisk-agent-secret-key"
)

$AGENT_URL = "http://asterisk.ss.local:5000"

function Invoke-AgentAPI {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null
    )
    
    $uri = "$AGENT_URL$Path"
    $headers = @{
        "X-API-Key" = $ApiKey
        "Accept" = "application/json"
    }
    
    $params = @{
        Uri = $uri
        Method = $Method
        Headers = $headers
        UseBasicParsing = $true
        TimeoutSec = 30
    }
    
    if ($Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 10)
        $params.ContentType = "application/json"
    }
    
    try {
        $response = Invoke-WebRequest @params
        return $response.Content | ConvertFrom-Json
    } catch {
        $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode } else { "ConnectionError" }
        Write-Host "Error ($statusCode): $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

switch ($Action) {
    "health" {
        Write-Host "=== Agent Health Check ===" -ForegroundColor Cyan
        $result = Invoke-AgentAPI -Path "/api/health"
        if ($result) {
            Write-Host "Status: $($result.status)" -ForegroundColor Green
            Write-Host "Agent: $($result.agent)"
        }
    }
    
    "status" {
        Write-Host "=== Agent Status ===" -ForegroundColor Cyan
        $result = Invoke-AgentAPI -Path "/api/status"
        if ($result -and $result.success) {
            Write-Host "Docker Running: $($result.data.docker_running)" -ForegroundColor $(if($result.data.docker_running){"Green"}else{"Red"})
            Write-Host "Containers:"
            foreach ($c in $result.data.containers) {
                $color = if ($c.status -eq "running") { "Green" } else { "Yellow" }
                Write-Host "  [$($c.status)] $($c.name)" -ForegroundColor $color
            }
        }
    }
    
    "build" {
        Write-Host "=== Building Docker Image ===" -ForegroundColor Cyan
        $result = Invoke-AgentAPI -Method "POST" -Path "/api/docker/build"
        if ($result) {
            if ($result.success) {
                Write-Host "Build successful!" -ForegroundColor Green
                Write-Host $result.output
            } else {
                Write-Host "Build failed:" -ForegroundColor Red
                Write-Host $result.error
            }
        }
    }
    
    "start" {
        Write-Host "=== Starting Container ===" -ForegroundColor Cyan
        $result = Invoke-AgentAPI -Method "POST" -Path "/api/docker/start"
        if ($result -and $result.success) {
            Write-Host "Container started: $($result.container_id)" -ForegroundColor Green
        }
    }
    
    "stop" {
        Write-Host "=== Stopping Container ===" -ForegroundColor Cyan
        $result = Invoke-AgentAPI -Method "POST" -Path "/api/docker/stop"
        if ($result -and $result.success) {
            Write-Host "Container stopped" -ForegroundColor Green
        }
    }
    
    "containers" {
        Write-Host "=== Docker Containers ===" -ForegroundColor Cyan
        $result = Invoke-AgentAPI -Path "/api/docker/containers"
        if ($result -and $result.success) {
            foreach ($c in $result.data) {
                $color = if ($c.status -eq "running") { "Green" } else { "Yellow" }
                Write-Host "[$($c.status)] $($c.name) - $($c.image)" -ForegroundColor $color
            }
        }
    }
    
    "sip-accounts" {
        Write-Host "=== SIP Accounts ===" -ForegroundColor Cyan
        $result = Invoke-AgentAPI -Path "/api/sip/accounts"
        if ($result -and $result.success) {
            Write-Host $result.data.config
        }
    }
    
    "restart" {
        Write-Host "=== Restarting Asterisk ===" -ForegroundColor Cyan
        $result = Invoke-AgentAPI -Method "POST" -Path "/api/asterisk/restart"
        if ($result -and $result.success) {
            Write-Host "Asterisk restarted" -ForegroundColor Green
            Write-Host $result.output
        }
    }
    
    "logs" {
        Write-Host "=== Asterisk Logs ===" -ForegroundColor Cyan
        $result = Invoke-AgentAPI -Path "/api/logs"
        if ($result -and $result.success) {
            Write-Host $result.data
        }
    }
}
