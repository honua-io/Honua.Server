#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${HONUA_BASE_URL:-}" ]]; then
  echo "HONUA_BASE_URL environment variable is required (e.g. https://localhost:5001)" >&2
  exit 1
fi

ETS_VERSION="${ETS_VERSION:-1.0.0}" # override to pin a different ETS release
DOCKER_IMAGE="ghcr.io/opengeospatial/ets-ogcapi-features10:${ETS_VERSION}"
REPORT_ROOT="${REPORT_ROOT:-qa-report}"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUTPUT_DIR="${REPORT_ROOT}/ogcfeatures-${TIMESTAMP}"
mkdir -p "${OUTPUT_DIR}"

TEST_SELECTION="${TEST_SELECTION:-confAll}"
ADDITIONAL_OPTS=( )
if [[ -n "${ETS_LOG_LEVEL:-}" ]]; then
  ADDITIONAL_OPTS+=("-Dorg.opengis.cite.logLevel=${ETS_LOG_LEVEL}")
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required to run the OGC conformance tests." >&2
  exit 1
fi

echo "Pulling ${DOCKER_IMAGE}..."
docker pull "${DOCKER_IMAGE}" >/dev/null

echo "Running OGC API Features ETS against ${HONUA_BASE_URL}/ogc"
docker run --rm \
  -v "$(pwd)/${OUTPUT_DIR}:/tmp/ets-results" \
  -e ETS_SERVICE_ENDPOINT="${HONUA_BASE_URL}/ogc" \
  -e ETS_TEST_SELECTION="${TEST_SELECTION}" \
  "${DOCKER_IMAGE}" \
  -o /tmp/ets-results/testng-results.xml \
  "${ADDITIONAL_OPTS[@]}"

echo "Conformance results stored in ${OUTPUT_DIR}"
