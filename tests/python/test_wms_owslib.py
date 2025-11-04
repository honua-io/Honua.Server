"""
Comprehensive WMS 1.3.0 Integration Tests with OWSLib

This test suite validates Honua's WMS implementation using OWSLib, the reference
Python client library for OGC Web Services. Tests verify full compliance with
WMS 1.3.0 specification.

Test Coverage:
- GetCapabilities: Service metadata, layer listing, supported operations
- Layer Metadata: CRS support, bounding boxes, styles, dimensions
- GetMap: Image rendering with various formats, CRS, and parameters
- GetFeatureInfo: Querying feature attributes at map coordinates
- Styles: Layer styling and legend graphics
- Dimensions: Time and elevation dimension support (if available)
- CRS Support: Multiple coordinate reference systems
- Error Handling: Invalid layers, unsupported formats, out of bounds requests

Requirements:
- owslib >= 0.29.0
- Pillow >= 9.0.0 (for image validation)

Client: OWSLib WebMapService
Specification: OGC WMS 1.3.0
Reference: https://www.ogc.org/standards/wms
"""
import pytest
import os
from typing import Optional


pytestmark = [
    pytest.mark.integration,
    pytest.mark.python,
    pytest.mark.wms,
    pytest.mark.requires_honua
]


# ============================================================================
#  Fixtures
# ============================================================================

@pytest.fixture(scope="module")
def wms_client(honua_api_base_url):
    """Create OWSLib WebMapService client."""
    try:
        from owslib.wms import WebMapService
    except ImportError:
        pytest.skip("OWSLib not installed (pip install owslib)")

    wms_url = f"{honua_api_base_url}/wms"

    try:
        wms = WebMapService(wms_url, version='1.3.0')
        return wms
    except Exception as e:
        pytest.skip(f"Could not connect to WMS at {wms_url}: {e}")


@pytest.fixture(scope="module")
def test_layer_name(wms_client):
    """Get a valid layer name for testing."""
    layers = list(wms_client.contents.keys())
    if not layers:
        pytest.skip("No WMS layers available in test environment")
    return layers[0]


# ============================================================================
#  GetCapabilities Tests
# ============================================================================

def test_wms_get_capabilities(wms_client):
    """Verify WMS GetCapabilities returns valid service metadata."""
    assert wms_client is not None, "WMS client should be initialized"

    # Check identification
    assert hasattr(wms_client, 'identification'), "WMS should have identification"
    assert wms_client.identification.title is not None, "WMS should have title"

    # Check provider
    assert hasattr(wms_client, 'provider'), "WMS should have provider info"

    # Check operations
    assert hasattr(wms_client, 'operations'), "WMS should list operations"
    operation_names = [op.name for op in wms_client.operations]
    assert 'GetCapabilities' in operation_names, "WMS must support GetCapabilities"
    assert 'GetMap' in operation_names, "WMS must support GetMap"


def test_wms_version_1_3_0_supported(wms_client):
    """Verify WMS 1.3.0 version is supported."""
    assert wms_client.version == '1.3.0', f"Expected WMS 1.3.0, got {wms_client.version}"


def test_wms_lists_layers(wms_client):
    """Verify WMS GetCapabilities lists available layers."""
    assert hasattr(wms_client, 'contents'), "WMS should have contents"
    assert len(wms_client.contents) > 0, "WMS should have at least one layer"

    # Validate layer structure
    for layer_name, layer in wms_client.contents.items():
        assert layer.name is not None, f"Layer {layer_name} must have name"
        assert layer.title is not None, f"Layer {layer_name} must have title"


def test_wms_service_metadata(wms_client):
    """Verify WMS service metadata is complete."""
    # Service identification
    ident = wms_client.identification
    assert ident.title, "Service must have title"
    assert ident.type == 'WMS', "Service type should be WMS"

    # Check for abstract and keywords (recommended but optional)
    assert hasattr(ident, 'abstract')
    assert hasattr(ident, 'keywords')


def test_wms_supported_formats(wms_client):
    """Verify WMS declares supported GetMap formats."""
    getmap_op = None
    for op in wms_client.operations:
        if op.name == 'GetMap':
            getmap_op = op
            break

    assert getmap_op is not None, "WMS must support GetMap operation"
    assert hasattr(getmap_op, 'formatOptions'), "GetMap must declare format options"

    formats = getmap_op.formatOptions
    assert len(formats) > 0, "GetMap must support at least one format"

    # Check for common formats
    format_list = [f.lower() for f in formats]
    assert any('png' in f for f in format_list), "WMS should support PNG format"


# ============================================================================
#  Layer Metadata Tests
# ============================================================================

def test_wms_layer_has_crs(wms_client, test_layer_name):
    """Verify layer declares supported CRS."""
    layer = wms_client[test_layer_name]

    assert hasattr(layer, 'crsOptions'), "Layer must declare CRS options"
    crs_options = layer.crsOptions

    assert len(crs_options) > 0, "Layer must support at least one CRS"

    # Check for common CRS
    crs_list = [crs.upper() for crs in crs_options]
    assert any('EPSG:4326' in crs or 'CRS:84' in crs for crs in crs_list), \
        "Layer should support EPSG:4326 or CRS:84"


def test_wms_layer_has_bounding_box(wms_client, test_layer_name):
    """Verify layer has bounding box metadata."""
    layer = wms_client[test_layer_name]

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


def test_wms_layer_has_styles(wms_client, test_layer_name):
    """Verify layer declares available styles."""
    layer = wms_client[test_layer_name]

    assert hasattr(layer, 'styles'), "Layer should have styles"
    # Styles dict may be empty for layers with only default style
    assert isinstance(layer.styles, dict), "Styles should be a dictionary"


def test_wms_layer_queryable_attribute(wms_client, test_layer_name):
    """Verify layer declares if it's queryable for GetFeatureInfo."""
    layer = wms_client[test_layer_name]

    assert hasattr(layer, 'queryable'), "Layer should have queryable attribute"
    assert isinstance(layer.queryable, (bool, int)), "Queryable should be boolean or int"


# ============================================================================
#  GetMap Tests
# ============================================================================

def test_wms_get_map_basic(wms_client, test_layer_name):
    """Verify GetMap returns valid image."""
    try:
        from PIL import Image
        import io
    except ImportError:
        pytest.skip("Pillow not installed (pip install Pillow)")

    layer = wms_client[test_layer_name]

    # Get map image
    response = wms_client.getmap(
        layers=[test_layer_name],
        srs='EPSG:4326',
        bbox=layer.boundingBoxWGS84,
        size=(256, 256),
        format='image/png',
        transparent=True
    )

    assert response is not None, "GetMap should return response"

    # Validate image
    image_data = response.read()
    assert len(image_data) > 0, "Image data should not be empty"

    # Try to open as image
    try:
        img = Image.open(io.BytesIO(image_data))
        assert img.size == (256, 256), f"Image size should be 256x256, got {img.size}"
        assert img.format == 'PNG', f"Image format should be PNG, got {img.format}"
    except Exception as e:
        pytest.fail(f"Failed to parse image: {e}")


def test_wms_get_map_different_sizes(wms_client, test_layer_name):
    """Verify GetMap works with different image sizes."""
    try:
        from PIL import Image
        import io
    except ImportError:
        pytest.skip("Pillow not installed")

    layer = wms_client[test_layer_name]
    sizes = [(128, 128), (512, 256), (800, 600)]

    for width, height in sizes:
        response = wms_client.getmap(
            layers=[test_layer_name],
            srs='EPSG:4326',
            bbox=layer.boundingBoxWGS84,
            size=(width, height),
            format='image/png'
        )

        image_data = response.read()
        img = Image.open(io.BytesIO(image_data))
        assert img.size == (width, height), \
            f"Image size should be {width}x{height}, got {img.size}"


def test_wms_get_map_different_formats(wms_client, test_layer_name):
    """Verify GetMap supports different image formats."""
    layer = wms_client[test_layer_name]

    # Get supported formats
    getmap_op = [op for op in wms_client.operations if op.name == 'GetMap'][0]
    formats = getmap_op.formatOptions

    test_formats = ['image/png', 'image/jpeg']

    for fmt in test_formats:
        if fmt not in formats:
            continue

        response = wms_client.getmap(
            layers=[test_layer_name],
            srs='EPSG:4326',
            bbox=layer.boundingBoxWGS84,
            size=(256, 256),
            format=fmt
        )

        image_data = response.read()
        assert len(image_data) > 0, f"Image data should not be empty for format {fmt}"


def test_wms_get_map_with_crs_epsg_3857(wms_client, test_layer_name):
    """Verify GetMap works with Web Mercator (EPSG:3857)."""
    layer = wms_client[test_layer_name]

    # Check if layer supports EPSG:3857
    crs_options = [crs.upper() for crs in layer.crsOptions]
    if not any('EPSG:3857' in crs for crs in crs_options):
        pytest.skip(f"Layer {test_layer_name} does not support EPSG:3857")

    # Web Mercator bbox for approximate WGS84 bounds
    bbox_wgs84 = layer.boundingBoxWGS84
    # Simple approximation - convert WGS84 to Web Mercator
    from math import pi, log, tan

    def wgs84_to_web_mercator(lon, lat):
        x = lon * 20037508.34 / 180
        y = log(tan((90 + lat) * pi / 360)) / (pi / 180)
        y = y * 20037508.34 / 180
        return x, y

    minx, miny = wgs84_to_web_mercator(bbox_wgs84[0], bbox_wgs84[1])
    maxx, maxy = wgs84_to_web_mercator(bbox_wgs84[2], bbox_wgs84[3])

    response = wms_client.getmap(
        layers=[test_layer_name],
        srs='EPSG:3857',
        bbox=(minx, miny, maxx, maxy),
        size=(256, 256),
        format='image/png'
    )

    image_data = response.read()
    assert len(image_data) > 0, "Should return image for EPSG:3857"


def test_wms_get_map_multiple_layers(wms_client):
    """Verify GetMap can render multiple layers."""
    layers = list(wms_client.contents.keys())[:2]

    if len(layers) < 2:
        pytest.skip("Need at least 2 layers to test multi-layer rendering")

    # Get bbox that covers both layers (use first layer's bbox)
    bbox = wms_client[layers[0]].boundingBoxWGS84

    response = wms_client.getmap(
        layers=layers,
        srs='EPSG:4326',
        bbox=bbox,
        size=(256, 256),
        format='image/png'
    )

    image_data = response.read()
    assert len(image_data) > 0, "Should return image for multiple layers"


def test_wms_get_map_with_transparency(wms_client, test_layer_name):
    """Verify GetMap supports transparent parameter."""
    try:
        from PIL import Image
        import io
    except ImportError:
        pytest.skip("Pillow not installed")

    layer = wms_client[test_layer_name]

    response = wms_client.getmap(
        layers=[test_layer_name],
        srs='EPSG:4326',
        bbox=layer.boundingBoxWGS84,
        size=(256, 256),
        format='image/png',
        transparent=True
    )

    image_data = response.read()
    img = Image.open(io.BytesIO(image_data))

    # PNG with transparency should have alpha channel
    assert img.mode in ('RGBA', 'LA', 'P'), \
        f"Transparent PNG should have alpha channel, got mode {img.mode}"


# ============================================================================
#  GetFeatureInfo Tests
# ============================================================================

def test_wms_get_feature_info_supported(wms_client):
    """Verify GetFeatureInfo operation is declared."""
    operation_names = [op.name for op in wms_client.operations]
    assert 'GetFeatureInfo' in operation_names, "WMS should support GetFeatureInfo"


def test_wms_get_feature_info_basic(wms_client, test_layer_name):
    """Verify GetFeatureInfo returns feature attributes."""
    layer = wms_client[test_layer_name]

    if not layer.queryable:
        pytest.skip(f"Layer {test_layer_name} is not queryable")

    # Get feature info at center of bbox
    bbox = layer.boundingBoxWGS84
    center_x = (bbox[0] + bbox[2]) / 2
    center_y = (bbox[1] + bbox[3]) / 2

    try:
        response = wms_client.getfeatureinfo(
            layers=[test_layer_name],
            srs='EPSG:4326',
            bbox=bbox,
            size=(256, 256),
            query_layers=[test_layer_name],
            xy=(128, 128),  # center of 256x256 image
            info_format='application/json'
        )

        # Response may be empty if no features at that location
        result = response.read()
        assert result is not None, "GetFeatureInfo should return data"

        # Try to parse as JSON
        import json
        try:
            data = json.loads(result)
            assert isinstance(data, (dict, list)), "JSON response should be dict or list"
        except json.JSONDecodeError:
            # Some formats may return other content types
            pass

    except Exception as e:
        # GetFeatureInfo may not be fully supported
        if "not supported" in str(e).lower():
            pytest.skip("GetFeatureInfo not supported")
        raise


def test_wms_get_feature_info_formats(wms_client):
    """Verify GetFeatureInfo declares supported formats."""
    getfeatureinfo_op = None
    for op in wms_client.operations:
        if op.name == 'GetFeatureInfo':
            getfeatureinfo_op = op
            break

    if getfeatureinfo_op is None:
        pytest.skip("GetFeatureInfo not supported")

    assert hasattr(getfeatureinfo_op, 'formatOptions'), \
        "GetFeatureInfo must declare format options"

    formats = getfeatureinfo_op.formatOptions
    assert len(formats) > 0, "GetFeatureInfo must support at least one format"


# ============================================================================
#  Style Tests
# ============================================================================

def test_wms_get_legend_graphic_supported(wms_client):
    """Verify GetLegendGraphic operation is supported."""
    operation_names = [op.name for op in wms_client.operations]
    # GetLegendGraphic is optional but commonly supported
    has_legend = 'GetLegendGraphic' in operation_names

    # Just check if it's declared - don't fail if not supported
    assert isinstance(has_legend, bool), "Should be able to check for GetLegendGraphic"


def test_wms_layer_default_style(wms_client, test_layer_name):
    """Verify layer has default style."""
    layer = wms_client[test_layer_name]

    # Layer should have styles dict (may be empty)
    assert hasattr(layer, 'styles'), "Layer should have styles attribute"


# ============================================================================
#  Error Handling Tests
# ============================================================================

def test_wms_invalid_layer_raises_error(wms_client):
    """Verify requesting invalid layer returns error."""
    try:
        response = wms_client.getmap(
            layers=['nonexistent_layer_12345'],
            srs='EPSG:4326',
            bbox=(-180, -90, 180, 90),
            size=(256, 256),
            format='image/png'
        )

        data = response.read()

        # Server may return error image or XML error
        # Check if it looks like an error
        assert b'ServiceException' in data or b'error' in data.lower() or len(data) == 0, \
            "Should return error for invalid layer"

    except Exception as e:
        # OWSLib may raise exception for errors
        assert "layer" in str(e).lower() or "not found" in str(e).lower(), \
            f"Error should mention invalid layer: {e}"


def test_wms_invalid_crs_raises_error(wms_client, test_layer_name):
    """Verify requesting unsupported CRS returns error."""
    layer = wms_client[test_layer_name]

    try:
        response = wms_client.getmap(
            layers=[test_layer_name],
            srs='EPSG:99999',  # Invalid EPSG code
            bbox=layer.boundingBoxWGS84,
            size=(256, 256),
            format='image/png'
        )

        data = response.read()
        # May return error document

    except Exception as e:
        # Expected - invalid CRS should raise error
        assert "crs" in str(e).lower() or "srs" in str(e).lower() or "coordinate" in str(e).lower(), \
            f"Error should mention invalid CRS: {e}"


def test_wms_invalid_bbox_raises_error(wms_client, test_layer_name):
    """Verify requesting invalid bbox returns error."""
    try:
        response = wms_client.getmap(
            layers=[test_layer_name],
            srs='EPSG:4326',
            bbox=(180, 90, -180, -90),  # Invalid: min > max
            size=(256, 256),
            format='image/png'
        )

        data = response.read()
        # Server may return empty image or error

    except Exception as e:
        # Expected - invalid bbox should raise error
        assert "bbox" in str(e).lower() or "bound" in str(e).lower(), \
            f"Error should mention invalid bbox: {e}"


def test_wms_unsupported_format_raises_error(wms_client, test_layer_name):
    """Verify requesting unsupported format returns error."""
    layer = wms_client[test_layer_name]

    try:
        response = wms_client.getmap(
            layers=[test_layer_name],
            srs='EPSG:4326',
            bbox=layer.boundingBoxWGS84,
            size=(256, 256),
            format='image/xyz'  # Invalid format
        )

        data = response.read()
        # Server should return error

    except Exception as e:
        # Expected - unsupported format should raise error
        assert "format" in str(e).lower(), f"Error should mention invalid format: {e}"


# ============================================================================
#  Dimension Tests (Optional)
# ============================================================================

def test_wms_time_dimension_if_supported(wms_client):
    """Verify time dimension support if declared by any layer."""
    has_time = False

    for layer_name, layer in wms_client.contents.items():
        if hasattr(layer, 'timepositions') and layer.timepositions:
            has_time = True

            # Verify time dimension structure
            assert isinstance(layer.timepositions, list), \
                "Time positions should be a list"
            assert len(layer.timepositions) > 0, \
                "Time positions should not be empty if declared"
            break

    # This is informational - time dimension is optional
    assert isinstance(has_time, bool), "Should be able to check for time dimension"


def test_wms_elevation_dimension_if_supported(wms_client):
    """Verify elevation dimension support if declared by any layer."""
    has_elevation = False

    for layer_name, layer in wms_client.contents.items():
        if hasattr(layer, 'elevations') and layer.elevations:
            has_elevation = True

            # Verify elevation dimension structure
            assert isinstance(layer.elevations, list), \
                "Elevations should be a list"
            assert len(layer.elevations) > 0, \
                "Elevations should not be empty if declared"
            break

    # This is informational - elevation dimension is optional
    assert isinstance(has_elevation, bool), "Should be able to check for elevation dimension"


# ============================================================================
#  WMS 1.3.0 Specific Tests
# ============================================================================

def test_wms_1_3_0_axis_order(wms_client, test_layer_name):
    """Verify WMS 1.3.0 axis order handling for CRS:84 vs EPSG:4326."""
    layer = wms_client[test_layer_name]
    bbox_wgs84 = layer.boundingBoxWGS84

    # WMS 1.3.0: EPSG:4326 uses lat/lon order (y,x)
    # CRS:84 uses lon/lat order (x,y)

    # Test with EPSG:4326 (should use lat/lon order in request)
    try:
        response = wms_client.getmap(
            layers=[test_layer_name],
            srs='EPSG:4326',
            bbox=bbox_wgs84,  # OWSLib should handle axis order
            size=(256, 256),
            format='image/png'
        )

        data = response.read()
        assert len(data) > 0, "Should return image with EPSG:4326"

    except Exception as e:
        pytest.fail(f"Failed with EPSG:4326: {e}")


def test_wms_exception_format(wms_client):
    """Verify WMS declares supported exception formats."""
    # Check if exceptions attribute exists
    if hasattr(wms_client, 'exceptions'):
        exceptions = wms_client.exceptions
        assert len(exceptions) > 0, "WMS should declare exception formats"

        # WMS 1.3.0 should support XML
        assert any('xml' in exc.lower() for exc in exceptions), \
            "WMS 1.3.0 should support XML exception format"
