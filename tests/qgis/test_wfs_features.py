import pytest


@pytest.mark.requires_qgis
@pytest.mark.requires_honua
def test_wfs_layer_loads_features(qgis_app, qgis_project, honua_base_url, layer_config):
    """Verify QGIS can load WFS 2.0 layers and retrieve features."""
    from qgis.core import QgsVectorLayer  # type: ignore

    collection_id = layer_config["collection_id"]
    # WFS uses service:layer format, extract layer name
    layer_name = collection_id.split("::")[-1] if "::" in collection_id else collection_id

    # QGIS WFS provider URI format
    uri = (
        f"pagingEnabled='true' "
        f"preferCoordinatesForWfsT11='false' "
        f"restrictToRequestBBOX='1' "
        f"srsname='EPSG:4326' "
        f"typename='{layer_name}' "
        f"url='{honua_base_url}/wfs' "
        f"version='2.0.0' "
        f"maxNumFeatures='50'"
    )

    layer = QgsVectorLayer(uri, "honua-wfs", "WFS")
    assert layer.isValid(), layer.error().summary()

    qgis_project.addMapLayer(layer)

    # Validate layer metadata
    assert layer.featureCount() >= 0, "WFS layer should report feature count"
    assert layer.crs().isValid(), "WFS layer must have valid CRS"

    # Attempt to load features
    features = list(layer.getFeatures())
    assert features, "WFS layer returned no features"

    # Validate feature structure
    first = features[0]
    assert first.isValid()
    assert first.geometry() is not None, "WFS features must include geometry"
    assert first.fields().count() > 0, "WFS features must include attributes"


@pytest.mark.requires_qgis
@pytest.mark.requires_honua
def test_wfs_getcapabilities_exposes_layers(qgis_app, honua_base_url):
    """Verify WFS GetCapabilities lists available layers."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl  # type: ignore
    from qgis.PyQt.QtNetwork import QNetworkRequest  # type: ignore
    from qgis.core import QgsNetworkAccessManager  # type: ignore

    capabilities_url = f"{honua_base_url}/wfs?service=WFS&request=GetCapabilities&version=2.0.0"

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

    # Basic validation of WFS capabilities response
    assert "<WFS_Capabilities" in content or "<wfs:WFS_Capabilities" in content
    assert "version=\"2.0.0\"" in content
    assert "<FeatureTypeList" in content or "<wfs:FeatureTypeList" in content


@pytest.mark.requires_qgis
@pytest.mark.requires_honua
def test_ogc_api_features_collection_via_ogr(qgis_app, honua_base_url, layer_config):
    """Verify QGIS can load OGC API - Features collections via OGR provider."""
    from qgis.core import QgsVectorLayer  # type: ignore

    collection_id = layer_config["collection_id"]

    # OGR supports OGC API - Features via OAPIF driver
    # Format: OAPIF:http://example.com/collections/collection-id
    uri = f"OAPIF:{honua_base_url}/ogc/collections/{collection_id}"

    layer = QgsVectorLayer(uri, "honua-oapif", "ogr")
    assert layer.isValid(), layer.error().summary()

    # Validate layer loads features
    features = list(layer.getFeatures())
    assert features, "OGC API Features collection returned no features"

    # Validate CRS metadata
    assert layer.crs().isValid(), "OGC API Features layer must have valid CRS"
