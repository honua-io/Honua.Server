#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
TEST_DIR="$ROOT_DIR/tests/qgis"

python3 -m pip install --user --disable-pip-version-check --no-warn-script-location -r "$TEST_DIR/requirements.txt"

PYQGIS_PATH=$(python3 - <<'PY'
import qgis
import os
print(os.path.dirname(qgis.__file__))
PY
)

if [ -n "${PYTHONPATH:-}" ]; then
  export PYTHONPATH="${PYQGIS_PATH}:${PYTHONPATH}"
else
  export PYTHONPATH="${PYQGIS_PATH}"
fi
export QT_QPA_PLATFORM=${QT_QPA_PLATFORM:-offscreen}

python3 -m pytest "$TEST_DIR" "$@"
