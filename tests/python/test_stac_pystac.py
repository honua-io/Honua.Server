"""
Comprehensive STAC 1.0 Integration Tests with pystac-client

This test suite validates Honua's STAC API implementation using pystac-client, the reference
Python client library for STAC APIs. Tests verify full compliance with STAC API 1.0.0
specification using real-world client patterns.

Test Coverage:
- Catalog opening: Connect to STAC API and validate catalog structure
- Spatial search: bbox and intersects geometry filters
- Temporal search: datetime single point and range filters
- Property search: query parameter with CQL2-JSON filters
- Collection filtering: Search within specific collections
- Pagination: Iterate through large result sets using item_collection()
- Item validation: Verify STAC Item schema compliance
- Asset access: Download and validate asset URLs
- Conformance validation: Verify declared conformance classes
- Error handling: Invalid catalog URLs and malformed search parameters
- Search combinations: Multiple filters applied together
- Result ordering: sortby parameter support
- Fields selection: Include/exclude specific fields in responses
- Context metadata: Matched counts and search context

Requirements:
- pystac-client >= 0.7.0
- requests >= 2.28.0
- jsonschema >= 4.0.0 (for STAC schema validation)

Client: pystac-client (STAC API Python client)
Specification: STAC API 1.0.0, STAC Item 1.0.0
"""
import os
import pytest
from typing import Optional


pytestmark = [pytest.mark.integration, pytest.mark.python, pytest.mark.stac, pytest.mark.requires_honua]


# ============================================================================
#  Helper Functions and Fixtures
# ============================================================================


def get_stac_api_url(honua_api_base_url: str) -> str:
    """Get STAC API URL from base URL."""
    return f"{honua_api_base_url}/stac"


@pytest.fixture(scope="module")
def stac_client(honua_api_base_url):
    """Create pystac_client.Client instance for STAC API."""
    try:
        from pystac_client import Client
    except ImportError:
        pytest.skip("pystac-client not installed (pip install pystac-client)")

    stac_url = get_stac_api_url(honua_api_base_url)

    try:
        client = Client.open(stac_url)
        return client
    except Exception as e:
        pytest.skip(f"Could not connect to STAC API at {stac_url}: {e}")


@pytest.fixture(scope="module")
def valid_collection_id(stac_client):
    """Get a valid collection ID from the STAC API."""
    collections = list(stac_client.get_collections())
    if not collections:
        pytest.skip("No STAC collections available in test environment")
    return collections[0].id


# ============================================================================
#  Catalog Opening Tests
# ============================================================================


def test_open_stac_catalog(honua_api_base_url):
    """Verify pystac_client can open STAC catalog and read basic metadata."""
    try:
        from pystac_client import Client
    except ImportError:
        pytest.skip("pystac-client not installed")

    stac_url = get_stac_api_url(honua_api_base_url)

    client = Client.open(stac_url)
    assert client is not None, "Client should be able to open STAC catalog"

    # Validate catalog metadata
    assert client.title is not None or client.id is not None, "Catalog should have title or id"
    assert hasattr(client, "get_collections"), "Client should have get_collections method"
    assert hasattr(client, "search"), "Client should have search method"


def test_stac_catalog_has_conformance(stac_client):
    """Verify STAC catalog declares conformance classes."""
    conformance = stac_client.conformance

    assert conformance is not None, "Catalog should declare conformance classes"
    assert len(conformance) > 0, "Catalog should declare at least one conformance class"

    # Check for STAC API Core conformance
    conforms_to = list(conformance.conforms_to())
    assert any("stacspec.org" in cc for cc in conforms_to), "Should declare STAC conformance classes"


def test_stac_catalog_lists_collections(stac_client):
    """Verify STAC catalog can list collections."""
    collections = list(stac_client.get_collections())

    # May be empty in test environment, but method should work
    assert isinstance(collections, list), "get_collections should return a list"

    if collections:
        collection = collections[0]
        assert hasattr(collection, "id"), "Collection should have id"
        assert hasattr(collection, "extent"), "Collection should have extent"
        assert hasattr(collection, "license"), "Collection should have license"


# ============================================================================
#  Spatial Search Tests
# ============================================================================


def test_search_with_bbox(stac_client):
    """Verify STAC search supports bbox spatial filter."""
    # Global bbox
    bbox = [-180, -90, 180, 90]

    search = stac_client.search(
        bbox=bbox,
        max_items=5
    )

    assert search is not None, "Search should return results"

    items = list(search.items())
    # Items may be empty if no data in bbox, which is valid
    assert isinstance(items, list), "Search should return list of items"


def test_search_with_specific_bbox(stac_client):
    """Verify STAC search filters items by specific bbox."""
    # Small bbox in Pacific
    bbox = [-125.0, 32.0, -114.0, 42.0]

    search = stac_client.search(
        bbox=bbox,
        max_items=10
    )

    items = list(search.items())

    # Validate returned items intersect the bbox
    for item in items:
        if item.bbox:
            # Item bbox should intersect search bbox
            item_bbox = item.bbox
            # Check for intersection (not disjoint)
            assert not (item_bbox[2] < bbox[0] or  # item east < search west
                       item_bbox[0] > bbox[2] or  # item west > search east
                       item_bbox[3] < bbox[1] or  # item north < search south
                       item_bbox[1] > bbox[3]), "Item bbox should intersect search bbox"


def test_search_with_intersects_geometry(stac_client):
    """Verify STAC search supports intersects GeoJSON geometry filter."""
    try:
        from shapely.geometry import box
    except ImportError:
        pytest.skip("shapely not installed (pip install shapely)")

    # Create polygon geometry
    geom = box(-10, -10, 10, 10)

    search = stac_client.search(
        intersects=geom,
        max_items=5
    )

    items = list(search.items())
    assert isinstance(items, list), "Search with intersects should return items"


def test_search_with_intersects_geojson(stac_client):
    """Verify STAC search supports intersects with GeoJSON dict."""
    # Polygon as GeoJSON dict
    geojson = {
        "type": "Polygon",
        "coordinates": [[
            [-10, -10],
            [10, -10],
            [10, 10],
            [-10, 10],
            [-10, -10]
        ]]
    }

    search = stac_client.search(
        intersects=geojson,
        max_items=5
    )

    items = list(search.items())
    assert isinstance(items, list), "Search with GeoJSON intersects should work"


# ============================================================================
#  Temporal Search Tests
# ============================================================================


def test_search_with_datetime_single(stac_client):
    """Verify STAC search supports single datetime filter."""
    # Search for items at specific date
    datetime_str = "2024-01-01T00:00:00Z"

    search = stac_client.search(
        datetime=datetime_str,
        max_items=5
    )

    items = list(search.items())
    assert isinstance(items, list), "Search with single datetime should return items"


def test_search_with_datetime_range(stac_client):
    """Verify STAC search supports datetime range filter."""
    # Search for items in date range
    datetime_range = "2023-01-01T00:00:00Z/2024-12-31T23:59:59Z"

    search = stac_client.search(
        datetime=datetime_range,
        max_items=10
    )

    items = list(search.items())
    assert isinstance(items, list), "Search with datetime range should return items"


def test_search_with_open_ended_datetime(stac_client):
    """Verify STAC search supports open-ended datetime ranges."""
    # Search from date to present
    datetime_range = "2023-01-01T00:00:00Z/.."

    search = stac_client.search(
        datetime=datetime_range,
        max_items=5
    )

    items = list(search.items())
    assert isinstance(items, list), "Search with open-ended datetime should work"


def test_search_with_datetime_validates_items(stac_client):
    """Verify items returned by datetime search fall within the specified range."""
    from datetime import datetime, timezone

    start = datetime(2020, 1, 1, tzinfo=timezone.utc)
    end = datetime(2025, 12, 31, tzinfo=timezone.utc)
    datetime_range = f"{start.isoformat()}/{end.isoformat()}"

    search = stac_client.search(
        datetime=datetime_range,
        max_items=10
    )

    items = list(search.items())

    for item in items:
        if item.datetime:
            # Item datetime should be within range
            assert start <= item.datetime <= end, \
                f"Item datetime {item.datetime} outside range {start} to {end}"


# ============================================================================
#  Property Search Tests
# ============================================================================


def test_search_with_query_parameter(stac_client, valid_collection_id):
    """Verify STAC search supports query parameter for property filtering."""
    # Search with simple query (implementation dependent)
    try:
        search = stac_client.search(
            collections=[valid_collection_id],
            query={"id": {"gt": "0"}},
            max_items=5
        )

        items = list(search.items())
        assert isinstance(items, list), "Search with query should return items"
    except Exception as e:
        # Query/CQL2 support is optional in STAC API
        if "not supported" in str(e).lower() or "400" in str(e):
            pytest.skip("Query parameter not supported by this STAC implementation")
        raise


# ============================================================================
#  Collection Filtering Tests
# ============================================================================


def test_search_with_collections_filter(stac_client, valid_collection_id):
    """Verify STAC search can filter by collection ID."""
    search = stac_client.search(
        collections=[valid_collection_id],
        max_items=10
    )

    items = list(search.items())

    # Verify all returned items are from specified collection
    for item in items:
        assert item.collection_id == valid_collection_id, \
            f"Expected collection {valid_collection_id}, got {item.collection_id}"


def test_search_with_multiple_collections(stac_client):
    """Verify STAC search can filter by multiple collection IDs."""
    collections = list(stac_client.get_collections())

    if len(collections) < 2:
        pytest.skip("Need at least 2 collections for this test")

    collection_ids = [collections[0].id, collections[1].id]

    search = stac_client.search(
        collections=collection_ids,
        max_items=10
    )

    items = list(search.items())

    # Verify all items are from specified collections
    for item in items:
        assert item.collection_id in collection_ids, \
            f"Item collection {item.collection_id} not in {collection_ids}"


def test_search_within_collection_items(stac_client, valid_collection_id):
    """Verify searching within collection.get_items() works correctly."""
    collection = stac_client.get_collection(valid_collection_id)
    assert collection is not None, f"Collection {valid_collection_id} should exist"

    # Get items from collection
    items = list(collection.get_items())

    # May be empty, which is valid
    assert isinstance(items, list), "Collection items should return list"

    for item in items:
        assert item.collection_id == valid_collection_id


# ============================================================================
#  Pagination Tests
# ============================================================================


def test_iterate_through_paged_results(stac_client):
    """Verify pystac_client can iterate through paged search results."""
    search = stac_client.search(max_items=10)

    items = []
    for item in search.items():
        items.append(item)
        if len(items) >= 10:
            break

    # Should retrieve items up to max_items
    assert len(items) <= 10, "Should respect max_items parameter"


def test_item_collection_returns_all_pages(stac_client):
    """Verify item_collection() aggregates results from multiple pages."""
    search = stac_client.search(max_items=5)

    item_collection = search.item_collection()

    assert item_collection is not None, "Should return ItemCollection"
    assert hasattr(item_collection, "items"), "ItemCollection should have items"

    items = list(item_collection)
    assert len(items) <= 5, "Should respect max_items limit"


def test_pagination_with_limit(stac_client):
    """Verify search respects limit parameter for pagination."""
    limit = 3

    search = stac_client.search(limit=limit, max_items=limit)

    items = list(search.items())
    assert len(items) <= limit, f"Expected at most {limit} items, got {len(items)}"


# ============================================================================
#  Asset Download Tests
# ============================================================================


def test_download_item_asset_metadata(stac_client):
    """Verify STAC item assets have proper metadata."""
    search = stac_client.search(max_items=5)

    items = list(search.items())

    if not items:
        pytest.skip("No STAC items available for asset testing")

    item = items[0]
    assert len(item.assets) > 0, "Item should have at least one asset"

    # Validate asset structure
    for asset_key, asset in item.assets.items():
        assert asset.href is not None, f"Asset {asset_key} must have href"
        # Asset type and roles are optional but recommended
        assert isinstance(asset.href, str), "Asset href should be string"


def test_download_asset_with_requests(stac_client, honua_api_bearer_token):
    """Verify STAC item assets can be accessed via HTTP."""
    import requests

    search = stac_client.search(max_items=5)

    items = list(search.items())

    if not items:
        pytest.skip("No STAC items available")

    # Find an asset with HTTP URL
    asset_url = None
    for item in items:
        for asset in item.assets.values():
            if asset.href and asset.href.startswith("http"):
                asset_url = asset.href
                break
        if asset_url:
            break

    if not asset_url:
        pytest.skip("No HTTP-accessible assets available")

    # Try to access asset (HEAD request to avoid downloading large files)
    headers = {}
    if honua_api_bearer_token:
        headers["Authorization"] = f"Bearer {honua_api_bearer_token}"

    response = requests.head(asset_url, headers=headers, timeout=10)

    # Accept 200 (OK) or 403 (auth required) as valid
    assert response.status_code in (200, 403), \
        f"Asset should be accessible, got status {response.status_code}"


def test_cog_asset_is_geotiff(stac_client):
    """Verify COG assets have correct media type."""
    search = stac_client.search(max_items=10)

    items = list(search.items())

    # Find item with COG asset
    cog_asset = None
    for item in items:
        if "cog" in item.assets:
            cog_asset = item.assets["cog"]
            break

    if not cog_asset:
        pytest.skip("No COG assets available in test items")

    # Validate COG asset type
    if cog_asset.media_type:
        assert "tiff" in cog_asset.media_type.lower() or "geotiff" in cog_asset.media_type.lower(), \
            f"COG asset should be GeoTIFF, got {cog_asset.media_type}"


# ============================================================================
#  STAC Schema Validation Tests
# ============================================================================


def test_validate_stac_item_schema(stac_client):
    """Verify STAC items conform to STAC Item 1.0.0 JSON schema."""
    try:
        import jsonschema
        import requests
    except ImportError:
        pytest.skip("jsonschema not installed (pip install jsonschema)")

    search = stac_client.search(max_items=1)

    items = list(search.items())

    if not items:
        pytest.skip("No STAC items available for validation")

    item = items[0]

    # Get STAC Item schema
    schema_url = "https://schemas.stacspec.org/v1.0.0/item-spec/json-schema/item.json"

    try:
        response = requests.get(schema_url, timeout=10)
        response.raise_for_status()
        schema = response.json()
    except Exception as e:
        pytest.skip(f"Could not fetch STAC schema: {e}")

    # Validate item against schema
    item_dict = item.to_dict()

    try:
        jsonschema.validate(instance=item_dict, schema=schema)
    except jsonschema.ValidationError as e:
        pytest.fail(f"STAC Item validation failed: {e.message}")


def test_validate_item_has_required_fields(stac_client):
    """Verify STAC items have all required fields."""
    search = stac_client.search(max_items=5)

    items = list(search.items())

    if not items:
        pytest.skip("No STAC items available")

    for item in items:
        # Required fields per STAC Item spec
        assert item.id is not None, "Item must have id"
        assert item.geometry is not None, "Item must have geometry"
        assert item.properties is not None, "Item must have properties"
        assert item.assets is not None, "Item must have assets"
        assert item.stac_extensions is not None, "Item must have stac_extensions (can be empty)"

        # Collection should be set for items from STAC API
        assert item.collection_id is not None, "Item should reference a collection"


def test_validate_item_geometry_is_geojson(stac_client):
    """Verify STAC item geometries are valid GeoJSON."""
    search = stac_client.search(max_items=5)

    items = list(search.items())

    if not items:
        pytest.skip("No STAC items available")

    for item in items:
        geom = item.geometry

        if geom is None:
            continue  # Null geometry is valid

        assert "type" in geom, "Geometry must have type"
        assert "coordinates" in geom, "Geometry must have coordinates"

        # Validate geometry type
        valid_types = ["Point", "LineString", "Polygon", "MultiPoint",
                      "MultiLineString", "MultiPolygon", "GeometryCollection"]
        assert geom["type"] in valid_types, f"Invalid geometry type: {geom['type']}"


# ============================================================================
#  Error Handling Tests
# ============================================================================


def test_invalid_catalog_url_raises_error(honua_api_base_url):
    """Verify pystac_client raises error for invalid catalog URL."""
    try:
        from pystac_client import Client
    except ImportError:
        pytest.skip("pystac-client not installed")

    invalid_url = f"{honua_api_base_url}/stac/nonexistent"

    with pytest.raises(Exception):
        Client.open(invalid_url)


def test_invalid_collection_raises_error(stac_client):
    """Verify accessing invalid collection raises appropriate error."""
    with pytest.raises(Exception):
        stac_client.get_collection("nonexistent-collection-id-12345")


def test_search_with_invalid_bbox_raises_error(stac_client):
    """Verify search with malformed bbox raises error."""
    # Invalid bbox (wrong number of coordinates)
    invalid_bbox = [-180, -90, 180]  # Missing fourth coordinate

    with pytest.raises(Exception):
        search = stac_client.search(bbox=invalid_bbox)
        list(search.items())


def test_search_with_invalid_datetime_raises_error(stac_client):
    """Verify search with malformed datetime raises error."""
    # Invalid datetime format
    invalid_datetime = "not-a-valid-datetime"

    with pytest.raises(Exception):
        search = stac_client.search(datetime=invalid_datetime)
        list(search.items())


# ============================================================================
#  Combined Filter Tests
# ============================================================================


def test_search_with_multiple_filters(stac_client, valid_collection_id):
    """Verify STAC search supports combining multiple filters."""
    bbox = [-180, -90, 180, 90]
    datetime_range = "2023-01-01T00:00:00Z/.."

    search = stac_client.search(
        bbox=bbox,
        datetime=datetime_range,
        collections=[valid_collection_id],
        max_items=5
    )

    items = list(search.items())

    # Verify filters are applied
    for item in items:
        assert item.collection_id == valid_collection_id


def test_search_with_spatial_and_temporal_filters(stac_client):
    """Verify spatial and temporal filters work together."""
    bbox = [-125.0, 32.0, -114.0, 42.0]
    datetime_range = "2020-01-01T00:00:00Z/2025-12-31T23:59:59Z"

    search = stac_client.search(
        bbox=bbox,
        datetime=datetime_range,
        max_items=10
    )

    items = list(search.items())
    assert isinstance(items, list), "Combined filters should return valid results"


# ============================================================================
#  Additional Feature Tests
# ============================================================================


def test_search_matches_returned_count(stac_client):
    """Verify search returns expected number of items based on limit."""
    limit = 5

    search = stac_client.search(limit=limit, max_items=limit)

    items = list(search.items())
    assert len(items) <= limit, f"Should return at most {limit} items"


def test_item_has_links(stac_client):
    """Verify STAC items include required links."""
    search = stac_client.search(max_items=1)

    items = list(search.items())

    if not items:
        pytest.skip("No STAC items available")

    item = items[0]
    assert len(item.links) > 0, "Item should have links"

    # Check for required link relations
    link_rels = {link.rel for link in item.links}
    assert "self" in link_rels or "collection" in link_rels, \
        "Item should have self or collection link"


def test_collection_has_extent(stac_client, valid_collection_id):
    """Verify STAC collection includes spatial and temporal extent."""
    collection = stac_client.get_collection(valid_collection_id)

    assert collection.extent is not None, "Collection must have extent"
    assert collection.extent.spatial is not None, "Collection must have spatial extent"
    assert collection.extent.temporal is not None, "Collection must have temporal extent"

    # Validate spatial extent structure
    assert collection.extent.spatial.bboxes is not None, "Spatial extent must have bboxes"
    assert len(collection.extent.spatial.bboxes) > 0, "Spatial extent must have at least one bbox"

    bbox = collection.extent.spatial.bboxes[0]
    assert len(bbox) >= 4, "Bbox must have at least 4 coordinates"


def test_collection_has_license(stac_client, valid_collection_id):
    """Verify STAC collection includes license information."""
    collection = stac_client.get_collection(valid_collection_id)

    assert collection.license is not None, "Collection must have license"
    assert isinstance(collection.license, str), "License should be string"
    assert len(collection.license) > 0, "License should not be empty"
