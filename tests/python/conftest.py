import os
import subprocess
import time
from collections.abc import Callable
from typing import Any, Optional

import pytest
import requests


# ============================================================================
#  Shared Test Environment Management
# ============================================================================

@pytest.fixture(scope="session", autouse=True)
def ensure_shared_test_env():
    """
    Ensure shared test environment is running before any tests execute.
    This fixture runs once per test session and starts the shared Docker
    environment if it's not already running.
    """
    # Check if HONUA_API_BASE_URL is explicitly set (manual mode)
    if os.getenv("HONUA_API_BASE_URL"):
        yield  # User has manually configured environment
        return

    # Check if shared test environment is already running
    base_url = "http://localhost:5100"
    try:
        response = requests.get(f"{base_url}/health", timeout=2)
        if response.status_code == 200:
            print(f"\n✓ Shared test environment already running at {base_url}")
            yield
            return
    except requests.exceptions.RequestException:
        pass

    # Check if user wants to skip environment startup
    if os.environ.get("SKIP_START_ENV"):
        print(f"\n✓ Skipping environment startup (SKIP_START_ENV=1). Assuming environment is running at {base_url}\n")
        yield
        return

    # Start shared test environment
    print("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
    print("  Starting Shared Test Environment...")
    print("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")

    tests_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    start_script = os.path.join(tests_dir, "start-shared-test-env.sh")

    try:
        subprocess.run([start_script, "start"], check=True, cwd=tests_dir)
        print(f"✓ Shared test environment started at {base_url}\n")
    except subprocess.CalledProcessError as e:
        pytest.exit(f"Failed to start shared test environment: {e}", returncode=1)

    yield

    # Note: We don't stop the environment after tests - keep it cached for next run
    # Users can manually stop with: ./start-shared-test-env.sh stop


# ============================================================================
#  Connection Fixtures
# ============================================================================

@pytest.fixture(scope="session")
def honua_api_base_url() -> str:
    """
    Get the base URL for Honua API tests.

    Priority:
    1. HONUA_API_BASE_URL environment variable (manual override)
    2. HONUA_QGIS_BASE_URL environment variable (legacy)
    3. Default shared test environment (http://localhost:5100)
    """
    base = os.getenv("HONUA_API_BASE_URL") or os.getenv("HONUA_QGIS_BASE_URL")
    if not base:
        base = "http://localhost:5100"  # Shared test environment default
    return base.rstrip("/")


@pytest.fixture(scope="session")
def honua_api_bearer_token() -> Optional[str]:
    """Get bearer token for authenticated tests (if required)."""
    return os.getenv("HONUA_API_BEARER") or os.getenv("HONUA_QGIS_BEARER")


@pytest.fixture(scope="session")
def api_session(honua_api_bearer_token: Optional[str]) -> requests.Session:
    """Create requests session with optional authentication."""
    session = requests.Session()
    if honua_api_bearer_token:
        session.headers.update({"Authorization": f"Bearer {honua_api_bearer_token}"})

    # Set reasonable timeouts
    session.request = lambda *args, **kwargs: requests.Session.request(
        session, *args, **{**kwargs, 'timeout': kwargs.get('timeout', 30)}
    )

    try:
        yield session
    finally:
        session.close()


@pytest.fixture(scope="session")
def api_request(api_session: requests.Session, honua_api_base_url: str) -> Callable[..., requests.Response]:
    """Factory function for making API requests."""
    def _request(method: str, path: str, **kwargs: Any) -> requests.Response:
        url = path if path.startswith("http") else f"{honua_api_base_url}{path}"
        timeout = kwargs.pop("timeout", 30)
        response = api_session.request(method, url, timeout=timeout, **kwargs)
        return response

    return _request


# ============================================================================
#  Shared Assertion Helpers
# ============================================================================

def assert_valid_geojson_feature_collection(data: dict, min_features: int = 0):
    """
    Assert that data is a valid GeoJSON FeatureCollection.

    Args:
        data: Dictionary to validate
        min_features: Minimum number of features expected
    """
    assert data.get("type") == "FeatureCollection", "Must be a FeatureCollection"
    assert "features" in data, "Must have features array"
    assert isinstance(data["features"], list), "Features must be a list"
    assert len(data["features"]) >= min_features, f"Expected at least {min_features} features"


def assert_valid_geojson_feature(feature: dict):
    """Assert that feature is a valid GeoJSON Feature."""
    assert feature.get("type") == "Feature", "Must be a Feature"
    assert "geometry" in feature, "Feature must have geometry"
    assert "properties" in feature, "Feature must have properties"


def assert_ogc_capabilities(data: dict, required_operations: list[str]):
    """
    Assert OGC service capabilities are valid.

    Args:
        data: Capabilities response data
        required_operations: List of operation names that must be supported
    """
    # This is a generic check - specific protocols will override
    assert data is not None, "Capabilities must not be None"

    # Check for operations (implementation depends on protocol)
    if "operations" in data:
        operation_names = [op.get("name") for op in data["operations"]]
        for required_op in required_operations:
            assert required_op in operation_names, f"Must support {required_op} operation"


def assert_stac_item_collection(data: dict, min_items: int = 0):
    """
    Assert that data is a valid STAC ItemCollection.

    Args:
        data: Dictionary to validate
        min_items: Minimum number of items expected
    """
    assert data.get("type") == "FeatureCollection", "STAC ItemCollection must be a FeatureCollection"
    assert "features" in data, "Must have features/items array"
    assert isinstance(data["features"], list), "Features must be a list"
    assert len(data["features"]) >= min_items, f"Expected at least {min_items} items"

    # Check STAC-specific fields
    for feature in data["features"]:
        assert feature.get("type") == "Feature", "STAC items must be GeoJSON Features"
        assert "properties" in feature, "STAC items must have properties"
        assert "assets" in feature or "properties" in feature, "STAC items should have assets or properties"


def wait_for_service_ready(base_url: str, endpoint: str = "/health", timeout: int = 30, interval: float = 1.0) -> bool:
    """
    Wait for a service to become ready.

    Args:
        base_url: Base URL of the service
        endpoint: Health check endpoint
        timeout: Maximum time to wait in seconds
        interval: Time between checks in seconds

    Returns:
        True if service became ready, False if timeout
    """
    start_time = time.time()
    url = f"{base_url.rstrip('/')}{endpoint}"

    while time.time() - start_time < timeout:
        try:
            response = requests.get(url, timeout=5)
            if response.status_code == 200:
                return True
        except requests.exceptions.RequestException:
            pass
        time.sleep(interval)

    return False


# ============================================================================
#  Protocol-Specific Fixtures
# ============================================================================

@pytest.fixture(scope="module")
def wfs_base_url(honua_api_base_url: str) -> str:
    """Base URL for WFS tests."""
    return f"{honua_api_base_url}/wfs"


@pytest.fixture(scope="module")
def wms_base_url(honua_api_base_url: str) -> str:
    """Base URL for WMS tests."""
    return f"{honua_api_base_url}/wms"


@pytest.fixture(scope="module")
def wmts_base_url(honua_api_base_url: str) -> str:
    """Base URL for WMTS tests."""
    return f"{honua_api_base_url}/wmts"


@pytest.fixture(scope="module")
def wcs_base_url(honua_api_base_url: str) -> str:
    """Base URL for WCS tests."""
    return f"{honua_api_base_url}/wcs"


@pytest.fixture(scope="module")
def stac_base_url(honua_api_base_url: str) -> str:
    """Base URL for STAC tests."""
    return f"{honua_api_base_url}/stac"


@pytest.fixture(scope="module")
def ogc_features_base_url(honua_api_base_url: str) -> str:
    """Base URL for OGC API Features tests."""
    return f"{honua_api_base_url}/ogc"


# ============================================================================
#  Pytest Configuration
# ============================================================================

def pytest_configure(config: pytest.Config) -> None:  # pragma: no cover - pytest hook
    """Configure pytest markers for Python integration tests."""

    # Test scope markers
    config.addinivalue_line("markers", "smoke: Quick smoke tests validating basic functionality (< 30 sec)")
    config.addinivalue_line("markers", "integration: Integration tests for standard compliance (2-5 min)")
    config.addinivalue_line("markers", "comprehensive: Full test suite with edge cases (10+ min)")

    # Test type markers
    config.addinivalue_line("markers", "requires_honua: Needs a running Honua API instance")
    config.addinivalue_line("markers", "python: Tests using Python requests library")
    config.addinivalue_line("markers", "read_only: Tests that only read data (safe for parallel execution)")
    config.addinivalue_line("markers", "write: Tests that modify data (require isolation)")
    config.addinivalue_line("markers", "performance: Performance and load tests")

    # Protocol-specific markers
    config.addinivalue_line("markers", "wfs: Tests for WFS (Web Feature Service) functionality")
    config.addinivalue_line("markers", "wms: Tests for WMS (Web Map Service) functionality")
    config.addinivalue_line("markers", "wmts: Tests for WMTS (Web Map Tile Service) functionality")
    config.addinivalue_line("markers", "wcs: Tests for WCS (Web Coverage Service) functionality")
    config.addinivalue_line("markers", "csw: Tests for CSW (Catalog Service for the Web) functionality")
    config.addinivalue_line("markers", "stac: Tests for STAC (SpatioTemporal Asset Catalog) functionality")
    config.addinivalue_line("markers", "ogc_features: Tests for OGC Features API functionality")
    config.addinivalue_line("markers", "ogc_tiles: Tests for OGC Tiles API functionality")
    config.addinivalue_line("markers", "ogc_processes: Tests for OGC Processes API functionality")
    config.addinivalue_line("markers", "geoservices: Tests for Esri GeoServices REST API functionality")

    # Misc markers
    config.addinivalue_line("markers", "slow: Marks tests as slow (deselect with '-m \"not slow\"')")


def pytest_collection_modifyitems(config: pytest.Config, items: list[pytest.Item]) -> None:
    """
    Modify test collection to add automatic markers and skip conditions.
    """
    # Auto-mark tests that require Honua
    for item in items:
        # If test uses honua_api_base_url fixture, mark as requires_honua
        if "honua_api_base_url" in item.fixturenames:
            item.add_marker(pytest.mark.requires_honua)

        # Auto-mark protocol tests based on file name
        test_file = str(item.fspath)
        if "test_wfs" in test_file:
            item.add_marker(pytest.mark.wfs)
        elif "test_wms" in test_file:
            item.add_marker(pytest.mark.wms)
        elif "test_wmts" in test_file:
            item.add_marker(pytest.mark.wmts)
        elif "test_wcs" in test_file:
            item.add_marker(pytest.mark.wcs)
        elif "test_stac" in test_file:
            item.add_marker(pytest.mark.stac)
        elif "test_ogc_features" in test_file:
            item.add_marker(pytest.mark.ogc_features)
        elif "test_ogc_tiles" in test_file:
            item.add_marker(pytest.mark.ogc_tiles)
        elif "test_ogc_processes" in test_file:
            item.add_marker(pytest.mark.ogc_processes)
        elif "test_geoservices" in test_file:
            item.add_marker(pytest.mark.geoservices)
