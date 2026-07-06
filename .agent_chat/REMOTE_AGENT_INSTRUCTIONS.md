# Инструкции для удалённого агента (Asterisk Server)

## Назначение

Ты — агент, работающий на машине `asterisk.ss.local`. Твоя задача:
- Собирать Docker-образ Asterisk
- Настраивать и запускать контейнер
- Управлять SIP-аккаунтами
- Предоставлять API для общения с локальным агентом (SIP Client)

## Архитектура

```
┌─────────────────────────────────────────────────────────────────┐
│                    asterisk.ss.local                             │
│                                                                 │
│  ┌──────────────┐     ┌──────────────┐     ┌──────────────┐   │
│  │  Docker       │     │  Asterisk    │     │  Agent API   │   │
│  │  Container    │────►│  SIP Server  │     │  (HTTP :5000)│   │
│  │               │     │  :5060 UDP   │     │              │   │
│  └──────────────┘     └──────────────┘     └──────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ HTTP
                              │
┌─────────────────────────────┴───────────────────────────────────┐
│                    Локальная машина                              │
│                                                                 │
│  ┌──────────────┐     ┌──────────────┐                         │
│  │  SIP Client  │     │  Локальный   │                         │
│  │  Agent       │────►│  Агент       │                         │
│  │  (тестирует) │     │  (управляет) │                         │
│  └──────────────┘     └──────────────┘                         │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Требования к API

### Эндпоинты

Ты ДОЛЖЕН предоставить HTTP API на порту 5000:

| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/api/health` | Проверка работоспособности |
| GET | `/api/status` | Статус сервера |
| POST | `/api/docker/build` | Собрать Docker-образ |
| POST | `/api/docker/start` | Запустить контейнер |
| POST | `/api/docker/stop` | Остановить контейнер |
| GET | `/api/docker/containers` | Список контейнеров |
| GET | `/api/sip/accounts` | SIP аккаунты |
| POST | `/api/sip/accounts` | Создать SIP аккаунт |
| POST | `/api/asterisk/restart` | Перезапустить Asterisk |
| GET | `/api/logs` | Получить логи |

### Формат запросов/ответов

Все ответы в JSON:

```json
{
  "success": true,
  "data": { ... },
  "error": null
}
```

### Аутентификация

API-ключ в заголовке:
```
X-API-Key: <your-secret-key>
```

## Реализация API

Создай файл `agent_api.py`:

```python
from flask import Flask, request, jsonify
import subprocess
import docker

app = Flask(__name__)
API_KEY = "asterisk-agent-secret-key"

def check_auth():
    return request.headers.get('X-API-Key') == API_KEY

@app.route('/api/health', methods=['GET'])
def health():
    return jsonify({"status": "ok", "agent": "asterisk"})

@app.route('/api/status', methods=['GET'])
def status():
    if not check_auth(): return jsonify({"error": "unauthorized"}), 401
    
    # Проверить статус Docker
    client = docker.from_env()
    containers = client.containers.list(all)
    
    return jsonify({
        "success": True,
        "data": {
            "docker_running": True,
            "containers": [{"name": c.name, "status": c.status} for c in containers],
            "host": "asterisk.ss.local"
        }
    })

@app.route('/api/docker/build', methods=['POST'])
def docker_build():
    if not check_auth(): return jsonify({"error": "unauthorized"}), 401
    
    # Собрать образ Asterisk
    result = subprocess.run(
        ["docker", "build", "-t", "asterisk-sip", "."],
        capture_output=True, text=True, cwd="/path/to/asterisk-config"
    )
    
    return jsonify({
        "success": result.returncode == 0,
        "output": result.stdout,
        "error": result.stderr if result.returncode != 0 else None
    })

@app.route('/api/docker/start', methods=['POST'])
def docker_start():
    if not check_auth(): return jsonify({"error": "unauthorized"}), 401
    
    client = docker.from_env()
    container = client.containers.run(
        "asterisk-sip",
        detach=True,
        name="asterisk-sip",
        ports={
            '5060/udp': 5060,
            '5038/tcp': 5038,
            '8088/tcp': 8088
        },
        volumes={
            '/path/to/config': {'bind': '/etc/asterisk', 'mode': 'ro'}
        }
    )
    
    return jsonify({"success": True, "container_id": container.id})

@app.route('/api/docker/stop', methods=['POST'])
def docker_stop():
    if not check_auth(): return jsonify({"error": "unauthorized"}), 401
    
    client = docker.from_env()
    container = client.containers.get("asterisk-sip")
    container.stop()
    
    return jsonify({"success": True})

@app.route('/api/docker/containers', methods=['GET'])
def docker_containers():
    if not check_auth(): return jsonify({"error": "unauthorized"}), 401
    
    client = docker.from_env()
    containers = client.containers.list(all)
    
    return jsonify({
        "success": True,
        "data": [
            {"name": c.name, "status": c.status, "image": c.image.tags}
            for c in containers
        ]
    })

@app.route('/api/sip/accounts', methods=['GET'])
def sip_accounts():
    if not check_auth(): return jsonify({"error": "unauthorized"}), 401
    
    # Прочитать pjsip.conf
    with open('/etc/asterisk/pjsip.conf', 'r') as f:
        config = f.read()
    
    return jsonify({"success": True, "data": {"config": config}})

@app.route('/api/asterisk/restart', methods=['POST'])
def asterisk_restart():
    if not check_auth(): return jsonify({"error": "unauthorized"}), 401
    
    result = subprocess.run(
        ["docker", "exec", "asterisk-sip", "asterisk", "-rx", "core restart now"],
        capture_output=True, text=True
    )
    
    return jsonify({"success": result.returncode == 0, "output": result.stdout})

@app.route('/api/logs', methods=['GET'])
def logs():
    if not check_auth(): return jsonify({"error": "unauthorized"}), 401
    
    result = subprocess.run(
        ["docker", "exec", "asterisk-sip", "tail", "-100", "/var/log/asterisk/full"],
        capture_output=True, text=True
    )
    
    return jsonify({"success": True, "data": result.stdout})

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000)
```

## Запуск API

```bash
# Установить зависимости
pip install flask docker

# Запустить API
python agent_api.py
```

## Файловая структура

```
/path/to/asterisk-config/
├── Dockerfile
├── docker-compose.yml
├── agent_api.py
├── pjsip.conf
├── extensions.conf
└── modules.conf
```

## Dockerfile для Asterisk

```dockerfile
FROM ubuntu:22.04

RUN apt-get update && apt-get install -y \
    asterisk \
    asterisk-core-sounds-en \
    asterisk-core-sounds-ru \
    && rm -rf /var/lib/apt/lists/*

COPY pjsip.conf /etc/asterisk/
COPY extensions.conf /etc/asterisk/
COPY modules.conf /etc/asterisk/

EXPOSE 5060/udp 5038/tcp 8088/tcp

CMD ["asterisk", "-f"]
```

## Важно

1. **API обязан работать на порту 5000**
2. **Аутентификация через X-API-Key**
3. **Все ответы в JSON формате**
4. **Docker-контейнер должен быть доступен из внешней сети**
