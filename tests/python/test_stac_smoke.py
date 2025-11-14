import pytest

pytestmark = pytest.mark.requires_honua


def test_stac_root_exposes_catalog(api_request):
    response = api_request("GET", "/stac")
    assert response.status_code == 200, response.text

    payload = response.json()
    assert payload.get("type") == "Catalog"
    links = payload.get("links", [])
    assert any(link.get("rel") == "data" for link in links)
    assert any(link.get("rel") == "conformance" for link in links)


def test_stac_collections_include_items_link(api_request):
    response = api_request("GET", "/stac/collections")
    assert response.status_code == 200, response.text

    payload = response.json()
    collections = payload.get("collections", [])
    assert collections, "expected at least one STAC collection"

    first = collections[0]
    assert "id" in first
    assert any(link.get("rel") == "items" for link in first.get("links", [])), "collection missing items link"


@pytest.mark.parametrize("limit", [5])
def test_stac_search_returns_assets(api_request, limit):
    response = api_request("GET", "/stac/search", params={"limit": limit})
    assert response.status_code == 200, response.text

    payload = response.json()
    features = payload.get("features", [])
    assert features, "expected STAC search to return at least one feature"

    context = payload.get("context", {})
    assert context.get("matched", 0) >= len(features)

    feature = features[0]
    asset_keys = feature.get("assets", {}).keys()
    assert "cog" in asset_keys, "STAC item missing COG asset"

    collection_id = feature.get("collection")
    item_id = feature.get("id")
    assert collection_id and item_id, "STAC feature missing identifiers"

    detail = api_request("GET", f"/stac/collections/{collection_id}/items/{item_id}")
    assert detail.status_code == 200, detail.text
    detail_payload = detail.json()
    assert detail_payload.get("id") == item_id
    assert "assets" in detail_payload


def test_cog_asset_can_be_downloaded(api_request):
    search = api_request("GET", "/stac/search", params={"limit": 1})
    assert search.status_code == 200, search.text
    payload = search.json()
    features = payload.get("features", [])
    if not features:
        pytest.skip("No STAC features available to validate COG download")

    asset = features[0].get("assets", {}).get("cog")
    if not asset:
        pytest.skip("Feature does not expose a COG asset")

    href = asset.get("href")
    if not href or not href.lower().startswith("http"):
        pytest.skip(f"COG asset is not accessible over HTTP(S): {href}")

    response = api_request("GET", href, stream=True)
    if response.status_code in (401, 403):
        pytest.skip("COG asset requires credentials that were not provided")

    assert response.status_code == 200, f"COG asset download failed: {response.text}"
    content_type = response.headers.get("content-type", "").lower()
    assert "tiff" in content_type or "geotiff" in content_type, content_type

    # Ensure we actually received bytes without loading the entire file into memory.
    chunk = next(response.iter_content(chunk_size=4096), b"")
    response.close()
    assert chunk, "COG asset response contained no data"
