#!/usr/bin/env bash
set -euo pipefail

DATA_DIR="${1:-samples/kml}"
mkdir -p "$DATA_DIR"

KML_URL=${KML_URL:-"https://github.com/opengeospatial/ets-kml2/raw/master/src/test/resources/kml23/AE_Kml23_Simple.kml"}
KML_NAME=${KML_NAME:-"ogc-kml-conformance-sample.kml"}
OUT_PATH="$DATA_DIR/$KML_NAME"

if command -v curl >/dev/null 2>&1; then
  curl -L "$KML_URL" -o "$OUT_PATH"
elif command -v wget >/dev/null 2>&1; then
  wget -O "$OUT_PATH" "$KML_URL"
else
  echo "Error: require curl or wget to download KML dataset" >&2
  exit 1
fi

echo "Downloaded KML sample to $OUT_PATH"
