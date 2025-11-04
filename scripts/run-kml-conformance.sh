#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <path-to-kml-or-kmz>" >&2
  exit 1
fi

INPUT_PATH="$1"
if [[ ! -f "$INPUT_PATH" ]]; then
  echo "File not found: $INPUT_PATH" >&2
  exit 1
fi

REPORT_ROOT="${REPORT_ROOT:-qa-report}"
ETS_VERSION="${ETS_VERSION:-latest}"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUTPUT_DIR="${REPORT_ROOT}/kml-${TIMESTAMP}"
mkdir -p "$OUTPUT_DIR"

ABS_INPUT="$(realpath "$INPUT_PATH")"
MOUNT_DIR="$(dirname "$ABS_INPUT")"
INPUT_FILE="$(basename "$ABS_INPUT")"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required to run the KML conformance tests." >&2
  exit 1
fi

get_free_port() {
  python - <<'PY'
import socket
import random
for _ in range(20):
    port = random.randint(20000, 40000)
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        try:
            s.bind(('127.0.0.1', port))
        except OSError:
            continue
        else:
            print(port)
            break
else:
    raise SystemExit('Unable to find a free TCP port')
PY
}

PORT=$(get_free_port)
CONTAINER="kml-ets-${TIMESTAMP}"
DOCKER_IMAGE="ogccite/ets-kml22:${ETS_VERSION}"

cleanup() {
  docker stop "$CONTAINER" >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "Starting ${DOCKER_IMAGE} as ${CONTAINER} (port ${PORT})"
docker run -d --rm --name "$CONTAINER" -p "${PORT}:8080" -v "${MOUNT_DIR}:/data:ro" "$DOCKER_IMAGE" >/dev/null

READY=0
for _ in {1..30}; do
  if curl -s "http://localhost:${PORT}/teamengine/" >/dev/null; then
    READY=1
    break
  fi
  sleep 2
done

if [[ $READY -eq 0 ]]; then
  echo "TEAM Engine did not become ready on port ${PORT}." >&2
  exit 1
fi

BODY="<testRunRequest xmlns='http://teamengine.sourceforge.net/ctl'><entry><string>iut</string><string>file:/data/${INPUT_FILE}</string></entry></testRunRequest>"
echo "Submitting test run request..."
curl -sSf -u ogctest:ogctest -H 'Content-Type: application/xml' -d "$BODY" "http://localhost:${PORT}/teamengine/rest/suites/kml22/run" > "${OUTPUT_DIR}/earl-results.rdf"

if grep -q 'earl#failed' "${OUTPUT_DIR}/earl-results.rdf"; then
  echo "KML conformance suite reported failures (see ${OUTPUT_DIR}/earl-results.rdf)" >&2
  exit 1
fi

echo "Conformance results stored in ${OUTPUT_DIR}"
