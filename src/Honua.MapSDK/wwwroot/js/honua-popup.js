// HonuaPopup - Feature info popup/tooltip for Honua.MapSDK
// Provides click/hover popups with customizable templates

const popupInstances = new Map();
const popupConfigs = new Map();
const dotNetRefs = new Map();
const currentPopups = new Map();

/**
 * Initialize popup functionality on a map
 */
export function initializePopup(mapId, componentId, config, dotNetRef) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return;
        }

        // Store DotNet reference and config
        dotNetRefs.set(componentId, dotNetRef);
        popupConfigs.set(componentId, config);

        // Create MapLibre popup instance
        const popup = new maplibregl.Popup({
            closeButton: false,
            closeOnClick: false,
            maxWidth: `${config.maxWidth}px`,
            className: 'honua-map-popup'
        });

        // Store popup instance
        popupInstances.set(componentId, { popup, mapId, mapInstance });

        // Setup event listeners based on trigger mode
        setupPopupEventListeners(mapInstance, popup, componentId, config, dotNetRef);

        console.log(`Popup initialized for component: ${componentId}`);
    } catch (error) {
        console.error('Error initializing popup:', error);
    }
}

/**
 * Setup event listeners for popup triggers
 */
function setupPopupEventListeners(mapInstance, popup, componentId, config, dotNetRef) {
    const { triggerMode, queryLayers } = config;

    if (triggerMode === 'click') {
        // Handle click events
        mapInstance.on('click', async (e) => {
            try {
                const features = queryFeaturesAtPoint(mapInstance, e.point, queryLayers);

                if (features.length > 0) {
                    // Pass features to Blazor component
                    const featuresJson = JSON.stringify(features.map(f => ({
                        id: f.id,
                        layer: f.layer.id,
                        properties: f.properties,
                        geometry: f.geometry
                    })));

                    await dotNetRef.invokeMethodAsync('OnFeaturesQueriedFromJS', featuresJson);
                } else {
                    // Clicked on empty area
                    await dotNetRef.invokeMethodAsync('OnMapClickedFromJS');
                }
            } catch (error) {
                console.error('Error handling map click:', error);
            }
        });

        // Change cursor on hover
        mapInstance.on('mousemove', (e) => {
            const features = queryFeaturesAtPoint(mapInstance, e.point, queryLayers);
            mapInstance.getCanvas().style.cursor = features.length > 0 ? 'pointer' : '';
        });
    } else if (triggerMode === 'hover') {
        // Handle hover events
        let hoveredFeatureId = null;

        mapInstance.on('mousemove', async (e) => {
            const features = queryFeaturesAtPoint(mapInstance, e.point, queryLayers);

            if (features.length > 0) {
                const feature = features[0];

                if (hoveredFeatureId !== feature.id) {
                    hoveredFeatureId = feature.id;

                    const featuresJson = JSON.stringify([{
                        id: feature.id,
                        layer: feature.layer.id,
                        properties: feature.properties,
                        geometry: feature.geometry
                    }]);

                    await dotNetRef.invokeMethodAsync('OnFeaturesQueriedFromJS', featuresJson);
                }

                mapInstance.getCanvas().style.cursor = 'pointer';
            } else {
                if (hoveredFeatureId !== null) {
                    hoveredFeatureId = null;
                    await dotNetRef.invokeMethodAsync('OnMapClickedFromJS');
                }
                mapInstance.getCanvas().style.cursor = '';
            }
        });

        mapInstance.on('mouseleave', async () => {
            if (hoveredFeatureId !== null) {
                hoveredFeatureId = null;
                await dotNetRef.invokeMethodAsync('OnMapClickedFromJS');
            }
            mapInstance.getCanvas().style.cursor = '';
        });
    }
}

/**
 * Query features at a point
 */
function queryFeaturesAtPoint(mapInstance, point, queryLayers) {
    const features = mapInstance.queryRenderedFeatures(point, {
        layers: queryLayers || undefined
    });

    // Filter out non-interactive features
    return features.filter(f => {
        // Exclude base layers, background, etc.
        const layerId = f.layer.id;
        return !layerId.startsWith('background') &&
               !layerId.startsWith('base-') &&
               !layerId.includes('hillshade') &&
               !layerId.includes('terrain');
    });
}

/**
 * Open popup at coordinates
 */
export function openPopup(mapId, lngLat) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = popupInstances.get(componentId);
    if (!instance) return;

    try {
        const { popup, mapInstance } = instance;

        // Create dummy content (will be replaced by Blazor component)
        popup.setLngLat(lngLat)
            .setHTML('<div class="popup-placeholder">Loading...</div>')
            .addTo(mapInstance);

        currentPopups.set(mapId, { lngLat, timestamp: Date.now() });
    } catch (error) {
        console.error('Error opening popup:', error);
    }
}

/**
 * Close popup
 */
export function closePopup(mapId) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = popupInstances.get(componentId);
    if (!instance) return;

    try {
        const { popup } = instance;
        popup.remove();
        currentPopups.delete(mapId);
    } catch (error) {
        console.error('Error closing popup:', error);
    }
}

/**
 * Update popup content
 */
export function updatePopupContent(mapId, content) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = popupInstances.get(componentId);
    if (!instance) return;

    try {
        const { popup } = instance;
        popup.setHTML(content);
    } catch (error) {
        console.error('Error updating popup content:', error);
    }
}

/**
 * Pan map to keep popup visible
 */
export function panToPopup(mapId, lngLat) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = popupInstances.get(componentId);
    if (!instance) return;

    try {
        const { mapInstance } = instance;

        // Get popup size and position
        const canvas = mapInstance.getCanvas();
        const canvasRect = canvas.getBoundingClientRect();
        const point = mapInstance.project(lngLat);

        // Calculate if popup would be outside viewport
        const popupWidth = 400; // default max width
        const popupHeight = 300; // estimated height
        const padding = 50;

        let needsPan = false;
        let offsetX = 0;
        let offsetY = 0;

        if (point.x + popupWidth + padding > canvasRect.width) {
            offsetX = (point.x + popupWidth + padding) - canvasRect.width;
            needsPan = true;
        }

        if (point.x - padding < 0) {
            offsetX = point.x - padding;
            needsPan = true;
        }

        if (point.y - popupHeight - padding < 0) {
            offsetY = point.y - popupHeight - padding;
            needsPan = true;
        }

        if (needsPan) {
            const center = mapInstance.getCenter();
            const offsetLngLat = mapInstance.unproject([
                mapInstance.project(center).x + offsetX,
                mapInstance.project(center).y + offsetY
            ]);

            mapInstance.easeTo({
                center: offsetLngLat,
                duration: 300
            });
        }
    } catch (error) {
        console.error('Error panning to popup:', error);
    }
}

/**
 * Get feature coordinates from geometry
 */
export function getFeatureCoordinates(mapId, featureId, layerId) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) return null;

        // Query the feature
        const features = mapInstance.querySourceFeatures(layerId, {
            filter: ['==', ['id'], featureId]
        });

        if (features.length === 0) {
            // Try querying rendered features
            const renderedFeatures = mapInstance.queryRenderedFeatures({
                layers: [layerId],
                filter: ['==', ['id'], featureId]
            });

            if (renderedFeatures.length > 0) {
                return getGeometryCenter(renderedFeatures[0].geometry);
            }
            return null;
        }

        const feature = features[0];
        return getGeometryCenter(feature.geometry);
    } catch (error) {
        console.error('Error getting feature coordinates:', error);
        return null;
    }
}

/**
 * Get center point of geometry
 */
function getGeometryCenter(geometry) {
    if (!geometry) return null;

    switch (geometry.type) {
        case 'Point':
            return geometry.coordinates;

        case 'LineString':
            // Return midpoint
            const coords = geometry.coordinates;
            const midIndex = Math.floor(coords.length / 2);
            return coords[midIndex];

        case 'Polygon':
            // Calculate centroid
            const polygon = geometry.coordinates[0];
            let x = 0, y = 0;
            for (const coord of polygon) {
                x += coord[0];
                y += coord[1];
            }
            return [x / polygon.length, y / polygon.length];

        case 'MultiPoint':
            return geometry.coordinates[0];

        case 'MultiLineString':
            return geometry.coordinates[0][0];

        case 'MultiPolygon':
            const firstPolygon = geometry.coordinates[0][0];
            let mx = 0, my = 0;
            for (const coord of firstPolygon) {
                mx += coord[0];
                my += coord[1];
            }
            return [mx / firstPolygon.length, my / firstPolygon.length];

        default:
            return null;
    }
}

/**
 * Highlight a feature on the map
 */
export function highlightFeature(mapId, featureId, layerId) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) return;

        // Add or update highlight layer
        const highlightLayerId = 'popup-highlight';

        if (mapInstance.getLayer(highlightLayerId)) {
            mapInstance.removeLayer(highlightLayerId);
        }

        if (mapInstance.getSource('popup-highlight-source')) {
            mapInstance.removeSource('popup-highlight-source');
        }

        // Query the feature
        const features = mapInstance.querySourceFeatures(layerId);
        const feature = features.find(f => f.id === featureId);

        if (feature) {
            mapInstance.addSource('popup-highlight-source', {
                type: 'geojson',
                data: {
                    type: 'FeatureCollection',
                    features: [feature]
                }
            });

            const geometryType = feature.geometry.type;

            if (geometryType.includes('Polygon')) {
                mapInstance.addLayer({
                    id: highlightLayerId,
                    type: 'line',
                    source: 'popup-highlight-source',
                    paint: {
                        'line-color': '#ffeb3b',
                        'line-width': 3,
                        'line-opacity': 0.8
                    }
                });
            } else if (geometryType.includes('LineString')) {
                mapInstance.addLayer({
                    id: highlightLayerId,
                    type: 'line',
                    source: 'popup-highlight-source',
                    paint: {
                        'line-color': '#ffeb3b',
                        'line-width': 4,
                        'line-opacity': 0.8
                    }
                });
            } else if (geometryType.includes('Point')) {
                mapInstance.addLayer({
                    id: highlightLayerId,
                    type: 'circle',
                    source: 'popup-highlight-source',
                    paint: {
                        'circle-radius': 10,
                        'circle-color': '#ffeb3b',
                        'circle-opacity': 0.3,
                        'circle-stroke-width': 2,
                        'circle-stroke-color': '#ffeb3b',
                        'circle-stroke-opacity': 0.8
                    }
                });
            }
        }
    } catch (error) {
        console.error('Error highlighting feature:', error);
    }
}

/**
 * Clear feature highlight
 */
export function clearHighlight(mapId) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) return;

        const highlightLayerId = 'popup-highlight';

        if (mapInstance.getLayer(highlightLayerId)) {
            mapInstance.removeLayer(highlightLayerId);
        }

        if (mapInstance.getSource('popup-highlight-source')) {
            mapInstance.removeSource('popup-highlight-source');
        }
    } catch (error) {
        console.error('Error clearing highlight:', error);
    }
}

/**
 * Copy text to clipboard
 */
export async function copyToClipboard(text) {
    try {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(text);
        } else {
            // Fallback for older browsers
            const textArea = document.createElement('textarea');
            textArea.value = text;
            textArea.style.position = 'fixed';
            textArea.style.left = '-999999px';
            document.body.appendChild(textArea);
            textArea.select();
            document.execCommand('copy');
            document.body.removeChild(textArea);
        }
        console.log('Copied to clipboard:', text);
    } catch (error) {
        console.error('Error copying to clipboard:', error);
    }
}

/**
 * Query features at point with layers filter
 */
export function queryFeaturesAtPointWithLayers(mapId, point, layers) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) return [];

        const features = queryFeaturesAtPoint(mapInstance, point, layers);

        return features.map(f => ({
            id: f.id,
            layer: f.layer.id,
            properties: f.properties,
            geometry: f.geometry
        }));
    } catch (error) {
        console.error('Error querying features:', error);
        return [];
    }
}

/**
 * Helper to get component ID for a map
 */
function getComponentIdForMap(mapId) {
    for (const [componentId, instance] of popupInstances.entries()) {
        if (instance.mapId === mapId) {
            return componentId;
        }
    }
    return null;
}

/**
 * Cleanup popup instance
 */
export function cleanup(mapId) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    try {
        const instance = popupInstances.get(componentId);
        if (instance) {
            const { popup } = instance;
            popup.remove();
        }

        popupInstances.delete(componentId);
        popupConfigs.delete(componentId);
        dotNetRefs.delete(componentId);
        currentPopups.delete(mapId);

        // Clear highlights
        clearHighlight(mapId);

        console.log(`Popup cleaned up for component: ${componentId}`);
    } catch (error) {
        console.error('Error cleaning up popup:', error);
    }
}
