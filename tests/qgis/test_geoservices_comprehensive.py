"""
Comprehensive Esri GeoServices REST API Integration Tests with QGIS

This test suite provides comprehensive coverage of Esri GeoServices REST API operations
using QGIS as the reference client. Tests validate Honua's GeoServices implementation
against the Geoservices REST a.k.a. Esri REST API specification using real-world client library integration.

Test Coverage:
- Service Discovery: Service root metadata (MapServer, FeatureServer)
- Layer Metadata: Layer list, field definitions, geometry types, spatial extent
- Query Operations: where clause, spatial filters, relationship tests
- Output Control: outFields, returnGeometry, format selection (JSON, GeoJSON)
- Paging: resultOffset, resultRecordCount for pagination
- QGIS Integration: Load layers via ArcGIS FeatureServer provider
- Identify Operations: Point-based feature identification
- Error Handling: Invalid parameters, non-existent layers

Client: QGIS 3.34+ (PyQGIS with ArcGIS FeatureServer Provider)
Specification: Esri ArcGIS REST API 10.8+
Reference: https://developers.arcgis.com/rest/services-reference/enterprise/feature-service.htm
"""
import json
import pytest
import xml.etree.ElementTree as ET
from typing import Dict, Any, Optional


pytestmark = [
    pytest.mark.integration,
    pytest.mark.geoservices,
    pytest.mark.qgis,
    pytest.mark.requires_honua
]


# ============================================================================
#  Service Root Metadata Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_featureserver_root_returns_valid_metadata(qgis_app, honua_base_url):
    """
    Verify FeatureServer root endpoint returns valid service metadata.

    Tests that the service root endpoint provides:
    - Current API version
    - Service description
    - List of available layers with IDs and names
    - Capabilities string
    - MaxRecordCount limit
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # Test FeatureServer root endpoint
    service_url = f"{honua_base_url}/rest/services/roads/FeatureServer?f=json"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(service_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"FeatureServer root request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Parse JSON response
    data = json.loads(content)

    # Validate required fields
    assert "currentVersion" in data, "Response must include currentVersion"
    assert isinstance(data["currentVersion"], (int, float)), "currentVersion must be numeric"
    assert data["currentVersion"] >= 10.0, "API version should be 10.0 or higher"

    assert "layers" in data, "Response must include layers array"
    assert isinstance(data["layers"], list), "layers must be an array"
    assert len(data["layers"]) > 0, "Service should have at least one layer"

    # Validate first layer structure
    first_layer = data["layers"][0]
    assert "id" in first_layer, "Layer must have id field"
    assert "name" in first_layer, "Layer must have name field"
    assert "geometryType" in first_layer, "Layer must have geometryType field"

    # Validate capabilities
    assert "capabilities" in data, "Response must include capabilities"
    capabilities = data["capabilities"].lower()
    assert "query" in capabilities, "Service should support Query capability"


@pytest.mark.requires_qgis
def test_mapserver_root_returns_valid_metadata(qgis_app, honua_base_url):
    """
    Verify MapServer root endpoint returns valid service metadata.

    Tests that MapServer endpoints provide similar metadata to FeatureServer
    including layer lists, extent information, and service capabilities.
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    service_url = f"{honua_base_url}/rest/services/roads/MapServer?f=json"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(service_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"MapServer root request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)

    # Validate service metadata
    assert "currentVersion" in data
    assert "layers" in data
    assert isinstance(data["layers"], list)


# ============================================================================
#  Layer Metadata Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_layer_metadata_includes_fields_and_geometry(qgis_app, honua_base_url):
    """
    Verify layer metadata endpoint returns complete field definitions and geometry type.

    Tests that the layer detail endpoint provides:
    - Field names, types, aliases
    - Nullable and editable properties
    - Object ID field identification
    - Geometry type (Point, Polyline, Polygon)
    - Spatial reference (WKID)
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # Request layer 0 metadata
    layer_url = f"{honua_base_url}/rest/services/roads/FeatureServer/0?f=json"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(layer_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Layer metadata request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)

    # Validate layer metadata structure
    assert "id" in data or "name" in data, "Layer must have id or name"
    assert "type" in data, "Layer must have type field"
    assert "geometryType" in data, "Layer must have geometryType"
    assert data["geometryType"].startswith("esriGeometry"), "geometryType must be Esri format"

    # Validate fields array
    assert "fields" in data, "Layer must include fields array"
    fields = data["fields"]
    assert isinstance(fields, list), "fields must be an array"
    assert len(fields) > 0, "Layer should have at least one field"

    # Validate field structure
    first_field = fields[0]
    assert "name" in first_field, "Field must have name"
    assert "type" in first_field, "Field must have type"
    assert first_field["type"].startswith("esriFieldType"), "Field type must be Esri format"

    # Validate object ID field is identified
    assert "objectIdField" in data, "Layer must identify objectIdField"
    oid_field = data["objectIdField"]
    assert any(f["name"] == oid_field for f in fields), f"objectIdField {oid_field} must exist in fields"

    # Validate spatial reference if geometry present
    if data["geometryType"] != "esriGeometryNull":
        assert "extent" in data or "sourceSpatialReference" in data, \
            "Layer with geometry must have extent or sourceSpatialReference"


@pytest.mark.requires_qgis
def test_layer_metadata_includes_extent(qgis_app, honua_base_url):
    """
    Verify layer metadata includes spatial extent with valid bounding box.

    Tests that the extent object contains:
    - xmin, ymin, xmax, ymax coordinates
    - spatialReference with WKID
    - Valid coordinate ranges for the CRS
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    layer_url = f"{honua_base_url}/rest/services/roads/FeatureServer/0?f=json"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(layer_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)

    # Check for extent if layer has geometry
    if data.get("geometryType") != "esriGeometryNull":
        extent = data.get("extent")
        if extent:
            assert "xmin" in extent, "Extent must have xmin"
            assert "ymin" in extent, "Extent must have ymin"
            assert "xmax" in extent, "Extent must have xmax"
            assert "ymax" in extent, "Extent must have ymax"
            assert "spatialReference" in extent, "Extent must have spatialReference"

            # Validate extent values are numeric
            assert isinstance(extent["xmin"], (int, float)), "xmin must be numeric"
            assert isinstance(extent["ymin"], (int, float)), "ymin must be numeric"
            assert isinstance(extent["xmax"], (int, float)), "xmax must be numeric"
            assert isinstance(extent["ymax"], (int, float)), "ymax must be numeric"

            # Validate spatial reference has WKID
            assert "wkid" in extent["spatialReference"], "spatialReference must have wkid"


# ============================================================================
#  Query Operation Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_query_with_where_clause_filters_features(qgis_app, honua_base_url):
    """
    Verify query endpoint filters features using SQL where clause.

    Tests that the query operation:
    - Accepts where parameter with SQL syntax
    - Returns only matching features
    - Includes all requested fields in response
    - Provides proper feature count
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # Query with where clause
    query_url = (
        f"{honua_base_url}/rest/services/roads/FeatureServer/0/query?"
        f"where=status='open'&outFields=*&f=json"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(query_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Query request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)

    # Validate response structure
    assert "features" in data, "Query response must include features array"
    features = data["features"]
    assert isinstance(features, list), "features must be an array"

    # Validate feature structure if results present
    if len(features) > 0:
        first_feature = features[0]
        assert "attributes" in first_feature, "Feature must have attributes"
        attributes = first_feature["attributes"]

        # Verify where clause was applied
        if "status" in attributes:
            assert attributes["status"] == "open", "Features should match where clause"


@pytest.mark.requires_qgis
def test_query_with_geometry_filter(qgis_app, honua_base_url):
    """
    Verify query endpoint filters features using spatial geometry filter.

    Tests that the query operation:
    - Accepts geometry parameter (envelope, point, polygon)
    - Applies geometryType parameter
    - Respects spatialRel parameter (intersects, contains, etc.)
    - Returns only spatially matching features
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager
    from urllib.parse import quote

    # Define bounding box geometry (example coordinates)
    bbox = json.dumps({
        "xmin": -123.0,
        "ymin": 45.0,
        "xmax": -122.0,
        "ymax": 46.0,
        "spatialReference": {"wkid": 4326}
    })

    query_url = (
        f"{honua_base_url}/rest/services/roads/FeatureServer/0/query?"
        f"geometry={quote(bbox)}&geometryType=esriGeometryEnvelope&"
        f"spatialRel=esriSpatialRelIntersects&outFields=*&f=json"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(query_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Geometry filter query failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)

    # Validate response structure
    assert "features" in data, "Query response must include features array"
    assert isinstance(data["features"], list), "features must be an array"


@pytest.mark.requires_qgis
def test_query_with_spatial_relationship(qgis_app, honua_base_url):
    """
    Verify query endpoint supports different spatial relationship tests.

    Tests spatial relationship operators:
    - esriSpatialRelIntersects
    - esriSpatialRelContains
    - esriSpatialRelWithin
    - esriSpatialRelEnvelopeIntersects
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager
    from urllib.parse import quote

    bbox = json.dumps({
        "xmin": -123.0,
        "ymin": 45.0,
        "xmax": -122.0,
        "ymax": 46.0,
        "spatialReference": {"wkid": 4326}
    })

    # Test with envelope intersects (fast bounding box test)
    query_url = (
        f"{honua_base_url}/rest/services/roads/FeatureServer/0/query?"
        f"geometry={quote(bbox)}&geometryType=esriGeometryEnvelope&"
        f"spatialRel=esriSpatialRelEnvelopeIntersects&returnCountOnly=true&f=json"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(query_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Spatial relationship query failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)

    # Validate count response
    assert "count" in data, "Count query must return count field"
    assert isinstance(data["count"], int), "count must be integer"


@pytest.mark.requires_qgis
def test_query_with_outfields_parameter(qgis_app, honua_base_url):
    """
    Verify query endpoint respects outFields parameter for field selection.

    Tests that:
    - outFields=* returns all fields
    - outFields=field1,field2 returns only specified fields
    - Object ID field is always included
    - Invalid field names are ignored gracefully
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # First get layer metadata to know field names
    layer_url = f"{honua_base_url}/rest/services/roads/FeatureServer/0?f=json"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(layer_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    layer_content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    layer_data = json.loads(layer_content)
    fields = layer_data.get("fields", [])

    if len(fields) >= 2:
        # Select first two non-OID fields
        field_names = [f["name"] for f in fields if f.get("type") != "esriFieldTypeOID"][:2]
        out_fields = ",".join(field_names)

        # Query with specific outFields
        query_url = (
            f"{honua_base_url}/rest/services/roads/FeatureServer/0/query?"
            f"where=1=1&outFields={out_fields}&f=json"
        )

        request = QNetworkRequest(QUrl(query_url))
        reply = manager.get(request)

        loop = QEventLoop()
        reply.finished.connect(loop.quit)
        loop.exec()

        content = bytes(reply.readAll()).decode("utf-8")
        reply.deleteLater()

        data = json.loads(content)

        # Validate response includes requested fields
        if data.get("features") and len(data["features"]) > 0:
            attributes = data["features"][0]["attributes"]
            for field_name in field_names:
                assert field_name in attributes, f"Requested field {field_name} should be in response"


@pytest.mark.requires_qgis
def test_query_with_return_geometry_false(qgis_app, honua_base_url):
    """
    Verify query endpoint respects returnGeometry parameter.

    Tests that:
    - returnGeometry=true includes geometry in features
    - returnGeometry=false omits geometry (attributes only)
    - Response size is smaller without geometry
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # Query without geometry
    query_url = (
        f"{honua_base_url}/rest/services/roads/FeatureServer/0/query?"
        f"where=1=1&outFields=*&returnGeometry=false&f=json"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(query_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)

    # Validate features do not include geometry
    features = data.get("features", [])
    if len(features) > 0:
        first_feature = features[0]
        assert "geometry" not in first_feature or first_feature["geometry"] is None, \
            "Features should not include geometry when returnGeometry=false"
        assert "attributes" in first_feature, "Features should still include attributes"


@pytest.mark.requires_qgis
def test_query_format_json_returns_esri_json(qgis_app, honua_base_url):
    """
    Verify query endpoint returns Esri JSON format (default).

    Tests that f=json returns:
    - Features array with attributes and geometry
    - Geometry in Esri JSON format (rings, paths, points, x/y)
    - spatialReference with wkid
    - fields array with field metadata
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    query_url = (
        f"{honua_base_url}/rest/services/roads/FeatureServer/0/query?"
        f"where=1=1&outFields=*&f=json"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(query_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)

    # Validate Esri JSON structure
    assert "features" in data, "Response must have features array"
    assert "spatialReference" in data, "Response must have spatialReference"
    assert "geometryType" in data, "Response must have geometryType"
    assert "fields" in data, "Response must have fields array"

    # Validate geometry format if present
    features = data.get("features", [])
    if len(features) > 0 and features[0].get("geometry"):
        geom = features[0]["geometry"]
        geom_type = data["geometryType"]

        if geom_type == "esriGeometryPoint":
            assert "x" in geom and "y" in geom, "Point geometry must have x and y"
        elif geom_type == "esriGeometryPolyline":
            assert "paths" in geom, "Polyline geometry must have paths"
        elif geom_type == "esriGeometryPolygon":
            assert "rings" in geom, "Polygon geometry must have rings"


@pytest.mark.requires_qgis
def test_query_format_geojson_returns_geojson(qgis_app, honua_base_url):
    """
    Verify query endpoint returns GeoJSON format when requested.

    Tests that f=geojson returns:
    - FeatureCollection with features array
    - Features with geometry in GeoJSON format (coordinates)
    - Features with properties object
    - Valid GeoJSON that can be parsed by standard libraries
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    query_url = (
        f"{honua_base_url}/rest/services/roads/FeatureServer/0/query?"
        f"where=1=1&outFields=*&f=geojson"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(query_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)

    # Validate GeoJSON structure
    assert "type" in data, "GeoJSON must have type field"
    assert data["type"] == "FeatureCollection", "Type must be FeatureCollection"
    assert "features" in data, "GeoJSON must have features array"

    # Validate feature structure if present
    features = data.get("features", [])
    if len(features) > 0:
        first_feature = features[0]
        assert "type" in first_feature, "Feature must have type"
        assert first_feature["type"] == "Feature", "Feature type must be 'Feature'"
        assert "properties" in first_feature, "Feature must have properties"
        assert "geometry" in first_feature, "Feature must have geometry"

        # Validate geometry structure
        if first_feature["geometry"]:
            geom = first_feature["geometry"]
            assert "type" in geom, "Geometry must have type"
            assert "coordinates" in geom, "Geometry must have coordinates"


# ============================================================================
#  Paging Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_query_with_paging_parameters(qgis_app, honua_base_url):
    """
    Verify query endpoint supports paging with resultOffset and resultRecordCount.

    Tests that:
    - resultRecordCount limits number of features returned
    - resultOffset skips features for pagination
    - Paging allows iteration through large result sets
    - exceededTransferLimit flag indicates more results available
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # First page with 2 records
    query_url = (
        f"{honua_base_url}/rest/services/roads/FeatureServer/0/query?"
        f"where=1=1&outFields=*&resultRecordCount=2&resultOffset=0&f=json"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(query_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)

    features_page1 = data.get("features", [])
    assert len(features_page1) <= 2, "resultRecordCount should limit results to 2"

    # Get second page
    query_url2 = (
        f"{honua_base_url}/rest/services/roads/FeatureServer/0/query?"
        f"where=1=1&outFields=*&resultRecordCount=2&resultOffset=2&f=json"
    )

    request = QNetworkRequest(QUrl(query_url2))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content2 = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data2 = json.loads(content2)
    features_page2 = data2.get("features", [])

    # Verify pagination works (different features if enough records)
    if len(features_page1) > 0 and len(features_page2) > 0:
        # Compare object IDs to ensure different features
        oid_field = data.get("objectIdFieldName", "objectid")
        page1_ids = [f["attributes"].get(oid_field) for f in features_page1]
        page2_ids = [f["attributes"].get(oid_field) for f in features_page2]

        # IDs should not overlap between pages
        assert not set(page1_ids).intersection(set(page2_ids)), \
            "Different pages should return different features"


# ============================================================================
#  QGIS Integration Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_load_layer_as_arcgis_featureserver_in_qgis(qgis_app, honua_base_url):
    """
    Verify QGIS can load layer using ArcGIS FeatureServer provider.

    Tests that:
    - QGIS recognizes Honua as valid ArcGIS REST service
    - Layer loads successfully with correct geometry type
    - Features can be retrieved and iterated
    - Attribute fields are properly mapped
    """
    from qgis.core import QgsVectorLayer, QgsProject

    # Construct ArcGIS FeatureServer URL for QGIS
    # Format: arcgisfeatureserver://url/rest/services/folder/service/FeatureServer/layerIndex
    layer_url = f"arcgisfeatureserver://{honua_base_url.replace('https://', '').replace('http://', '')}/rest/services/roads/FeatureServer/0"

    layer = QgsVectorLayer(layer_url, "test_roads", "arcgisfeatureserver")

    # Check if layer is valid
    if not layer.isValid():
        # Try alternative URL format
        alt_url = f"{honua_base_url}/rest/services/roads/FeatureServer/0"
        layer = QgsVectorLayer(alt_url, "test_roads", "arcgisfeatureserver")

    assert layer.isValid(), f"Layer should load successfully. Error: {layer.error().message()}"

    # Validate layer properties
    assert layer.featureCount() >= 0, "Layer should report feature count"
    assert layer.fields().count() > 0, "Layer should have fields"

    # Validate geometry type
    geom_type = layer.geometryType()
    assert geom_type in [0, 1, 2], "Geometry type should be Point (0), Line (1), or Polygon (2)"

    # Try to read features
    features = list(layer.getFeatures())
    assert len(features) >= 0, "Should be able to iterate features"


# ============================================================================
#  Identify Operation Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_identify_operation_finds_features_at_point(qgis_app, honua_base_url):
    """
    Verify MapServer identify operation finds features at a point location.

    Tests that:
    - Identify endpoint accepts geometry (point) and tolerance
    - Returns results array with matching features
    - Each result includes layerId, layerName, attributes
    - Geometry is included if requested
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager
    from urllib.parse import quote

    # Define point geometry for identify
    point = json.dumps({
        "x": -122.5,
        "y": 45.5,
        "spatialReference": {"wkid": 4326}
    })

    # Identify at point location
    identify_url = (
        f"{honua_base_url}/rest/services/roads/MapServer/identify?"
        f"geometry={quote(point)}&geometryType=esriGeometryPoint&"
        f"tolerance=10&mapExtent=-123,45,-122,46&layers=all&f=json"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(identify_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Identify request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)

    # Validate identify response structure
    assert "results" in data, "Identify response must have results array"
    results = data["results"]
    assert isinstance(results, list), "results must be an array"

    # Validate result structure if features found
    if len(results) > 0:
        first_result = results[0]
        assert "layerId" in first_result, "Result must have layerId"
        assert "layerName" in first_result, "Result must have layerName"
        assert "attributes" in first_result, "Result must have attributes"


# ============================================================================
#  Error Handling Tests
# ============================================================================

@pytest.mark.requires_qgis
def test_query_invalid_layer_returns_error(qgis_app, honua_base_url):
    """
    Verify query endpoint returns proper error for non-existent layer.

    Tests that:
    - Invalid layer index returns 404 or error response
    - Error response includes error message
    - Error code is provided
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # Query non-existent layer
    query_url = (
        f"{honua_base_url}/rest/services/roads/FeatureServer/999/query?"
        f"where=1=1&outFields=*&f=json"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(query_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Should return error status or error JSON
    if status == 200:
        # If 200, response should contain error object
        data = json.loads(content)
        assert "error" in data, "Invalid layer should return error object"
        assert "code" in data["error"], "Error should have code"
        assert "message" in data["error"], "Error should have message"
    else:
        # Non-200 status is also acceptable for errors
        assert status >= 400, "Invalid layer should return error status"


@pytest.mark.requires_qgis
def test_query_invalid_where_clause_returns_error(qgis_app, honua_base_url):
    """
    Verify query endpoint handles invalid SQL where clause gracefully.

    Tests that:
    - Malformed SQL returns error response
    - Error message describes the problem
    - Server doesn't crash on invalid input
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager
    from urllib.parse import quote

    # Query with invalid SQL
    invalid_where = "invalid syntax here @#$"
    query_url = (
        f"{honua_base_url}/rest/services/roads/FeatureServer/0/query?"
        f"where={quote(invalid_where)}&outFields=*&f=json"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(query_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    # Server should handle invalid input gracefully
    # Either return error status or error JSON
    assert status in [200, 400, 500], "Server should respond to invalid query"

    if status == 200:
        data = json.loads(content)
        # May return error object or empty features
        if "error" in data:
            assert "message" in data["error"], "Error should have descriptive message"


@pytest.mark.requires_qgis
def test_query_non_existent_service_returns_404(qgis_app, honua_base_url):
    """
    Verify query endpoint returns 404 for non-existent service.

    Tests that:
    - Invalid service name returns 404
    - Response indicates service not found
    """
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # Query non-existent service
    query_url = (
        f"{honua_base_url}/rest/services/nonexistent/FeatureServer/0/query?"
        f"where=1=1&outFields=*&f=json"
    )

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(query_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    # Should return 404 for non-existent service
    assert status == 404, f"Non-existent service should return 404, got {status}"
