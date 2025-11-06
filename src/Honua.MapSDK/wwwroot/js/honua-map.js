// Honua Map JavaScript Module
// Wraps MapLibre GL JS with optimized features

import maplibregl from 'https://cdn.jsdelivr.net/npm/maplibre-gl@5.0.0/+esm';

export function createMap(container, options, dotNetRef) {
    const map = new maplibregl.Map({
        container: container,
        style: options.style,
        center: options.center,
        zoom: options.zoom,
        bearing: options.bearing,
        pitch: options.pitch,
        projection: options.projection,
        maxBounds: options.maxBounds,
        minZoom: options.minZoom,
        maxZoom: options.maxZoom,
        hash: false,
        trackResize: true
    });

    // Store references
    map._dotNetRef = dotNetRef;
    map._honuaId = options.id;
    map._filters = new Map();
    map._highlightLayer = null;

    // Setup event handlers
    setupEventHandlers(map, dotNetRef);

    // Create API wrapper
    const api = createMapAPI(map);

    return api;
}

function setupEventHandlers(map, dotNetRef) {
    // Map ready
    map.on('load', () => {
        console.log(`Honua Map ${map._honuaId} loaded`);
    });

    // Extent changed (debounced)
    let extentChangeTimeout;
    const notifyExtentChanged = () => {
        const bounds = map.getBounds();
        const center = map.getCenter();

        dotNetRef.invokeMethodAsync('OnExtentChangedInternal',
            [bounds.getWest(), bounds.getSouth(), bounds.getEast(), bounds.getNorth()],
            map.getZoom(),
            [center.lng, center.lat],
            map.getBearing(),
            map.getPitch()
        );
    };

    map.on('moveend', () => {
        clearTimeout(extentChangeTimeout);
        extentChangeTimeout = setTimeout(notifyExtentChanged, 100);
    });

    // Feature click
    map.on('click', (e) => {
        const features = map.queryRenderedFeatures(e.point);
        if (features.length > 0) {
            const feature = features[0];
            dotNetRef.invokeMethodAsync('OnFeatureClickedInternal',
                feature.layer.id,
                feature.id?.toString() || feature.properties.id?.toString() || 'unknown',
                feature.properties,
                feature.geometry
            );
        }
    });

    // Feature hover
    let hoveredFeatureId = null;
    map.on('mousemove', (e) => {
        const features = map.queryRenderedFeatures(e.point);
        if (features.length > 0) {
            const feature = features[0];
            const featureId = feature.id?.toString() || feature.properties.id?.toString();

            if (featureId !== hoveredFeatureId) {
                hoveredFeatureId = featureId;
                map.getCanvas().style.cursor = 'pointer';
                dotNetRef.invokeMethodAsync('OnFeatureHoveredInternal',
                    featureId,
                    feature.layer.id,
                    feature.properties
                );
            }
        } else if (hoveredFeatureId !== null) {
            hoveredFeatureId = null;
            map.getCanvas().style.cursor = '';
            dotNetRef.invokeMethodAsync('OnFeatureHoveredInternal', null, null, null);
        }
    });
}

function createMapAPI(map) {
    return {
        // Navigation
        flyTo: (options) => {
            map.flyTo({
                center: options.center,
                zoom: options.zoom,
                bearing: options.bearing,
                pitch: options.pitch,
                duration: options.duration || 1000
            });
        },

        fitBounds: (bounds, padding) => {
            map.fitBounds(bounds, { padding: padding || 50 });
        },

        getBounds: () => {
            const bounds = map.getBounds();
            return [bounds.getWest(), bounds.getSouth(), bounds.getEast(), bounds.getNorth()];
        },

        getCenter: () => {
            const center = map.getCenter();
            return [center.lng, center.lat];
        },

        getZoom: () => {
            return map.getZoom();
        },

        // Style
        setStyle: (style) => {
            map.setStyle(style);
        },

        // Layers
        addLayer: (layer) => {
            if (!map.getLayer(layer.id)) {
                map.addLayer(layer);
            }
        },

        removeLayer: (layerId) => {
            if (map.getLayer(layerId)) {
                map.removeLayer(layerId);
            }
        },

        setLayerVisibility: (layerId, visible) => {
            if (map.getLayer(layerId)) {
                map.setLayoutProperty(layerId, 'visibility', visible ? 'visible' : 'none');
            }
        },

        setLayerOpacity: (layerId, opacity) => {
            if (map.getLayer(layerId)) {
                const layer = map.getLayer(layerId);
                const opacityProp = getOpacityProperty(layer.type);
                if (opacityProp) {
                    map.setPaintProperty(layerId, opacityProp, opacity);
                }
            }
        },

        // Sources
        addSource: (sourceId, source) => {
            if (!map.getSource(sourceId)) {
                map.addSource(sourceId, source);
            }
        },

        removeSource: (sourceId) => {
            if (map.getSource(sourceId)) {
                map.removeSource(sourceId);
            }
        },

        // Filters
        applyFilter: (filterId, expression) => {
            map._filters.set(filterId, expression);
            applyAllFilters(map);
        },

        clearFilter: (filterId) => {
            map._filters.delete(filterId);
            applyAllFilters(map);
        },

        clearAllFilters: () => {
            map._filters.clear();
            applyAllFilters(map);
        },

        // Highlighting
        highlightFeature: (featureId, geometry) => {
            clearHighlights(map);

            if (!map.getSource('honua-highlight')) {
                map.addSource('honua-highlight', {
                    type: 'geojson',
                    data: {
                        type: 'Feature',
                        geometry: geometry,
                        properties: { id: featureId }
                    }
                });

                map.addLayer({
                    id: 'honua-highlight-layer',
                    type: geometry.type === 'Point' ? 'circle' : 'line',
                    source: 'honua-highlight',
                    paint: geometry.type === 'Point' ? {
                        'circle-color': '#00ffff',
                        'circle-radius': 8,
                        'circle-stroke-width': 2,
                        'circle-stroke-color': '#0000ff'
                    } : {
                        'line-color': '#00ffff',
                        'line-width': 3
                    }
                });

                map._highlightLayer = 'honua-highlight-layer';
            }
        },

        highlightFeatures: (featureIds, layerId) => {
            // Set feature state for highlighting
            featureIds.forEach(id => {
                map.setFeatureState(
                    { source: layerId, id: id },
                    { highlighted: true }
                );
            });
        },

        clearHighlights: () => {
            clearHighlights(map);
        },

        // Cleanup
        dispose: () => {
            map.remove();
        }
    };
}

function getOpacityProperty(layerType) {
    const opacityProps = {
        'fill': 'fill-opacity',
        'line': 'line-opacity',
        'circle': 'circle-opacity',
        'symbol': 'icon-opacity',
        'raster': 'raster-opacity',
        'fill-extrusion': 'fill-extrusion-opacity',
        'heatmap': 'heatmap-opacity'
    };
    return opacityProps[layerType];
}

function applyAllFilters(map) {
    // Combine all active filters
    const filters = Array.from(map._filters.values());

    if (filters.length === 0) {
        // Clear all filters
        map.getStyle().layers.forEach(layer => {
            if (layer.source && layer.source !== 'honua-highlight') {
                map.setFilter(layer.id, null);
            }
        });
    } else if (filters.length === 1) {
        // Single filter
        applyFilterToAllLayers(map, filters[0]);
    } else {
        // Multiple filters - combine with AND
        const combined = ['all', ...filters];
        applyFilterToAllLayers(map, combined);
    }
}

function applyFilterToAllLayers(map, filter) {
    map.getStyle().layers.forEach(layer => {
        if (layer.source && layer.source !== 'honua-highlight') {
            map.setFilter(layer.id, filter);
        }
    });
}

function clearHighlights(map) {
    if (map._highlightLayer) {
        if (map.getLayer(map._highlightLayer)) {
            map.removeLayer(map._highlightLayer);
        }
        if (map.getSource('honua-highlight')) {
            map.removeSource('honua-highlight');
        }
        map._highlightLayer = null;
    }
}
