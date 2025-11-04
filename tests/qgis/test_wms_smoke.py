import pytest


@pytest.mark.requires_qgis
@pytest.mark.requires_honua
def test_wms_layer_renders_image(qgis_app, qgis_project, honua_base_url, layer_config, tmp_path):
    from qgis.core import (
        QgsMapRendererSequentialJob,
        QgsMapSettings,
        QgsRasterLayer,
    )  # type: ignore
    from qgis.PyQt.QtCore import QSize  # type: ignore

    layer_name = layer_config["wms_layer"]
    uri = (
        f"contextualWMSLegend=0&url={honua_base_url}/wms"
        f"&layers={layer_name}&styles=&format=image/png"
        "&crs=EPSG:3857&dpiMode=7"
    )

    layer = QgsRasterLayer(uri, "honua-wms", "wms")
    assert layer.isValid(), layer.error().summary()

    qgis_project.addMapLayer(layer)

    settings = QgsMapSettings()
    settings.setLayers([layer])
    settings.setDestinationCrs(layer.crs())
    settings.setExtent(layer.extent())
    settings.setOutputSize(QSize(256, 256))

    job = QgsMapRendererSequentialJob(settings)
    job.start()
    job.waitForFinished()

    image = job.renderedImage()
    output = tmp_path / "wms-smoke.png"
    assert not image.isNull()
    assert image.save(str(output), "PNG")
    assert output.stat().st_size > 0
