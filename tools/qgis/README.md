# PyQGIS Runtime Bootstrap

This helper installs a self-contained QGIS + PyQGIS environment (via micromamba) that matches the version used in CI.

## Install
```bash
./tools/qgis/install.sh
```
This downloads the micromamba binary locally (under `tools/qgis/bin/`) and creates/updates the `honua-qgis` environment in `tools/qgis/mamba-root`.

## Activate
```bash
export MAMBA_ROOT_PREFIX="$PWD/tools/qgis/mamba-root"
eval "$(./tools/qgis/bin/micromamba shell hook -s bash)"
micromamba activate honua-qgis
```

## Run Without Activating
```bash
MAMBA_ROOT_PREFIX="$PWD/tools/qgis/mamba-root" \
  ./tools/qgis/bin/micromamba run -n honua-qgis python -c "import qgis"
```

## Using With Tests
```bash
MAMBA_ROOT_PREFIX="$PWD/tools/qgis/mamba-root" \
  ./tools/qgis/bin/micromamba run -n honua-qgis \
  ./tests/qgis/run-qgis-tests.sh
```
