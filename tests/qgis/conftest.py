import os
from contextlib import contextmanager

import pytest


class _MissingDependency(Exception):
    pass


def _import_qgis():
    try:
        from qgis.core import QgsApplication  # type: ignore
    except ImportError as exc:  # pragma: no cover - exercised only without PyQGIS
        raise _MissingDependency(
            "PyQGIS is not available in the current Python environment"
        ) from exc
    return QgsApplication


@pytest.fixture(scope="session")
def qgis_app():
    """Initialise a headless QgsApplication once for all tests."""
    try:
        QgsApplication = _import_qgis()
    except _MissingDependency as exc:  # pragma: no cover - dependency missing path
        pytest.skip(str(exc))

    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

    prefix_override = os.environ.get("QGIS_PREFIX_PATH")
    app = QgsApplication([], False)
    if prefix_override:
        QgsApplication.setPrefixPath(prefix_override, True)
    QgsApplication.initQgis()

    yield app

    QgsApplication.exitQgis()


@pytest.fixture(scope="session", autouse=True)
def configure_bearer_token(qgis_app):
    """Inject a bearer token into all outgoing network requests when provided."""

    token = os.environ.get("HONUA_QGIS_BEARER")
    if not token:
        yield
        return

    try:
        from qgis.core import QgsNetworkAccessManager  # type: ignore
    except ImportError:  # pragma: no cover - handled by qgis_app fixture skip
        yield
        return

    header_value = f"Bearer {token}".encode("ascii")
    manager = QgsNetworkAccessManager.instance()

    def _append_authorization(operation, request, device):  # pragma: no cover - Qt signal
        del operation, device  # unused by our hook
        request.setRawHeader(b"Authorization", header_value)

    manager.requestAboutToBeCreated.connect(_append_authorization)

    try:
        yield
    finally:
        try:
            manager.requestAboutToBeCreated.disconnect(_append_authorization)
        except (TypeError, RuntimeError):
            pass


@pytest.fixture(scope="session")
def honua_base_url():
    base_url = os.environ.get("HONUA_QGIS_BASE_URL")
    if not base_url:
        pytest.skip("HONUA_QGIS_BASE_URL is not set; skipping QGIS integration tests")
    return base_url.rstrip("/")


@pytest.fixture(scope="session")
def layer_config():
    return {
        "wms_layer": os.environ.get("HONUA_QGIS_WMS_LAYER", "roads:roads-imagery"),
        "collection_id": os.environ.get("HONUA_QGIS_COLLECTION_ID", "roads::roads-primary"),
        "items_query": os.environ.get("HONUA_QGIS_ITEMS_QUERY", "limit=25"),
    }


@contextmanager
def project_context():
    from qgis.core import QgsProject  # type: ignore

    project = QgsProject()
    try:
        yield project
    finally:
        project.removeAllMapLayers()
        project.clear()


@pytest.fixture
def qgis_project():
    with project_context() as project:
        yield project


def pytest_configure(config):
    """Configure pytest markers for QGIS integration tests."""
    config.addinivalue_line("markers", "requires_qgis: needs the PyQGIS runtime")
    config.addinivalue_line("markers", "requires_honua: needs a running Honua instance")
    config.addinivalue_line("markers", "wms: tests for WMS (Web Map Service) functionality")
    config.addinivalue_line("markers", "wmts: tests for WMTS (Web Map Tile Service) functionality")
    config.addinivalue_line("markers", "ogc_features: tests for OGC Features API functionality")
    config.addinivalue_line("markers", "ogc_tiles: tests for OGC Tiles API functionality")
    config.addinivalue_line("markers", "wcs: tests for WCS (Web Coverage Service) functionality")
    config.addinivalue_line("markers", "stac: tests for STAC (SpatioTemporal Asset Catalog) functionality")
    config.addinivalue_line("markers", "geoservices: tests for GeoServices REST API functionality")
    config.addinivalue_line("markers", "slow: marks tests as slow (deselect with '-m \"not slow\"')")
