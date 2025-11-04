# QGIS automation tests

This suite runs smoke tests against a live Honua deployment with PyQGIS to ensure popular desktop workflows keep functioning.

## Prerequisites
- QGIS 3.28 or newer installed with the Python bindings (`qgis.core` / `qgis.PyQt`).
- Access to a running Honua API instance that exposes OGC endpoints (typically `dotnet run --project src/Honua.Server.Host` or a deployed stack).
- `pytest` available in the same Python environment you use for PyQGIS.

## Environment
Set the following variables before running the tests:

| Variable | Purpose | Default |
| --- | --- | --- |
| `QGIS_PREFIX_PATH` | Points to the QGIS install prefix so PyQGIS can locate plugins | If omitted we fall back to `QgsApplication.prefixPath()` |
| `HONUA_QGIS_BASE_URL` | Base URL of the Honua deployment (e.g. `https://localhost:5001`) | **Required** |
| `HONUA_QGIS_WMS_LAYER` | WMS layer name to request | `roads:roads-imagery` |
| `HONUA_QGIS_COLLECTION_ID` | Collection identifier for GeoJSON checks | `roads::roads-primary` |
| `HONUA_QGIS_ITEMS_QUERY` | Additional query string appended to `/items` | `limit=25` |

If a variable is missing and no default is provided the matching test is skipped.

## Running
The tests can run inside the QGIS runtime or any Python that has PyQGIS on its `PYTHONPATH`:

```bash
# Option A: use qgis_process to ensure the QGIS runtime is initialised
qgis_process run qgis:execpython -- -- pytest tests/qgis

# Option B: activate the PyQGIS virtual env directly
export PYTHONPATH="$(python3 -c 'import qgis; import os; print(os.path.dirname(qgis.__file__))'):$PYTHONPATH"
pytest tests/qgis

# Option C: run inside the official QGIS container (matches CI)
docker run --rm \
  --network host \
  -e HONUA_QGIS_BASE_URL="http://127.0.0.1:5005" \
  -e QT_QPA_PLATFORM=offscreen \
  -v "$PWD":/workspace \
  -w /workspace \
  qgis/qgis:3.34.6 \
  bash -lc "./tests/qgis/run-qgis-tests.sh"
```

For CI pipelines we recommend invoking `qgis_process` inside the official `qgis/qgis:latest` container. These tests leave the API untouched and only perform read-only requests.

The helper script installs pytest dependencies on demand and adjusts `PYTHONPATH` so PyQGIS modules resolve inside the container.

### Local PyQGIS runtime
If you prefer running the tests without Docker, bootstrap a local PyQGIS install using micromamba:

```bash
./tools/qgis/install.sh
export MAMBA_ROOT_PREFIX="$PWD/tools/qgis/mamba-root"
eval "$(./tools/qgis/bin/micromamba shell hook -s bash)"
micromamba activate honua-qgis
pytest tests/qgis
```

When running inside Docker remember to start a Honua instance beforehand (for example `HONUA_ALLOW_QUICKSTART=true DOTNET_ENVIRONMENT=QuickStart dotnet run --project src/Honua.Server.Host --urls http://127.0.0.1:5005`).

## One-command smoke run
Invoke the helper script to bootstrap authentication, launch Honua, obtain a bearer token, and execute the PyQGIS suite:

```bash
./scripts/run-qgis-smoke.sh
```

Flags such as `--base-url`, `--container`, and `--no-container` (to use a local PyQGIS environment) are available; run with `--help` for details. The script emits server logs on failure and cleans up temporary publish assets automatically.
When running without Docker, ensure the local Python environment exposes PyQGIS and GDAL libraries (the `tools/qgis/install.sh` helper wires this up via micromamba).

## Extending
- Add new fixtures in `conftest.py` to share connection logic.
- Prefer small, deterministic datasets so renders remain stable.
- Include additional tile cache scenarios (e.g., WMTS zoom levels, mixed formats) by extending `test_wmts_tiles.py` (currently covers PNG + JPEG for both `WorldWebMercatorQuad` and `WorldCRS84Quad`).
- Wrap long running checks with `pytest.mark.slow` to make them opt-in.
