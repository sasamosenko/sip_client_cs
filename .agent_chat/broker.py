"""
Agent Chat Broker — HTTP-сервер для общения между AI-агентами.

Запуск:
    python broker.py

Эндпоинты:
    POST /send                     — отправить сообщение
    GET  /recv?to=<agent_name>     — получить входящие
    GET  /all                      — все сообщения
    GET  /health                   — проверка работоспособности

Примеры (curl):
    curl http://localhost:9999/health
    curl -X POST http://localhost:9999/send -H "Content-Type: application/json" -d "{\"from\":\"agent_a\",\"to\":\"agent_b\",\"message\":\"hello\"}"
    curl "http://localhost:9999/recv?to=agent_b"
"""

from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs
from datetime import datetime, timezone
import json
import sys
import os

LOG_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "chat.log")
messages = []


def log(msg):
    ts = datetime.now().strftime("%H:%M:%S")
    line = f"[{ts}] {msg}"
    print(line, flush=True)
    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(line + "\n")


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        pass

    def _json(self, data, status=200):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def do_GET(self):
        path = urlparse(self.path)
        params = parse_qs(path.query)

        if path.path == "/health":
            self._json({"status": "ok", "messages": len(messages)})

        elif path.path == "/recv":
            to = params.get("to", [None])[0]
            if not to:
                self._json({"error": "use /recv?to=<agent_name>"}, 400)
                return
            incoming = [m for m in messages if m["to"] == to]
            self._json({"messages": incoming})

        elif path.path == "/all":
            self._json({"messages": messages})

        else:
            self._json({"error": "not found"}, 404)

    def do_POST(self):
        path = urlparse(self.path)

        if path.path == "/send":
            length = int(self.headers.get("Content-Length", 0))
            raw = self.rfile.read(length).decode("utf-8") if length else "{}"
            data = json.loads(raw)

            fr = data.get("from", "")
            to = data.get("to", "")
            msg = data.get("message", "")

            if not fr or not to or not msg:
                self._json({"error": "fields required: from, to, message"}, 400)
                return

            entry = {
                "id": len(messages) + 1,
                "from": fr,
                "to": to,
                "message": msg,
                "time": datetime.now(timezone.utc).isoformat(),
            }
            messages.append(entry)

            log(f"MSG [{fr} -> {to}]: {msg}")
            self._json({"ok": True, "id": entry["id"]})

        else:
            self._json({"error": "not found"}, 404)


def main():
    host = "0.0.0.0"
    port = 9999

    if len(sys.argv) > 1:
        port = int(sys.argv[1])

    server = HTTPServer((host, port), Handler)

    print("=" * 60)
    print("  Agent Chat Broker")
    print("=" * 60)
    print(f"  Listening: http://{host}:{port}")
    print()
    print("  Endpoints:")
    print("    POST /send              {from, to, message}")
    print("    GET  /recv?to=<agent>   incoming messages")
    print("    GET  /all               all messages")
    print("    GET  /health            health check")
    print()
    print("  Messages will appear below:")
    print("-" * 60)
    print(flush=True)

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down.")
        server.server_close()


if __name__ == "__main__":
    main()
