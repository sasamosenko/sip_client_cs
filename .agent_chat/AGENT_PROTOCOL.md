# Протокол общения между AI-агентами через Git

## Архитектура

```
┌─────────────────────────┐         Git          ┌─────────────────────────┐
│   Локальный агент       │ ◄─────────────────► │   Удалённый агент       │
│   (SIP Client Agent)    │   shared repo        │   (Asterisk Agent)      │
│                         │                      │   asterisk.ss.local     │
│   - Тестирует SIP       │                      │   - Собирает Docker     │
│   - Анализирует логи    │                      │   - Настраивает Asterisk│
│   - Исправляет код      │                      │   - Управляет контейнером│
└─────────────────────────┘                      └─────────────────────────┘
```

## Протокол

### Структура файлов для общения

```
.agent_chat/
├── COMMANDS/              # Команды от локального агента
│   ├── build-asterisk.md  # Собрать Docker образ
│   ├── start-server.md    # Запустить контейнер
│   └── check-status.md    # Проверить статус
├── RESPONSES/             # Ответы от удалённого агента
│   ├── build-result.md    # Результат сборки
│   ├── start-result.md    # Результат запуска
│   └── status-result.md   # Статус сервера
└── LOG/                   # Логи общения
    └── communication.md   # Хронология общения
```

### Формат команды

```markdown
---
command: build-asterisk
from: sip-client
to: asterisk
timestamp: 2026-07-06T15:00:00Z
priority: high
status: pending
---

## Задача

Собрать Docker-образ Asterisk с PJSIP и ARI.

## Требования

- Ubuntu 22.04 базовый образ
- Asterisk с PJSIP
- ARI включен
- SIP порты: 5060/udp, 5038/tcp, 8088/tcp

## Ожидаемый результат

- Docker- образ `asterisk-sip:latest`
- Контейнер запущен и доступен
```

### Формат ответа

```markdown
---
command: build-asterisk
from: asterisk
to: sip-client
timestamp: 2026-07-06T15:05:00Z
status: completed|failed
---

## Результат

- Образ собран: `asterisk-sip:latest` (size: 450MB)
- Контейнер запущен: `asterisk-sip`
- Порты: 5060/udp, 5038/tcp, 8088/tcp

## Проверка

```bash
docker ps | grep asterisk
# asterisk-sip   latest   ...   Up 2 minutes   0.0.0.0:5060->5060/udp, ...
```
```

## Процесс общения

### 1. Локальный агент отправляет команду

```bash
# Создать файл команды
cat > .agent_chat/COMMANDS/build-asterisk.md << 'EOF'
---
command: build-asterisk
from: sip-client
to: asterisk
timestamp: 2026-07-06T15:00:00Z
status: pending
---

Собрать Docker-образ Asterisk
EOF

# Закоммитить
git add .agent_chat/COMMANDS/
git commit -m "agent: request build-asterisk"
git push
```

### 2. Удалённый агент проверяет новые команды

```bash
# Получить последние изменения
git pull

# Проверить новые команды
ls .agent_chat/COMMANDS/
# build-asterisk.md

# Прочитать команду
cat .agent_chat/COMMANDS/build-asterisk.md
```

### 3. Удалённый агент выполняет команду

```bash
# Собрать Docker образ
docker build -t asterisk-sip .

# Запустить контейнер
docker run -d --name asterisk-sip \
  -p 5060:5060/udp \
  -p 5038:5038 \
  -p 8088:8088 \
  asterisk-sip
```

### 4. Удалённый агент отправляет ответ

```bash
# Создать файл ответа
cat > .agent_chat/RESPONSES/build-result.md << 'EOF'
---
command: build-asterisk
from: asterisk
to: sip-client
timestamp: 2026-07-06T15:05:00Z
status: completed
---

Результат сборки:
- Образ: asterisk-sip:latest
- Контейнер: asterisk-sip (running)
- Порты: 5060/udp, 5038/tcp, 8088/tcp
EOF

# Закоммитить
git add .agent_chat/RESPONSES/
git commit -m "agent: response build-asterisk completed"
git push
```

### 5. Локальный агент получает ответ

```bash
# Получить последние изменения
git pull

# Проверить ответы
cat .agent_chat/RESPONSES/build-result.md
```

## Доступные команды

| Команда | Описание | Ожидаемый ответ |
|---------|----------|-----------------|
| `build-asterisk` | Собрать Docker образ | Результат сборки |
| `start-server` | Запустить контейнер | Статус запуска |
| `stop-server` | Остановить контейнер | Статус остановки |
| `restart-server` | Перезапустить Asterisk | Статус перезапуска |
| `check-status` | Проверить статус | Информация о сервере |
| `get-logs` | Получить логи | Текст логов |
| `configure-sip` | Настроить SIP аккаунты | Результат настройки |
| `test-connection` | Проверить подключение | Результат теста |

## Мониторинг

### Автоматическая проверка

Локальный агент проверяет ответы каждые N секунд:

```bash
# В скрипте или cron
while true; do
    git pull --quiet
    if [ -f .agent_chat/RESPONSES/new-response.md ]; then
        cat .agent_chat/RESPONSES/new-response.md
        # Обработать ответ
    fi
    sleep 30
done
```

### Логирование

Все команды и ответы логируются в `.agent_chat/LOG/communication.md`:

```markdown
# Communication Log

## 2026-07-06

### 15:00 - sip-client -> asterisk
Command: build-asterisk
Status: pending

### 15:05 - asterisk -> sip-client
Response: build-asterisk
Status: completed
Result: Image built, container started
```

## Важно

1. **Всегда проверяй `git pull` перед началом работы**
2. **Коммить с префиксом `agent:` для автоматической обработки**
3. **Удаляй обработанные команды из COMMANDS/**
4. **Логируй все действия в LOG/communication.md**
