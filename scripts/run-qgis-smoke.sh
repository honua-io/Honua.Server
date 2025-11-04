#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(git rev-parse --show-toplevel)
cd "$ROOT_DIR"
BASE_URL=${HONUA_QGIS_BASE_URL:-http://127.0.0.1:5010}
CONTAINER_IMAGE=${QGIS_CONTAINER_IMAGE:-qgis/qgis:3.44.3}
AUTH_STORE=${HONUA_QGIS_AUTH_STORE:-$ROOT_DIR/tmp/qgis-auth/auth.db}
ADMIN_USERNAME=${HONUA_QGIS_ADMIN_USERNAME:-qgis-admin}
ADMIN_PASSWORD=${HONUA_QGIS_ADMIN_PASSWORD:-QgisAdmin!123}
ADMIN_EMAIL=${HONUA_QGIS_ADMIN_EMAIL:-${ADMIN_USERNAME}@example.com}
METADATA_PATH=${HONUA_QGIS_METADATA_PATH:-$ROOT_DIR/tests/Honua.Server.Core.Tests/Data/metadata-ogc-sample.json}
RUNNER="container"
KEEP_ARTIFACTS=false

usage() {
  cat <<USAGE
Usage: $0 [options] [-- pytest-args]
Options:
  --base-url URL            Override Honua base URL (default: $BASE_URL)
  --container IMAGE         Use the specified QGIS Docker image (default: $CONTAINER_IMAGE)
  --no-container            Run tests using the current Python environment (requires PyQGIS)
  --keep-artifacts          Do not delete temporary publish directory on exit
  -h, --help                Show this help

Any additional arguments after "--" are forwarded to pytest.
USAGE
}

ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --base-url)
      BASE_URL=$2
      shift 2
      ;;
    --container)
      CONTAINER_IMAGE=$2
      shift 2
      ;;
    --no-container)
      RUNNER="local"
      shift
      ;;
    --keep-artifacts)
      KEEP_ARTIFACTS=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      ARGS=("$@")
      break
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

PUBLISH_DIR=$(mktemp -d -t honua-qgis-publish.XXXXXX)
SERVER_LOG=$PUBLISH_DIR/honua-server.log
SERVER_PID=""
PROXY_PID=""
PROXY_LOG=$PUBLISH_DIR/qgis-proxy.log
CONFIG_JSON=$PUBLISH_DIR/qgis-config.json

cleanup() {
  if [[ -n "$SERVER_PID" ]]; then
    kill "$SERVER_PID" 2>/dev/null || true
    wait "$SERVER_PID" 2>/dev/null || true
  fi
  if [[ -n "$PROXY_PID" ]]; then
    kill "$PROXY_PID" 2>/dev/null || true
    wait "$PROXY_PID" 2>/dev/null || true
  fi
  if [[ "$KEEP_ARTIFACTS" != "true" ]]; then
    rm -rf "$PUBLISH_DIR"
  else
    echo "Keeping artifacts in $PUBLISH_DIR"
  fi
}

on_exit() {
  status=$?
  if [[ $status -ne 0 ]]; then
    echo "QGIS smoke run failed (exit code $status). Logs:" >&2
    if [[ -f "$SERVER_LOG" ]]; then
      echo "--- Honua server log ---" >&2
      cat "$SERVER_LOG" >&2
    fi
    if [[ -f "$PROXY_LOG" ]]; then
      echo "--- Proxy log ---" >&2
      cat "$PROXY_LOG" >&2
    fi
  fi
  cleanup
  exit $status
}
trap on_exit EXIT

mkdir -p "$(dirname "$AUTH_STORE")"

dotnet publish src/Honua.Server.Host/Honua.Server.Host.csproj \
  --configuration Release \
  --output "$PUBLISH_DIR" >/dev/null

export HONUA__AUTHENTICATION__MODE=Local
export HONUA__AUTHENTICATION__ENFORCE=false
export HONUA__AUTHENTICATION__LOCAL__STOREPATH="$AUTH_STORE"
export HONUA__AUTHENTICATION__BOOTSTRAP__ADMINUSERNAME="$ADMIN_USERNAME"
export HONUA__AUTHENTICATION__BOOTSTRAP__ADMINEMAIL="$ADMIN_EMAIL"
export HONUA__AUTHENTICATION__BOOTSTRAP__ADMINPASSWORD="$ADMIN_PASSWORD"
export HONUA__METADATA__PROVIDER=json
export HONUA__METADATA__PATH="$METADATA_PATH"
export ASPNETCORE_ENVIRONMENT=Development

if [[ ! -f "$AUTH_STORE" ]]; then
  dotnet run --project src/Honua.Cli/Honua.Cli.csproj -- \
    auth bootstrap --mode Local >/dev/null
fi

SERVER_URL="$BASE_URL"
export ASPNETCORE_URLS="$SERVER_URL"

dotnet "$PUBLISH_DIR/Honua.Server.Host.dll" >"$SERVER_LOG" 2>&1 &
SERVER_PID=$!
sleep 1

for attempt in {1..40}; do
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$SERVER_URL/healthz/ready" || true)
  if [[ "$STATUS" == "200" ]]; then
    break
  fi
  if [[ "$attempt" -eq 40 ]]; then
    echo "Honua server failed to start; see $SERVER_LOG" >&2
    cat "$SERVER_LOG" >&2
    exit 1
  fi
  sleep 2
done

LOGIN_RESPONSE=$(curl -s -H "Content-Type: application/json" \
  -d "{\"username\":\"$ADMIN_USERNAME\",\"password\":\"$ADMIN_PASSWORD\"}" \
  "$SERVER_URL/api/auth/local/login")
TOKEN=$(python3 - <<PY
import json, sys
payload=json.loads("""$LOGIN_RESPONSE""")
print(payload.get("token", ""))
PY
)
if [[ -z "$TOKEN" ]]; then
  echo "Failed to obtain authentication token" >&2
  cat "$SERVER_LOG" >&2
  exit 1
fi

export HONUA_QGIS_BASE_URL="$SERVER_URL"
export HONUA_QGIS_BEARER="$TOKEN"

PROXY_PORT=$(python3 - <<'PY'
import socket
with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
    s.bind(('127.0.0.1', 0))
    print(s.getsockname()[1])
PY
)

python3 - <<PY >"$PROXY_LOG" 2>&1 &
import urllib.request
import urllib.parse
from http.server import BaseHTTPRequestHandler, HTTPServer
from socketserver import ThreadingMixIn

UPSTREAM = "$SERVER_URL"
if not UPSTREAM.endswith('/'):
    UPSTREAM += '/'
TOKEN = "$TOKEN"
PORT = int("$PROXY_PORT")

parsed = urllib.parse.urlparse(UPSTREAM)
UPSTREAM_BASE = parsed.scheme + '://' + parsed.netloc
UPSTREAM_PATH = parsed.path.rstrip('/')

class ThreadedHTTPServer(ThreadingMixIn, HTTPServer):
    daemon_threads = True

class ProxyHandler(BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.1'

    def log_message(self, *args, **kwargs):
        pass

    def do_GET(self):
        self._forward()

    def do_HEAD(self):
        self._forward(head_only=True)

    def _forward(self, head_only=False):
        path = urllib.parse.urljoin(UPSTREAM_PATH + '/', self.path.lstrip('/'))
        target = urllib.parse.urljoin(UPSTREAM_BASE, path)
        headers = {key: value for key, value in self.headers.items()
                   if key.lower() not in {'host', 'connection', 'content-length'}}
        headers['Authorization'] = f'Bearer {TOKEN}'
        req = urllib.request.Request(target, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=300) as resp:
                self.send_response(resp.status)
                for key, value in resp.getheaders():
                    if key.lower() in {'transfer-encoding', 'connection'}:
                        continue
                    self.send_header(key, value)
                self.end_headers()
                if not head_only:
                    while True:
                        chunk = resp.read(65536)
                        if not chunk:
                            break
                        self.wfile.write(chunk)
        except Exception as exc:  # pragma: no cover - best effort logging
            self.send_error(502, explain=str(exc))

ThreadedHTTPServer(('127.0.0.1', PORT), ProxyHandler).serve_forever()
PY
PROXY_PID=$!

export HONUA_QGIS_BASE_URL="http://127.0.0.1:$PROXY_PORT"

CMD=("./tests/qgis/run-qgis-tests.sh")
if [[ ${#ARGS[@]} -gt 0 ]]; then
  CMD+=("${ARGS[@]}")
fi

if [[ "$RUNNER" == "container" ]]; then
  if ! command -v docker >/dev/null 2>&1; then
    echo "Docker is required to run the QGIS smoke tests in container mode." >&2
    exit 1
  fi
  QUOTED_CMD=$(printf '%q ' "${CMD[@]}")
  docker run --rm \
    --network host \
    -e HONUA_QGIS_BASE_URL \
    -e HONUA_QGIS_BEARER \
    -e QT_QPA_PLATFORM=offscreen \
    -v "$ROOT_DIR":/workspace \
    -w /workspace \
    "$CONTAINER_IMAGE" \
    bash -lc "$QUOTED_CMD"
else
  export QT_QPA_PLATFORM=${QT_QPA_PLATFORM:-offscreen}
  "${CMD[@]}"
fi
