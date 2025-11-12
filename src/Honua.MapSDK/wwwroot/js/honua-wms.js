/**
 * Honua WMS Layer Integration
 * Provides WMS layer support for MapLibre with GetFeatureInfo
 */

const wmsLayers = new Map();

/**
 * Create a WMS layer on the map
 * @param {string} mapId - Map identifier
 * @param {object} options - WMS layer options
 * @param {object} dotNetRef - .NET reference for callbacks
 * @returns {object} Layer instance
 */
export function createWmsLayer(mapId, options, dotNetRef) {
    const map = window.honuaMaps?.get(mapId);
    if (!map) {
        throw new Error(`Map not found: ${mapId}`);
    }

    const {
        sourceId,
        layerId,
        serviceUrl,
        version = '1.3.0',
        layers,
        srs = 'EPSG:3857',
        format = 'image/png',
        transparent = true,
        opacity = 1.0,
        minZoom,
        maxZoom,
        time
    } = options;

    // Build WMS tile URL template
    const tileUrl = buildWmsTileUrl(
        serviceUrl,
        version,
        layers,
        srs,
        format,
        transparent,
        time
    );

    // Add WMS source
    map.addSource(sourceId, {
        type: 'raster',
        tiles: [tileUrl],
        tileSize: 256,
        minzoom: minZoom || 0,
        maxzoom: maxZoom || 22
    });

    // Add WMS layer
    map.addLayer({
        id: layerId,
        type: 'raster',
        source: sourceId,
        paint: {
            'raster-opacity': opacity
        },
        minzoom: minZoom,
        maxzoom: maxZoom
    });

    // Setup GetFeatureInfo on click
    const clickHandler = (e) => {
        handleGetFeatureInfo(map, e, {
            serviceUrl,
            version,
            layers,
            srs,
            format
        }, dotNetRef);
    };

    map.on('click', layerId, clickHandler);

    // Change cursor on hover
    map.on('mouseenter', layerId, () => {
        map.getCanvas().style.cursor = 'pointer';
    });

    map.on('mouseleave', layerId, () => {
        map.getCanvas().style.cursor = '';
    });

    const layerInstance = {
        map,
        sourceId,
        layerId,
        serviceUrl,
        version,
        layers,
        srs,
        format,
        transparent,
        clickHandler,
        dotNetRef,

        updateLayers(newLayers) {
            this.layers = newLayers;
            const newTileUrl = buildWmsTileUrl(
                this.serviceUrl,
                this.version,
                newLayers,
                this.srs,
                this.format,
                this.transparent,
                this.time
            );

            // Update source
            const source = map.getSource(sourceId);
            if (source) {
                source.tiles = [newTileUrl];
                source.load();
            }
        },

        setOpacity(opacity) {
            map.setPaintProperty(layerId, 'raster-opacity', opacity);
        },

        setTime(time) {
            this.time = time;
            const newTileUrl = buildWmsTileUrl(
                this.serviceUrl,
                this.version,
                this.layers,
                this.srs,
                this.format,
                this.transparent,
                time
            );

            const source = map.getSource(sourceId);
            if (source) {
                source.tiles = [newTileUrl];
                source.load();
            }
        },

        setVisibility(visible) {
            map.setLayoutProperty(
                layerId,
                'visibility',
                visible ? 'visible' : 'none'
            );
        },

        refresh() {
            // Force reload tiles
            const source = map.getSource(sourceId);
            if (source) {
                map.style.sourceCaches[sourceId].clearTiles();
                map.style.sourceCaches[sourceId].update(map.transform);
                map.triggerRepaint();
            }
        },

        dispose() {
            map.off('click', layerId, this.clickHandler);
            map.off('mouseenter', layerId);
            map.off('mouseleave', layerId);

            if (map.getLayer(layerId)) {
                map.removeLayer(layerId);
            }
            if (map.getSource(sourceId)) {
                map.removeSource(sourceId);
            }

            wmsLayers.delete(layerId);
        }
    };

    wmsLayers.set(layerId, layerInstance);
    return layerInstance;
}

/**
 * Build WMS tile URL template
 */
function buildWmsTileUrl(serviceUrl, version, layers, srs, format, transparent, time) {
    const separator = serviceUrl.includes('?') ? '&' : '?';
    const layerList = Array.isArray(layers) ? layers.join(',') : layers;

    let url = `${serviceUrl}${separator}SERVICE=WMS&VERSION=${version}&REQUEST=GetMap`;
    url += `&LAYERS=${encodeURIComponent(layerList)}`;
    url += `&STYLES=`;
    url += version === '1.3.0' ? `&CRS=${srs}` : `&SRS=${srs}`;
    url += `&BBOX={bbox-epsg-3857}`;
    url += `&WIDTH=256&HEIGHT=256`;
    url += `&FORMAT=${encodeURIComponent(format)}`;
    url += `&TRANSPARENT=${transparent.toString().toUpperCase()}`;

    if (time) {
        url += `&TIME=${encodeURIComponent(time)}`;
    }

    return url;
}

/**
 * Handle GetFeatureInfo request on map click
 */
async function handleGetFeatureInfo(map, e, wmsConfig, dotNetRef) {
    const { serviceUrl, version, layers, srs, format } = wmsConfig;

    const bbox = map.getBounds().toArray().flat();
    const size = map.getCanvas();
    const width = size.width;
    const height = size.height;

    // Convert click coordinates to pixel coordinates
    const point = map.project(e.lngLat);
    const x = Math.floor(point.x);
    const y = Math.floor(point.y);

    const separator = serviceUrl.includes('?') ? '&' : '?';
    const layerList = Array.isArray(layers) ? layers.join(',') : layers;

    let url = `${serviceUrl}${separator}SERVICE=WMS&VERSION=${version}&REQUEST=GetFeatureInfo`;
    url += `&LAYERS=${encodeURIComponent(layerList)}`;
    url += `&QUERY_LAYERS=${encodeURIComponent(layerList)}`;
    url += version === '1.3.0' ? `&CRS=${srs}` : `&SRS=${srs}`;
    url += `&BBOX=${bbox.join(',')}`;
    url += `&WIDTH=${width}&HEIGHT=${height}`;
    url += `&FORMAT=${encodeURIComponent(format)}`;
    url += `&INFO_FORMAT=application/json`;
    url += version === '1.3.0' ? `&I=${x}&J=${y}` : `&X=${x}&Y=${y}`;
    url += `&FEATURE_COUNT=10`;

    try {
        const response = await fetch(url);
        const data = await response.text();

        if (dotNetRef && dotNetRef.invokeMethodAsync) {
            await dotNetRef.invokeMethodAsync('OnFeatureInfoCallback', data);
        }
    } catch (error) {
        console.error('Error fetching GetFeatureInfo:', error);
    }
}

/**
 * Get WMS layer instance
 */
export function getWmsLayer(layerId) {
    return wmsLayers.get(layerId);
}

// Initialize global map registry if not exists
if (!window.honuaMaps) {
    window.honuaMaps = new Map();
}
