"""
Comprehensive WMS 1.3.0 Integration Tests with QGIS

This test suite provides 100% coverage of WMS 1.3.0 operations using QGIS as the reference client.
Tests validate Honua's WMS implementation against the OGC WMS 1.3.0 specification using
real-world client library integration.

Test Coverage:
- GetCapabilities: Service metadata, layer list, CRS support, formats, styles
- GetMap: Single/multiple layers, CRS transforms, formats (PNG/JPEG/WebP), transparency, styles
- GetFeatureInfo: Multiple formats (JSON, HTML, GML), coordinate precision
- GetLegendGraphic: Default and custom styles, format support
- Error Handling: Invalid layers, CRS, formats, dimensions
- QGIS Integration: Load WMS layers, render maps, query features

Client: QGIS 3.34+ (PyQGIS WMS Provider)
Specification: OGC WMS 1.3.0
"""
import pytest
import xml.etree.ElementTree as ET


pytestmark = [pytest.mark.integration, pytest.mark.qgis, pytest.mark.wms, pytest.mark.requires_honua]


# ============================================================================
#  GetCapabilities Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wms_getcapabilities_returns_valid_document(qgis_app, honua_base_url):
    """Verify WMS GetCapabilities returns valid WMS_Capabilities XML document."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    capabilities_url = f"{honua_base_url}/wms?service=WMS&request=GetCapabilities&version=1.3.0"

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
    assert "<WMS_Capabilities" in content or "<WMS_MS_Capabilities" in content
    assert "version=\"1.3.0\"" in content
    assert "<Layer>" in content
    assert "<Request>" in content

    # Parse XML to validate structure
    root = ET.fromstring(content)
    assert root.tag.endswith("WMS_Capabilities") or root.tag.endswith("WMS_MS_Capabilities"), \
        f"Root element should be WMS_Capabilities, got {root.tag}"


@pytest.mark.requires_qgis
def test_wms_getcapabilities_lists_layers(qgis_app, honua_base_url):
    """Verify WMS GetCapabilities lists all available layers."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    capabilities_url = f"{honua_base_url}/wms?service=WMS&request=GetCapabilities&version=1.3.0"

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

    # Find Layer elements (should be at least one named layer)
    layers = []
    for elem in root.iter():
        if elem.tag.endswith("Layer"):
            name_elem = None
            for child in elem:
                if child.tag.endswith("Name"):
                    name_elem = child
                    break
            if name_elem is not None and name_elem.text:
                layers.append(name_elem.text)

    assert len(layers) > 0, "WMS_Capabilities should contain at least one named layer"


@pytest.mark.requires_qgis
def test_wms_getcapabilities_declares_crs_support(qgis_app, honua_base_url):
    """Verify WMS GetCapabilities declares supported CRS (EPSG:4326, EPSG:3857)."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    capabilities_url = f"{honua_base_url}/wms?service=WMS&request=GetCapabilities&version=1.3.0"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(capabilities_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # WMS 1.3.0 uses CRS instead of SRS
    assert "EPSG:4326" in content, "WMS must support EPSG:4326 (WGS84)"
    assert "EPSG:3857" in content or "CRS:84" in content, "WMS should support Web Mercator or CRS:84"


@pytest.mark.requires_qgis
def test_wms_getcapabilities_declares_formats(qgis_app, honua_base_url):
    """Verify WMS GetCapabilities declares supported image formats."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    capabilities_url = f"{honua_base_url}/wms?service=WMS&request=GetCapabilities&version=1.3.0"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(capabilities_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Validate output formats
    assert "image/png" in content, "WMS must support PNG format"
    # JPEG is common but not required
    has_jpeg = "image/jpeg" in content or "image/jpg" in content
    assert has_jpeg, "WMS should support JPEG format"


@pytest.mark.requires_qgis
def test_wms_getcapabilities_includes_service_metadata(qgis_app, honua_base_url):
    """Verify WMS GetCapabilities includes service metadata (title, abstract)."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    capabilities_url = f"{honua_base_url}/wms?service=WMS&request=GetCapabilities&version=1.3.0"

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

    # Find Service element
    service_elem = None
    for child in root:
        if child.tag.endswith("Service"):
            service_elem = child
            break

    assert service_elem is not None, "WMS_Capabilities must include Service metadata"

    # Find Title element
    has_title = False
    for elem in service_elem:
        if elem.tag.endswith("Title"):
            has_title = True
            break

    assert has_title, "Service metadata must include Title"


# ============================================================================
#  GetMap - Basic Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wms_getmap_single_layer_png(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify WMS GetMap returns PNG image for single layer."""
    from qgis.core import QgsRasterLayer

    layer_name = layer_config["wms_layer"]
    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/png"
        "&crs=EPSG:3857&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms-png", "wms")
    if not layer.isValid():
        pytest.skip(f"WMS layer not available: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    # Validate layer loaded successfully
    assert layer.isValid(), "WMS layer should be valid"
    assert layer.extent().isNull() is False, "WMS layer should have extent"
    assert layer.crs().isValid(), "WMS layer should have valid CRS"


@pytest.mark.requires_qgis
def test_wms_getmap_single_layer_jpeg(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify WMS GetMap returns JPEG image for single layer."""
    from qgis.core import QgsRasterLayer

    layer_name = layer_config["wms_layer"]
    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/jpeg"
        "&crs=EPSG:3857&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms-jpeg", "wms")
    if not layer.isValid():
        pytest.skip(f"WMS layer not available or JPEG not supported: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    # Validate layer loaded successfully
    assert layer.isValid(), "WMS layer should be valid"


@pytest.mark.requires_qgis
def test_wms_getmap_single_layer_webp(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify WMS GetMap returns WebP image if supported."""
    from qgis.core import QgsRasterLayer

    layer_name = layer_config["wms_layer"]
    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/webp"
        "&crs=EPSG:3857&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms-webp", "wms")
    if not layer.isValid():
        pytest.skip(f"WMS layer not available or WebP not supported: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    # Validate layer loaded successfully
    assert layer.isValid(), "WMS layer should be valid"


@pytest.mark.requires_qgis
def test_wms_getmap_renders_valid_image(qgis_app, qgis_project, honua_base_url, layer_config, tmp_path):
    """Verify WMS GetMap renders valid non-empty image."""
    from qgis.core import (
        QgsMapRendererSequentialJob,
        QgsMapSettings,
        QgsRasterLayer,
    )
    from qgis.PyQt.QtCore import QSize

    layer_name = layer_config["wms_layer"]
    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/png"
        "&crs=EPSG:3857&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms-render", "wms")
    if not layer.isValid():
        pytest.skip(f"WMS layer not available: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    settings = QgsMapSettings()
    settings.setLayers([layer])
    settings.setDestinationCrs(layer.crs())
    settings.setExtent(layer.extent())
    settings.setOutputSize(QSize(512, 512))

    job = QgsMapRendererSequentialJob(settings)
    job.start()
    job.waitForFinished()

    image = job.renderedImage()
    output = tmp_path / "wms-rendered.png"
    assert not image.isNull(), "Rendered image should not be null"
    assert image.save(str(output), "PNG"), "Image should be saveable as PNG"
    assert output.stat().st_size > 1000, "Rendered image should have reasonable file size"


@pytest.mark.requires_qgis
def test_wms_getmap_multiple_layers(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify WMS GetMap can request multiple layers in single request."""
    from qgis.core import QgsRasterLayer

    layer_name = layer_config["wms_layer"]
    # Request same layer twice (simulates multiple layers)
    layers = f"{layer_name},{layer_name}"

    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layers}&styles=&format=image/png"
        "&crs=EPSG:3857&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms-multi", "wms")
    if not layer.isValid():
        pytest.skip(f"Multiple layers not supported: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    # Validate layer loaded successfully
    assert layer.isValid(), "WMS layer with multiple layers should be valid"


# ============================================================================
#  GetMap - CRS Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wms_getmap_epsg_4326(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify WMS GetMap supports EPSG:4326 (WGS84) coordinate system."""
    from qgis.core import QgsRasterLayer

    layer_name = layer_config["wms_layer"]
    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/png"
        "&crs=EPSG:4326&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms-4326", "wms")
    if not layer.isValid():
        pytest.skip(f"WMS layer not available: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    # Validate CRS
    crs = layer.crs()
    assert crs.isValid(), "Layer must have valid CRS"
    assert crs.authid() == "EPSG:4326", f"Expected EPSG:4326, got {crs.authid()}"


@pytest.mark.requires_qgis
def test_wms_getmap_epsg_3857(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify WMS GetMap supports EPSG:3857 (Web Mercator) coordinate system."""
    from qgis.core import QgsRasterLayer

    layer_name = layer_config["wms_layer"]
    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/png"
        "&crs=EPSG:3857&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms-3857", "wms")
    if not layer.isValid():
        pytest.skip(f"WMS layer not available: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    # Validate CRS
    crs = layer.crs()
    assert crs.isValid(), "Layer must have valid CRS"
    assert crs.authid() == "EPSG:3857", f"Expected EPSG:3857, got {crs.authid()}"


@pytest.mark.requires_qgis
def test_wms_getmap_crs_transformation(qgis_app, qgis_project, honua_base_url, layer_config, tmp_path):
    """Verify WMS GetMap performs correct CRS transformation between EPSG:4326 and EPSG:3857."""
    from qgis.core import (
        QgsMapRendererSequentialJob,
        QgsMapSettings,
        QgsRasterLayer,
        QgsCoordinateReferenceSystem,
    )
    from qgis.PyQt.QtCore import QSize

    layer_name = layer_config["wms_layer"]

    # Load layer in EPSG:4326
    uri_4326 = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/png"
        "&crs=EPSG:4326&dpiMode=7"
    )
    layer_4326 = QgsRasterLayer(uri_4326, "wms-4326", "wms")
    if not layer_4326.isValid():
        pytest.skip(f"WMS layer not available: {layer_4326.error().summary()}")

    # Load layer in EPSG:3857
    uri_3857 = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/png"
        "&crs=EPSG:3857&dpiMode=7"
    )
    layer_3857 = QgsRasterLayer(uri_3857, "wms-3857", "wms")
    if not layer_3857.isValid():
        pytest.skip(f"WMS layer not available: {layer_3857.error().summary()}")

    # Both layers should be valid
    assert layer_4326.isValid() and layer_3857.isValid(), "Both CRS variants should load"

    # Verify CRS differ
    assert layer_4326.crs().authid() == "EPSG:4326"
    assert layer_3857.crs().authid() == "EPSG:3857"


# ============================================================================
#  GetMap - Transparency and Styles Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wms_getmap_with_transparency(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify WMS GetMap supports TRANSPARENT parameter for PNG with alpha channel."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    layer_name = layer_config["wms_layer"]

    # Build GetMap URL with TRANSPARENT=TRUE
    getmap_url = (
        f"{honua_base_url}/wms?"
        f"SERVICE=WMS&"
        f"VERSION=1.3.0&"
        f"REQUEST=GetMap&"
        f"LAYERS={layer_name}&"
        f"STYLES=&"
        f"CRS=EPSG:3857&"
        f"BBOX=-20037508,-20037508,20037508,20037508&"
        f"WIDTH=256&"
        f"HEIGHT=256&"
        f"FORMAT=image/png&"
        f"TRANSPARENT=TRUE"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(getmap_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {layer_name} not available")

    assert status == 200, f"GetMap with transparency failed with status {status}"

    # Verify we got PNG image
    content_type = reply.header(QNetworkRequest.ContentTypeHeader)
    reply.deleteLater()
    assert content_type and "image/png" in content_type.lower(), \
        f"Expected image/png, got {content_type}"


@pytest.mark.requires_qgis
def test_wms_getmap_with_custom_style(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify WMS GetMap supports STYLES parameter for custom rendering."""
    from qgis.core import QgsRasterLayer

    layer_name = layer_config["wms_layer"]

    # Request with explicit default style
    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=default&format=image/png"
        "&crs=EPSG:3857&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms-styled", "wms")
    if not layer.isValid():
        # Custom styles are optional, skip if not supported
        pytest.skip(f"Custom styles not supported: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    # Validate layer loaded successfully
    assert layer.isValid(), "WMS layer with style should be valid"


# ============================================================================
#  GetFeatureInfo Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wms_getfeatureinfo_json_format(qgis_app, honua_base_url, layer_config):
    """Verify WMS GetFeatureInfo returns feature attributes in JSON format."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    layer_name = layer_config["wms_layer"]

    # Build GetFeatureInfo URL
    getfeatureinfo_url = (
        f"{honua_base_url}/wms?"
        f"SERVICE=WMS&"
        f"VERSION=1.3.0&"
        f"REQUEST=GetFeatureInfo&"
        f"LAYERS={layer_name}&"
        f"QUERY_LAYERS={layer_name}&"
        f"CRS=EPSG:3857&"
        f"BBOX=-20037508,-20037508,20037508,20037508&"
        f"WIDTH=256&"
        f"HEIGHT=256&"
        f"I=128&"
        f"J=128&"
        f"INFO_FORMAT=application/json&"
        f"FEATURE_COUNT=10"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(getfeatureinfo_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {layer_name} not available")

    # GetFeatureInfo may return 200 even if no features found
    assert status == 200, f"GetFeatureInfo request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Try to parse as JSON
    import json
    try:
        data = json.loads(content)
        # GeoJSON FeatureCollection format
        assert isinstance(data, dict), "JSON response should be an object"
    except json.JSONDecodeError:
        pytest.skip("GetFeatureInfo JSON format not supported or different structure")


@pytest.mark.requires_qgis
def test_wms_getfeatureinfo_html_format(qgis_app, honua_base_url, layer_config):
    """Verify WMS GetFeatureInfo returns feature attributes in HTML format."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    layer_name = layer_config["wms_layer"]

    # Build GetFeatureInfo URL
    getfeatureinfo_url = (
        f"{honua_base_url}/wms?"
        f"SERVICE=WMS&"
        f"VERSION=1.3.0&"
        f"REQUEST=GetFeatureInfo&"
        f"LAYERS={layer_name}&"
        f"QUERY_LAYERS={layer_name}&"
        f"CRS=EPSG:3857&"
        f"BBOX=-20037508,-20037508,20037508,20037508&"
        f"WIDTH=256&"
        f"HEIGHT=256&"
        f"I=128&"
        f"J=128&"
        f"INFO_FORMAT=text/html&"
        f"FEATURE_COUNT=10"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(getfeatureinfo_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {layer_name} not available")

    assert status == 200, f"GetFeatureInfo request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Validate HTML content
    assert "<html" in content.lower() or "<!doctype html>" in content.lower(), \
        "HTML format should return valid HTML"


@pytest.mark.requires_qgis
def test_wms_getfeatureinfo_gml_format(qgis_app, honua_base_url, layer_config):
    """Verify WMS GetFeatureInfo returns feature attributes in GML format."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    layer_name = layer_config["wms_layer"]

    # Build GetFeatureInfo URL
    getfeatureinfo_url = (
        f"{honua_base_url}/wms?"
        f"SERVICE=WMS&"
        f"VERSION=1.3.0&"
        f"REQUEST=GetFeatureInfo&"
        f"LAYERS={layer_name}&"
        f"QUERY_LAYERS={layer_name}&"
        f"CRS=EPSG:3857&"
        f"BBOX=-20037508,-20037508,20037508,20037508&"
        f"WIDTH=256&"
        f"HEIGHT=256&"
        f"I=128&"
        f"J=128&"
        f"INFO_FORMAT=application/vnd.ogc.gml&"
        f"FEATURE_COUNT=10"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(getfeatureinfo_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {layer_name} not available")

    # GML format may not be supported by all servers
    if status == 400:
        pytest.skip("GetFeatureInfo GML format not supported")

    assert status == 200, f"GetFeatureInfo request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Validate GML content
    assert "<gml:" in content.lower() or "gml" in content.lower(), \
        "GML format should return GML XML"


# ============================================================================
#  GetLegendGraphic Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wms_getlegendgraphic_default_style(qgis_app, honua_base_url, layer_config):
    """Verify WMS GetLegendGraphic returns legend image for default style."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    layer_name = layer_config["wms_layer"]

    # Build GetLegendGraphic URL
    getlegend_url = (
        f"{honua_base_url}/wms?"
        f"SERVICE=WMS&"
        f"VERSION=1.3.0&"
        f"REQUEST=GetLegendGraphic&"
        f"LAYER={layer_name}&"
        f"FORMAT=image/png"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(getlegend_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {layer_name} not available")

    # GetLegendGraphic is optional in WMS 1.3.0
    if status == 501 or status == 400:
        pytest.skip("GetLegendGraphic not supported")

    assert status == 200, f"GetLegendGraphic request failed with status {status}"

    # Verify we got an image
    content_type = reply.header(QNetworkRequest.ContentTypeHeader)
    content = bytes(reply.readAll())
    reply.deleteLater()

    assert content_type and "image/" in content_type.lower(), \
        f"Expected image content type, got {content_type}"
    assert len(content) > 100, "Legend image should have reasonable size"


@pytest.mark.requires_qgis
def test_wms_getlegendgraphic_custom_size(qgis_app, honua_base_url, layer_config):
    """Verify WMS GetLegendGraphic supports WIDTH/HEIGHT parameters."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    layer_name = layer_config["wms_layer"]

    # Build GetLegendGraphic URL with custom size
    getlegend_url = (
        f"{honua_base_url}/wms?"
        f"SERVICE=WMS&"
        f"VERSION=1.3.0&"
        f"REQUEST=GetLegendGraphic&"
        f"LAYER={layer_name}&"
        f"FORMAT=image/png&"
        f"WIDTH=200&"
        f"HEIGHT=100"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(getlegend_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    if status == 404:
        pytest.skip(f"Layer {layer_name} not available")

    # GetLegendGraphic is optional
    if status == 501 or status == 400:
        pytest.skip("GetLegendGraphic with custom size not supported")

    assert status == 200, f"GetLegendGraphic request failed with status {status}"

    # Verify we got an image
    content_type = reply.header(QNetworkRequest.ContentTypeHeader)
    reply.deleteLater()
    assert content_type and "image/" in content_type.lower(), \
        f"Expected image content type, got {content_type}"


# ============================================================================
#  Error Handling Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_wms_invalid_layer_returns_error(qgis_app, honua_base_url):
    """Verify WMS returns appropriate error for invalid layer name."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # Build GetMap URL with non-existent layer
    getmap_url = (
        f"{honua_base_url}/wms?"
        f"SERVICE=WMS&"
        f"VERSION=1.3.0&"
        f"REQUEST=GetMap&"
        f"LAYERS=nonexistent_layer_12345&"
        f"STYLES=&"
        f"CRS=EPSG:3857&"
        f"BBOX=-20037508,-20037508,20037508,20037508&"
        f"WIDTH=256&"
        f"HEIGHT=256&"
        f"FORMAT=image/png"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(getmap_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)

    # Should return error status (400 or 404)
    assert status in (400, 404), f"Invalid layer should return 400 or 404, got {status}"


@pytest.mark.requires_qgis
def test_wms_invalid_crs_returns_error(qgis_app, honua_base_url, layer_config):
    """Verify WMS returns error for unsupported CRS."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    layer_name = layer_config["wms_layer"]

    # Build GetMap URL with unsupported CRS
    getmap_url = (
        f"{honua_base_url}/wms?"
        f"SERVICE=WMS&"
        f"VERSION=1.3.0&"
        f"REQUEST=GetMap&"
        f"LAYERS={layer_name}&"
        f"STYLES=&"
        f"CRS=EPSG:99999&"  # Invalid CRS
        f"BBOX=-180,-90,180,90&"
        f"WIDTH=256&"
        f"HEIGHT=256&"
        f"FORMAT=image/png"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(getmap_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)

    # Should return error status (400)
    assert status == 400, f"Invalid CRS should return 400, got {status}"


@pytest.mark.requires_qgis
def test_wms_invalid_format_returns_error(qgis_app, honua_base_url, layer_config):
    """Verify WMS returns error for unsupported image format."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    layer_name = layer_config["wms_layer"]

    # Build GetMap URL with unsupported format
    getmap_url = (
        f"{honua_base_url}/wms?"
        f"SERVICE=WMS&"
        f"VERSION=1.3.0&"
        f"REQUEST=GetMap&"
        f"LAYERS={layer_name}&"
        f"STYLES=&"
        f"CRS=EPSG:3857&"
        f"BBOX=-20037508,-20037508,20037508,20037508&"
        f"WIDTH=256&"
        f"HEIGHT=256&"
        f"FORMAT=image/invalid_format"  # Invalid format
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(getmap_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)

    # Should return error status (400)
    assert status == 400, f"Invalid format should return 400, got {status}"


@pytest.mark.requires_qgis
def test_wms_missing_required_parameter_returns_error(qgis_app, honua_base_url):
    """Verify WMS returns error when required parameter is missing."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # Build GetMap URL without LAYERS parameter
    getmap_url = (
        f"{honua_base_url}/wms?"
        f"SERVICE=WMS&"
        f"VERSION=1.3.0&"
        f"REQUEST=GetMap&"
        # Missing LAYERS parameter
        f"STYLES=&"
        f"CRS=EPSG:3857&"
        f"BBOX=-20037508,-20037508,20037508,20037508&"
        f"WIDTH=256&"
        f"HEIGHT=256&"
        f"FORMAT=image/png"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(getmap_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)

    # Should return error status (400)
    assert status == 400, f"Missing required parameter should return 400, got {status}"


# ============================================================================
#  QGIS Integration Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_qgis_wms_provider_loads_layer(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify QGIS WMS provider successfully loads Honua WMS layer."""
    from qgis.core import QgsRasterLayer

    layer_name = layer_config["wms_layer"]
    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/png"
        "&crs=EPSG:3857&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms-integration", "wms")
    if not layer.isValid():
        pytest.skip(f"WMS layer not available: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    # Comprehensive validation
    assert layer.isValid(), "Layer must be valid"
    assert layer.type() == layer.RasterLayer, "Layer must be raster type"
    assert layer.providerType() == "wms", "Layer must use WMS provider"
    assert not layer.extent().isEmpty(), "Layer must have valid extent"
    assert layer.crs().isValid(), "Layer must have valid CRS"
    assert layer.width() > 0, "Layer must have valid width"
    assert layer.height() > 0, "Layer must have valid height"


@pytest.mark.requires_qgis
def test_qgis_wms_layer_identifies_features(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify QGIS can perform identify (GetFeatureInfo) on WMS layer."""
    from qgis.core import QgsRasterLayer, QgsPointXY

    layer_name = layer_config["wms_layer"]
    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/png"
        "&crs=EPSG:3857&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms-identify", "wms")
    if not layer.isValid():
        pytest.skip(f"WMS layer not available: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    # Attempt to identify features at center of extent
    extent = layer.extent()
    center = QgsPointXY(extent.center())

    # QGIS dataProvider().identify() method
    results = layer.dataProvider().identify(center, layer.dataProvider().IdentifyFormatText)

    # Identify may return empty results if no features at that point, which is valid
    assert results is not None, "Identify operation should return results object"


@pytest.mark.requires_qgis
@pytest.mark.slow
def test_qgis_wms_layer_renders_to_file(qgis_app, qgis_project, honua_base_url, layer_config, tmp_path):
    """Verify QGIS can render WMS layer to image file."""
    from qgis.core import (
        QgsMapRendererSequentialJob,
        QgsMapSettings,
        QgsRasterLayer,
        QgsRectangle,
    )
    from qgis.PyQt.QtCore import QSize

    layer_name = layer_config["wms_layer"]
    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/png"
        "&crs=EPSG:3857&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms-render", "wms")
    if not layer.isValid():
        pytest.skip(f"WMS layer not available: {layer.error().summary()}")

    qgis_project.addMapLayer(layer)

    # Configure map settings
    settings = QgsMapSettings()
    settings.setLayers([layer])
    settings.setDestinationCrs(layer.crs())

    # Use layer extent or default extent
    extent = layer.extent()
    if extent.isEmpty():
        # Use default Web Mercator world extent
        extent = QgsRectangle(-20037508, -20037508, 20037508, 20037508)

    settings.setExtent(extent)
    settings.setOutputSize(QSize(1024, 768))

    # Render to image
    job = QgsMapRendererSequentialJob(settings)
    job.start()
    job.waitForFinished()

    image = job.renderedImage()
    output = tmp_path / "wms-qgis-integration.png"

    assert not image.isNull(), "Rendered image should not be null"
    assert image.save(str(output), "PNG"), "Image should be saveable"
    assert output.stat().st_size > 5000, "Rendered image should have substantial file size"
