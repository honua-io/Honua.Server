#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'HELP'
Usage: run-wfs-conformance.sh [capabilities-url]

Runs the OGC WFS 2.0 executable test suite (ETS) against the provided
GetCapabilities document. You can pass the capabilities URL as an argument or
via the WFS_CAPABILITIES_URL environment variable. When the argument is not
supplied and WFS_CAPABILITIES_URL is unset, the script falls back to
HONUA_BASE_URL by appending the canonical WFS GetCapabilities request.

Environment variables:
  WFS_CAPABILITIES_URL   Fully-qualified WFS GetCapabilities URL.
  HONUA_BASE_URL         Base Honua URL (used as a fallback).
  ETS_VERSION            Docker tag for ogccite/ets-wfs20 (default: latest).
  REPORT_ROOT            Directory to store ETS output (default: qa-report).

Example:
  export HONUA_BASE_URL="https://localhost:5001"
  ./scripts/run-wfs-conformance.sh

HELP
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

CAPABILITIES_URL="${1:-}" 

if [[ -z "$CAPABILITIES_URL" ]]; then
  CAPABILITIES_URL="${WFS_CAPABILITIES_URL:-}"
fi

if [[ -z "$CAPABILITIES_URL" && -n "${HONUA_BASE_URL:-}" ]]; then
  base="${HONUA_BASE_URL%/}"
  CAPABILITIES_URL="${base}/wfs?service=WFS&request=GetCapabilities&version=2.0.0"
fi

if [[ -z "$CAPABILITIES_URL" ]]; then
  echo "WFS capabilities URL not provided. Pass it as an argument or set WFS_CAPABILITIES_URL or HONUA_BASE_URL." >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required to run the WFS conformance tests." >&2
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required to run this script." >&2
  exit 1
fi

REPORT_ROOT="${REPORT_ROOT:-qa-report}"
ETS_VERSION="${ETS_VERSION:-latest}"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUTPUT_DIR="${REPORT_ROOT}/wfs-${TIMESTAMP}"
mkdir -p "$OUTPUT_DIR"

# Reserve a random high port for the temporary TEAM Engine container
PORT="$(python3 - <<'PY'
import random
import socket

for _ in range(100):
    port = random.randint(20000, 40000)
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        try:
            sock.bind(('127.0.0.1', port))
        except OSError:
            continue
        else:
            print(port)
            break
else:
    raise SystemExit('Unable to locate an available local port')
PY
)"

CONTAINER="wfs-ets-${TIMESTAMP}"
DOCKER_IMAGE="ogccite/ets-wfs20:${ETS_VERSION}"

cleanup() {
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "Starting ${DOCKER_IMAGE} as ${CONTAINER} (port ${PORT})"
docker run -d --rm --name "$CONTAINER" -p "${PORT}:8080" "$DOCKER_IMAGE" >/dev/null

READY=0
for _ in {1..60}; do
  if curl -s -u ogctest:ogctest "http://localhost:${PORT}/teamengine/rest/" >/dev/null 2>&1; then
    READY=1
    break
  fi
  sleep 2
done

if [[ $READY -ne 1 ]]; then
  echo "TEAM Engine did not become ready on port ${PORT}." >&2
  exit 1
fi

ENCODED_CAPABILITIES="$(python3 - <<PY
import urllib.parse
print(urllib.parse.quote('${CAPABILITIES_URL}', safe=''))
PY
)"

RUN_URL="http://localhost:${PORT}/teamengine/rest/suites/wfs20/run?wfs=${ENCODED_CAPABILITIES}&format=xml"
RESULT_FILE="${OUTPUT_DIR}/wfs-conformance-response.xml"

echo "Executing WFS 2.0 ETS against ${CAPABILITIES_URL}"
HTTP_CODE=$(curl -s -u ogctest:ogctest -o "$RESULT_FILE" -w '%{http_code}' "$RUN_URL" || true)

if [[ -z "$HTTP_CODE" ]]; then
  echo "Failed to invoke the ETS run endpoint." >&2
  exit 1
fi

if [[ "$HTTP_CODE" -ge 400 ]]; then
  echo "WFS conformance suite reported an error (HTTP ${HTTP_CODE})." >&2
  echo "See ${RESULT_FILE} for details." >&2
  exit 1
fi

if grep -qi 'FAIL' "$RESULT_FILE"; then
  echo "WFS conformance suite reported failures. See ${RESULT_FILE} for details." >&2
  exit 1
fi

echo "Conformance results stored in ${OUTPUT_DIR}"
