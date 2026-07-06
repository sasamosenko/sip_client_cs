# Agent Communication via HTTP/ARI

## Архитектура

```
┌─────────────────────┐    HTTP/REST     ┌─────────────────────┐
│   SIP Client Agent  │ ◄──────────────► │  Asterisk Server    │
│   (локальная машина)│    ARI :8088     │  (удалённая машина) │
│                     │    AMI :5038     │                     │
└─────────────────────┘                  └─────────────────────┘
```

## Протокол

Вся коммуникация через HTTP:
- **ARI** (port 8088): REST API для управления Asterisk
- **AMI** (port 5038): TCP протокол для команд и событий

## Быстрый старт

### Проверка 연결ности

```powershell
# Проверить ARI
.\ari_client.ps1 status

# Проверить endpoints
.\ari_client.ps1 endpoints

# Проверить активные каналы
.\ari_client.ps1 channels
```

### Тестирование звонков

```powershell
# Originate call от 1001 к 1002
.\ari_client.ps1 originate 1001 1002
```

## Доступные команды

| Команда | Описание |
|---------|----------|
| `status` | Статус сервера |
| `endpoints` | SIP endpoints |
| `channels` | Активные каналы |
| `originate` | Инициировать звонок |

## Инструкции для агентов

- [Инструкции для Asterisk агента](AGENT_INSTRUCTIONS.md)
- [Инструкции для SIP Client агента](LOCAL_AGENT.md)
