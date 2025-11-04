#!/usr/bin/env bash
#
# start-emulators.sh - one-stop launcher for Honua cloud emulators
#
# Usage:
#   ./scripts/start-emulators.sh up       # start or refresh all emulators
#   ./scripts/start-emulators.sh down     # stop and remove emulator containers
#   ./scripts/start-emulators.sh restart  # restart emulators
#   ./scripts/start-emulators.sh status   # show container status
#
# Optional environment variables:
#   HONUA_EMULATOR_COMPOSE   Override docker compose file path.
#   HONUA_EMULATOR_TIMEOUT   Seconds to wait for readiness (default 120).

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${HONUA_EMULATOR_COMPOSE:-${ROOT_DIR}/scripts/emulators/docker-compose.emulators.yml}"
TIMEOUT="${HONUA_EMULATOR_TIMEOUT:-120}"
WAIT_SCRIPT="${ROOT_DIR}/scripts/wait-for-emulators.sh"

blue='\033[0;34m'
green='\033[0;32m'
yellow='\033[1;33m'
red='\033[0;31m'
nc='\033[0m'

log() {
    local color="$1"; shift
    echo -e "${color}[emulators]${nc} $*"
}

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        log "$red" "Missing required command: $1"
        exit 1
    }
}

compose() {
    docker compose -f "$COMPOSE_FILE" "$@"
}

ensure_compose_file() {
    if [[ ! -f "$COMPOSE_FILE" ]]; then
        log "$red" "Docker compose file not found: $COMPOSE_FILE"
        exit 1
    }
}

run_wait_script() {
    if [[ ! -f "$WAIT_SCRIPT" ]]; then
        log "$yellow" "Wait script not found (${WAIT_SCRIPT}); skipping readiness probe."
        return
    fi
    log "$blue" "Waiting for emulators to report healthy (timeout ${TIMEOUT}s)..."
    HONUA_TIMEOUT="$TIMEOUT" "$WAIT_SCRIPT" "$TIMEOUT"
}

cmd_up() {
    ensure_compose_file
    log "$blue" "Starting emulator stack (compose file: $COMPOSE_FILE)..."
    compose pull >/dev/null 2>&1 || true
    compose up -d --remove-orphans
    run_wait_script
    log "$green" "Emulators ready. LocalStack http://localhost:4566  • Azurite http://localhost:10000  • GCS http://localhost:4443  • PostGIS localhost:15432"
}

cmd_down() {
    ensure_compose_file
    log "$blue" "Stopping emulator stack..."
    compose down -v
    log "$green" "Emulators stopped."
}

cmd_status() {
    ensure_compose_file
    compose ps
}

case "${1:-up}" in
    up)
        require_command docker
        cmd_up
        ;;
    down|stop)
        require_command docker
        cmd_down
        ;;
    restart)
        require_command docker
        cmd_down
        cmd_up
        ;;
    status|ps)
        require_command docker
        cmd_status
        ;;
    *)
        cat <<EOF
Usage: $(basename "$0") [up|down|restart|status]

Environment:
  HONUA_EMULATOR_COMPOSE   Override docker compose file path.
  HONUA_EMULATOR_TIMEOUT   Seconds to wait for readiness (default 120).
EOF
        exit 1
        ;;
esac
