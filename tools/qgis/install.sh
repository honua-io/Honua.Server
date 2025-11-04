#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
MICROMAMBA_DIR="$SCRIPT_DIR/bin"
MICROMAMBA_BIN="$MICROMAMBA_DIR/micromamba"
export MAMBA_ROOT_PREFIX="$SCRIPT_DIR/mamba-root"

mkdir -p "$MICROMAMBA_DIR"
mkdir -p "$MAMBA_ROOT_PREFIX"

if [ ! -x "$MICROMAMBA_BIN" ]; then
  echo "Downloading micromamba binary…"
  tmp_archive=$(mktemp)
  curl -Ls https://micro.mamba.pm/api/micromamba/linux-64/latest -o "$tmp_archive"
  tar -xvjf "$tmp_archive" -C "$MICROMAMBA_DIR" --strip-components=1 bin/micromamba >/dev/null
  chmod +x "$MICROMAMBA_BIN"
  rm "$tmp_archive"
else
  echo "Reusing existing micromamba binary at $MICROMAMBA_BIN"
fi

echo "Installing/refreshing honua-qgis environment under $MAMBA_ROOT_PREFIX"
"$MICROMAMBA_BIN" create -y -n honua-qgis -c conda-forge python=3.10 qgis pytest pip

cat <<EOF

PyQGIS runtime installed.
Activate it with:
  export MAMBA_ROOT_PREFIX="$MAMBA_ROOT_PREFIX"
  eval "$("$MICROMAMBA_BIN" shell hook -s bash)"
  micromamba activate honua-qgis

Or run a command directly without activation:
  MAMBA_ROOT_PREFIX="$MAMBA_ROOT_PREFIX" "$MICROMAMBA_BIN" run -n honua-qgis python -c "import qgis"

EOF
