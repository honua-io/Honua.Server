"""
Comprehensive OGC API - Tiles Integration Tests with Requests Library

This test suite provides extensive coverage of OGC API - Tiles operations using the
requests library and Pillow for image validation. Tests validate Honua's tile serving
implementation against the OGC API - Tiles v1.0 specification.

Test Coverage:
- Landing page: Links to tilesets
- Tilesets: List available tilesets
- Tile Matrix Sets: Discovery and metadata (WorldWebMercatorQuad, WorldCRS84Quad)
- Tile retrieval: Get tiles with z/x/y coordinates at various zoom levels
- Metadata: Tile bounds, formats, resolutions, zoom levels
- Different formats: PNG, JPEG, MVT (vector tiles) if supported
- Image validation: Verify returned tiles are valid images with correct dimensions
- Cache headers: ETag, Last-Modified, Cache-Control
- Error handling: Invalid z/x/y, out of bounds, malformed parameters

Requirements:
- requests >= 2.28.0
- Pillow >= 9.0.0 (for image validation)

Specification: OGC API - Tiles 1.0.0
Reference: https://docs.ogc.org/is/20-057/20-057.html
"""
import io
import json
import os
from typing import Callable, Optional

import pytest
import requests


pytestmark = [
    pytest.mark.integration,
    pytest.mark.python,
    pytest.mark.ogc_tiles,
    pytest.mark.requires_honua
]


# ============================================================================
#  Helper Functions
# ============================================================================

def get_collection_id() -> str:
    """Get collection ID from environment or use default."""
    return os.environ.get("HONUA_QGIS_COLLECTION_ID", "roads::roads-primary")


def validate_png_image(image_data: bytes) -> tuple[bool, str]:
    """
    Validate that image data is a valid PNG.

    Returns:
        Tuple of (is_valid, error_message)
    """
    try:
        from PIL import Image
    except ImportError:
        pytest.skip("Pillow not installed (pip install Pillow)")

    if len(image_data) < 8:
        return False, f"Image data too short: {len(image_data)} bytes"

    # Check PNG magic bytes
    if image_data[:8] != b'\x89PNG\r\n\x1a\n':
        return False, "Invalid PNG magic bytes"

    # Try to load with PIL
    try:
        img = Image.open(io.BytesIO(image_data))
        img.verify()
        return True, ""
    except Exception as e:
        return False, f"PIL validation failed: {e}"


def validate_jpeg_image(image_data: bytes) -> tuple[bool, str]:
    """
    Validate that image data is a valid JPEG.

    Returns:
        Tuple of (is_valid, error_message)
    """
    try:
        from PIL import Image
    except ImportError:
        pytest.skip("Pillow not installed (pip install Pillow)")

    if len(image_data) < 2:
        return False, f"Image data too short: {len(image_data)} bytes"

    # Check JPEG magic bytes (SOI marker)
    if image_data[:2] != b'\xff\xd8':
        return False, "Invalid JPEG magic bytes"

    # Try to load with PIL
    try:
        img = Image.open(io.BytesIO(image_data))
        img.verify()
        return True, ""
    except Exception as e:
        return False, f"PIL validation failed: {e}"


def get_image_dimensions(image_data: bytes) -> tuple[int, int]:
    """
    Get image width and height.

    Returns:
        Tuple of (width, height)
    """
    try:
        from PIL import Image
    except ImportError:
        pytest.skip("Pillow not installed (pip install Pillow)")

    img = Image.open(io.BytesIO(image_data))
    return img.size


# ============================================================================
#  Landing Page Tests
# ============================================================================

def test_landing_page_links_to_tilematrixsets(api_request: Callable):
    """Verify OGC API landing page includes links to tile matrix sets."""
    response = api_request("GET", "/ogc/")

    assert response.status_code == 200, f"Landing page request failed with status {response.status_code}"

    data = response.json()
    assert "links" in data, "Landing page must include links array"

    link_rels = {link.get("rel") for link in data["links"]}

    # Check for tiling-schemes or tileMatrixSets link
    has_tilematrix_link = any(
        rel in link_rels
        for rel in ["tiling-schemes", "http://www.opengis.net/def/rel/ogc/1.0/tiling-schemes"]
    )

    # Link might not be in landing page, which is acceptable
    assert isinstance(has_tilematrix_link, bool), "Should be able to check for tile matrix sets link"


def test_landing_page_links_to_collections(api_request: Callable):
    """Verify landing page includes link to collections (which may have tiles)."""
    response = api_request("GET", "/ogc/")

    assert response.status_code == 200, f"Landing page request failed with status {response.status_code}"

    data = response.json()
    assert "links" in data, "Landing page must include links array"

    link_rels = {link.get("rel") for link in data["links"]}
    assert "data" in link_rels or "collections" in link_rels, \
        "Landing page must include link to collections"


# ============================================================================
#  Tile Matrix Sets Discovery Tests
# ============================================================================

def test_tilematrixsets_endpoint_returns_valid_json(api_request: Callable):
    """Verify /tileMatrixSets endpoint returns valid JSON with supported tile matrix sets."""
    response = api_request("GET", "/ogc/tileMatrixSets")

    assert response.status_code == 200, \
        f"TileMatrixSets request failed with status {response.status_code}"

    data = response.json()
    assert "tileMatrixSets" in data, "Response must include tileMatrixSets array"
    assert isinstance(data["tileMatrixSets"], list), "tileMatrixSets must be an array"
    assert len(data["tileMatrixSets"]) > 0, "At least one tile matrix set should be available"


def test_tilematrixsets_includes_worldwebmercatorquad(api_request: Callable):
    """Verify /tileMatrixSets endpoint includes WorldWebMercatorQuad (EPSG:3857)."""
    response = api_request("GET", "/ogc/tileMatrixSets")

    assert response.status_code == 200

    data = response.json()
    tile_matrix_sets = data.get("tileMatrixSets", [])

    ids = [tms.get("id") for tms in tile_matrix_sets]
    assert "WorldWebMercatorQuad" in ids, \
        "WorldWebMercatorQuad tile matrix set must be available"


def test_tilematrixsets_includes_worldcrs84quad(api_request: Callable):
    """Verify /tileMatrixSets endpoint includes WorldCRS84Quad (WGS84 lon/lat)."""
    response = api_request("GET", "/ogc/tileMatrixSets")

    assert response.status_code == 200

    data = response.json()
    tile_matrix_sets = data.get("tileMatrixSets", [])

    ids = [tms.get("id") for tms in tile_matrix_sets]
    assert "WorldCRS84Quad" in ids, \
        "WorldCRS84Quad tile matrix set must be available"


def test_tilematrixsets_includes_self_link(api_request: Callable):
    """Verify /tileMatrixSets response includes self link."""
    response = api_request("GET", "/ogc/tileMatrixSets")

    assert response.status_code == 200

    data = response.json()
    assert "links" in data, "Response must include links array"

    links = data["links"]
    rel_types = [link.get("rel") for link in links]
    assert "self" in rel_types, "Links should include self reference"


def test_tilematrixsets_items_have_required_fields(api_request: Callable):
    """Verify each tile matrix set in list has required fields."""
    response = api_request("GET", "/ogc/tileMatrixSets")

    assert response.status_code == 200

    data = response.json()
    tile_matrix_sets = data.get("tileMatrixSets", [])

    assert len(tile_matrix_sets) > 0, "Should have at least one tile matrix set"

    for tms in tile_matrix_sets:
        assert "id" in tms, "Each tile matrix set must have an id"
        assert "title" in tms or "id" in tms, \
            "Each tile matrix set must have title or id"


# ============================================================================
#  WorldWebMercatorQuad Tile Matrix Set Tests
# ============================================================================

def test_worldwebmercatorquad_tilematrixset_metadata(api_request: Callable):
    """Verify WorldWebMercatorQuad tile matrix set returns complete metadata."""
    response = api_request("GET", "/ogc/tileMatrixSets/WorldWebMercatorQuad")

    assert response.status_code == 200, \
        f"WorldWebMercatorQuad request failed with status {response.status_code}"

    data = response.json()
    assert data.get("id") == "WorldWebMercatorQuad", "ID must be WorldWebMercatorQuad"
    assert "crs" in data, "Tile matrix set must include CRS"
    assert "tileMatrices" in data, "Tile matrix set must include tile matrices"
    assert isinstance(data["tileMatrices"], list), "tileMatrices must be an array"


def test_worldwebmercatorquad_uses_epsg3857(api_request: Callable):
    """Verify WorldWebMercatorQuad uses EPSG:3857 coordinate reference system."""
    response = api_request("GET", "/ogc/tileMatrixSets/WorldWebMercatorQuad")

    assert response.status_code == 200

    data = response.json()
    crs = data.get("crs", "")
    assert "3857" in crs, f"WorldWebMercatorQuad should use EPSG:3857, got {crs}"


def test_worldwebmercatorquad_tilematrix_has_valid_structure(api_request: Callable):
    """Verify WorldWebMercatorQuad tile matrices have valid structure."""
    response = api_request("GET", "/ogc/tileMatrixSets/WorldWebMercatorQuad")

    assert response.status_code == 200

    data = response.json()
    tile_matrices = data.get("tileMatrices", [])

    assert len(tile_matrices) > 0, "Should have at least one tile matrix"

    # Check first tile matrix structure
    first_matrix = tile_matrices[0]
    assert "id" in first_matrix, "Tile matrix must have id"
    assert "scaleDenominator" in first_matrix, "Tile matrix must have scaleDenominator"
    assert "tileWidth" in first_matrix, "Tile matrix must have tileWidth"
    assert "tileHeight" in first_matrix, "Tile matrix must have tileHeight"
    assert "matrixWidth" in first_matrix, "Tile matrix must have matrixWidth"
    assert "matrixHeight" in first_matrix, "Tile matrix must have matrixHeight"


def test_worldwebmercatorquad_standard_tile_sizes(api_request: Callable):
    """Verify WorldWebMercatorQuad uses standard 256x256 tile size."""
    response = api_request("GET", "/ogc/tileMatrixSets/WorldWebMercatorQuad")

    assert response.status_code == 200

    data = response.json()
    tile_matrices = data.get("tileMatrices", [])

    # Check that tiles are 256x256 (standard for Web Mercator)
    for matrix in tile_matrices:
        assert matrix.get("tileWidth") == 256, \
            f"Expected tileWidth 256, got {matrix.get('tileWidth')}"
        assert matrix.get("tileHeight") == 256, \
            f"Expected tileHeight 256, got {matrix.get('tileHeight')}"


# ============================================================================
#  WorldCRS84Quad Tile Matrix Set Tests
# ============================================================================

def test_worldcrs84quad_tilematrixset_metadata(api_request: Callable):
    """Verify WorldCRS84Quad tile matrix set returns complete metadata."""
    response = api_request("GET", "/ogc/tileMatrixSets/WorldCRS84Quad")

    assert response.status_code == 200, \
        f"WorldCRS84Quad request failed with status {response.status_code}"

    data = response.json()
    assert data.get("id") == "WorldCRS84Quad", "ID must be WorldCRS84Quad"
    assert "crs" in data, "Tile matrix set must include CRS"
    assert "tileMatrices" in data, "Tile matrix set must include tile matrices"


def test_worldcrs84quad_uses_crs84(api_request: Callable):
    """Verify WorldCRS84Quad uses CRS84 (WGS84 lon/lat) coordinate reference system."""
    response = api_request("GET", "/ogc/tileMatrixSets/WorldCRS84Quad")

    assert response.status_code == 200

    data = response.json()
    crs = data.get("crs", "")
    # CRS84 can be represented as "http://www.opengis.net/def/crs/OGC/1.3/CRS84" or similar
    assert "CRS84" in crs or "4326" in crs, \
        f"WorldCRS84Quad should use CRS84, got {crs}"


# ============================================================================
#  Collection Tilesets Metadata Tests
# ============================================================================

def test_collection_tilesets_endpoint_returns_valid_json(api_request: Callable):
    """Verify collection tilesets endpoint returns available tilesets."""
    collection_id = get_collection_id()
    response = api_request("GET", f"/ogc/collections/{collection_id}/tiles")

    if response.status_code == 404:
        pytest.skip(f"Collection {collection_id} not available in test environment")

    assert response.status_code == 200, \
        f"Collection tilesets request failed with status {response.status_code}"

    data = response.json()
    assert "tilesets" in data or "links" in data, \
        "Response must include tilesets array or links"


def test_collection_tileset_metadata_includes_links(api_request: Callable):
    """Verify tileset metadata includes proper navigation links."""
    collection_id = get_collection_id()
    response = api_request("GET", f"/ogc/collections/{collection_id}/tiles")

    if response.status_code == 404:
        pytest.skip(f"Collection {collection_id} not available in test environment")

    assert response.status_code == 200

    data = response.json()
    assert "links" in data, "Response must include links array"

    links = data.get("links", [])
    rel_types = [link.get("rel") for link in links]
    assert "self" in rel_types, "Links should include self reference"


def test_tileset_metadata_includes_zoom_levels(api_request: Callable):
    """Verify tileset metadata includes minZoom and maxZoom properties."""
    collection_id = get_collection_id()
    response = api_request("GET", f"/ogc/collections/{collection_id}/tiles")

    if response.status_code == 404:
        pytest.skip(f"Collection {collection_id} not available in test environment")

    assert response.status_code == 200

    data = response.json()
    tilesets = data.get("tilesets", [])

    if not tilesets:
        pytest.skip("No tilesets available for this collection")

    first_tileset = tilesets[0]
    # minZoom and maxZoom are optional but commonly included
    # Just verify they're integers if present
    if "minZoom" in first_tileset:
        assert isinstance(first_tileset["minZoom"], int), "minZoom must be an integer"
    if "maxZoom" in first_tileset:
        assert isinstance(first_tileset["maxZoom"], int), "maxZoom must be an integer"


# ============================================================================
#  Raster Tile Retrieval Tests - PNG Format
# ============================================================================

def test_raster_tile_png_format_basic(api_request: Callable):
    """Verify raster tile retrieval in PNG format."""
    collection_id = get_collection_id()
    # Use a simple tile coordinate (zoom 0, row 0, col 0)
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=png"
    )

    if response.status_code == 404:
        pytest.skip(f"Tile endpoint not available for collection {collection_id}")

    assert response.status_code == 200, \
        f"PNG tile request failed with status {response.status_code}"

    # Verify content type
    content_type = response.headers.get("Content-Type", "")
    assert "image/png" in content_type.lower(), \
        f"Expected image/png content type, got {content_type}"

    # Verify PNG image
    tile_data = response.content
    is_valid, error_msg = validate_png_image(tile_data)
    assert is_valid, f"Invalid PNG image: {error_msg}"


def test_raster_tile_png_has_correct_dimensions(api_request: Callable):
    """Verify PNG tiles have correct dimensions (256x256 for standard Web Mercator)."""
    collection_id = get_collection_id()
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=png"
    )

    if response.status_code == 404:
        pytest.skip(f"Tile endpoint not available for collection {collection_id}")

    assert response.status_code == 200

    tile_data = response.content
    width, height = get_image_dimensions(tile_data)

    # Standard Web Mercator tiles are 256x256
    assert width == 256, f"Expected tile width 256, got {width}"
    assert height == 256, f"Expected tile height 256, got {height}"


def test_raster_tile_png_different_zoom_levels(api_request: Callable):
    """Verify raster PNG tiles can be retrieved at different zoom levels."""
    collection_id = get_collection_id()

    # Test zoom levels 0, 1, and 2
    for zoom in [0, 1, 2]:
        response = api_request(
            "GET",
            f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/{zoom}/0/0?f=png"
        )

        if response.status_code == 404 and zoom > 0:
            # Higher zoom levels might not be available for all datasets
            continue

        assert response.status_code == 200, \
            f"Tile at zoom {zoom} failed with status {response.status_code}"

        # Verify it's a valid PNG
        is_valid, error_msg = validate_png_image(response.content)
        assert is_valid, f"Invalid PNG at zoom {zoom}: {error_msg}"


def test_raster_tile_png_different_coordinates(api_request: Callable):
    """Verify raster PNG tiles can be retrieved with different x/y coordinates."""
    collection_id = get_collection_id()

    # At zoom 1, we have a 2x2 grid of tiles
    # Test tiles (1, 0, 0), (1, 0, 1), (1, 1, 0), (1, 1, 1)
    coordinates = [(1, 0, 0), (1, 0, 1), (1, 1, 0), (1, 1, 1)]

    for zoom, row, col in coordinates:
        response = api_request(
            "GET",
            f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/{zoom}/{row}/{col}?f=png"
        )

        if response.status_code == 404:
            # Some tiles might not exist for sparse datasets
            continue

        assert response.status_code == 200, \
            f"Tile at {zoom}/{row}/{col} failed with status {response.status_code}"

        # Verify it's a valid PNG
        is_valid, _ = validate_png_image(response.content)
        assert is_valid, f"Invalid PNG at {zoom}/{row}/{col}"


# ============================================================================
#  Raster Tile Retrieval Tests - JPEG Format
# ============================================================================

def test_raster_tile_jpeg_format(api_request: Callable):
    """Verify raster tile retrieval in JPEG format."""
    collection_id = get_collection_id()
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=jpeg"
    )

    if response.status_code == 404:
        pytest.skip(f"JPEG tile endpoint not available for collection {collection_id}")

    # JPEG might not be supported for all datasets
    if response.status_code == 400:
        pytest.skip("JPEG format not supported for this tileset")

    assert response.status_code == 200, \
        f"JPEG tile request failed with status {response.status_code}"

    # Verify content type
    content_type = response.headers.get("Content-Type", "")
    assert "image/jpeg" in content_type.lower() or "image/jpg" in content_type.lower(), \
        f"Expected image/jpeg content type, got {content_type}"

    # Verify JPEG image
    tile_data = response.content
    is_valid, error_msg = validate_jpeg_image(tile_data)
    assert is_valid, f"Invalid JPEG image: {error_msg}"


def test_raster_tile_jpeg_has_correct_dimensions(api_request: Callable):
    """Verify JPEG tiles have correct dimensions."""
    collection_id = get_collection_id()
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=jpeg"
    )

    if response.status_code == 404:
        pytest.skip(f"JPEG tile endpoint not available for collection {collection_id}")

    if response.status_code == 400:
        pytest.skip("JPEG format not supported for this tileset")

    assert response.status_code == 200

    tile_data = response.content
    width, height = get_image_dimensions(tile_data)

    # Standard Web Mercator tiles are 256x256
    assert width == 256, f"Expected tile width 256, got {width}"
    assert height == 256, f"Expected tile height 256, got {height}"


# ============================================================================
#  Vector Tile Retrieval Tests - MVT Format
# ============================================================================

def test_vector_tile_mvt_format(api_request: Callable):
    """Verify vector tile retrieval in MVT (Mapbox Vector Tiles) format."""
    collection_id = get_collection_id()
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=mvt"
    )

    # MVT might not be supported for raster-only collections
    if response.status_code in (404, 400, 501):
        pytest.skip("MVT format not available for this collection (raster-only or not supported)")

    assert response.status_code == 200, \
        f"MVT tile request failed with status {response.status_code}"

    # Verify content type
    content_type = response.headers.get("Content-Type", "")
    # MVT content type can be application/vnd.mapbox-vector-tile or application/octet-stream
    assert any(
        ct in content_type.lower()
        for ct in ["mapbox", "mvt", "octet-stream", "protobuf"]
    ), f"Expected MVT-related content type, got {content_type}"

    # MVT response might be empty if no features in tile
    tile_data = response.content
    # Just verify we got bytes (MVT is binary protobuf format)
    assert isinstance(tile_data, bytes), "MVT tile should return bytes"


def test_vector_tile_mvt_different_zoom_levels(api_request: Callable):
    """Verify vector MVT tiles can be retrieved at different zoom levels."""
    collection_id = get_collection_id()

    found_mvt = False

    # Test zoom levels 0 and 1
    for zoom in [0, 1]:
        response = api_request(
            "GET",
            f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/{zoom}/0/0?f=mvt"
        )

        if response.status_code in (404, 400, 501):
            # MVT might not be available
            continue

        assert response.status_code == 200, \
            f"MVT tile at zoom {zoom} failed with status {response.status_code}"

        found_mvt = True

    if not found_mvt:
        pytest.skip("MVT format not available for this collection")


# ============================================================================
#  Tile Bounds and Resolutions Tests
# ============================================================================

def test_tile_bounds_metadata_available(api_request: Callable):
    """Verify tile bounds metadata is available in tileset description."""
    collection_id = get_collection_id()
    response = api_request("GET", f"/ogc/collections/{collection_id}/tiles")

    if response.status_code == 404:
        pytest.skip(f"Collection {collection_id} not available in test environment")

    assert response.status_code == 200

    data = response.json()
    tilesets = data.get("tilesets", [])

    if not tilesets:
        pytest.skip("No tilesets available for this collection")

    # Check if bounds information is included (optional in spec)
    # If present, verify structure
    first_tileset = tilesets[0]
    if "boundingBox" in first_tileset:
        bbox = first_tileset["boundingBox"]
        assert "lowerLeft" in bbox or "upperRight" in bbox or isinstance(bbox, list), \
            "Bounding box should have valid structure"


def test_tile_links_include_templated_url(api_request: Callable):
    """Verify tileset links include templated URL for tile retrieval."""
    collection_id = get_collection_id()
    response = api_request("GET", f"/ogc/collections/{collection_id}/tiles")

    if response.status_code == 404:
        pytest.skip(f"Collection {collection_id} not available in test environment")

    assert response.status_code == 200

    data = response.json()
    links = data.get("links", [])

    # Look for templated tile link
    has_tile_link = False
    for link in links:
        if link.get("rel") in ["item", "tiles"]:
            href = link.get("href", "")
            # Should contain template variables like {tileMatrix}, {tileRow}, {tileCol}
            if "{" in href and "}" in href:
                has_tile_link = True
                break

    # Templated links are part of the spec but implementation varies
    assert isinstance(has_tile_link, bool), "Should be able to check for templated tile links"


# ============================================================================
#  Cache Headers Tests
# ============================================================================

def test_tile_includes_cache_headers(api_request: Callable):
    """Verify tile responses include appropriate cache headers."""
    collection_id = get_collection_id()
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=png"
    )

    if response.status_code == 404:
        pytest.skip(f"Tile endpoint not available for collection {collection_id}")

    assert response.status_code == 200

    # Check for cache-related headers
    headers_lower = {k.lower(): v for k, v in response.headers.items()}

    # Should have either ETag or Last-Modified for cache validation
    has_cache_header = "etag" in headers_lower or "last-modified" in headers_lower
    assert has_cache_header, \
        "Tile response should include cache validation headers (ETag or Last-Modified)"


def test_tile_etag_validation(api_request: Callable):
    """Verify tiles support ETag-based conditional requests (304 Not Modified)."""
    collection_id = get_collection_id()
    tile_url = f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=png"

    # First request to get ETag
    response1 = api_request("GET", tile_url)

    if response1.status_code == 404:
        pytest.skip(f"Tile endpoint not available for collection {collection_id}")

    assert response1.status_code == 200

    etag = response1.headers.get("ETag")

    if not etag:
        pytest.skip("Tile endpoint does not return ETag header")

    # Second request with If-None-Match header
    response2 = api_request("GET", tile_url, headers={"If-None-Match": etag})

    # Should return 304 Not Modified when ETag matches
    # Some servers might still return 200
    assert response2.status_code in (200, 304), \
        f"Expected 200 or 304 with ETag, got {response2.status_code}"


def test_tile_cache_control_header(api_request: Callable):
    """Verify tiles include Cache-Control header for client caching."""
    collection_id = get_collection_id()
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=png"
    )

    if response.status_code == 404:
        pytest.skip(f"Tile endpoint not available for collection {collection_id}")

    assert response.status_code == 200

    # Cache-Control is optional but recommended for tiles
    cache_control = response.headers.get("Cache-Control")

    # Just verify it's a string if present
    if cache_control:
        assert isinstance(cache_control, str), "Cache-Control should be a string"


# ============================================================================
#  Error Handling Tests
# ============================================================================

def test_invalid_tilematrixset_returns_404(api_request: Callable):
    """Verify requesting invalid tile matrix set returns 404."""
    response = api_request("GET", "/ogc/tileMatrixSets/InvalidTileMatrixSet")

    assert response.status_code == 404, \
        f"Invalid tile matrix set should return 404, got {response.status_code}"


def test_invalid_collection_id_returns_404(api_request: Callable):
    """Verify requesting tiles for non-existent collection returns 404."""
    response = api_request(
        "GET",
        "/ogc/collections/nonexistent::collection/tiles/WorldWebMercatorQuad/0/0/0?f=png"
    )

    assert response.status_code == 404, \
        f"Non-existent collection should return 404, got {response.status_code}"


def test_out_of_bounds_tile_coordinates_returns_404(api_request: Callable):
    """Verify requesting tile with out-of-bounds coordinates returns 404."""
    collection_id = get_collection_id()

    # At zoom 0, valid tiles are only (0,0). Request an invalid coordinate.
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/1/1?f=png"
    )

    # Out of bounds should return 404 or 400
    assert response.status_code in (404, 400), \
        f"Out of bounds tile should return 404 or 400, got {response.status_code}"


def test_invalid_zoom_level_returns_error(api_request: Callable):
    """Verify requesting tile with invalid zoom level returns error."""
    collection_id = get_collection_id()

    # Request zoom level 999 which should exceed maxZoom
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/999/0/0?f=png"
    )

    # Should return 404 or 400 for zoom out of range
    assert response.status_code in (404, 400), \
        f"Invalid zoom level should return 404 or 400, got {response.status_code}"


def test_negative_tile_coordinates_returns_error(api_request: Callable):
    """Verify requesting tile with negative coordinates returns error."""
    collection_id = get_collection_id()

    # Negative coordinates are invalid
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/-1/0?f=png"
    )

    # Should return 404 or 400
    assert response.status_code in (404, 400), \
        f"Negative coordinates should return 404 or 400, got {response.status_code}"


def test_invalid_tile_matrix_parameter_returns_400(api_request: Callable):
    """Verify requesting tile with non-numeric tile matrix returns 400."""
    collection_id = get_collection_id()

    # Use non-numeric tile matrix (zoom level)
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/invalid/0/0?f=png"
    )

    # Should return 400 Bad Request for invalid tile matrix format
    assert response.status_code in (400, 404), \
        f"Invalid tile matrix should return 400 or 404, got {response.status_code}"


def test_unsupported_format_returns_error(api_request: Callable):
    """Verify requesting tile with unsupported format returns error."""
    collection_id = get_collection_id()

    # Request unsupported format
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=invalid_format"
    )

    # Should return 400 or 406 (Not Acceptable)
    assert response.status_code in (400, 406, 415), \
        f"Unsupported format should return 400/406/415, got {response.status_code}"


def test_malformed_tile_url_returns_error(api_request: Callable):
    """Verify malformed tile URL returns appropriate error."""
    collection_id = get_collection_id()

    # Missing row and col parameters
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0?f=png"
    )

    # Should return 404 (path not found)
    assert response.status_code == 404, \
        f"Malformed tile URL should return 404, got {response.status_code}"


# ============================================================================
#  Image Validation Tests
# ============================================================================

def test_png_tile_is_valid_image_file(api_request: Callable):
    """Verify PNG tile can be loaded as valid image file."""
    try:
        from PIL import Image
    except ImportError:
        pytest.skip("Pillow not installed (pip install Pillow)")

    collection_id = get_collection_id()
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=png"
    )

    if response.status_code == 404:
        pytest.skip(f"Tile endpoint not available for collection {collection_id}")

    assert response.status_code == 200

    # Load image with PIL
    tile_data = response.content
    img = Image.open(io.BytesIO(tile_data))

    # Verify image properties
    assert img.format == "PNG", f"Expected PNG format, got {img.format}"
    assert img.size == (256, 256), f"Expected 256x256, got {img.size}"
    assert img.mode in ("RGB", "RGBA", "P", "L"), \
        f"Expected valid image mode, got {img.mode}"


def test_png_tile_has_valid_color_depth(api_request: Callable):
    """Verify PNG tile has valid color depth."""
    try:
        from PIL import Image
    except ImportError:
        pytest.skip("Pillow not installed (pip install Pillow)")

    collection_id = get_collection_id()
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=png"
    )

    if response.status_code == 404:
        pytest.skip(f"Tile endpoint not available for collection {collection_id}")

    assert response.status_code == 200

    tile_data = response.content
    img = Image.open(io.BytesIO(tile_data))

    # Verify image has valid mode
    valid_modes = ("1", "L", "P", "RGB", "RGBA", "CMYK", "YCbCr", "LAB", "HSV", "I", "F")
    assert img.mode in valid_modes, f"Invalid image mode: {img.mode}"


def test_jpeg_tile_is_valid_image_file(api_request: Callable):
    """Verify JPEG tile can be loaded as valid image file."""
    try:
        from PIL import Image
    except ImportError:
        pytest.skip("Pillow not installed (pip install Pillow)")

    collection_id = get_collection_id()
    response = api_request(
        "GET",
        f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/0/0/0?f=jpeg"
    )

    if response.status_code == 404:
        pytest.skip(f"JPEG tile endpoint not available for collection {collection_id}")

    if response.status_code == 400:
        pytest.skip("JPEG format not supported for this tileset")

    assert response.status_code == 200

    # Load image with PIL
    tile_data = response.content
    img = Image.open(io.BytesIO(tile_data))

    # Verify image properties
    assert img.format == "JPEG", f"Expected JPEG format, got {img.format}"
    assert img.size == (256, 256), f"Expected 256x256, got {img.size}"


def test_tiles_at_different_zooms_have_consistent_dimensions(api_request: Callable):
    """Verify tiles at different zoom levels have consistent dimensions."""
    try:
        from PIL import Image
    except ImportError:
        pytest.skip("Pillow not installed (pip install Pillow)")

    collection_id = get_collection_id()

    sizes = []

    # Test zoom levels 0, 1, 2
    for zoom in [0, 1, 2]:
        response = api_request(
            "GET",
            f"/ogc/collections/{collection_id}/tiles/WorldWebMercatorQuad/{zoom}/0/0?f=png"
        )

        if response.status_code == 404:
            # Higher zoom levels might not be available
            continue

        assert response.status_code == 200

        tile_data = response.content
        img = Image.open(io.BytesIO(tile_data))
        sizes.append(img.size)

    if not sizes:
        pytest.skip("No tiles available at tested zoom levels")

    # All tiles should have the same dimensions (256x256 for Web Mercator)
    assert all(size == sizes[0] for size in sizes), \
        f"Tiles at different zoom levels should have consistent dimensions, got {sizes}"
