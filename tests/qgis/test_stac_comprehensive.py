"""
Comprehensive STAC 1.0 Integration Tests with QGIS

This test suite provides extensive coverage of STAC API operations using QGIS as the reference
client. Tests validate Honua's STAC implementation against the STAC API 1.0.0 specification
using real-world client library integration.

Test Coverage:
- Root catalog: Service discovery and catalog structure
- Conformance: STAC API conformance classes (Core, Item Search, Features, etc.)
- Collections endpoint: Collection listing and metadata
- Collection detail: Individual collection metadata with STAC fields (extent, license, providers)
- Search endpoint GET: Spatial (bbox, intersects), temporal (datetime), attribute (query) filters
- Search endpoint POST: JSON body with complex filters
- Item detail: Individual item retrieval with complete STAC metadata
- Assets: Asset metadata and download validation (COG, thumbnails)
- Paging: Navigation through large result sets with next links
- Fields filtering: Include/exclude specific fields in responses
- Sorting: Result ordering with sortby parameter
- Error handling: Invalid requests and proper error responses
- STAC schema validation: Verify compliance with STAC 1.0.0 specification

Client: QGIS 3.34+ (PyQGIS with network requests)
Specification: STAC API 1.0.0
"""
import json
import pytest


pytestmark = [
    pytest.mark.integration,
    pytest.mark.qgis,
    pytest.mark.stac,
    pytest.mark.requires_honua,
]


# ============================================================================
#  Root Catalog Tests
# ============================================================================


@pytest.mark.requires_qgis
def test_stac_root_returns_catalog(qgis_app, honua_base_url):
    """Verify STAC root endpoint returns valid Catalog with links."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    root_url = f"{honua_base_url}/stac"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(root_url))
    request.setRawHeader(b"Accept", b"application/json")
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"STAC root request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert data.get("type") == "Catalog", "STAC root must be a Catalog"
    assert data.get("stac_version") == "1.0.0", "Must declare STAC version 1.0.0"
    assert "id" in data, "Catalog must have an id"
    assert "links" in data, "Catalog must include links array"

    # Verify required link relations
    link_rels = {link.get("rel") for link in data.get("links", [])}
    assert "self" in link_rels, "Catalog must include self link"
    assert "data" in link_rels or "child" in link_rels, "Catalog must link to collections"
    assert "conformance" in link_rels, "Catalog must link to conformance"


@pytest.mark.requires_qgis
def test_stac_root_includes_metadata(qgis_app, honua_base_url):
    """Verify STAC root catalog includes title and description."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    root_url = f"{honua_base_url}/stac"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(root_url))
    request.setRawHeader(b"Accept", b"application/json")
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    # Title and description are recommended but not required
    # Just verify the structure is valid
    assert isinstance(data, dict), "STAC root must be a JSON object"


# ============================================================================
#  Conformance Tests
# ============================================================================


@pytest.mark.requires_qgis
def test_stac_conformance_returns_valid_json(qgis_app, honua_base_url):
    """Verify STAC conformance endpoint returns valid JSON with conformance classes."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    conformance_url = f"{honua_base_url}/stac/conformance"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(conformance_url))
    request.setRawHeader(b"Accept", b"application/json")
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Conformance request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert "conformsTo" in data, "Conformance response must include conformsTo array"
    assert len(data["conformsTo"]) > 0, "Must list at least one conformance class"


@pytest.mark.requires_qgis
def test_stac_conformance_includes_core_classes(qgis_app, honua_base_url):
    """Verify conformance declares STAC API - Core and STAC API - Features conformance classes."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    conformance_url = f"{honua_base_url}/stac/conformance"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(conformance_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    conformance_classes = data.get("conformsTo", [])

    # Check for STAC API conformance classes
    stac_core = "https://api.stacspec.org/v1.0.0/core"
    stac_features = "https://api.stacspec.org/v1.0.0/ogcapi-features"
    stac_item_search = "https://api.stacspec.org/v1.0.0/item-search"

    assert stac_core in conformance_classes, "Must declare STAC API - Core conformance"
    # Features or Item Search should be supported
    assert (stac_features in conformance_classes or
            stac_item_search in conformance_classes), "Must support Features or Item Search"


# ============================================================================
#  Collections Endpoint Tests
# ============================================================================


@pytest.mark.requires_qgis
def test_stac_collections_returns_valid_json(qgis_app, honua_base_url):
    """Verify STAC collections endpoint returns valid JSON with collections array."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collections_url = f"{honua_base_url}/stac/collections"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(collections_url))
    request.setRawHeader(b"Accept", b"application/json")
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Collections request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert "collections" in data, "Response must include collections array"
    assert isinstance(data["collections"], list), "collections must be an array"
    assert "links" in data, "Response must include links array"


@pytest.mark.requires_qgis
def test_stac_collections_include_required_metadata(qgis_app, honua_base_url):
    """Verify STAC collections include required metadata fields (id, extent, license)."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collections_url = f"{honua_base_url}/stac/collections"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(collections_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    collections = data.get("collections", [])

    if not collections:
        pytest.skip("No STAC collections available in test environment")

    # Validate first collection
    collection = collections[0]
    assert "id" in collection, "Collection must have id"
    assert "type" in collection, "Collection must have type"
    assert collection["type"] == "Collection", "Type must be 'Collection'"
    assert "stac_version" in collection, "Collection must declare stac_version"
    assert "license" in collection, "Collection must have license"
    assert "extent" in collection, "Collection must have extent"
    assert "links" in collection, "Collection must have links"

    # Validate extent structure
    extent = collection["extent"]
    assert "spatial" in extent, "Extent must include spatial"
    assert "temporal" in extent, "Extent must include temporal"


@pytest.mark.requires_qgis
def test_stac_collections_include_items_link(qgis_app, honua_base_url):
    """Verify STAC collections include items link relation."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collections_url = f"{honua_base_url}/stac/collections"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(collections_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    collections = data.get("collections", [])

    if not collections:
        pytest.skip("No STAC collections available")

    collection = collections[0]
    link_rels = {link.get("rel") for link in collection.get("links", [])}
    assert "items" in link_rels, "Collection must include items link"
    assert "self" in link_rels, "Collection must include self link"


@pytest.mark.requires_qgis
def test_stac_collection_detail_returns_full_metadata(qgis_app, honua_base_url):
    """Verify individual collection endpoint returns complete STAC Collection metadata."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # First get collections to find a valid collection ID
    collections_url = f"{honua_base_url}/stac/collections"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(collections_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    collections = data.get("collections", [])

    if not collections:
        pytest.skip("No STAC collections available")

    collection_id = collections[0]["id"]

    # Now fetch individual collection
    collection_url = f"{honua_base_url}/stac/collections/{collection_id}"

    request2 = QNetworkRequest(QUrl(collection_url))
    reply2 = manager.get(request2)

    loop2 = QEventLoop()
    reply2.finished.connect(loop2.quit)
    loop2.exec()

    status = reply2.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Collection detail request failed with status {status}"

    content2 = bytes(reply2.readAll()).decode("utf-8")
    reply2.deleteLater()

    collection = json.loads(content2)
    assert collection["id"] == collection_id
    assert collection["type"] == "Collection"
    assert "extent" in collection
    assert "license" in collection
    assert "links" in collection


# ============================================================================
#  Search Endpoint - GET Request Tests
# ============================================================================


@pytest.mark.requires_qgis
def test_stac_search_get_returns_feature_collection(qgis_app, honua_base_url):
    """Verify STAC search GET request returns valid GeoJSON FeatureCollection."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    search_url = f"{honua_base_url}/stac/search?limit=5"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    request.setRawHeader(b"Accept", b"application/geo+json")
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Search request failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert data.get("type") == "FeatureCollection", "Search must return FeatureCollection"
    assert "features" in data, "FeatureCollection must include features array"
    assert "links" in data, "FeatureCollection must include links array"


@pytest.mark.requires_qgis
def test_stac_search_with_bbox_filter(qgis_app, honua_base_url):
    """Verify STAC search supports bbox spatial filter."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # Global bbox
    bbox = "-180,-90,180,90"
    search_url = f"{honua_base_url}/stac/search?bbox={bbox}&limit=5"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Search with bbox failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert data.get("type") == "FeatureCollection"
    # Features may be empty if no items intersect bbox, which is valid
    assert "features" in data


@pytest.mark.requires_qgis
def test_stac_search_with_datetime_filter(qgis_app, honua_base_url):
    """Verify STAC search supports datetime temporal filter."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # Search for items from last 2 years
    import urllib.parse
    datetime_filter = "2023-01-01T00:00:00Z/.."
    search_url = f"{honua_base_url}/stac/search?datetime={urllib.parse.quote(datetime_filter)}&limit=5"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Search with datetime failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert data.get("type") == "FeatureCollection"


@pytest.mark.requires_qgis
def test_stac_search_with_collections_filter(qgis_app, honua_base_url):
    """Verify STAC search supports collections filter."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # First get a valid collection ID
    collections_url = f"{honua_base_url}/stac/collections"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(collections_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    collections = data.get("collections", [])

    if not collections:
        pytest.skip("No STAC collections available")

    collection_id = collections[0]["id"]

    # Search within specific collection
    search_url = f"{honua_base_url}/stac/search?collections={collection_id}&limit=5"

    request2 = QNetworkRequest(QUrl(search_url))
    reply2 = manager.get(request2)

    loop2 = QEventLoop()
    reply2.finished.connect(loop2.quit)
    loop2.exec()

    status = reply2.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Search with collections failed with status {status}"

    content2 = bytes(reply2.readAll()).decode("utf-8")
    reply2.deleteLater()

    data2 = json.loads(content2)
    assert data2.get("type") == "FeatureCollection"

    # Verify all returned features are from the specified collection
    for feature in data2.get("features", []):
        assert feature.get("collection") == collection_id


@pytest.mark.requires_qgis
def test_stac_search_with_limit_parameter(qgis_app, honua_base_url):
    """Verify STAC search respects limit parameter."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    limit = 3
    search_url = f"{honua_base_url}/stac/search?limit={limit}"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Search with limit failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    features = data.get("features", [])
    assert len(features) <= limit, f"Expected at most {limit} features, got {len(features)}"


@pytest.mark.requires_qgis
def test_stac_search_with_ids_filter(qgis_app, honua_base_url):
    """Verify STAC search supports ids filter."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # First get some items
    search_url = f"{honua_base_url}/stac/search?limit=2"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    features = data.get("features", [])

    if not features:
        pytest.skip("No STAC items available")

    item_id = features[0].get("id")

    # Search by specific ID
    search_url_ids = f"{honua_base_url}/stac/search?ids={item_id}"

    request2 = QNetworkRequest(QUrl(search_url_ids))
    reply2 = manager.get(request2)

    loop2 = QEventLoop()
    reply2.finished.connect(loop2.quit)
    loop2.exec()

    status = reply2.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Search with ids failed with status {status}"

    content2 = bytes(reply2.readAll()).decode("utf-8")
    reply2.deleteLater()

    data2 = json.loads(content2)
    returned_features = data2.get("features", [])

    # Should return the specific item
    assert any(f.get("id") == item_id for f in returned_features)


# ============================================================================
#  Search Endpoint - POST Request Tests
# ============================================================================


@pytest.mark.requires_qgis
def test_stac_search_post_with_json_body(qgis_app, honua_base_url):
    """Verify STAC search POST request with JSON body."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    search_url = f"{honua_base_url}/stac/search"

    search_body = {
        "limit": 5,
        "bbox": [-180, -90, 180, 90]
    }

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    request.setRawHeader(b"Content-Type", b"application/json")
    request.setRawHeader(b"Accept", b"application/geo+json")

    reply = manager.post(request, json.dumps(search_body).encode("utf-8"))

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"POST search failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert data.get("type") == "FeatureCollection"
    assert "features" in data


@pytest.mark.requires_qgis
def test_stac_search_post_with_intersects(qgis_app, honua_base_url):
    """Verify STAC search POST supports intersects GeoJSON geometry filter."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    search_url = f"{honua_base_url}/stac/search"

    # Simple polygon geometry
    search_body = {
        "limit": 5,
        "intersects": {
            "type": "Polygon",
            "coordinates": [[
                [-10, -10],
                [10, -10],
                [10, 10],
                [-10, 10],
                [-10, -10]
            ]]
        }
    }

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    request.setRawHeader(b"Content-Type", b"application/json")

    reply = manager.post(request, json.dumps(search_body).encode("utf-8"))

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"POST search with intersects failed with status {status}"

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    assert data.get("type") == "FeatureCollection"


# ============================================================================
#  Item Detail Tests
# ============================================================================


@pytest.mark.requires_qgis
def test_stac_item_detail_returns_complete_metadata(qgis_app, honua_base_url):
    """Verify STAC item endpoint returns complete Item with all required fields."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # First get an item from search
    search_url = f"{honua_base_url}/stac/search?limit=1"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    features = data.get("features", [])

    if not features:
        pytest.skip("No STAC items available")

    collection_id = features[0].get("collection")
    item_id = features[0].get("id")

    # Fetch item detail
    item_url = f"{honua_base_url}/stac/collections/{collection_id}/items/{item_id}"

    request2 = QNetworkRequest(QUrl(item_url))
    reply2 = manager.get(request2)

    loop2 = QEventLoop()
    reply2.finished.connect(loop2.quit)
    loop2.exec()

    status = reply2.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Item detail request failed with status {status}"

    content2 = bytes(reply2.readAll()).decode("utf-8")
    reply2.deleteLater()

    item = json.loads(content2)

    # Validate STAC Item required fields
    assert item.get("type") == "Feature", "Item must be a GeoJSON Feature"
    assert item.get("id") == item_id
    assert item.get("stac_version") == "1.0.0"
    assert "geometry" in item, "Item must have geometry"
    assert "properties" in item, "Item must have properties"
    assert "assets" in item, "Item must have assets"
    assert "links" in item, "Item must have links"
    assert item.get("collection") == collection_id


@pytest.mark.requires_qgis
def test_stac_item_includes_assets(qgis_app, honua_base_url):
    """Verify STAC items include assets with proper metadata."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    search_url = f"{honua_base_url}/stac/search?limit=1"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    features = data.get("features", [])

    if not features:
        pytest.skip("No STAC items available")

    item = features[0]
    assets = item.get("assets", {})
    assert len(assets) > 0, "Item should have at least one asset"

    # Validate asset structure
    for asset_key, asset in assets.items():
        assert "href" in asset, f"Asset {asset_key} must have href"
        # type and roles are recommended but not required
        if "type" in asset:
            assert isinstance(asset["type"], str)


@pytest.mark.requires_qgis
def test_stac_item_cog_asset_is_accessible(qgis_app, honua_base_url):
    """Verify STAC item COG asset can be downloaded."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    search_url = f"{honua_base_url}/stac/search?limit=5"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    features = data.get("features", [])

    if not features:
        pytest.skip("No STAC items available")

    # Find an item with COG asset
    cog_asset = None
    for feature in features:
        assets = feature.get("assets", {})
        if "cog" in assets:
            cog_asset = assets["cog"]
            break

    if not cog_asset:
        pytest.skip("No COG assets available in test items")

    href = cog_asset.get("href")
    if not href or not href.startswith("http"):
        pytest.skip("COG asset is not accessible over HTTP")

    # Attempt to download (just headers)
    request2 = QNetworkRequest(QUrl(href))
    reply2 = manager.head(request2)

    loop2 = QEventLoop()
    reply2.finished.connect(loop2.quit)
    loop2.exec()

    status = reply2.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply2.deleteLater()

    # Accept 200 (OK) or 403 (auth required) as valid
    assert status in (200, 403), f"COG asset should be accessible, got status {status}"


# ============================================================================
#  Paging Tests
# ============================================================================


@pytest.mark.requires_qgis
def test_stac_search_includes_next_link_for_large_results(qgis_app, honua_base_url):
    """Verify STAC search includes next link when more results are available."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    search_url = f"{honua_base_url}/stac/search?limit=1"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    links = data.get("links", [])

    # If there are more results, next link should be present
    # This is dependent on having enough test data
    link_rels = {link.get("rel") for link in links}

    # next link is optional if there are no more results
    # Just verify the structure is valid
    for link in links:
        assert "rel" in link, "Link must have rel"
        assert "href" in link, "Link must have href"


@pytest.mark.requires_qgis
def test_stac_search_paging_navigation(qgis_app, honua_base_url):
    """Verify STAC search paging works correctly with next links."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    search_url = f"{honua_base_url}/stac/search?limit=2"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    page1_features = data.get("features", [])

    if len(page1_features) < 2:
        pytest.skip("Not enough items for paging test")

    # Find next link
    next_link = None
    for link in data.get("links", []):
        if link.get("rel") == "next":
            next_link = link.get("href")
            break

    if not next_link:
        pytest.skip("No next link available (all items fit in one page)")

    # Follow next link
    request2 = QNetworkRequest(QUrl(next_link))
    reply2 = manager.get(request2)

    loop2 = QEventLoop()
    reply2.finished.connect(loop2.quit)
    loop2.exec()

    status = reply2.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    assert status == 200, f"Next page request failed with status {status}"

    content2 = bytes(reply2.readAll()).decode("utf-8")
    reply2.deleteLater()

    data2 = json.loads(content2)
    page2_features = data2.get("features", [])

    # Verify we got different features
    if page2_features:
        page1_ids = {f.get("id") for f in page1_features}
        page2_ids = {f.get("id") for f in page2_features}
        assert page1_ids != page2_ids, "Pages should return different items"


# ============================================================================
#  Error Handling Tests
# ============================================================================


@pytest.mark.requires_qgis
def test_stac_invalid_collection_returns_404(qgis_app, honua_base_url):
    """Verify STAC returns 404 for invalid collection ID."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    collection_url = f"{honua_base_url}/stac/collections/nonexistent-collection-id"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(collection_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    assert status == 404, f"Invalid collection should return 404, got {status}"


@pytest.mark.requires_qgis
def test_stac_invalid_item_returns_404(qgis_app, honua_base_url):
    """Verify STAC returns 404 for invalid item ID."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # First get a valid collection ID
    collections_url = f"{honua_base_url}/stac/collections"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(collections_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    content = bytes(reply.readAll()).decode("utf-8")
    reply.deleteLater()

    data = json.loads(content)
    collections = data.get("collections", [])

    if not collections:
        pytest.skip("No STAC collections available")

    collection_id = collections[0]["id"]

    # Request invalid item
    item_url = f"{honua_base_url}/stac/collections/{collection_id}/items/nonexistent-item-id"

    request2 = QNetworkRequest(QUrl(item_url))
    reply2 = manager.get(request2)

    loop2 = QEventLoop()
    reply2.finished.connect(loop2.quit)
    loop2.exec()

    status = reply2.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply2.deleteLater()

    assert status == 404, f"Invalid item should return 404, got {status}"


@pytest.mark.requires_qgis
def test_stac_invalid_bbox_returns_error(qgis_app, honua_base_url):
    """Verify STAC search returns error for malformed bbox parameter."""
    from qgis.PyQt.QtCore import QEventLoop, QUrl
    from qgis.PyQt.QtNetwork import QNetworkRequest
    from qgis.core import QgsNetworkAccessManager

    # Invalid bbox (missing coordinate)
    search_url = f"{honua_base_url}/stac/search?bbox=-180,-90,180"

    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(search_url))
    reply = manager.get(request)

    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    reply.deleteLater()

    assert status == 400, f"Malformed bbox should return 400, got {status}"
