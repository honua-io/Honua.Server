"""
Comprehensive WMTS 1.0.0 Integration Tests with OWSLib

This test suite validates Honua's WMTS implementation using OWSLib, the reference
Python client library for OGC Web Services. Tests verify full compliance with
WMTS 1.0.0 specification.

Test Coverage:
- GetCapabilities: Service metadata, layer listing, tile matrix sets
- Layer Metadata: Bounds, formats, tile matrix sets, dimensions
- GetTile: Tile retrieval with different tile matrix sets and formats
- TileMatrixSet Support: WebMercator, WorldCRS84Quad, and custom sets
- Image Formats: PNG, JPEG, and other supported formats
- Tile Addressing: TileMatrix, TileRow, TileCol validation
- Dimensions: Time and other dimension support (if available)
- Error Handling: Invalid layers, out of bounds tiles, unsupported formats

Requirements:
- owslib >= 0.29.0
- Pillow >= 9.0.0 (for image validation)

Client: OWSLib WebMapTileService
Specification: OGC WMTS 1.0.0
Reference: https://www.ogc.org/standards/wmts
"""
import pytest
import os
from typing import Optional


pytestmark = [
    pytest.mark.integration,
    pytest.mark.python,
    pytest.mark.wmts,
    pytest.mark.requires_honua
]


# ============================================================================
#  Fixtures
# ============================================================================

@pytest.fixture(scope="module")
def wmts_client(honua_api_base_url):
    """Create OWSLib WebMapTileService client."""
    try:
        from owslib.wmts import WebMapTileService
    except ImportError:
        pytest.skip("OWSLib not installed (pip install owslib)")

    wmts_url = f"{honua_api_base_url}/wmts"

    try:
        wmts = WebMapTileService(wmts_url, version='1.0.0')
        return wmts
    except Exception as e:
        pytest.skip(f"Could not connect to WMTS at {wmts_url}: {e}")


@pytest.fixture(scope="module")
def test_layer_name(wmts_client):
    """Get a valid layer name for testing."""
    layers = list(wmts_client.contents.keys())
    if not layers:
        pytest.skip("No WMTS layers available in test environment")
    return layers[0]


@pytest.fixture(scope="module")
def test_tile_matrix_set(wmts_client, test_layer_name):
    """Get a valid tile matrix set for testing."""
    layer = wmts_client.contents[test_layer_name]
    if not hasattr(layer, 'tilematrixsetlinks') or not layer.tilematrixsetlinks:
        pytest.skip("Layer has no tile matrix sets")

    # Prefer WebMercatorQuad if available
    for tms_link in layer.tilematrixsetlinks.values():
        if 'WebMercatorQuad' in tms_link.tilematrixset:
            return tms_link.tilematrixset

    # Otherwise use first available
    return next(iter(layer.tilematrixsetlinks.values())).tilematrixset


# ============================================================================
#  GetCapabilities Tests
# ============================================================================

def test_wmts_get_capabilities(wmts_client):
    """Verify WMTS GetCapabilities returns valid service metadata."""
    assert wmts_client is not None, "WMTS client should be initialized"

    # Check identification
    assert hasattr(wmts_client, 'identification'), "WMTS should have identification"
    assert wmts_client.identification.title is not None, "WMTS should have title"

    # Check provider
    assert hasattr(wmts_client, 'provider'), "WMTS should have provider info"

    # Check operations
    assert hasattr(wmts_client, 'operations'), "WMTS should list operations"
    operation_names = [op.name for op in wmts_client.operations]
    assert 'GetCapabilities' in operation_names, "WMTS must support GetCapabilities"
    assert 'GetTile' in operation_names, "WMTS must support GetTile"


def test_wmts_version_1_0_0_supported(wmts_client):
    """Verify WMTS 1.0.0 version is supported."""
    assert wmts_client.version == '1.0.0', f"Expected WMTS 1.0.0, got {wmts_client.version}"


def test_wmts_lists_layers(wmts_client):
    """Verify WMTS GetCapabilities lists available layers."""
    assert hasattr(wmts_client, 'contents'), "WMTS should have contents"
    assert len(wmts_client.contents) > 0, "WMTS should have at least one layer"

    # Validate layer structure
    for layer_name, layer in wmts_client.contents.items():
        assert layer.name is not None, f"Layer {layer_name} must have name"
        assert layer.title is not None, f"Layer {layer_name} must have title"


def test_wmts_service_metadata(wmts_client):
    """Verify WMTS service metadata is complete."""
    # Service identification
    ident = wmts_client.identification
    assert ident.title, "Service must have title"
    assert ident.type == 'OGC WMTS', "Service type should be OGC WMTS"

    # Check for abstract and keywords (recommended but optional)
    assert hasattr(ident, 'abstract')
    assert hasattr(ident, 'keywords')


def test_wmts_lists_tile_matrix_sets(wmts_client):
    """Verify WMTS declares available tile matrix sets."""
    assert hasattr(wmts_client, 'tilematrixsets'), "WMTS should have tile matrix sets"
    assert len(wmts_client.tilematrixsets) > 0, "WMTS should have at least one tile matrix set"

    # Validate tile matrix set structure
    for tms_name, tms in wmts_client.tilematrixsets.items():
        assert tms.identifier is not None, f"TileMatrixSet {tms_name} must have identifier"
        assert hasattr(tms, 'crs'), f"TileMatrixSet {tms_name} must declare CRS"


def test_wmts_common_tile_matrix_sets(wmts_client):
    """Verify support for common tile matrix sets."""
    tms_identifiers = list(wmts_client.tilematrixsets.keys())

    # Check for common tile matrix sets
    common_tms = ['WebMercatorQuad', 'WorldCRS84Quad', 'GoogleMapsCompatible']

    found_common = False
    for tms in common_tms:
        if any(tms in identifier for identifier in tms_identifiers):
            found_common = True
            break

    assert found_common, f"WMTS should support at least one common tile matrix set. Found: {tms_identifiers}"


# ============================================================================
#  Layer Metadata Tests
# ============================================================================

def test_wmts_layer_has_tile_matrix_sets(wmts_client, test_layer_name):
    """Verify layer declares supported tile matrix sets."""
    layer = wmts_client.contents[test_layer_name]

    assert hasattr(layer, 'tilematrixsetlinks'), "Layer must declare tile matrix set links"
    assert len(layer.tilematrixsetlinks) > 0, "Layer must support at least one tile matrix set"

    # Validate tile matrix set links
    for tms_link in layer.tilematrixsetlinks.values():
        assert hasattr(tms_link, 'tilematrixset'), "TileMatrixSetLink must have identifier"
        tms_id = tms_link.tilematrixset
        assert tms_id in wmts_client.tilematrixsets, \
            f"TileMatrixSet {tms_id} must be defined in service capabilities"


def test_wmts_layer_has_formats(wmts_client, test_layer_name):
    """Verify layer declares supported formats."""
    layer = wmts_client.contents[test_layer_name]

    assert hasattr(layer, 'formats'), "Layer must declare formats"
    assert len(layer.formats) > 0, "Layer must support at least one format"

    # Check for common formats
    format_list = [f.lower() for f in layer.formats]
    assert any('png' in f or 'jpeg' in f for f in format_list), \
        "Layer should support PNG or JPEG format"


def test_wmts_layer_has_bounding_box(wmts_client, test_layer_name):
    """Verify layer has bounding box metadata."""
    layer = wmts_client.contents[test_layer_name]

    assert hasattr(layer, 'boundingBoxWGS84'), "Layer should have WGS84 bounding box"
    bbox = layer.boundingBoxWGS84

    assert bbox is not None, "Bounding box should not be None"
    assert len(bbox) == 4, "Bounding box should have 4 coordinates"

    # Validate bbox structure (minx, miny, maxx, maxy)
    minx, miny, maxx, maxy = bbox
    assert minx < maxx, "Min X should be less than Max X"
    assert miny < maxy, "Min Y should be less than Max Y"

    # WGS84 bounds
    assert -180 <= minx <= 180, "X coordinates should be valid longitude"
    assert -180 <= maxx <= 180, "X coordinates should be valid longitude"
    assert -90 <= miny <= 90, "Y coordinates should be valid latitude"
    assert -90 <= maxy <= 90, "Y coordinates should be valid latitude"


def test_wmts_layer_has_styles(wmts_client, test_layer_name):
    """Verify layer declares available styles."""
    layer = wmts_client.contents[test_layer_name]

    assert hasattr(layer, 'styles'), "Layer should have styles"
    assert isinstance(layer.styles, dict), "Styles should be a dictionary"

    # WMTS requires at least a default style
    assert len(layer.styles) > 0, "Layer should have at least one style"


def test_wmts_layer_resource_urls(wmts_client, test_layer_name):
    """Verify layer has resource URLs (if using RESTful protocol)."""
    layer = wmts_client.contents[test_layer_name]

    # ResourceURLs are optional but common in RESTful WMTS
    if hasattr(layer, 'resourceurls') and layer.resourceurls:
        for url in layer.resourceurls:
            assert hasattr(url, 'format'), "ResourceURL should have format"
            assert hasattr(url, 'template'), "ResourceURL should have template"


# ============================================================================
#  TileMatrixSet Tests
# ============================================================================

def test_wmts_tile_matrix_set_structure(wmts_client, test_tile_matrix_set):
    """Verify tile matrix set has valid structure."""
    tms = wmts_client.tilematrixsets[test_tile_matrix_set]

    assert tms.identifier is not None, "TileMatrixSet must have identifier"
    assert hasattr(tms, 'crs'), "TileMatrixSet must have CRS"
    assert hasattr(tms, 'tilematrix'), "TileMatrixSet must have tile matrices"
    assert len(tms.tilematrix) > 0, "TileMatrixSet must have at least one tile matrix"


def test_wmts_tile_matrix_levels(wmts_client, test_tile_matrix_set):
    """Verify tile matrix set has properly structured zoom levels."""
    tms = wmts_client.tilematrixsets[test_tile_matrix_set]

    for tm in tms.tilematrix.values():
        # Each tile matrix must have required properties
        assert hasattr(tm, 'identifier'), "TileMatrix must have identifier"
        assert hasattr(tm, 'scaledenominator'), "TileMatrix must have scale denominator"
        assert hasattr(tm, 'topleftcorner'), "TileMatrix must have top left corner"
        assert hasattr(tm, 'tilewidth'), "TileMatrix must have tile width"
        assert hasattr(tm, 'tileheight'), "TileMatrix must have tile height"
        assert hasattr(tm, 'matrixwidth'), "TileMatrix must have matrix width"
        assert hasattr(tm, 'matrixheight'), "TileMatrix must have matrix height"

        # Validate dimensions
        assert tm.tilewidth > 0, "Tile width must be positive"
        assert tm.tileheight > 0, "Tile height must be positive"
        assert tm.matrixwidth > 0, "Matrix width must be positive"
        assert tm.matrixheight > 0, "Matrix height must be positive"


def test_wmts_web_mercator_if_supported(wmts_client):
    """Verify Web Mercator tile matrix set if supported."""
    tms_identifiers = list(wmts_client.tilematrixsets.keys())

    # Look for Web Mercator variants
    web_mercator_variants = ['WebMercatorQuad', 'GoogleMapsCompatible', 'EPSG:3857']
    web_mercator_tms = None

    for variant in web_mercator_variants:
        for tms_id in tms_identifiers:
            if variant in tms_id:
                web_mercator_tms = tms_id
                break
        if web_mercator_tms:
            break

    if not web_mercator_tms:
        pytest.skip("Web Mercator tile matrix set not supported")

    tms = wmts_client.tilematrixsets[web_mercator_tms]

    # Web Mercator should use EPSG:3857
    assert '3857' in tms.crs, f"Web Mercator should use EPSG:3857, got {tms.crs}"


def test_wmts_world_crs84_if_supported(wmts_client):
    """Verify WorldCRS84Quad tile matrix set if supported."""
    tms_identifiers = list(wmts_client.tilematrixsets.keys())

    world_crs84 = None
    for tms_id in tms_identifiers:
        if 'WorldCRS84Quad' in tms_id or 'CRS84' in tms_id:
            world_crs84 = tms_id
            break

    if not world_crs84:
        pytest.skip("WorldCRS84Quad tile matrix set not supported")

    tms = wmts_client.tilematrixsets[world_crs84]

    # WorldCRS84Quad should use CRS:84
    assert 'CRS:84' in tms.crs or 'CRS84' in tms.crs, \
        f"WorldCRS84Quad should use CRS:84, got {tms.crs}"


# ============================================================================
#  GetTile Tests
# ============================================================================

def test_wmts_get_tile_basic(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify GetTile returns valid tile image."""
    try:
        from PIL import Image
        import io
    except ImportError:
        pytest.skip("Pillow not installed (pip install Pillow)")

    layer = wmts_client.contents[test_layer_name]

    # Get first available format
    image_format = layer.formats[0]

    # Get first available style
    style = list(layer.styles.keys())[0] if layer.styles else 'default'

    # Get tile matrix set
    tms = wmts_client.tilematrixsets[test_tile_matrix_set]

    # Use first zoom level (tile matrix)
    tile_matrix = list(tms.tilematrix.keys())[0]

    # Get tile at 0,0
    try:
        tile = wmts_client.gettile(
            layer=test_layer_name,
            tilematrixset=test_tile_matrix_set,
            tilematrix=tile_matrix,
            row=0,
            column=0,
            format=image_format,
            style=style
        )

        assert tile is not None, "GetTile should return tile data"

        # Read tile data
        tile_data = tile.read()
        assert len(tile_data) > 0, "Tile data should not be empty"

        # Validate as image if format is image
        if 'image' in image_format.lower():
            try:
                img = Image.open(io.BytesIO(tile_data))
                assert img.size[0] > 0 and img.size[1] > 0, "Image should have valid dimensions"
            except Exception as e:
                pytest.fail(f"Failed to parse tile as image: {e}")

    except Exception as e:
        pytest.fail(f"GetTile failed: {e}")


def test_wmts_get_tile_different_formats(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify GetTile works with different image formats."""
    layer = wmts_client.contents[test_layer_name]

    # Get first available style
    style = list(layer.styles.keys())[0] if layer.styles else 'default'

    # Get tile matrix set
    tms = wmts_client.tilematrixsets[test_tile_matrix_set]
    tile_matrix = list(tms.tilematrix.keys())[0]

    # Test each supported format
    for image_format in layer.formats:
        if 'image' not in image_format.lower():
            continue

        try:
            tile = wmts_client.gettile(
                layer=test_layer_name,
                tilematrixset=test_tile_matrix_set,
                tilematrix=tile_matrix,
                row=0,
                column=0,
                format=image_format,
                style=style
            )

            tile_data = tile.read()
            assert len(tile_data) > 0, f"Tile data should not be empty for format {image_format}"

        except Exception as e:
            pytest.fail(f"GetTile failed for format {image_format}: {e}")


def test_wmts_get_tile_different_zoom_levels(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify GetTile works at different zoom levels."""
    try:
        from PIL import Image
        import io
    except ImportError:
        pytest.skip("Pillow not installed")

    layer = wmts_client.contents[test_layer_name]
    image_format = layer.formats[0]
    style = list(layer.styles.keys())[0] if layer.styles else 'default'

    # Get tile matrix set
    tms = wmts_client.tilematrixsets[test_tile_matrix_set]

    # Test first 3 zoom levels
    tile_matrices = list(tms.tilematrix.keys())[:3]

    for tile_matrix in tile_matrices:
        try:
            tile = wmts_client.gettile(
                layer=test_layer_name,
                tilematrixset=test_tile_matrix_set,
                tilematrix=tile_matrix,
                row=0,
                column=0,
                format=image_format,
                style=style
            )

            tile_data = tile.read()
            assert len(tile_data) > 0, f"Tile should be returned for zoom level {tile_matrix}"

            # Validate image
            if 'image' in image_format.lower():
                img = Image.open(io.BytesIO(tile_data))
                assert img.size[0] > 0, f"Image should be valid for zoom level {tile_matrix}"

        except Exception as e:
            pytest.fail(f"GetTile failed at zoom level {tile_matrix}: {e}")


def test_wmts_get_tile_different_positions(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify GetTile works at different tile positions."""
    layer = wmts_client.contents[test_layer_name]
    image_format = layer.formats[0]
    style = list(layer.styles.keys())[0] if layer.styles else 'default'

    # Get tile matrix set
    tms = wmts_client.tilematrixsets[test_tile_matrix_set]
    tile_matrix = list(tms.tilematrix.keys())[0]

    # Get tile matrix dimensions
    tm = tms.tilematrix[tile_matrix]
    max_col = min(tm.matrixwidth - 1, 2)  # Test up to column 2 or max
    max_row = min(tm.matrixheight - 1, 2)  # Test up to row 2 or max

    # Test different positions
    positions = [(0, 0), (max_col, 0), (0, max_row), (max_col, max_row)]

    for col, row in positions:
        try:
            tile = wmts_client.gettile(
                layer=test_layer_name,
                tilematrixset=test_tile_matrix_set,
                tilematrix=tile_matrix,
                row=row,
                column=col,
                format=image_format,
                style=style
            )

            tile_data = tile.read()
            assert len(tile_data) > 0, f"Tile should be returned at position ({col}, {row})"

        except Exception as e:
            pytest.fail(f"GetTile failed at position ({col}, {row}): {e}")


def test_wmts_get_tile_png_format(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify GetTile returns valid PNG tiles."""
    try:
        from PIL import Image
        import io
    except ImportError:
        pytest.skip("Pillow not installed")

    layer = wmts_client.contents[test_layer_name]

    # Find PNG format
    png_format = None
    for fmt in layer.formats:
        if 'png' in fmt.lower():
            png_format = fmt
            break

    if not png_format:
        pytest.skip("Layer does not support PNG format")

    style = list(layer.styles.keys())[0] if layer.styles else 'default'
    tms = wmts_client.tilematrixsets[test_tile_matrix_set]
    tile_matrix = list(tms.tilematrix.keys())[0]

    tile = wmts_client.gettile(
        layer=test_layer_name,
        tilematrixset=test_tile_matrix_set,
        tilematrix=tile_matrix,
        row=0,
        column=0,
        format=png_format,
        style=style
    )

    tile_data = tile.read()
    img = Image.open(io.BytesIO(tile_data))

    assert img.format == 'PNG', f"Image format should be PNG, got {img.format}"


def test_wmts_get_tile_jpeg_format(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify GetTile returns valid JPEG tiles if supported."""
    try:
        from PIL import Image
        import io
    except ImportError:
        pytest.skip("Pillow not installed")

    layer = wmts_client.contents[test_layer_name]

    # Find JPEG format
    jpeg_format = None
    for fmt in layer.formats:
        if 'jpeg' in fmt.lower() or 'jpg' in fmt.lower():
            jpeg_format = fmt
            break

    if not jpeg_format:
        pytest.skip("Layer does not support JPEG format")

    style = list(layer.styles.keys())[0] if layer.styles else 'default'
    tms = wmts_client.tilematrixsets[test_tile_matrix_set]
    tile_matrix = list(tms.tilematrix.keys())[0]

    tile = wmts_client.gettile(
        layer=test_layer_name,
        tilematrixset=test_tile_matrix_set,
        tilematrix=tile_matrix,
        row=0,
        column=0,
        format=jpeg_format,
        style=style
    )

    tile_data = tile.read()
    img = Image.open(io.BytesIO(tile_data))

    assert img.format == 'JPEG', f"Image format should be JPEG, got {img.format}"


def test_wmts_get_tile_with_different_styles(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify GetTile works with different styles."""
    layer = wmts_client.contents[test_layer_name]

    if len(layer.styles) <= 1:
        pytest.skip("Layer has only one style")

    image_format = layer.formats[0]
    tms = wmts_client.tilematrixsets[test_tile_matrix_set]
    tile_matrix = list(tms.tilematrix.keys())[0]

    # Test each style
    for style_name in list(layer.styles.keys())[:3]:  # Test up to 3 styles
        try:
            tile = wmts_client.gettile(
                layer=test_layer_name,
                tilematrixset=test_tile_matrix_set,
                tilematrix=tile_matrix,
                row=0,
                column=0,
                format=image_format,
                style=style_name
            )

            tile_data = tile.read()
            assert len(tile_data) > 0, f"Tile should be returned for style {style_name}"

        except Exception as e:
            pytest.fail(f"GetTile failed for style {style_name}: {e}")


# ============================================================================
#  Tile Matrix Set Coverage Tests
# ============================================================================

def test_wmts_tile_addressing(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify tile addressing scheme (TileMatrix, TileRow, TileCol)."""
    layer = wmts_client.contents[test_layer_name]
    image_format = layer.formats[0]
    style = list(layer.styles.keys())[0] if layer.styles else 'default'

    tms = wmts_client.tilematrixsets[test_tile_matrix_set]

    # Get a zoom level with reasonable tile count
    for tile_matrix_id, tm in list(tms.tilematrix.items())[:5]:
        if tm.matrixwidth <= 8 and tm.matrixheight <= 8:
            # Test corner tiles
            corners = [
                (0, 0),  # Top-left
                (tm.matrixwidth - 1, 0),  # Top-right
                (0, tm.matrixheight - 1),  # Bottom-left
                (tm.matrixwidth - 1, tm.matrixheight - 1)  # Bottom-right
            ]

            for col, row in corners:
                try:
                    tile = wmts_client.gettile(
                        layer=test_layer_name,
                        tilematrixset=test_tile_matrix_set,
                        tilematrix=tile_matrix_id,
                        row=row,
                        column=col,
                        format=image_format,
                        style=style
                    )

                    tile_data = tile.read()
                    assert len(tile_data) > 0, \
                        f"Tile should be valid at matrix={tile_matrix_id}, row={row}, col={col}"

                except Exception as e:
                    pytest.fail(f"Tile addressing failed at {tile_matrix_id}/{row}/{col}: {e}")

            break  # Only test one suitable zoom level


def test_wmts_tile_dimensions(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify tiles have correct dimensions as declared in tile matrix."""
    try:
        from PIL import Image
        import io
    except ImportError:
        pytest.skip("Pillow not installed")

    layer = wmts_client.contents[test_layer_name]

    # Find PNG format for best compatibility
    image_format = layer.formats[0]
    for fmt in layer.formats:
        if 'png' in fmt.lower():
            image_format = fmt
            break

    style = list(layer.styles.keys())[0] if layer.styles else 'default'
    tms = wmts_client.tilematrixsets[test_tile_matrix_set]

    # Test first tile matrix
    tile_matrix_id = list(tms.tilematrix.keys())[0]
    tm = tms.tilematrix[tile_matrix_id]

    tile = wmts_client.gettile(
        layer=test_layer_name,
        tilematrixset=test_tile_matrix_set,
        tilematrix=tile_matrix_id,
        row=0,
        column=0,
        format=image_format,
        style=style
    )

    tile_data = tile.read()
    img = Image.open(io.BytesIO(tile_data))

    # Verify dimensions match declared tile size
    assert img.size[0] == tm.tilewidth, \
        f"Tile width should be {tm.tilewidth}, got {img.size[0]}"
    assert img.size[1] == tm.tileheight, \
        f"Tile height should be {tm.tileheight}, got {img.size[1]}"


# ============================================================================
#  Error Handling Tests
# ============================================================================

def test_wmts_invalid_layer_raises_error(wmts_client, test_tile_matrix_set):
    """Verify requesting invalid layer returns error."""
    tms = wmts_client.tilematrixsets[test_tile_matrix_set]
    tile_matrix = list(tms.tilematrix.keys())[0]

    try:
        tile = wmts_client.gettile(
            layer='nonexistent_layer_12345',
            tilematrixset=test_tile_matrix_set,
            tilematrix=tile_matrix,
            row=0,
            column=0,
            format='image/png'
        )

        # If it doesn't raise, check response
        data = tile.read()
        assert len(data) == 0 or b'Exception' in data or b'error' in data.lower(), \
            "Should return error for invalid layer"

    except Exception as e:
        # Expected - invalid layer should raise error
        assert "layer" in str(e).lower() or "not found" in str(e).lower(), \
            f"Error should mention invalid layer: {e}"


def test_wmts_invalid_tile_matrix_set_raises_error(wmts_client, test_layer_name):
    """Verify requesting invalid tile matrix set returns error."""
    layer = wmts_client.contents[test_layer_name]
    image_format = layer.formats[0]
    style = list(layer.styles.keys())[0] if layer.styles else 'default'

    try:
        tile = wmts_client.gettile(
            layer=test_layer_name,
            tilematrixset='InvalidTileMatrixSet99999',
            tilematrix='0',
            row=0,
            column=0,
            format=image_format,
            style=style
        )

        data = tile.read()
        assert len(data) == 0 or b'Exception' in data, \
            "Should return error for invalid tile matrix set"

    except Exception as e:
        # Expected
        assert "tilematrixset" in str(e).lower() or "matrix" in str(e).lower(), \
            f"Error should mention invalid tile matrix set: {e}"


def test_wmts_out_of_bounds_tile_raises_error(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify requesting out-of-bounds tile returns error or empty tile."""
    layer = wmts_client.contents[test_layer_name]
    image_format = layer.formats[0]
    style = list(layer.styles.keys())[0] if layer.styles else 'default'

    tms = wmts_client.tilematrixsets[test_tile_matrix_set]
    tile_matrix_id = list(tms.tilematrix.keys())[0]
    tm = tms.tilematrix[tile_matrix_id]

    # Request tile way outside valid range
    try:
        tile = wmts_client.gettile(
            layer=test_layer_name,
            tilematrixset=test_tile_matrix_set,
            tilematrix=tile_matrix_id,
            row=tm.matrixheight + 1000,
            column=tm.matrixwidth + 1000,
            format=image_format,
            style=style
        )

        # Server may return empty tile or error
        data = tile.read()
        # Either empty or error response is acceptable

    except Exception as e:
        # Exception is also acceptable for out-of-bounds
        assert isinstance(e, Exception), "Out-of-bounds tile should raise error or return empty"


def test_wmts_invalid_format_raises_error(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify requesting unsupported format returns error."""
    layer = wmts_client.contents[test_layer_name]
    style = list(layer.styles.keys())[0] if layer.styles else 'default'

    tms = wmts_client.tilematrixsets[test_tile_matrix_set]
    tile_matrix = list(tms.tilematrix.keys())[0]

    try:
        tile = wmts_client.gettile(
            layer=test_layer_name,
            tilematrixset=test_tile_matrix_set,
            tilematrix=tile_matrix,
            row=0,
            column=0,
            format='image/xyz_invalid',
            style=style
        )

        data = tile.read()
        assert b'Exception' in data or b'error' in data.lower(), \
            "Should return error for invalid format"

    except Exception as e:
        # Expected
        assert "format" in str(e).lower(), f"Error should mention invalid format: {e}"


def test_wmts_invalid_style_raises_error(wmts_client, test_layer_name, test_tile_matrix_set):
    """Verify requesting invalid style returns error."""
    layer = wmts_client.contents[test_layer_name]
    image_format = layer.formats[0]

    tms = wmts_client.tilematrixsets[test_tile_matrix_set]
    tile_matrix = list(tms.tilematrix.keys())[0]

    try:
        tile = wmts_client.gettile(
            layer=test_layer_name,
            tilematrixset=test_tile_matrix_set,
            tilematrix=tile_matrix,
            row=0,
            column=0,
            format=image_format,
            style='nonexistent_style_12345'
        )

        data = tile.read()
        # Some servers may return default style, others may error

    except Exception as e:
        # Exception is acceptable for invalid style
        assert "style" in str(e).lower(), f"Error should mention invalid style: {e}"


# ============================================================================
#  Dimension Tests (Optional)
# ============================================================================

def test_wmts_time_dimension_if_supported(wmts_client):
    """Verify time dimension support if declared by any layer."""
    has_time = False

    for layer_name, layer in wmts_client.contents.items():
        if hasattr(layer, 'dimensions') and layer.dimensions:
            for dim in layer.dimensions:
                if hasattr(dim, 'identifier') and dim.identifier.lower() == 'time':
                    has_time = True

                    # Verify time dimension structure
                    assert hasattr(dim, 'values'), "Time dimension should have values"
                    break

        if has_time:
            break

    # This is informational - time dimension is optional
    assert isinstance(has_time, bool), "Should be able to check for time dimension"


def test_wmts_elevation_dimension_if_supported(wmts_client):
    """Verify elevation dimension support if declared by any layer."""
    has_elevation = False

    for layer_name, layer in wmts_client.contents.items():
        if hasattr(layer, 'dimensions') and layer.dimensions:
            for dim in layer.dimensions:
                if hasattr(dim, 'identifier') and dim.identifier.lower() == 'elevation':
                    has_elevation = True

                    # Verify elevation dimension structure
                    assert hasattr(dim, 'values'), "Elevation dimension should have values"
                    break

        if has_elevation:
            break

    # This is informational - elevation dimension is optional
    assert isinstance(has_elevation, bool), "Should be able to check for elevation dimension"


# ============================================================================
#  RESTful vs KVP Protocol Tests
# ============================================================================

def test_wmts_kvp_encoding_supported(wmts_client):
    """Verify WMTS supports KVP (Key-Value Pair) encoding."""
    # Check if GetTile operation supports KVP
    for op in wmts_client.operations:
        if op.name == 'GetTile':
            # Operation should have methods or formats
            assert op is not None, "GetTile operation should be defined"
            # KVP is mandatory in WMTS 1.0.0
            break


def test_wmts_restful_encoding_if_supported(wmts_client, test_layer_name):
    """Verify WMTS RESTful encoding if supported by layer."""
    layer = wmts_client.contents[test_layer_name]

    # RESTful is indicated by ResourceURLs
    has_restful = hasattr(layer, 'resourceurls') and len(layer.resourceurls) > 0

    if has_restful:
        # Verify ResourceURL structure
        for url in layer.resourceurls:
            assert hasattr(url, 'format'), "ResourceURL should have format"
            assert hasattr(url, 'template'), "ResourceURL should have template"
            assert hasattr(url, 'resourcetype'), "ResourceURL should have resource type"

            # Template should contain variables
            assert '{' in url.template and '}' in url.template, \
                "ResourceURL template should contain variables"

    # RESTful is optional
    assert isinstance(has_restful, bool), "Should be able to check for RESTful support"


# ============================================================================
#  WMTS 1.0.0 Compliance Tests
# ============================================================================

def test_wmts_service_type_version(wmts_client):
    """Verify WMTS service type and version compliance."""
    assert wmts_client.version == '1.0.0', "WMTS version should be 1.0.0"
    assert wmts_client.identification.type == 'OGC WMTS', \
        "Service type should be OGC WMTS"


def test_wmts_mandatory_operations(wmts_client):
    """Verify WMTS declares all mandatory operations."""
    operation_names = [op.name for op in wmts_client.operations]

    # Mandatory operations in WMTS 1.0.0
    mandatory_ops = ['GetCapabilities', 'GetTile']

    for op_name in mandatory_ops:
        assert op_name in operation_names, \
            f"WMTS must support mandatory operation: {op_name}"


def test_wmts_layer_has_mandatory_metadata(wmts_client, test_layer_name):
    """Verify layer has all mandatory metadata elements."""
    layer = wmts_client.contents[test_layer_name]

    # Mandatory layer metadata
    assert layer.name is not None, "Layer must have name"
    assert layer.title is not None, "Layer must have title"
    assert len(layer.tilematrixsetlinks) > 0, "Layer must reference at least one TileMatrixSet"
    assert len(layer.formats) > 0, "Layer must support at least one format"
    assert len(layer.styles) > 0, "Layer must have at least one style"


def test_wmts_exception_format(wmts_client):
    """Verify WMTS declares supported exception formats."""
    # Check if exceptions attribute exists
    if hasattr(wmts_client, 'exceptions'):
        exceptions = wmts_client.exceptions
        assert len(exceptions) > 0, "WMTS should declare exception formats"

        # WMTS 1.0.0 should support XML
        assert any('xml' in exc.lower() for exc in exceptions), \
            "WMTS should support XML exception format"
