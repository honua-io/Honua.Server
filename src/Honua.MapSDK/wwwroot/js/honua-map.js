// Honua Map JavaScript Module
// Wraps MapLibre GL JS with optimized features and WebGPU support

import maplibregl from 'https://cdn.jsdelivr.net/npm/maplibre-gl@5.0.0/+esm';
import { WebGpuRendererManager } from './webgpu-manager.js';

// Global renderer manager instance
let _rendererManager = null;

export async function createMap(container, options, dotNetRef) {
    // Initialize WebGPU renderer manager if not already done
    if (!_rendererManager) {
        _rendererManager = new WebGpuRendererManager();
        const rendererInfo = await _rendererManager.initialize(options.renderingEngine || 'Auto');

        console.log(`[Honua Map] Renderer initialized:`, rendererInfo);
        console.log(`[Honua Map] Engine: ${rendererInfo.engine}, Fallback: ${rendererInfo.isFallback}`);

        // Start performance monitoring
        _rendererManager.startMonitoring();
    }

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
    map._rendererManager = _rendererManager;

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
        // Renderer information
        getRendererInfo: () => {
            if (map._rendererManager) {
                return map._rendererManager.getRendererInfo();
            }
            return {
                engine: 'WebGL',
                isPreferred: true,
                isFallback: false,
                fps: 0,
                gpuVendor: 'Unknown',
                gpuRenderer: 'Unknown'
            };
        },

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

        // Projection
        setProjection: (projectionType, options) => {
            try {
                const projection = projectionType.toLowerCase();

                if (!['mercator', 'globe'].includes(projection)) {
                    console.error('Invalid projection type:', projection);
                    return false;
                }

                const projectionConfig = {
                    type: projection
                };

                // Add globe-specific options
                if (projection === 'globe') {
                    projectionConfig.atmosphere = options?.enableAtmosphere !== false;
                    projectionConfig.atmosphereColor = options?.atmosphereColor || '#87CEEB';
                    projectionConfig.space = options?.enableSpace !== false;
                }

                map.setProjection(projectionConfig);

                // Auto-adjust camera when switching to globe
                if (projection === 'globe' && options?.autoAdjustCamera !== false) {
                    const currentZoom = map.getZoom();
                    const targetZoom = options?.globeDefaultZoom !== undefined ? options.globeDefaultZoom : 1.5;

                    if (currentZoom > targetZoom) {
                        map.easeTo({
                            zoom: targetZoom,
                            duration: options?.transitionDuration || 1000,
                            easing: (t) => t * (2 - t)
                        });
                    }
                }

                console.log('Projection changed to:', projection);
                return true;
            } catch (error) {
                console.error('Error setting projection:', error);
                return false;
            }
        },

        getProjection: () => {
            try {
                const style = map.getStyle();
                return style?.projection?.type || 'mercator';
            } catch (error) {
                console.error('Error getting projection:', error);
                return 'mercator';
            }
        },

        // Cleanup
        dispose: () => {
            if (map._rendererManager) {
                map._rendererManager.stopMonitoring();
            }
            map.remove();
        },

        // Direct map access (for advanced use)
        _getMapInstance: () => map
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
