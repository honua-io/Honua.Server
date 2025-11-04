#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
DEFAULT_TARGET_DB="${ROOT_DIR}/data/sqlite/ogc-sample.db"
DEFAULT_METADATA_SOURCE="${ROOT_DIR}/samples/ogc/metadata.json"
DEFAULT_SAMPLE_DB="${ROOT_DIR}/samples/ogc/ogc-sample.db"
SERVER_URL="${SERVER_URL:-http://localhost:5000}"
TARGET_DB="$DEFAULT_TARGET_DB"
METADATA_SOURCE="$DEFAULT_METADATA_SOURCE"
SOURCE_URL=""
SKIP_APPLY=0

usage() {
  cat <<'EOF'
Usage: fetch-ogc-sample-data.sh [options]

Options:
  --database <path>     Target SQLite database path (default: data/sqlite/ogc-sample.db)
  --metadata <path>     Source metadata template (default: samples/ogc/metadata.json)
  --server-url <url>    Honua host base URL for metadata apply (default: http://localhost:5000)
  --source-url <url>    Remote archive to download and transform with ogr2ogr
  --skip-apply          Skip POSTing metadata to the running Honua host
  -h, --help            Show this help message
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --database)
      TARGET_DB="$2"; shift 2 ;;
    --metadata)
      METADATA_SOURCE="$2"; shift 2 ;;
    --server-url)
      SERVER_URL="$2"; shift 2 ;;
    --source-url)
      SOURCE_URL="$2"; shift 2 ;;
    --skip-apply)
      SKIP_APPLY=1; shift ;;
    -h|--help)
      usage; exit 0 ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1 ;;
  esac
done

mkdir -p "$(dirname "$TARGET_DB")"

prepare_from_archive() {
  local url="$1"
  local temp_dir
  temp_dir="$(mktemp -d)"
  trap 'rm -rf "${temp_dir}"' EXIT

  local archive_path="${temp_dir}/dataset"
  echo "Downloading dataset from ${url} ..."
  if command -v curl >/dev/null 2>&1; then
    curl -fsSL "$url" -o "$archive_path"
  elif command -v wget >/dev/null 2>&1; then
    wget -q "$url" -O "$archive_path"
  else
    echo "Neither curl nor wget available. Aborting." >&2
    exit 1
  fi

  local extract_dir="${temp_dir}/extracted"
  mkdir -p "$extract_dir"

  case "$archive_path" in
    *.zip)
      if command -v unzip >/dev/null 2>&1; then
        unzip -qq "$archive_path" -d "$extract_dir"
      else
        echo "unzip not available to extract archive." >&2
        exit 1
      fi
      ;;
    *.tar.gz|*.tgz)
      tar -xzf "$archive_path" -C "$extract_dir"
      ;;
    *.tar.bz2|*.tbz|*.tbz2)
      tar -xjf "$archive_path" -C "$extract_dir"
      ;;
    *)
      echo "Unsupported archive format for $archive_path" >&2
      exit 1
      ;;
  esac

  if ! command -v ogr2ogr >/dev/null 2>&1; then
    echo "ogr2ogr not found on PATH; cannot transform dataset." >&2
    exit 1
  fi

  local source_path
  source_path=$(find "$extract_dir" -type f \( -iname '*.gpkg' -o -iname '*.geojson' -o -iname '*.json' -o -iname '*.shp' \) | head -n 1)
  if [[ -z "$source_path" ]]; then
    echo "Unable to locate a geospatial dataset inside archive." >&2
    exit 1
  fi

  echo "Transforming dataset with ogr2ogr from $source_path"
  ogr2ogr -overwrite -f SQLite "$TARGET_DB" "$source_path"
  echo "Dataset written to $TARGET_DB"
}

if [[ -n "$SOURCE_URL" ]]; then
  prepare_from_archive "$SOURCE_URL"
else
  cp "$DEFAULT_SAMPLE_DB" "$TARGET_DB"
  echo "Copied bundled sample dataset to $TARGET_DB"
fi

update_metadata() {
  local template="$1"
  local destination="$2"
  local connection_string="$3"
  local python_cmd
  if command -v python3 >/dev/null 2>&1; then
    python_cmd=python3
  else
    python_cmd=python
  fi
  "$python_cmd" - "$template" "$destination" "$connection_string" <<'PY'
import json
import sys
from pathlib import Path

template, destination, connection = sys.argv[1:]
with open(template, 'r', encoding='utf-8-sig') as fh:
    data = json.load(fh)
for ds in data.get('dataSources', []):
    ds['connectionString'] = connection
with open(destination, 'w', encoding='utf-8') as fh:
    json.dump(data, fh, indent=2)
    fh.write('\n')
PY
}

CONNECTION_STRING="Data Source=${TARGET_DB};Version=3;Pooling=false;"
GENERATED_METADATA="${TARGET_DB%.*}-metadata.json"
update_metadata "$METADATA_SOURCE" "$GENERATED_METADATA" "$CONNECTION_STRING"
echo "Generated metadata at $GENERATED_METADATA"

if [[ "$SKIP_APPLY" -eq 0 ]]; then
  if command -v curl >/dev/null 2>&1; then
    echo "Applying metadata to ${SERVER_URL}"
    if ! curl -fsS -X POST "${SERVER_URL%/}/admin/metadata/apply" -H 'Content-Type: application/json' --data-binary "@${GENERATED_METADATA}" > /tmp/honua-metadata-apply.log 2>&1; then
      echo "Metadata apply request failed. See /tmp/honua-metadata-apply.log for details." >&2
      exit 1
    fi
    echo "Metadata apply completed."
  else
    echo "curl not available; skipping metadata apply." >&2
  fi
fi

echo "OGC sample dataset ready."
