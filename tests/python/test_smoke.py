"""
Smoke tests for Honua Server - Fast validation that all protocols work

These tests verify basic functionality of each protocol.
Run with: pytest tests/python/test_smoke.py -v

Total: ~15 tests, should complete in < 30 seconds
"""

import pytest
import requests

# Base URL for the Honua server
BASE_URL = "http://localhost:8080"


@pytest.fixture(scope="module")
def server_url():
    """Get server URL, skip all tests if server not available."""
    try:
        response = requests.get(f"{BASE_URL}/health", timeout=5)
        if response.status_code == 200:
            return BASE_URL
    except requests.exceptions.RequestException:
        pass

    pytest.skip(f"Honua server not available at {BASE_URL}")


class TestOGCAPI:
    """OGC API smoke tests"""

    def test_landing_page(self, server_url):
        """Verify OGC API landing page is accessible."""
        response = requests.get(f"{server_url}/")
        assert response.status_code == 200
        data = response.json()
        assert "links" in data

    def test_conformance(self, server_url):
        """Verify conformance declaration."""
        response = requests.get(f"{server_url}/conformance")
        assert response.status_code == 200
        data = response.json()
        assert "conformsTo" in data


class TestWFS:
    """WFS smoke tests"""

    def test_get_capabilities(self, server_url):
        """Verify WFS GetCapabilities works."""
        params = {
            "service": "WFS",
            "version": "2.0.0",
            "request": "GetCapabilities"
        }
        response = requests.get(f"{server_url}/wfs", params=params)
        assert response.status_code == 200
        assert b"WFS_Capabilities" in response.content


class TestWMS:
    """WMS smoke tests"""

    def test_get_capabilities(self, server_url):
        """Verify WMS GetCapabilities works."""
        params = {
            "service": "WMS",
            "version": "1.3.0",
            "request": "GetCapabilities"
        }
        response = requests.get(f"{server_url}/wms", params=params)
        assert response.status_code == 200
        assert b"WMS_Capabilities" in response.content


class TestWMTS:
    """WMTS smoke tests"""

    def test_get_capabilities(self, server_url):
        """Verify WMTS GetCapabilities works."""
        params = {
            "service": "WMTS",
            "version": "1.0.0",
            "request": "GetCapabilities"
        }
        response = requests.get(f"{server_url}/wmts", params=params)
        assert response.status_code == 200
        assert b"Capabilities" in response.content


class TestSTAC:
    """STAC smoke tests"""

    def test_catalog_root(self, server_url):
        """Verify STAC catalog is accessible."""
        response = requests.get(f"{server_url}/stac")
        assert response.status_code == 200
        data = response.json()
        assert data.get("type") in ["Catalog", "Collection"]


class TestGeoServicesREST:
    """Esri GeoServices REST API smoke tests"""

    def test_rest_info(self, server_url):
        """Verify GeoServices REST endpoint works."""
        response = requests.get(f"{server_url}/rest/services", params={"f": "json"})
        assert response.status_code == 200
        data = response.json()
        assert "services" in data or "folders" in data


class TestHealth:
    """Health check tests"""

    def test_health_live(self, server_url):
        """Verify liveness probe."""
        response = requests.get(f"{server_url}/health/live")
        assert response.status_code == 200

    def test_health_ready(self, server_url):
        """Verify readiness probe."""
        response = requests.get(f"{server_url}/health/ready")
        assert response.status_code == 200


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
