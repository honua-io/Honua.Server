import os
from collections.abc import Callable
from typing import Any, Optional

import pytest
import requests


@pytest.fixture(scope="session")
def honua_api_base_url() -> str:
    base = os.getenv("HONUA_API_BASE_URL") or os.getenv("HONUA_QGIS_BASE_URL")
    if not base:
        pytest.skip("HONUA_API_BASE_URL is not set; skipping STAC smoke tests")
    return base.rstrip("/")


@pytest.fixture(scope="session")
def honua_api_bearer_token() -> Optional[str]:
    return os.getenv("HONUA_API_BEARER") or os.getenv("HONUA_QGIS_BEARER")


@pytest.fixture(scope="session")
def api_session(honua_api_bearer_token: Optional[str]) -> requests.Session:
    session = requests.Session()
    if honua_api_bearer_token:
        session.headers.update({"Authorization": f"Bearer {honua_api_bearer_token}"})
    try:
        yield session
    finally:
        session.close()


@pytest.fixture(scope="session")
def api_request(api_session: requests.Session, honua_api_base_url: str) -> Callable[..., requests.Response]:
    def _request(method: str, path: str, **kwargs: Any) -> requests.Response:
        url = path if path.startswith("http") else f"{honua_api_base_url}{path}"
        timeout = kwargs.pop("timeout", 30)
        response = api_session.request(method, url, timeout=timeout, **kwargs)
        return response

    return _request


def pytest_configure(config: pytest.Config) -> None:  # pragma: no cover - pytest hook
    """Configure pytest markers for Python integration tests."""
    config.addinivalue_line("markers", "integration: marks tests as integration tests")
    config.addinivalue_line("markers", "requires_honua: needs a running Honua API instance")
    config.addinivalue_line("markers", "python: tests using Python requests library")
    config.addinivalue_line("markers", "wms: tests for WMS (Web Map Service) functionality")
    config.addinivalue_line("markers", "wmts: tests for WMTS (Web Map Tile Service) functionality")
    config.addinivalue_line("markers", "wfs: tests for WFS (Web Feature Service) functionality")
    config.addinivalue_line("markers", "csw: tests for CSW (Catalog Service for the Web) functionality")
    config.addinivalue_line("markers", "ogc_features: tests for OGC Features API functionality")
    config.addinivalue_line("markers", "ogc_tiles: tests for OGC Tiles API functionality")
    config.addinivalue_line("markers", "ogc_processes: tests for OGC Processes API functionality")
    config.addinivalue_line("markers", "wcs: tests for WCS (Web Coverage Service) functionality")
    config.addinivalue_line("markers", "stac: tests for STAC (SpatioTemporal Asset Catalog) functionality")
    config.addinivalue_line("markers", "geoservices: tests for GeoServices REST API functionality")
    config.addinivalue_line("markers", "slow: marks tests as slow (deselect with '-m \"not slow\"')")
