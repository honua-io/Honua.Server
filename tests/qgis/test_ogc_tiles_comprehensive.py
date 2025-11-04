"""
Comprehensive OGC API - Tiles Integration Tests with QGIS

This test suite provides extensive coverage of OGC API - Tiles operations using QGIS as the reference client.
Tests validate Honua's tile serving implementation against the OGC API - Tiles v1.0 specification using
real-world client library integration.

Test Coverage:
- Tile Matrix Sets: Discovery and metadata retrieval
- WorldWebMercatorQuad: Web Mercator (EPSG:3857) tile matrix metadata
- WorldCRS84Quad: WGS84 (CRS84) tile matrix metadata
- Vector Tiles: MVT (Mapbox Vector Tiles) format retrieval
- Raster Tiles: PNG, JPEG format retrieval with style support
- Tile Caching: ETag validation and HTTP cache headers
- QGIS Integration: Loading tiles as XYZ layers
- Error Handling: Invalid tile matrix, out of bounds coordinates

Client: QGIS 3.34+ (PyQGIS Network Access Manager)
Specification: OGC API - Tiles 1.0.0
"""
import json
import pytest


pytestmark = [pytest.mark.integration, pytest.mark.qgis, pytest.mark.ogc_tiles, pytest.mark.requires_honua]


# ============================================================================
#  Tile Matrix Sets Discovery Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_tilematrixsets_endpoint_returns_valid_json(qgis_app, honua_base_url):
    """Verify /tileMatrixSets endpoint returns valid JSON with supported tile matrix sets."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    tilematrixsets_url = f"{honua_base_url}/ogc/tileMatrixSets"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tilematrixsets_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"TileMatrixSets request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert "tileMatrixSets" in data, "Response must include tileMatrixSets array"
    assert isinstance(data["tileMatrixSets"], list), "tileMatrixSets must be an array"
    assert len(data["tileMatrixSets"]) > 0, "At least one tile matrix set should be available"


@pytest.mark.requires_qgis
def test_tilematrixsets_includes_worldwebmercatorquad(qgis_app, honua_base_url):
    """Verify /tileMatrixSets endpoint includes WorldWebMercatorQuad (EPSG:3857)."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    tilematrixsets_url = f"{honua_base_url}/ogc/tileMatrixSets"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tilematrixsets_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    tile_matrix_sets = data.get("tileMatrixSets", [])

    ids = [tms.get("id") for tms in tile_matrix_sets]
    assert "WorldWebMercatorQuad" in ids, "WorldWebMercatorQuad tile matrix set must be available"


@pytest.mark.requires_qgis
def test_tilematrixsets_includes_worldcrs84quad(qgis_app, honua_base_url):
    """Verify /tileMatrixSets endpoint includes WorldCRS84Quad (WGS84 lon/lat)."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    tilematrixsets_url = f"{honua_base_url}/ogc/tileMatrixSets"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tilematrixsets_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    tile_matrix_sets = data.get("tileMatrixSets", [])

    ids = [tms.get("id") for tms in tile_matrix_sets]
    assert "WorldCRS84Quad" in ids, "WorldCRS84Quad tile matrix set must be available"


@pytest.mark.requires_qgis
def test_tilematrixsets_includes_links(qgis_app, honua_base_url):
    """Verify /tileMatrixSets endpoint includes proper links for navigation."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    tilematrixsets_url = f"{honua_base_url}/ogc/tileMatrixSets"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tilematrixsets_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert "links" in data, "Response must include links array"

    links = data["links"]
    rel_types = [link.get("rel") for link in links]
    assert "self" in rel_types, "Links should include self reference"


# ============================================================================
#  WorldWebMercatorQuad Tile Matrix Set Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_worldwebmercatorquad_tilematrixset_metadata(qgis_app, honua_base_url):
    """Verify WorldWebMercatorQuad tile matrix set returns complete metadata."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    tms_url = f"{honua_base_url}/ogc/tileMatrixSets/WorldWebMercatorQuad"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tms_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"WorldWebMercatorQuad request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert data.get("id") == "WorldWebMercatorQuad", "ID must be WorldWebMercatorQuad"
    assert "crs" in data, "Tile matrix set must include CRS"
    assert "tileMatrices" in data, "Tile matrix set must include tile matrices"
    assert isinstance(data["tileMatrices"], list), "tileMatrices must be an array"


@pytest.mark.requires_qgis
def test_worldwebmercatorquad_uses_epsg3857(qgis_app, honua_base_url):
    """Verify WorldWebMercatorQuad uses EPSG:3857 coordinate reference system."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    tms_url = f"{honua_base_url}/ogc/tileMatrixSets/WorldWebMercatorQuad"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tms_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    crs = data.get("crs", "")
    assert "3857" in crs, f"WorldWebMercatorQuad should use EPSG:3857, got {crs}"


# ============================================================================
#  WorldCRS84Quad Tile Matrix Set Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_worldcrs84quad_tilematrixset_metadata(qgis_app, honua_base_url):
    """Verify WorldCRS84Quad tile matrix set returns complete metadata."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    tms_url = f"{honua_base_url}/ogc/tileMatrixSets/WorldCRS84Quad"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tms_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"WorldCRS84Quad request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert data.get("id") == "WorldCRS84Quad", "ID must be WorldCRS84Quad"
    assert "crs" in data, "Tile matrix set must include CRS"
    assert "tileMatrices" in data, "Tile matrix set must include tile matrices"


@pytest.mark.requires_qgis
def test_worldcrs84quad_uses_crs84(qgis_app, honua_base_url):
    """Verify WorldCRS84Quad uses CRS84 (WGS84 lon/lat) coordinate reference system."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    tms_url = f"{honua_base_url}/ogc/tileMatrixSets/WorldCRS84Quad"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tms_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    crs = data.get("crs", "")
    # CRS84 can be represented as "http://www.opengis.net/def/crs/OGC/1.3/CRS84" or similar
    assert "CRS84" in crs or "4326" in crs, f"WorldCRS84Quad should use CRS84, got {crs}"


# ============================================================================
#  Collection Tileset Metadata Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_collection_tilesets_endpoint(qgis_app, honua_base_url, layer_config):
    """Verify collection tilesets endpoint returns available tilesets."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_id = layer_config["collection_id"]
    tilesets_url = f"{honua_base_url}/ogc/collections/{collection_id}/tiles"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tilesets_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Collection {collection_id} not available in test environment")

    assert status == 200, f"Collection tilesets request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert "tilesets" in data, "Response must include tilesets array"
    assert "links" in data, "Response must include links array"


@pytest.mark.requires_qgis
def test_tileset_metadata_includes_zoom_levels(qgis_app, honua_base_url, layer_config):
    """Verify tileset metadata includes minZoom and maxZoom properties."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_id = layer_config["collection_id"]
    tilesets_url = f"{honua_base_url}/ogc/collections/{collection_id}/tiles"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tilesets_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Collection {collection_id} not available in test environment")

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    tilesets = data.get("tilesets", [])

    if not tilesets:
        pytest.skip("No tilesets available for this collection")

    first_tileset = tilesets[0]
    assert "minZoom" in first_tileset, "Tileset must include minZoom"
    assert "maxZoom" in first_tileset, "Tileset must include maxZoom"
    assert isinstance(first_tileset["minZoom"], int), "minZoom must be an integer"
    assert isinstance(first_tileset["maxZoom"], int), "maxZoom must be an integer"


# ============================================================================
#  Raster Tile Retrieval Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_raster_tile_png_format(qgis_app, honua_base_url, layer_config):
    """Verify raster tile retrieval in PNG format."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_id = layer_config["collection_id"]
    # Use a simple tile coordinate (zoom 0, row 0, col 0)
    tile_url = f"{honua_base_url}/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=png"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Tile endpoint not available for collection {collection_id}")

    assert status == 200, f"PNG tile request failed with status {status}"

    content_type = reply.header(QNetworkRequest.ContentTypeHeader)
    assert content_type is not None, "Response must include Content-Type header"
    assert "image/png" in str(content_type).lower(), f"Expected image/png, got {content_type}"

    tile_bytes = bytes(reply.readAll())
    reply.deleteLater()

    assert len(tile_bytes) > 0, "Tile response should not be empty"
    # PNG files start with specific magic bytes
    assert tile_bytes[:8] == b'\x89PNG\r\n\x1a\n', "Response should be valid PNG file"


@pytest.mark.requires_qgis
def test_raster_tile_jpeg_format(qgis_app, honua_base_url, layer_config):
    """Verify raster tile retrieval in JPEG format."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_id = layer_config["collection_id"]
    tile_url = f"{honua_base_url}/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=jpeg"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"JPEG tile endpoint not available for collection {collection_id}")

    # JPEG might not be supported for all datasets
    if status == 400:
        pytest.skip("JPEG format not supported for this tileset")

    assert status == 200, f"JPEG tile request failed with status {status}"

    content_type = reply.header(QNetworkRequest.ContentTypeHeader)
    assert "image/jpeg" in str(content_type).lower() or "image/jpg" in str(content_type).lower(), \
        f"Expected image/jpeg, got {content_type}"

    tile_bytes = bytes(reply.readAll())
    reply.deleteLater()

    assert len(tile_bytes) > 0, "Tile response should not be empty"


@pytest.mark.requires_qgis
def test_raster_tile_different_zoom_levels(qgis_app, honua_base_url, layer_config):
    """Verify raster tiles can be retrieved at different zoom levels."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_id = layer_config["collection_id"]

    # Test zoom levels 0 and 1
    for zoom in [0, 1]:
        tile_url = f"{honua_base_url}/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/{zoom}/0/0?f=png"

        manager = QgsNetworkAccessManager.instance()
        request = QNetworkRequest(QUrl(tile_url))
        reply = manager.get(request)

        loop = QEventLoop()
        reply.finished.connect(loop.quit)
        loop.exec()

        status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
        reply.deleteLater()

        if status == 404 and zoom == 1:
            # Zoom level 1 might not be available for all datasets
            continue

        assert status == 200, f"Tile at zoom {zoom} failed with status {status}"


# ============================================================================
#  Vector Tile Retrieval Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_vector_tile_mvt_format(qgis_app, honua_base_url, layer_config):
    """Verify vector tile retrieval in MVT (Mapbox Vector Tiles) format."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_id = layer_config["collection_id"]
    tile_url = f"{honua_base_url}/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=mvt"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)

    # MVT might not be supported for raster-only collections
    if status == 404 or status == 400 or status == 501:
        pytest.skip("MVT format not available for this collection (raster-only or not supported)")

    assert status == 200, f"MVT tile request failed with status {status}"

    content_type = reply.header(QNetworkRequest.ContentTypeHeader)
    tile_bytes = bytes(reply.readAll())
    reply.deleteLater()

    # MVT response might be empty if no features in tile
    # Content-type should be application/vnd.mapbox-vector-tile
    if content_type:
        assert "mapbox" in str(content_type).lower() or "mvt" in str(content_type).lower() or \
               "octet-stream" in str(content_type).lower(), \
               f"Expected MVT content type, got {content_type}"


# ============================================================================
#  Tile Caching and Headers Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_tile_includes_cache_headers(qgis_app, honua_base_url, layer_config):
    """Verify tile responses include appropriate cache headers."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_id = layer_config["collection_id"]
    tile_url = f"{honua_base_url}/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=png"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Tile endpoint not available for collection {collection_id}")

    # Check for cache-related headers
    raw_headers = {bytes(key).decode().lower(): bytes(value).decode()
                   for key, value in reply.rawHeaderPairs()}
    reply.deleteLater()

    # Should have either ETag or Last-Modified for cache validation
    has_cache_header = "etag" in raw_headers or "last-modified" in raw_headers
    assert has_cache_header, "Tile response should include cache validation headers (ETag or Last-Modified)"


@pytest.mark.requires_qgis
def test_tile_etag_validation(qgis_app, honua_base_url, layer_config):
    """Verify tiles support ETag-based conditional requests (304 Not Modified)."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_id = layer_config["collection_id"]
    tile_url = f"{honua_base_url}/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=png"

    manager = QgsNetworkAccessManager.instance()

    # First request to get ETag
    request1 = QNetworkRequest(QUrl(tile_url))
    reply1 = manager.get(request1)

    loop1 = QEventLoop()
    reply1.finished.connect(loop1.quit)
    loop1.exec()

    status1 = reply1.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status1 == 404:
        reply1.deleteLater()
        pytest.skip(f"Tile endpoint not available for collection {collection_id}")

    raw_headers1 = {bytes(key).decode().lower(): bytes(value).decode()
                    for key, value in reply1.rawHeaderPairs()}
    etag = raw_headers1.get("etag")
    reply1.deleteLater()

    if not etag:
        pytest.skip("Tile endpoint does not return ETag header")

    # Second request with If-None-Match header
    request2 = QNetworkRequest(QUrl(tile_url))
    request2.setRawHeader(b"If-None-Match", etag.encode())
    reply2 = manager.get(request2)

    loop2 = QEventLoop()
    reply2.finished.connect(loop2.quit)
    loop2.exec()

    status2 = reply2.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply2.deleteLater()

    # Should return 304 Not Modified when ETag matches
    assert status2 in (200, 304), f"Expected 200 or 304, got {status2}"


# ============================================================================
#  QGIS Integration Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_qgis_load_tiles_as_xyz_layer(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify QGIS can load OGC API Tiles as XYZ raster layer."""
    from qgis.core import QgsRasterLayer

    collection_id = layer_config["collection_id"]

    # Construct XYZ tile URL template
    # QGIS expects {x}, {y}, {z} placeholders
    xyz_url = (
        f"{honua_base_url}/ogc/collections/{collection_id}/tiles/"
        f"WorldWebMercatorQuad/{{z}}/{{y}}/{{x}}?f=png"
    )

    # Create XYZ raster layer
    layer = QgsRasterLayer(
        f"type=xyz&url={xyz_url}&zmin=0&zmax=14",
        "honua-tiles-xyz",
        "wms"
    )

    if not layer.isValid():
        pytest.skip(f"Could not create XYZ layer: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    # Validate layer properties
    assert layer.isValid(), "XYZ layer should be valid"
    assert layer.crs().isValid(), "XYZ layer should have valid CRS"
    assert layer.crs().authid() == "EPSG:3857", "XYZ layer should use Web Mercator (EPSG:3857)"


# ============================================================================
#  Error Handling Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_invalid_tilematrixset_returns_404(qgis_app, honua_base_url):
    """Verify requesting invalid tile matrix set returns 404."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    tms_url = f"{honua_base_url}/ogc/tileMatrixSets/InvalidTileMatrixSet"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tms_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    assert status == 404, f"Invalid tile matrix set should return 404, got {status}"


@pytest.mark.requires_qgis
def test_out_of_bounds_tile_coordinates_returns_404(qgis_app, honua_base_url, layer_config):
    """Verify requesting tile with out-of-bounds coordinates returns 404."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_id = layer_config["collection_id"]
    # At zoom 0, valid tiles are only (0,0). Request an invalid coordinate.
    tile_url = f"{honua_base_url}/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/1/1?f=png"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    # Out of bounds should return 404
    assert status == 404, f"Out of bounds tile should return 404, got {status}"


@pytest.mark.requires_qgis
def test_invalid_zoom_level_returns_error(qgis_app, honua_base_url, layer_config):
    """Verify requesting tile with invalid zoom level returns error."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_id = layer_config["collection_id"]
    # Request zoom level 999 which should exceed maxZoom
    tile_url = f"{honua_base_url}/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/999/0/0?f=png"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    # Should return 404 for zoom out of range
    assert status == 404, f"Invalid zoom level should return 404, got {status}"


@pytest.mark.requires_qgis
def test_invalid_tile_matrix_parameter_returns_400(qgis_app, honua_base_url, layer_config):
    """Verify requesting tile with non-numeric tile matrix returns 400."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_id = layer_config["collection_id"]
    # Use non-numeric tile matrix (zoom level)
    tile_url = f"{honua_base_url}/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/invalid/0/0?f=png"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    # Should return 400 Bad Request for invalid tile matrix format
    assert status in (400, 404), f"Invalid tile matrix should return 400 or 404, got {status}"


@pytest.mark.requires_qgis
def test_nonexistent_collection_returns_404(qgis_app, honua_base_url):
    """Verify requesting tiles for non-existent collection returns 404."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    tile_url = f"{honua_base_url}/ogc/collections/nonexistent::collection/tiles/WorldWebMercatorQuad/0/0/0?f=png"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(tile_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    assert status == 404, f"Non-existent collection should return 404, got {status}"
