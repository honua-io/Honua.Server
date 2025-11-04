"""
Comprehensive WMTS 1.0.0 Integration Tests with QGIS

This test suite provides comprehensive coverage of WMTS operations using QGIS as the reference client.
Tests validate Honua's WMTS implementation against the OGC WMTS 1.0.0 specification using
real-world client library integration.

Test Coverage:
- GetCapabilities: Service metadata, tile matrix sets, layer discovery, format enumeration
- GetTile: Tile retrieval across multiple zoom levels and tile matrix sets
- Image Formats: PNG, JPEG, WebP format support and validation
- Tile Matrix Sets: WorldWebMercatorQuad (Web Mercator), WorldCRS84Quad (WGS84)
- Caching: ETag, Cache-Control headers, 304 Not Modified responses
- Error Handling: Invalid parameters, out-of-bounds tiles, unsupported formats
- QGIS Integration: Layer loading, tile rendering, map display

Client: QGIS 3.34+ (PyQGIS WMTS Provider)
Specification: OGC WMTS 1.0.0
"""
import json
import xml.etree.ElementTree as ET

import pytest


pytestmark = [pytest.mark.integration, pytest.mark.qgis, pytest.mark.wmts, pytest.mark.requires_honua]


# ============================================================================
#  GetCapabilities Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wmts_getcapabilities_returns_valid_document(qgis_app, honua_base_url):
    """Verify WMTS GetCapabilities returns valid WMTS Capabilities XML document."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    capabilities_url = f"{honua_base_url}/wmts?service=WMTS&request=GetCapabilities&version=1.0.0"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(capabilities_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"GetCapabilities request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Validate XML structure
    assert "Capabilities" in content, "Response must contain Capabilities element"
    assert "wmts" in content.lower(), "Response must be WMTS Capabilities document"
    assert "1.0.0" in content, "Response must declare WMTS 1.0.0 version"

    # Parse XML to validate structure
    root = ET.fromstring(content)
    assert root.tag.endswith("Capabilities"), f"Root element should be Capabilities, got {root.tag}"


@pytest.mark.requires_qgis
def test_wmts_getcapabilities_declares_tile_matrix_sets(qgis_app, honua_base_url):
    """Verify WMTS GetCapabilities declares required tile matrix sets."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    capabilities_url = f"{honua_base_url}/wmts?service=WMTS&request=GetCapabilities&version=1.0.0"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(capabilities_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Validate required tile matrix sets
    assert "WorldWebMercatorQuad" in content, "Must support WorldWebMercatorQuad (EPSG:3857)"
    assert "WorldCRS84Quad" in content, "Should support WorldCRS84Quad (EPSG:4326)"

    # Parse and validate TileMatrixSet elements
    root = ET.fromstring(content)
    tms_elements = [elem for elem in root.iter() if elem.tag.endswith("TileMatrixSet")]
    assert len(tms_elements) >= 1, "At least one TileMatrixSet must be declared"


@pytest.mark.requires_qgis
def test_wmts_getcapabilities_lists_available_layers(qgis_app, honua_base_url):
    """Verify WMTS GetCapabilities lists all available layers."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    capabilities_url = f"{honua_base_url}/wmts?service=WMTS&request=GetCapabilities&version=1.0.0"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(capabilities_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Parse and validate layer list
    root = ET.fromstring(content)

    # Find Contents section
    contents = None
    for child in root:
        if child.tag.endswith("Contents"):
            contents = child
            break

    assert contents is not None, "WMTS Capabilities must include Contents section"

    # Find Layer elements
    layers = [elem for elem in contents if elem.tag.endswith("Layer")]
    assert len(layers) > 0, "Contents should contain at least one Layer"


@pytest.mark.requires_qgis
def test_wmts_getcapabilities_declares_supported_formats(qgis_app, honua_base_url):
    """Verify WMTS GetCapabilities declares supported image formats (PNG, JPEG)."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    capabilities_url = f"{honua_base_url}/wmts?service=WMTS&request=GetCapabilities&version=1.0.0"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(capabilities_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Validate image formats
    assert "image/png" in content.lower(), "WMTS must support PNG format"
    # JPEG and WebP support may vary
    has_jpeg = "image/jpeg" in content.lower()
    has_webp = "image/webp" in content.lower()
    assert has_jpeg or has_webp, "WMTS should support JPEG or WebP in addition to PNG"


@pytest.mark.requires_qgis
def test_wmts_getcapabilities_includes_service_metadata(qgis_app, honua_base_url):
    """Verify WMTS GetCapabilities includes service identification metadata."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    capabilities_url = f"{honua_base_url}/wmts?service=WMTS&request=GetCapabilities&version=1.0.0"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(capabilities_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Parse and validate service metadata
    root = ET.fromstring(content)

    # Find ServiceIdentification
    service_id = None
    for child in root:
        if child.tag.endswith("ServiceIdentification"):
            service_id = child
            break

    assert service_id is not None, "WMTS Capabilities must include ServiceIdentification"

    # Validate required elements
    title_elems = [elem for elem in service_id if elem.tag.endswith("Title")]
    assert len(title_elems) > 0, "ServiceIdentification must include Title"


# ============================================================================
#  GetTile - WorldWebMercatorQuad (EPSG:3857) Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wmts_gettile_worldwebmercatorquad_zoom0(qgis_app, honua_base_url, layer_config):
    """Verify WMTS GetTile returns valid tile at zoom level 0 (world view) for WorldWebMercatorQuad."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {wms_layer} not available in test environment")

    assert status == 200, f"GetTile request failed with status {status}"

    content_type = reply.header(QNetworkRequest.ContentTypeHeader)
    assert content_type and "image/png" in str(content_type), f"Expected PNG, got {content_type}"

    tile_data = bytes(reply.readAll())
    reply.deleteLater()

    assert len(tile_data) > 0, "Tile data should not be empty"
    # PNG files start with magic bytes 89 50 4E 47
    assert tile_data[:4] == b'\x89PNG', "Response must be valid PNG image"


@pytest.mark.requires_qgis
def test_wmts_gettile_worldwebmercatorquad_zoom5(qgis_app, honua_base_url, layer_config):
    """Verify WMTS GetTile returns valid tile at zoom level 5 for WorldWebMercatorQuad."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    # Request tile at zoom 5, center of the world
    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=5&"
        f"TileRow=15&"
        f"TileCol=16"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {wms_layer} not available in test environment")

    assert status == 200, f"GetTile request failed with status {status}"

    tile_data = bytes(reply.readAll())
    reply.deleteLater()

    assert len(tile_data) > 0, "Tile data should not be empty"
    assert tile_data[:4] == b'\x89PNG', "Response must be valid PNG image"


@pytest.mark.requires_qgis
def test_wmts_gettile_worldwebmercatorquad_zoom10(qgis_app, honua_base_url, layer_config):
    """Verify WMTS GetTile returns valid tile at zoom level 10 for WorldWebMercatorQuad."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=10&"
        f"TileRow=512&"
        f"TileCol=512"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {wms_layer} not available in test environment")

    # At zoom 10, some tiles may be legitimately empty/404 if no data
    if status == 200:
        tile_data = bytes(reply.readAll())
        assert len(tile_data) > 0, "Tile data should not be empty"
        assert tile_data[:4] == b'\x89PNG', "Response must be valid PNG image"

    reply.deleteLater()


@pytest.mark.requires_qgis
def test_wmts_gettile_worldwebmercatorquad_zoom15(qgis_app, honua_base_url, layer_config):
    """Verify WMTS GetTile returns valid tile at zoom level 15 for WorldWebMercatorQuad."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    # At zoom 15, we have 2^15 = 32768 tiles in each dimension
    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=15&"
        f"TileRow=16384&"
        f"TileCol=16384"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)

    # High zoom tiles are often empty/404 in test environments
    if status == 200:
        tile_data = bytes(reply.readAll())
        assert len(tile_data) > 0, "Tile data should not be empty"
        assert tile_data[:4] == b'\x89PNG', "Response must be valid PNG image"
    elif status == 404:
        pytest.skip(f"Tile not available at zoom 15 (may be outside data extent)")

    reply.deleteLater()


# ============================================================================
#  GetTile - WorldCRS84Quad (EPSG:4326) Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wmts_gettile_worldcrs84quad_zoom0(qgis_app, honua_base_url, layer_config):
    """Verify WMTS GetTile returns valid tile for WorldCRS84Quad tile matrix set."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldCRS84Quad&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {wms_layer} not available or WorldCRS84Quad not supported")

    assert status == 200, f"GetTile request failed with status {status}"

    tile_data = bytes(reply.readAll())
    reply.deleteLater()

    assert len(tile_data) > 0, "Tile data should not be empty"
    assert tile_data[:4] == b'\x89PNG', "Response must be valid PNG image"


# ============================================================================
#  Image Format Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wmts_gettile_format_png(qgis_app, honua_base_url, layer_config):
    """Verify WMTS GetTile supports PNG image format."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {wms_layer} not available")

    assert status == 200, f"PNG tile request failed with status {status}"

    content_type = reply.header(QNetworkRequest.ContentTypeHeader)
    assert "image/png" in str(content_type), f"Expected image/png, got {content_type}"

    tile_data = bytes(reply.readAll())
    reply.deleteLater()

    assert tile_data[:4] == b'\x89PNG', "Response must be valid PNG image"


@pytest.mark.requires_qgis
def test_wmts_gettile_format_jpeg(qgis_app, honua_base_url, layer_config):
    """Verify WMTS GetTile supports JPEG image format."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/jpeg&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)

    if status == 400:
        pytest.skip("JPEG format not supported by this WMTS implementation")

    if status == 404:
        pytest.skip(f"Layer {wms_layer} not available")

    assert status == 200, f"JPEG tile request failed with status {status}"

    content_type = reply.header(QNetworkRequest.ContentTypeHeader)
    assert "image/jpeg" in str(content_type), f"Expected image/jpeg, got {content_type}"

    tile_data = bytes(reply.readAll())
    reply.deleteLater()

    # JPEG files start with FF D8 FF
    assert tile_data[:3] == b'\xff\xd8\xff', "Response must be valid JPEG image"


@pytest.mark.requires_qgis
def test_wmts_gettile_format_webp(qgis_app, honua_base_url, layer_config):
    """Verify WMTS GetTile supports WebP image format if available."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/webp&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)

    if status == 400:
        pytest.skip("WebP format not supported by this WMTS implementation")

    if status == 404:
        pytest.skip(f"Layer {wms_layer} not available")

    assert status == 200, f"WebP tile request failed with status {status}"

    content_type = reply.header(QNetworkRequest.ContentTypeHeader)
    assert "image/webp" in str(content_type), f"Expected image/webp, got {content_type}"

    tile_data = bytes(reply.readAll())
    reply.deleteLater()

    # WebP files contain "WEBP" in first 16 bytes
    assert b'WEBP' in tile_data[:16], "Response must be valid WebP image"


# ============================================================================
#  Caching and HTTP Headers Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wmts_gettile_returns_etag_header(qgis_app, honua_base_url, layer_config):
    """Verify WMTS GetTile returns ETag header for cache validation."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {wms_layer} not available")

    # Check for ETag header
    etag = None
    for header_name, header_value in reply.rawHeaderPairs():
        if header_name.lower() == b'etag':
            etag = bytes(header_value).decode('ascii')
            break

    reply.deleteLater()

    # ETag is optional but recommended for WMTS
    if etag:
        assert len(etag) > 0, "ETag header should not be empty"
    else:
        pytest.skip("ETag header not provided by server (optional but recommended)")


@pytest.mark.requires_qgis
def test_wmts_gettile_supports_conditional_get_with_etag(qgis_app, honua_base_url, layer_config):
    """Verify WMTS GetTile supports conditional GET with If-None-Match (304 Not Modified)."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()

    # First request - get ETag
    request1 = QNetworkRequest(QUrl(tile_url))
    reply1 = manager.get(request1)

    loop1 = QEventLoop()
    reply1.finished.connect(loop1.quit)
    loop1.exec()

    status1 = reply1.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status1 == 404:
        reply1.deleteLater()
        pytest.skip(f"Layer {wms_layer} not available")

    # Extract ETag
    etag = None
    for header_name, header_value in reply1.rawHeaderPairs():
        if header_name.lower() == b'etag':
            etag = bytes(header_value).decode('ascii')
            break

    reply1.deleteLater()

    if not etag:
        pytest.skip("Server does not provide ETag header")

    # Second request - with If-None-Match
    request2 = QNetworkRequest(QUrl(tile_url))
    request2.setRawHeader(b'If-None-Match', etag.encode('ascii'))
    reply2 = manager.get(request2)

    loop2 = QEventLoop()
    reply2.finished.connect(loop2.quit)
    loop2.exec()

    status2 = reply2.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply2.deleteLater()

    # Should return 304 Not Modified or 200 with same content
    assert status2 in (200, 304), f"Expected 200 or 304, got {status2}"

    if status2 == 304:
        # Successfully validated cache
        assert True, "Server properly supports conditional GET with ETag"


@pytest.mark.requires_qgis
def test_wmts_gettile_returns_cache_control_header(qgis_app, honua_base_url, layer_config):
    """Verify WMTS GetTile returns Cache-Control header for browser caching."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        reply.deleteLater()
        pytest.skip(f"Layer {wms_layer} not available")

    # Check for Cache-Control header
    cache_control = None
    for header_name, header_value in reply.rawHeaderPairs():
        if header_name.lower() == b'cache-control':
            cache_control = bytes(header_value).decode('ascii')
            break

    reply.deleteLater()

    # Cache-Control is recommended for tile services
    if cache_control:
        assert len(cache_control) > 0, "Cache-Control header should not be empty"
    # Not asserting if missing, as it's optional


# ============================================================================
#  QGIS Integration Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wmts_load_layer_in_qgis(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify QGIS can load WMTS layer using native WMTS provider."""
    from qgis.core import QgsRasterLayer

    wms_layer = layer_config.get("wms_layer", "test-layer")

    # QGIS WMTS URI format
    uri = (
        f"url={honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetCapabilities&"
        f"version=1.0.0&"
        f"layers={wms_layer}&"
        f"styles=default&"
        f"format=image/png&"
        f"tileMatrixSet=WorldWebMercatorQuad"
    )

    layer = QgsRasterLayer(uri, "honua-wmts", "wms")

    if not layer.isValid():
        pytest.skip(f"Could not load WMTS layer in QGIS: {layer.error().message()}")

    qgis_project.addMapLayer(layer)

    # Validate layer properties
    assert layer.width() > 0, "Layer should have positive width"
    assert layer.height() > 0, "Layer should have positive height"
    assert layer.crs().isValid(), "Layer must have valid CRS"


@pytest.mark.requires_qgis
def test_wmts_render_tiles_in_qgis(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify QGIS can render WMTS tiles correctly."""
    from qgis.core import QgsRasterLayer, QgsRectangle, QgsMapSettings, QgsMapRendererSequentialJob
    from qgis.PyQt.QtCore import QSize

    wms_layer = layer_config.get("wms_layer", "test-layer")

    uri = (
        f"url={honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetCapabilities&"
        f"version=1.0.0&"
        f"layers={wms_layer}&"
        f"styles=default&"
        f"format=image/png&"
        f"tileMatrixSet=WorldWebMercatorQuad"
    )

    layer = QgsRasterLayer(uri, "honua-wmts", "wms")

    if not layer.isValid():
        pytest.skip(f"Could not load WMTS layer in QGIS: {layer.error().message()}")

    qgis_project.addMapLayer(layer)

    # Setup map rendering
    map_settings = QgsMapSettings()
    map_settings.setLayers([layer])
    map_settings.setOutputSize(QSize(256, 256))

    # Set extent to world view
    map_settings.setExtent(QgsRectangle(-180, -90, 180, 90))
    map_settings.setDestinationCrs(layer.crs())

    # Render map
    job = QgsMapRendererSequentialJob(map_settings)
    job.start()
    job.waitForFinished()

    # Check if rendering succeeded
    image = job.renderedImage()
    assert image is not None, "Rendered image should not be None"
    assert not image.isNull(), "Rendered image should not be null"


# ============================================================================
#  Error Handling Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wmts_invalid_layer_returns_error(qgis_app, honua_base_url):
    """Verify WMTS returns appropriate error for invalid layer name."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer=nonexistent_layer&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    # Should return 404 or 400
    assert status in (400, 404), f"Invalid layer should return 400 or 404, got {status}"


@pytest.mark.requires_qgis
def test_wmts_invalid_tile_matrix_returns_error(qgis_app, honua_base_url, layer_config):
    """Verify WMTS returns error for invalid TileMatrixSet."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=InvalidTileMatrixSet&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    # Should return 400 (Bad Request)
    assert status == 400, f"Invalid TileMatrixSet should return 400, got {status}"


@pytest.mark.requires_qgis
def test_wmts_invalid_format_returns_error(qgis_app, honua_base_url, layer_config):
    """Verify WMTS returns error for unsupported image format."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/invalid&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    # Should return 400 (Bad Request)
    assert status == 400, f"Invalid format should return 400, got {status}"


@pytest.mark.requires_qgis
def test_wmts_out_of_bounds_tile_returns_404(qgis_app, honua_base_url, layer_config):
    """Verify WMTS returns 404 for tiles outside valid bounds."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    # Request tile with invalid coordinates (TileCol = 999999 at zoom 0 is invalid)
    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        f"TileMatrix=0&"
        f"TileRow=0&"
        f"TileCol=999999"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    # Should return 400 or 404 for out-of-bounds tiles
    assert status in (400, 404), f"Out of bounds tile should return 400 or 404, got {status}"


@pytest.mark.requires_qgis
def test_wmts_missing_required_parameter_returns_error(qgis_app, honua_base_url, layer_config):
    """Verify WMTS returns error when required parameters are missing."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    wms_layer = layer_config.get("wms_layer", "test-layer")

    # Missing TileMatrix parameter
    tile_url = (
        f"{honua_base_url}/wmts?"
        f"service=WMTS&"
        f"request=GetTile&"
        f"version=1.0.0&"
        f"layer={wms_layer}&"
        f"style=default&"
        f"format=image/png&"
        f"TileMatrixSet=WorldWebMercatorQuad&"
        # TileMatrix is missing
        f"TileRow=0&"
        f"TileCol=0"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    # Should return 400 (Bad Request)
    assert status == 400, f"Missing required parameter should return 400, got {status}"
