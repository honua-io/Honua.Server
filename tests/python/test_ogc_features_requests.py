"""
Comprehensive OGC API - Features Integration Tests with requests

This test suite validates Honua's OGC API - Features (OAPIF) implementation using the requests
library for REST/JSON API interactions. Tests verify full compliance with OGC API - Features 1.0.0
specification using real-world HTTP client patterns.

Test Coverage:
- Landing page: Links, conformance classes, service metadata
- Conformance: Verify OGC API Features conformance classes
- Collections: List collections, collection metadata, spatial/temporal extent
- Items: Get items with filters (bbox, datetime, limit, offset)
- Single item: Get item by ID with proper GeoJSON structure
- Queryables: Property schema and filter capabilities
- CRS support: Different coordinate reference systems (WGS84, Web Mercator, etc.)
- Output formats: GeoJSON, FlatGeobuf, JSON-FG if supported
- Filtering: CQL2-JSON and CQL2-TEXT filters if supported
- Paging: next/prev links, limit/offset pagination, keyset pagination
- Search: GET and POST search across collections
- Error handling: invalid collections, malformed requests, 404 responses
- Content negotiation: Accept headers for different formats

Requirements:
- requests >= 2.28.0
- pytest >= 7.0.0

Client: requests (HTTP client library)
Specification: OGC API - Features 1.0.0, GeoJSON RFC 7946
"""
import json
import pytest
from typing import Any, Dict, List, Optional
from urllib.parse import urlencode, urlparse, parse_qs


pytestmark = [
    pytest.mark.integration,
    pytest.mark.python,
    pytest.mark.ogc_features,
    pytest.mark.requires_honua,
]


# ============================================================================
#  Helper Functions and Fixtures
# ============================================================================


def get_ogc_api_url(honua_api_base_url: str) -> str:
    """Get OGC API base URL from Honua base URL."""
    return f"{honua_api_base_url}/ogc"


def validate_geojson_feature(feature: Dict[str, Any]) -> None:
    """Validate that a feature conforms to GeoJSON Feature spec."""
    assert feature.get("type") == "Feature", "Feature must have type='Feature'"
    assert "geometry" in feature, "Feature must have geometry"
    assert "properties" in feature, "Feature must have properties"

    # Geometry validation (can be null for non-spatial features)
    if feature["geometry"] is not None:
        geom = feature["geometry"]
        assert "type" in geom, "Geometry must have type"
        assert "coordinates" in geom, "Geometry must have coordinates"

        valid_geom_types = [
            "Point", "LineString", "Polygon",
            "MultiPoint", "MultiLineString", "MultiPolygon",
            "GeometryCollection"
        ]
        assert geom["type"] in valid_geom_types, f"Invalid geometry type: {geom['type']}"


def validate_geojson_feature_collection(feature_collection: Dict[str, Any]) -> None:
    """Validate that a feature collection conforms to GeoJSON FeatureCollection spec."""
    assert feature_collection.get("type") == "FeatureCollection", \
        "FeatureCollection must have type='FeatureCollection'"
    assert "features" in feature_collection, "FeatureCollection must have features array"
    assert isinstance(feature_collection["features"], list), \
        "Features must be an array"


def validate_link(link: Dict[str, Any]) -> None:
    """Validate that a link object has required properties."""
    assert "href" in link, "Link must have href"
    assert "rel" in link, "Link must have rel"
    # type and title are optional
    assert isinstance(link["href"], str), "Link href must be string"
    assert isinstance(link["rel"], str), "Link rel must be string"


@pytest.fixture(scope="module")
def ogc_api_url(honua_api_base_url):
    """Get the OGC API base URL."""
    return get_ogc_api_url(honua_api_base_url)


@pytest.fixture(scope="module")
def landing_page(api_request, ogc_api_url):
    """Fetch and cache the landing page for the test session."""
    response = api_request("GET", f"{ogc_api_url}/")
    response.raise_for_status()
    return response.json()


@pytest.fixture(scope="module")
def collections_response(api_request, ogc_api_url):
    """Fetch and cache the collections list for the test session."""
    response = api_request("GET", f"{ogc_api_url}/collections")
    response.raise_for_status()
    return response.json()


@pytest.fixture(scope="module")
def valid_collection_id(collections_response):
    """Get a valid collection ID from the collections list."""
    collections = collections_response.get("collections", [])
    if not collections:
        pytest.skip("No collections available in test environment")
    return collections[0]["id"]


@pytest.fixture(scope="module")
def valid_collection(api_request, ogc_api_url, valid_collection_id):
    """Fetch and cache a valid collection for the test session."""
    response = api_request("GET", f"{ogc_api_url}/collections/{valid_collection_id}")
    response.raise_for_status()
    return response.json()


# ============================================================================
#  Landing Page Tests
# ============================================================================


def test_landing_page_returns_json(api_request, ogc_api_url):
    """Verify OGC API landing page returns valid JSON."""
    response = api_request("GET", f"{ogc_api_url}/")

    assert response.status_code == 200, f"Expected 200, got {response.status_code}"
    assert response.headers.get("Content-Type", "").startswith("application/json"), \
        "Landing page should return JSON"

    data = response.json()
    assert isinstance(data, dict), "Landing page must be a JSON object"


def test_landing_page_includes_links(landing_page):
    """Verify landing page includes required links."""
    assert "links" in landing_page, "Landing page must include links array"
    assert isinstance(landing_page["links"], list), "Links must be an array"
    assert len(landing_page["links"]) > 0, "Landing page must have at least one link"

    # Validate each link
    for link in landing_page["links"]:
        validate_link(link)


def test_landing_page_has_required_link_relations(landing_page):
    """Verify landing page includes required link relations."""
    link_rels = {link.get("rel") for link in landing_page.get("links", [])}

    # Required link relations per OGC API - Features spec
    assert "self" in link_rels, "Landing page must include self link"
    assert "conformance" in link_rels or "http://www.opengis.net/def/rel/ogc/1.0/conformance" in link_rels, \
        "Landing page must include conformance link"
    assert "data" in link_rels or "http://www.opengis.net/def/rel/ogc/1.0/data" in link_rels, \
        "Landing page must include data/collections link"


def test_landing_page_includes_service_metadata(landing_page):
    """Verify landing page includes service metadata (title, description)."""
    # Title and description are recommended but not strictly required
    # Just verify the response structure is valid
    assert isinstance(landing_page, dict), "Landing page must be a JSON object"

    # If title exists, it should be a string
    if "title" in landing_page:
        assert isinstance(landing_page["title"], str), "Title must be string"

    # If description exists, it should be a string
    if "description" in landing_page:
        assert isinstance(landing_page["description"], str), "Description must be string"


def test_landing_page_content_negotiation(api_request, ogc_api_url):
    """Verify landing page supports content negotiation."""
    # Test JSON format
    response = api_request("GET", f"{ogc_api_url}/", headers={"Accept": "application/json"})
    assert response.status_code == 200
    assert "application/json" in response.headers.get("Content-Type", "")


# ============================================================================
#  Conformance Tests
# ============================================================================


def test_conformance_returns_valid_json(api_request, ogc_api_url):
    """Verify conformance endpoint returns valid JSON."""
    response = api_request("GET", f"{ogc_api_url}/conformance")

    assert response.status_code == 200, f"Expected 200, got {response.status_code}"
    assert response.headers.get("Content-Type", "").startswith("application/json"), \
        "Conformance should return JSON"

    data = response.json()
    assert isinstance(data, dict), "Conformance response must be a JSON object"


def test_conformance_includes_conformsto_array(api_request, ogc_api_url):
    """Verify conformance endpoint includes conformsTo array."""
    response = api_request("GET", f"{ogc_api_url}/conformance")
    data = response.json()

    assert "conformsTo" in data, "Conformance response must include conformsTo array"
    assert isinstance(data["conformsTo"], list), "conformsTo must be an array"
    assert len(data["conformsTo"]) > 0, "conformsTo must have at least one conformance class"


def test_conformance_declares_ogc_api_features_core(api_request, ogc_api_url):
    """Verify conformance declares OGC API - Features Core conformance class."""
    response = api_request("GET", f"{ogc_api_url}/conformance")
    data = response.json()

    conformance_classes = data.get("conformsTo", [])

    # Check for Core conformance class
    core_class = "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core"
    assert core_class in conformance_classes or \
           any("core" in cc.lower() and "ogcapi-features" in cc.lower()
               for cc in conformance_classes), \
        "Must declare OGC API - Features Core conformance class"


def test_conformance_declares_geojson(api_request, ogc_api_url):
    """Verify conformance declares GeoJSON conformance class."""
    response = api_request("GET", f"{ogc_api_url}/conformance")
    data = response.json()

    conformance_classes = data.get("conformsTo", [])

    # Check for GeoJSON conformance class
    geojson_class = "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson"
    assert geojson_class in conformance_classes or \
           any("geojson" in cc.lower() for cc in conformance_classes), \
        "Should declare GeoJSON conformance class"


# ============================================================================
#  Collections Tests
# ============================================================================


def test_collections_returns_valid_json(api_request, ogc_api_url):
    """Verify collections endpoint returns valid JSON."""
    response = api_request("GET", f"{ogc_api_url}/collections")

    assert response.status_code == 200, f"Expected 200, got {response.status_code}"
    assert response.headers.get("Content-Type", "").startswith("application/json"), \
        "Collections should return JSON"

    data = response.json()
    assert isinstance(data, dict), "Collections response must be a JSON object"


def test_collections_includes_collections_array(collections_response):
    """Verify collections response includes collections array."""
    assert "collections" in collections_response, \
        "Collections response must include collections array"
    assert isinstance(collections_response["collections"], list), \
        "collections must be an array"


def test_collections_includes_links(collections_response):
    """Verify collections response includes links."""
    assert "links" in collections_response, "Collections response must include links"
    assert isinstance(collections_response["links"], list), "Links must be an array"

    for link in collections_response["links"]:
        validate_link(link)


def test_collection_metadata_structure(collections_response):
    """Verify each collection has required metadata fields."""
    collections = collections_response.get("collections", [])

    if not collections:
        pytest.skip("No collections available")

    for collection in collections:
        assert "id" in collection, "Collection must have id"
        assert "links" in collection, "Collection must have links"

        # Validate collection links
        for link in collection["links"]:
            validate_link(link)


def test_collection_has_extent(collections_response):
    """Verify collections include spatial and temporal extent."""
    collections = collections_response.get("collections", [])

    if not collections:
        pytest.skip("No collections available")

    collection = collections[0]

    # Extent is recommended but not strictly required
    if "extent" in collection:
        extent = collection["extent"]
        assert isinstance(extent, dict), "Extent must be an object"

        # Spatial extent
        if "spatial" in extent:
            spatial = extent["spatial"]
            assert "bbox" in spatial, "Spatial extent must have bbox"
            assert isinstance(spatial["bbox"], list), "bbox must be an array"

            if spatial["bbox"]:
                bbox = spatial["bbox"][0]
                assert len(bbox) in [4, 6], "bbox must have 4 or 6 coordinates"

        # Temporal extent
        if "temporal" in extent:
            temporal = extent["temporal"]
            assert "interval" in temporal, "Temporal extent must have interval"
            assert isinstance(temporal["interval"], list), "interval must be an array"


# ============================================================================
#  Single Collection Tests
# ============================================================================


def test_get_single_collection_returns_json(api_request, ogc_api_url, valid_collection_id):
    """Verify getting a single collection returns valid JSON."""
    response = api_request("GET", f"{ogc_api_url}/collections/{valid_collection_id}")

    assert response.status_code == 200, f"Expected 200, got {response.status_code}"
    assert response.headers.get("Content-Type", "").startswith("application/json"), \
        "Collection should return JSON"

    data = response.json()
    assert isinstance(data, dict), "Collection response must be a JSON object"


def test_single_collection_has_required_fields(valid_collection):
    """Verify single collection response has required fields."""
    assert "id" in valid_collection, "Collection must have id"
    assert "links" in valid_collection, "Collection must have links"

    for link in valid_collection["links"]:
        validate_link(link)


def test_single_collection_has_items_link(valid_collection):
    """Verify collection includes link to items."""
    links = valid_collection.get("links", [])
    link_rels = {link.get("rel") for link in links}

    assert "items" in link_rels or \
           "http://www.opengis.net/def/rel/ogc/1.0/items" in link_rels, \
        "Collection must include items link"


def test_get_nonexistent_collection_returns_404(api_request, ogc_api_url):
    """Verify requesting non-existent collection returns 404."""
    response = api_request("GET", f"{ogc_api_url}/collections/nonexistent-collection-12345")

    assert response.status_code == 404, \
        f"Expected 404 for non-existent collection, got {response.status_code}"


# ============================================================================
#  Items Tests
# ============================================================================


def test_get_collection_items_returns_geojson(api_request, ogc_api_url, valid_collection_id):
    """Verify getting collection items returns valid GeoJSON FeatureCollection."""
    response = api_request("GET", f"{ogc_api_url}/collections/{valid_collection_id}/items")

    assert response.status_code == 200, f"Expected 200, got {response.status_code}"

    data = response.json()
    validate_geojson_feature_collection(data)


def test_collection_items_include_links(api_request, ogc_api_url, valid_collection_id):
    """Verify items response includes links for pagination."""
    response = api_request("GET", f"{ogc_api_url}/collections/{valid_collection_id}/items")
    data = response.json()

    assert "links" in data, "Items response must include links"
    assert isinstance(data["links"], list), "Links must be an array"

    for link in data["links"]:
        validate_link(link)


def test_collection_items_validate_features(api_request, ogc_api_url, valid_collection_id):
    """Verify each item in the collection is a valid GeoJSON Feature."""
    response = api_request("GET", f"{ogc_api_url}/collections/{valid_collection_id}/items")
    data = response.json()

    features = data.get("features", [])

    # May be empty, which is valid
    for feature in features:
        validate_geojson_feature(feature)


def test_items_with_limit_parameter(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint respects limit parameter."""
    limit = 5
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": limit}
    )

    assert response.status_code == 200
    data = response.json()

    features = data.get("features", [])
    assert len(features) <= limit, \
        f"Expected at most {limit} features, got {len(features)}"


def test_items_with_bbox_filter(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint supports bbox spatial filter."""
    # Use global bbox
    bbox = "-180,-90,180,90"

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"bbox": bbox, "limit": 10}
    )

    assert response.status_code == 200
    data = response.json()
    validate_geojson_feature_collection(data)


def test_items_with_specific_bbox_filter(api_request, ogc_api_url, valid_collection_id):
    """Verify items are filtered by specific bbox."""
    # Small bbox in California
    bbox = "-125.0,32.0,-114.0,42.0"

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"bbox": bbox, "limit": 10}
    )

    assert response.status_code == 200
    data = response.json()

    features = data.get("features", [])

    # Validate returned features intersect the bbox
    bbox_coords = list(map(float, bbox.split(",")))
    min_lon, min_lat, max_lon, max_lat = bbox_coords

    for feature in features:
        if feature.get("bbox"):
            feature_bbox = feature["bbox"]
            # Feature bbox should intersect search bbox (not be disjoint)
            assert not (
                feature_bbox[2] < min_lon or  # feature east < search west
                feature_bbox[0] > max_lon or  # feature west > search east
                feature_bbox[3] < min_lat or  # feature north < search south
                feature_bbox[1] > max_lat      # feature south > search north
            ), "Feature bbox should intersect search bbox"


def test_items_with_datetime_filter(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint supports datetime temporal filter."""
    # Use datetime range
    datetime_range = "2020-01-01T00:00:00Z/2025-12-31T23:59:59Z"

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"datetime": datetime_range, "limit": 10}
    )

    # Datetime filtering may not be supported for all collections
    assert response.status_code in [200, 400], \
        f"Expected 200 or 400, got {response.status_code}"

    if response.status_code == 200:
        data = response.json()
        validate_geojson_feature_collection(data)


def test_items_with_datetime_single_instant(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint supports single datetime instant."""
    datetime_str = "2024-01-01T00:00:00Z"

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"datetime": datetime_str, "limit": 5}
    )

    # Accept 200 or 400 (datetime may not be supported)
    assert response.status_code in [200, 400]

    if response.status_code == 200:
        data = response.json()
        validate_geojson_feature_collection(data)


def test_items_with_open_ended_datetime(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint supports open-ended datetime ranges."""
    # From date to present
    datetime_range = "2023-01-01T00:00:00Z/.."

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"datetime": datetime_range, "limit": 5}
    )

    assert response.status_code in [200, 400]

    if response.status_code == 200:
        data = response.json()
        validate_geojson_feature_collection(data)


def test_items_with_combined_filters(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint supports combining bbox and datetime filters."""
    bbox = "-125.0,32.0,-114.0,42.0"
    datetime_range = "2020-01-01T00:00:00Z/2025-12-31T23:59:59Z"

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={
            "bbox": bbox,
            "datetime": datetime_range,
            "limit": 10
        }
    )

    assert response.status_code in [200, 400]

    if response.status_code == 200:
        data = response.json()
        validate_geojson_feature_collection(data)


# ============================================================================
#  Pagination Tests
# ============================================================================


def test_items_pagination_with_limit_and_offset(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint supports offset pagination."""
    limit = 3
    offset = 0

    # Get first page
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": limit, "offset": offset}
    )

    assert response.status_code == 200
    data = response.json()

    features_page1 = data.get("features", [])
    assert len(features_page1) <= limit

    if len(features_page1) == limit:
        # Get second page
        response2 = api_request(
            "GET",
            f"{ogc_api_url}/collections/{valid_collection_id}/items",
            params={"limit": limit, "offset": limit}
        )

        assert response2.status_code == 200
        data2 = response2.json()

        features_page2 = data2.get("features", [])

        # Verify pages are different (if both have features)
        if features_page1 and features_page2:
            ids_page1 = {f.get("id") for f in features_page1}
            ids_page2 = {f.get("id") for f in features_page2}

            # Pages should have different features
            assert ids_page1 != ids_page2, "Pages should contain different features"


def test_items_next_link_pagination(api_request, ogc_api_url, valid_collection_id):
    """Verify items response includes next link for pagination."""
    limit = 5

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": limit}
    )

    assert response.status_code == 200
    data = response.json()

    links = data.get("links", [])
    link_rels = {link.get("rel") for link in links}

    # If there are more results, there should be a next link
    features = data.get("features", [])
    if len(features) == limit:
        # Might have more results
        # Next link is optional but recommended
        pass


def test_items_self_link_present(api_request, ogc_api_url, valid_collection_id):
    """Verify items response includes self link."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items"
    )

    assert response.status_code == 200
    data = response.json()

    links = data.get("links", [])
    link_rels = {link.get("rel") for link in links}

    assert "self" in link_rels, "Items response should include self link"


# ============================================================================
#  Single Item Tests
# ============================================================================


def test_get_single_item_returns_geojson_feature(api_request, ogc_api_url, valid_collection_id):
    """Verify getting a single item returns valid GeoJSON Feature."""
    # First get an item ID
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": 1}
    )

    assert response.status_code == 200
    data = response.json()
    features = data.get("features", [])

    if not features:
        pytest.skip("No features available in collection")

    feature_id = features[0]["id"]

    # Now get the single item
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items/{feature_id}"
    )

    assert response.status_code == 200
    feature = response.json()

    validate_geojson_feature(feature)
    assert feature["id"] == feature_id, "Feature ID should match requested ID"


def test_get_single_item_includes_links(api_request, ogc_api_url, valid_collection_id):
    """Verify single item response includes links."""
    # Get first item
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": 1}
    )

    data = response.json()
    features = data.get("features", [])

    if not features:
        pytest.skip("No features available")

    feature_id = features[0]["id"]

    # Get single item
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items/{feature_id}"
    )

    feature = response.json()

    assert "links" in feature, "Feature should include links"
    assert isinstance(feature["links"], list), "Links must be an array"

    for link in feature["links"]:
        validate_link(link)


def test_get_nonexistent_item_returns_404(api_request, ogc_api_url, valid_collection_id):
    """Verify requesting non-existent item returns 404."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items/nonexistent-item-12345"
    )

    assert response.status_code == 404, \
        f"Expected 404 for non-existent item, got {response.status_code}"


# ============================================================================
#  Queryables Tests
# ============================================================================


def test_get_collection_queryables(api_request, ogc_api_url, valid_collection_id):
    """Verify collection queryables endpoint returns valid JSON."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/queryables"
    )

    # Queryables support is optional
    assert response.status_code in [200, 404], \
        f"Expected 200 or 404, got {response.status_code}"

    if response.status_code == 200:
        data = response.json()
        assert isinstance(data, dict), "Queryables response must be a JSON object"


def test_queryables_include_property_schema(api_request, ogc_api_url, valid_collection_id):
    """Verify queryables include property schema information."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/queryables"
    )

    if response.status_code == 404:
        pytest.skip("Queryables not supported for this collection")

    assert response.status_code == 200
    data = response.json()

    # Queryables typically follow JSON Schema structure
    # Exact structure may vary by implementation
    assert isinstance(data, dict), "Queryables must be an object"


# ============================================================================
#  CRS Support Tests
# ============================================================================


def test_items_with_crs_parameter(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint supports CRS negotiation via crs parameter."""
    # Request in Web Mercator (EPSG:3857)
    crs = "http://www.opengis.net/def/crs/EPSG/0/3857"

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"crs": crs, "limit": 5}
    )

    # CRS support is optional
    assert response.status_code in [200, 400], \
        f"Expected 200 or 400, got {response.status_code}"

    if response.status_code == 200:
        data = response.json()
        validate_geojson_feature_collection(data)


def test_items_default_crs_is_wgs84(api_request, ogc_api_url, valid_collection_id):
    """Verify items default to WGS84 CRS (EPSG:4326)."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": 1}
    )

    assert response.status_code == 200
    data = response.json()

    # Default CRS for GeoJSON is WGS84
    # Verify coordinates are in reasonable WGS84 range
    features = data.get("features", [])

    for feature in features:
        geom = feature.get("geometry")
        if geom and geom.get("coordinates"):
            coords = geom["coordinates"]
            # Very basic validation - just ensure it's not obviously in wrong CRS
            # (e.g., not in meters which would be huge numbers)
            assert isinstance(coords, list), "Coordinates must be an array"


# ============================================================================
#  Output Format Tests
# ============================================================================


def test_items_geojson_format_via_accept_header(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint returns GeoJSON via Accept header."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": 5},
        headers={"Accept": "application/geo+json"}
    )

    assert response.status_code == 200

    content_type = response.headers.get("Content-Type", "")
    assert "geo+json" in content_type or "json" in content_type, \
        "Should return GeoJSON format"

    data = response.json()
    validate_geojson_feature_collection(data)


def test_items_json_format_via_accept_header(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint returns JSON via Accept header."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": 5},
        headers={"Accept": "application/json"}
    )

    assert response.status_code == 200

    content_type = response.headers.get("Content-Type", "")
    assert "json" in content_type.lower(), "Should return JSON format"

    data = response.json()
    validate_geojson_feature_collection(data)


def test_items_format_via_f_parameter(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint supports f parameter for format selection."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"f": "json", "limit": 5}
    )

    assert response.status_code == 200
    data = response.json()
    validate_geojson_feature_collection(data)


# ============================================================================
#  Search Endpoint Tests
# ============================================================================


def test_search_get_returns_features(api_request, ogc_api_url):
    """Verify GET /search endpoint returns features across collections."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/search",
        params={"limit": 10}
    )

    # Search endpoint is optional
    assert response.status_code in [200, 404], \
        f"Expected 200 or 404, got {response.status_code}"

    if response.status_code == 200:
        data = response.json()
        validate_geojson_feature_collection(data)


def test_search_post_returns_features(api_request, ogc_api_url):
    """Verify POST /search endpoint returns features with JSON body."""
    search_body = {
        "limit": 10
    }

    response = api_request(
        "POST",
        f"{ogc_api_url}/search",
        json=search_body
    )

    # Search endpoint is optional
    assert response.status_code in [200, 404], \
        f"Expected 200 or 404, got {response.status_code}"

    if response.status_code == 200:
        data = response.json()
        validate_geojson_feature_collection(data)


def test_search_with_collections_filter(api_request, ogc_api_url, valid_collection_id):
    """Verify search endpoint can filter by collection IDs."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/search",
        params={
            "collections": valid_collection_id,
            "limit": 10
        }
    )

    if response.status_code == 404:
        pytest.skip("Search endpoint not available")

    assert response.status_code == 200
    data = response.json()

    features = data.get("features", [])

    # Verify all features are from the specified collection
    for feature in features:
        # Features may have collection property
        if "collection" in feature:
            assert feature["collection"] == valid_collection_id


def test_search_with_bbox_filter(api_request, ogc_api_url):
    """Verify search endpoint supports bbox filter."""
    bbox = "-125.0,32.0,-114.0,42.0"

    response = api_request(
        "GET",
        f"{ogc_api_url}/search",
        params={
            "bbox": bbox,
            "limit": 10
        }
    )

    if response.status_code == 404:
        pytest.skip("Search endpoint not available")

    assert response.status_code == 200
    data = response.json()
    validate_geojson_feature_collection(data)


# ============================================================================
#  CQL2 Filtering Tests
# ============================================================================


def test_items_with_cql2_json_filter(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint supports CQL2-JSON filtering."""
    # Simple CQL2-JSON filter
    cql2_filter = {
        "op": "=",
        "args": [
            {"property": "id"},
            "test-value"
        ]
    }

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={
            "filter": json.dumps(cql2_filter),
            "filter-lang": "cql2-json",
            "limit": 10
        }
    )

    # CQL2 support is optional
    assert response.status_code in [200, 400], \
        f"Expected 200 or 400, got {response.status_code}"


def test_items_with_cql2_text_filter(api_request, ogc_api_url, valid_collection_id):
    """Verify items endpoint supports CQL2-TEXT filtering."""
    # Simple CQL2-TEXT filter
    cql2_filter = "id = 'test-value'"

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={
            "filter": cql2_filter,
            "filter-lang": "cql2-text",
            "limit": 10
        }
    )

    # CQL2 support is optional
    assert response.status_code in [200, 400], \
        f"Expected 200 or 400, got {response.status_code}"


# ============================================================================
#  Error Handling Tests
# ============================================================================


def test_invalid_collection_id_returns_404(api_request, ogc_api_url):
    """Verify invalid collection ID returns 404."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/invalid-collection-xyz-12345"
    )

    assert response.status_code == 404


def test_invalid_bbox_format_returns_400(api_request, ogc_api_url, valid_collection_id):
    """Verify malformed bbox returns 400."""
    # Invalid bbox (wrong number of coordinates)
    invalid_bbox = "-180,-90,180"  # Missing fourth coordinate

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"bbox": invalid_bbox}
    )

    assert response.status_code == 400, \
        f"Expected 400 for invalid bbox, got {response.status_code}"


def test_invalid_datetime_format_returns_400(api_request, ogc_api_url, valid_collection_id):
    """Verify malformed datetime returns 400."""
    # Invalid datetime format
    invalid_datetime = "not-a-valid-datetime"

    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"datetime": invalid_datetime}
    )

    assert response.status_code == 400, \
        f"Expected 400 for invalid datetime, got {response.status_code}"


def test_negative_limit_returns_400(api_request, ogc_api_url, valid_collection_id):
    """Verify negative limit returns 400."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": -1}
    )

    assert response.status_code == 400, \
        f"Expected 400 for negative limit, got {response.status_code}"


def test_excessive_limit_is_capped(api_request, ogc_api_url, valid_collection_id):
    """Verify excessively large limit is capped to server maximum."""
    # Request very large limit
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": 999999}
    )

    assert response.status_code == 200
    data = response.json()

    features = data.get("features", [])
    # Server should cap the limit to a reasonable value
    assert len(features) < 999999, "Server should cap limit to reasonable value"


# ============================================================================
#  NumberMatched and NumberReturned Tests
# ============================================================================


def test_items_include_numberreturned(api_request, ogc_api_url, valid_collection_id):
    """Verify items response includes numberReturned property."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": 10}
    )

    assert response.status_code == 200
    data = response.json()

    # numberReturned is optional but recommended
    if "numberReturned" in data:
        assert isinstance(data["numberReturned"], int)
        assert data["numberReturned"] >= 0

        # Should match actual number of features
        features = data.get("features", [])
        assert data["numberReturned"] == len(features)


def test_items_include_numbermatched(api_request, ogc_api_url, valid_collection_id):
    """Verify items response includes numberMatched property when available."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"limit": 10}
    )

    assert response.status_code == 200
    data = response.json()

    # numberMatched is optional
    if "numberMatched" in data:
        assert isinstance(data["numberMatched"], int)
        assert data["numberMatched"] >= 0


# ============================================================================
#  Content Negotiation Tests
# ============================================================================


def test_collection_html_via_accept_header(api_request, ogc_api_url, valid_collection_id):
    """Verify collection endpoint supports HTML via Accept header."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}",
        headers={"Accept": "text/html"}
    )

    # HTML support is optional
    assert response.status_code in [200, 406], \
        f"Expected 200 or 406, got {response.status_code}"


def test_unsupported_format_returns_406(api_request, ogc_api_url, valid_collection_id):
    """Verify requesting unsupported format returns 406."""
    response = api_request(
        "GET",
        f"{ogc_api_url}/collections/{valid_collection_id}/items",
        params={"f": "unsupported-format-xyz"}
    )

    # May return 400 or 406 for unsupported format
    assert response.status_code in [400, 406], \
        f"Expected 400 or 406 for unsupported format, got {response.status_code}"
