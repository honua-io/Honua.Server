from urllib.parse import parse_qsl, urlencode, urlparse, urlunparse

import pytest


def _ensure_param(url: str, key: str, value: str) -> str:
    parsed = urlparse(url)
    pairs = list(parse_qsl(parsed.query, keep_blank_values=True))
    if all(existing_key != key for existing_key, _ in pairs):
        pairs.append((key, value))
    new_query = urlencode(pairs, doseq=True)
    return urlunparse(parsed._replace(query=new_query))


@pytest.mark.requires_qgis
@pytest.mark.requires_honua
def test_geojson_collection_loads_with_features(qgis_app, honua_base_url, layer_config):
    from qgis.core import QgsVectorLayer  # type: ignore

    collection_id = layer_config["collection_id"]
    query = layer_config["items_query"]

    items_url = f"{honua_base_url}/ogc/collections/{collection_id}/items"
    if query:
        if "?" in items_url:
            items_url = f"{items_url}&{query}"
        else:
            items_url = f"{items_url}?{query}"
    items_url = _ensure_param(items_url, "f", "json")

    layer = QgsVectorLayer(items_url, "honua-collection", "ogr")
    assert layer.isValid(), layer.error().summary()

    features = list(layer.getFeatures())
    assert features, "collection returned no features"

    authid = layer.crs().authid()
    assert authid, "layer CRS is missing; QGIS requires valid CRS metadata"
