import json

import pytest
from qgis.PyQt.QtCore import QEventLoop, QUrl  # type: ignore
from qgis.PyQt.QtNetwork import QNetworkRequest  # type: ignore
from qgis.core import QgsNetworkAccessManager  # type: ignore


@pytest.mark.requires_qgis
@pytest.mark.requires_honua
@pytest.mark.parametrize(
    ("matrix", "format_param", "expected_format"),
    [
        ("WorldWebMercatorQuad", "png", "image/png"),
        ("WorldCRS84Quad", "jpeg", "image/jpeg"),
    ],
)
def test_tilejson_exposes_tiles_for_matrix_and_format(
    honua_base_url,
    layer_config,
    matrix,
    format_param,
    expected_format,
):
    collection_id = layer_config["collection_id"]
    tilejson_url = (
        f"{honua_base_url}/ogc/collections/{collection_id}/tiles/roads-imagery/tilejson"
        f"?tileMatrixSet={matrix}&format={format_param}"
    )

    status, payload, _ = _fetch_bytes(tilejson_url)
    assert status == 200, f"TileJSON request failed ({status})"

    data = json.loads(payload.decode())
    assert data["tileMatrixSet"] == matrix
    assert data["format"].lower().endswith(format_param)
    tiles = data.get("tiles", [])
    assert tiles, "TileJSON response did not specify tile endpoints"
    assert any(matrix in url for url in tiles)

    # Sample the first tile in the matrix/format pairing to ensure the renderer honours format
    tile_url = (
        tiles[0]
        .replace("{z}", "0")
        .replace("{x}", "0")
        .replace("{y}", "0")
    )

    tile_status, tile_bytes, tile_headers = _fetch_bytes(tile_url)
    assert tile_status == 200
    assert tile_bytes, "Tile response was empty"
    assert tile_headers.get("content-type", "").startswith(expected_format)


@pytest.mark.requires_qgis
@pytest.mark.requires_honua
def test_wmts_tile_returns_etag_and_cached_bytes(honua_base_url, layer_config):
    collection_id = layer_config["collection_id"]
    tilejson_url = (
        f"{honua_base_url}/ogc/collections/{collection_id}/tiles/roads-imagery/tilejson"
        "?tileMatrixSet=WorldWebMercatorQuad&format=png"
    )

    status, payload, _ = _fetch_bytes(tilejson_url)
    assert status == 200, "Failed to retrieve TileJSON descriptor"
    data = json.loads(payload.decode())
    template = data["tiles"][0]
    tile_url = (
        template.replace("{z}", "0")
        .replace("{x}", "0")
        .replace("{y}", "0")
    )

    first_status, first_bytes, first_headers = _fetch_bytes(tile_url)
    assert first_status == 200, f"Tile fetch failed with status {first_status}"
    assert len(first_bytes) > 0, "Tile response was empty"
    etag = first_headers.get("etag")

    extra_headers = {"If-None-Match": etag} if etag else None
    second_status, second_bytes, second_headers = _fetch_bytes(tile_url, extra_headers)

    if second_status == 304:
        assert etag, "Received 304 without an initial ETag"
    else:
        assert second_status == 200, f"Expected 200/304 but received {second_status}"
        assert second_bytes == first_bytes
        if etag:
            assert second_headers.get("etag") == etag


def _fetch_bytes(url: str, headers: dict | None = None):
    manager = QgsNetworkAccessManager.instance()
    request = QNetworkRequest(QUrl(url))
    if headers:
        for key, value in headers.items():
            request.setRawHeader(key.encode("ascii"), value.encode("ascii"))

    reply = manager.get(request)
    loop = QEventLoop()
    reply.finished.connect(loop.quit)
    loop.exec()

    status = reply.attribute(QNetworkRequest.HttpStatusCodeAttribute)
    raw_headers = {
        bytes(key).decode("ascii", "ignore").lower(): bytes(value).decode("ascii", "ignore")
        for key, value in reply.rawHeaderPairs()
    }
    content = bytes(reply.readAll())
    reply.deleteLater()
    return status or 0, content, raw_headers
