# Инструкция для удалённого агента (Asterisk Server)

## Что это

Это инструкция для AI-агента (Claude Code / MiMoCode), работающего на машине `asterisk.ss.local`. Ты управляешь Docker-контейнером Asterisk.

## Как общаться

На **локальной машине** (192.168.3.199) запущен HTTP-брокер на порту **9999**.

### Отправить сообщение

```bash
curl -X POST http://192.168.3.199:9999/send \
  -H "Content-Type: application/json" \
  -d '{"from":"asterisk_agent","to":"sip_client","message":"Текст сообщения"}'
```

### Получить входящие

```bash
curl "http://192.168.3.199:9999/recv?to=asterisk_agent"
```

### Проверить все сообщения

```bash
curl http://192.168.3.199:9999/all
```

### Проверить здоровье брокера

```bash
curl http://192.168.3.199:9999/health
```

## Твоё имя в чате

```
asterisk_agent
```

## Протокол общения

### When you receive a message

1. Проверяй входящие: `curl "http://192.168.3.199:9999/recv?to=asterisk_agent"`
2. Обрабатывай команды
3. Отвечай результатом

### When you need to ask something

1. Отправь сообщение с `from: "asterisk_agent"`, `to: "sip_client"`
2. SIP Client Agent ответит через брокер

## Доступные команды от sip_client

| Команда | Действие |
|---------|----------|
| `check_docker` | Проверить статус Docker |
| `build_image` | Собрать Docker-образ Asterisk |
| `start_container` | Запустить контейнер |
| `stop_container` | Остановить контейнер |
| `restart_asterisk` | Перезапустить Asterisk внутри контейнера |
| `get_status` | Получить статус сервера |
| `get_logs` | Получить логи Asterisk |
| `check_registration` | Проверить регистрацию SIP-клиентов |

## Формат ответа

```json
{
  "from": "asterisk_agent",
  "to": "sip_client",
  "message": "Команда check_docker: Docker запущен, контейнер asterisk-sip активен."
}
```

## Пример диалога

```
sip_client:  Привет! Проверь Docker.
asterisk_agent: Docker работает. Контейнер asterisk-sip: running, порты 5060,5038,8088.
sip_client:  Originate звонок 1001 -> 1002.
asterisk_agent: Звонок initiated. Канал PJSIP/1002 -> default/1002.
sip_client:  Спасибо!
```

## Важно

- Брокер запущен на **192.168.3.199:9999** (локальная машина)
- Ты на **asterisk.ss.local** (удалённая машина)
- Используй `curl` для HTTP-запросов
- Always respond to commands from sip_client
- Send status updates periodically
