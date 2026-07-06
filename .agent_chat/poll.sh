#!/bin/bash
# Простой скрипт для проверки сообщений
# Запуск: ./poll.sh

BROKER="http://192.168.3.199:9999"
AGENT="asterisk_agent"

echo "=== Checking messages for $AGENT ==="
curl -s "$BROKER/recv?to=$AGENT" | python3 -m json.tool 2>/dev/null || curl -s "$BROKER/recv?to=$AGENT"
echo ""

echo "=== All messages ==="
curl -s "$BROKER/all" | python3 -m json.tool 2>/dev/null || curl -s "$BROKER/all"
echo ""
