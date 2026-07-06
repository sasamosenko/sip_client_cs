# Asterisk Server - AI Agent Connection Guide

## Overview

Asterisk SIP server for AI agent management via AMI (Asterisk Manager Interface) and ARI (Asterisk REST Interface).

## Server Configuration

| Parameter | Value |
|-----------|-------|
| Host | `asterisk.ss.local` or server IP |
| SIP Port | `5060/udp` |
| AMI Port | `5038/tcp` |
| ARI Port | `8088/tcp` |
| RTP Range | `10000-20000/udp` |

## AMI Connection

### Credentials

| Parameter | Value |
|-----------|-------|
| Username | `admin` |
| Password | `admin123` |
| Port | `5038` |

### Connect via CLI

```bash
# Test AMI connection
nc -zv asterisk.ss.local 5038

# Connect with netcat
nc asterisk.ss.local 5038
```

### AMI Protocol (raw)

```
Action: Login
Username: admin
Secret: admin123
Events: off
```

### Python AMI Client

```python
import socket

def send_ami_command(host, port, username, password, command):
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.connect((host, port))
    
    # Login
    login_msg = (
        "Action: Login\r\n"
        f"Username: {username}\r\n"
        f"Secret: {password}\r\n"
        "Events: off\r\n\r\n"
    )
    sock.send(login_msg.encode())
    
    # Read response
    response = sock.recv(4096).decode()
    
    # Send command
    sock.send((command + "\r\n\r\n").encode())
    response = sock.recv(4096).decode()
    
    sock.close()
    return response

# Example usage
result = send_ami_command(
    "asterisk.ss.local", 5038,
    "admin", "admin123",
    "Action: CoreShowChannels"
)
print(result)
```

## ARI Connection

### Credentials

| Parameter | Value |
|-----------|-------|
| URL | `http://asterisk.ss.local:8088` |
| Username | `admin` |
| Password | `admin123` |

### REST API Examples

```bash
# List channels
curl -u admin:admin123 http://asterisk.ss.local:8088/channels

# List endpoints
curl -u admin:admin123 http://asterisk.ss.local:8088/endpoints

# List bridges
curl -u admin:admin123 http://asterisk.ss.local:8088/bridges

# Originate a call
curl -u admin:admin123 -X POST http://asterisk.ss.local:8088/channels \
  -d "endpoint=sip:1002" \
  -d "context=default" \
  -d "extension=1002"
```

## SIP Accounts

| Extension | Username  | Password  |
|-----------|-----------|-----------|
| 1001      | testuser1 | pass1001  |
| 1002      | testuser2 | pass1002  |

## Quick Commands

```bash
# Check AMI port
nc -zv asterisk.ss.local 5038

# Check ARI port
curl -s -o /dev/null -w "%{http_code}" http://asterisk.ss.local:8088

# Show endpoints via docker
docker exec asterisk-sip asterisk -rvvvx "pjsip show endpoints"

# Show channels
docker exec asterisk-sip asterisk -rvvvx "core show channels"
```

## Firewall Requirements

| Port | Protocol | Purpose |
|------|----------|---------|
| 5060 | UDP | SIP signaling |
| 5038 | TCP | AMI management |
| 8088 | TCP | ARI REST API |
| 10000-20000 | UDP | RTP media |
