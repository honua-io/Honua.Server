/**
 * Honua WFS Layer Integration
 * Provides WFS layer support for MapLibre with feature querying
 */

const wfsLayers = new Map();

/**
 * Create a WFS layer on the map
 * @param {string} mapId - Map identifier
 * @param {object} options - WFS layer options
 * @param {object} dotNetRef - .NET reference for callbacks
 * @returns {object} Layer instance
 */
export function createWfsLayer(mapId, options, dotNetRef) {
    const map = window.honuaMaps?.get(mapId);
    if (!map) {
        throw new Error(`Map not found: ${mapId}`);
    }

    const {
        sourceId,
        layerId,
        serviceUrl,
        version = '2.0.0',
        featureType,
        outputFormat = 'application/json',
        styleConfig
    } = options;

    // Add empty GeoJSON source (will be populated later)
    map.addSource(sourceId, {
        type: 'geojson',
        data: {
            type: 'FeatureCollection',
            features: []
        }
    });

    // Determine geometry type and add appropriate layer(s)
    const layerStyle = styleConfig || getDefaultStyle();

    // Add point layer
    if (layerStyle.point !== false) {
        map.addLayer({
            id: `${layerId}-point`,
            type: 'circle',
            source: sourceId,
            filter: ['==', ['geometry-type'], 'Point'],
            paint: {
                'circle-radius': layerStyle.point?.radius || 6,
                'circle-color': layerStyle.point?.color || '#3b82f6',
                'circle-stroke-width': layerStyle.point?.strokeWidth || 2,
                'circle-stroke-color': layerStyle.point?.strokeColor || '#ffffff',
                'circle-opacity': layerStyle.point?.opacity || 0.8
            }
        });
    }

    // Add line layer
    if (layerStyle.line !== false) {
        map.addLayer({
            id: `${layerId}-line`,
            type: 'line',
            source: sourceId,
            filter: ['==', ['geometry-type'], 'LineString'],
            paint: {
                'line-color': layerStyle.line?.color || '#3b82f6',
                'line-width': layerStyle.line?.width || 2,
                'line-opacity': layerStyle.line?.opacity || 0.8
            }
        });
    }

    // Add polygon fill layer
    if (layerStyle.polygon !== false) {
        map.addLayer({
            id: `${layerId}-fill`,
            type: 'fill',
            source: sourceId,
            filter: ['==', ['geometry-type'], 'Polygon'],
            paint: {
                'fill-color': layerStyle.polygon?.fillColor || '#3b82f6',
                'fill-opacity': layerStyle.polygon?.fillOpacity || 0.3
            }
        });

        // Add polygon outline
        map.addLayer({
            id: `${layerId}-outline`,
            type: 'line',
            source: sourceId,
            filter: ['==', ['geometry-type'], 'Polygon'],
            paint: {
                'line-color': layerStyle.polygon?.strokeColor || '#1e40af',
                'line-width': layerStyle.polygon?.strokeWidth || 2,
                'line-opacity': layerStyle.polygon?.strokeOpacity || 0.8
            }
        });
    }

    // Setup click handlers for all layer types
    const clickHandler = (e) => {
        if (e.features && e.features.length > 0) {
            const feature = e.features[0];
            if (dotNetRef && dotNetRef.invokeMethodAsync) {
                dotNetRef.invokeMethodAsync('OnFeatureClickCallback', JSON.stringify(feature));
            }
        }
    };

    const layerIds = [
        `${layerId}-point`,
        `${layerId}-line`,
        `${layerId}-fill`,
        `${layerId}-outline`
    ];

    layerIds.forEach(id => {
        if (map.getLayer(id)) {
            map.on('click', id, clickHandler);

            // Change cursor on hover
            map.on('mouseenter', id, () => {
                map.getCanvas().style.cursor = 'pointer';
            });

            map.on('mouseleave', id, () => {
                map.getCanvas().style.cursor = '';
            });
        }
    });

    const layerInstance = {
        map,
        sourceId,
        layerId,
        layerIds,
        serviceUrl,
        version,
        featureType,
        outputFormat,
        styleConfig,
        clickHandler,
        dotNetRef,

        updateFeatures(geoJsonString) {
            try {
                const geoJson = JSON.parse(geoJsonString);
                const source = map.getSource(sourceId);
                if (source) {
                    source.setData(geoJson);
                }
            } catch (error) {
                console.error('Error updating WFS features:', error);
            }
        },

        setVisibility(visible) {
            this.layerIds.forEach(id => {
                if (map.getLayer(id)) {
                    map.setLayoutProperty(
                        id,
                        'visibility',
                        visible ? 'visible' : 'none'
                    );
                }
            });
        },

        setStyle(newStyle) {
            this.styleConfig = newStyle;

            // Update point style
            if (map.getLayer(`${layerId}-point`) && newStyle.point) {
                if (newStyle.point.radius !== undefined) {
                    map.setPaintProperty(`${layerId}-point`, 'circle-radius', newStyle.point.radius);
                }
                if (newStyle.point.color !== undefined) {
                    map.setPaintProperty(`${layerId}-point`, 'circle-color', newStyle.point.color);
                }
                if (newStyle.point.opacity !== undefined) {
                    map.setPaintProperty(`${layerId}-point`, 'circle-opacity', newStyle.point.opacity);
                }
            }

            // Update line style
            if (map.getLayer(`${layerId}-line`) && newStyle.line) {
                if (newStyle.line.color !== undefined) {
                    map.setPaintProperty(`${layerId}-line`, 'line-color', newStyle.line.color);
                }
                if (newStyle.line.width !== undefined) {
                    map.setPaintProperty(`${layerId}-line`, 'line-width', newStyle.line.width);
                }
                if (newStyle.line.opacity !== undefined) {
                    map.setPaintProperty(`${layerId}-line`, 'line-opacity', newStyle.line.opacity);
                }
            }

            // Update polygon style
            if (map.getLayer(`${layerId}-fill`) && newStyle.polygon) {
                if (newStyle.polygon.fillColor !== undefined) {
                    map.setPaintProperty(`${layerId}-fill`, 'fill-color', newStyle.polygon.fillColor);
                }
                if (newStyle.polygon.fillOpacity !== undefined) {
                    map.setPaintProperty(`${layerId}-fill`, 'fill-opacity', newStyle.polygon.fillOpacity);
                }
            }

            if (map.getLayer(`${layerId}-outline`) && newStyle.polygon) {
                if (newStyle.polygon.strokeColor !== undefined) {
                    map.setPaintProperty(`${layerId}-outline`, 'line-color', newStyle.polygon.strokeColor);
                }
                if (newStyle.polygon.strokeWidth !== undefined) {
                    map.setPaintProperty(`${layerId}-outline`, 'line-width', newStyle.polygon.strokeWidth);
                }
            }
        },

        dispose() {
            this.layerIds.forEach(id => {
                if (map.getLayer(id)) {
                    map.off('click', id, this.clickHandler);
                    map.off('mouseenter', id);
                    map.off('mouseleave', id);
                    map.removeLayer(id);
                }
            });

            if (map.getSource(sourceId)) {
                map.removeSource(sourceId);
            }

            wfsLayers.delete(layerId);
        }
    };

    wfsLayers.set(layerId, layerInstance);
    return layerInstance;
}

/**
 * Get default style configuration
 */
function getDefaultStyle() {
    return {
        point: {
            radius: 6,
            color: '#3b82f6',
            strokeWidth: 2,
            strokeColor: '#ffffff',
            opacity: 0.8
        },
        line: {
            color: '#3b82f6',
            width: 2,
            opacity: 0.8
        },
        polygon: {
            fillColor: '#3b82f6',
            fillOpacity: 0.3,
            strokeColor: '#1e40af',
            strokeWidth: 2,
            strokeOpacity: 0.8
        }
    };
}

/**
 * Get WFS layer instance
 */
export function getWfsLayer(layerId) {
    return wfsLayers.get(layerId);
}

// Initialize global map registry if not exists
if (!window.honuaMaps) {
    window.honuaMaps = new Map();
}
