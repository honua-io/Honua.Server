// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Manages preview layers for spatial analysis operations.
 * Provides real-time visualization of operation results before execution.
 */

const previewLayers = new Map();
const previewStyles = {
    buffer: {
        fillColor: '#3B82F6',
        fillOpacity: 0.25,
        strokeColor: '#2563EB',
        strokeWidth: 2,
        strokeDashArray: [5, 5]
    },
    clip: {
        fillColor: '#8B5CF6',
        fillOpacity: 0.25,
        strokeColor: '#7C3AED',
        strokeWidth: 2,
        strokeDashArray: [5, 5]
    },
    intersect: {
        fillColor: '#10B981',
        fillOpacity: 0.25,
        strokeColor: '#059669',
        strokeWidth: 2,
        strokeDashArray: [5, 5]
    },
    dissolve: {
        fillColor: '#F59E0B',
        fillOpacity: 0.25,
        strokeColor: '#D97706',
        strokeWidth: 2,
        strokeDashArray: [5, 5]
    },
    default: {
        fillColor: '#6B7280',
        fillOpacity: 0.25,
        strokeColor: '#4B5563',
        strokeWidth: 2,
        strokeDashArray: [5, 5]
    }
};

/**
 * Loads a preview from the API and displays it on the map.
 * @param {string} mapViewId - Map view ID
 * @param {string} url - Preview endpoint URL
 * @param {object} parameters - Process parameters
 * @returns {Promise<object>} Preview result with metadata
 */
export async function loadPreview(mapViewId, url, parameters) {
    const mapView = window.honuaMapViews?.get(mapViewId);
    if (!mapView) {
        throw new Error(`Map view ${mapViewId} not found`);
    }

    // Remove existing preview layer if any
    const existingLayerId = `preview-${mapViewId}`;
    if (previewLayers.has(existingLayerId)) {
        await clearPreview(mapViewId, existingLayerId);
    }

    try {
        // Fetch preview data
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ inputs: parameters })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.detail || 'Preview request failed');
        }

        const data = await response.json();

        // Determine operation type for styling
        const operationType = detectOperationType(parameters);
        const style = previewStyles[operationType] || previewStyles.default;

        // Create preview layer
        const layerId = await createPreviewLayer(mapView, data, style);
        previewLayers.set(existingLayerId, layerId);

        // Add preview label
        if (data.metadata?.message) {
            addPreviewLabel(mapView, data.metadata.message);
        }

        return {
            layerId: existingLayerId,
            metadata: data.metadata,
            data: data
        };
    } catch (error) {
        console.error('Preview load error:', error);
        throw error;
    }
}

/**
 * Loads a streaming preview (for large datasets).
 * @param {string} mapViewId - Map view ID
 * @param {string} url - Streaming preview endpoint URL
 * @param {object} parameters - Process parameters
 * @param {function} onProgress - Progress callback
 * @returns {Promise<object>} Preview result
 */
export async function loadStreamingPreview(mapViewId, url, parameters, onProgress) {
    const mapView = window.honuaMapViews?.get(mapViewId);
    if (!mapView) {
        throw new Error(`Map view ${mapViewId} not found`);
    }

    const existingLayerId = `preview-${mapViewId}`;
    if (previewLayers.has(existingLayerId)) {
        await clearPreview(mapViewId, existingLayerId);
    }

    const response = await fetch(url + '&stream=true', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ inputs: parameters })
    });

    if (!response.ok) {
        throw new Error('Streaming preview request failed');
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let features = [];
    let metadata = null;

    const operationType = detectOperationType(parameters);
    const style = previewStyles[operationType] || previewStyles.default;

    try {
        while (true) {
            const { done, value } = await reader.read();

            if (done) break;

            buffer += decoder.decode(value, { stream: true });

            // Parse complete features from buffer
            const lines = buffer.split('\n');
            buffer = lines.pop() || ''; // Keep incomplete line in buffer

            for (const line of lines) {
                if (line.trim()) {
                    try {
                        const data = JSON.parse(line);
                        if (data.type === 'Feature') {
                            features.push(data);

                            // Update layer with new features
                            if (features.length % 10 === 0) { // Update every 10 features
                                await updatePreviewLayer(mapView, existingLayerId, features, style);
                                if (onProgress) {
                                    onProgress({ featuresLoaded: features.length });
                                }
                            }
                        } else if (data.metadata) {
                            metadata = data.metadata;
                        }
                    } catch (e) {
                        console.warn('Failed to parse streaming data:', e);
                    }
                }
            }
        }

        // Final update with all features
        const layerId = await createPreviewLayer(mapView,
            { type: 'FeatureCollection', features },
            style);
        previewLayers.set(existingLayerId, layerId);

        return {
            layerId: existingLayerId,
            metadata,
            featureCount: features.length
        };
    } finally {
        reader.releaseLock();
    }
}

/**
 * Creates a preview layer on the map.
 * @param {object} mapView - Map view instance
 * @param {object} geoJson - GeoJSON data
 * @param {object} style - Layer style
 * @returns {Promise<string>} Layer ID
 */
async function createPreviewLayer(mapView, geoJson, style) {
    // Check if using Leaflet
    if (mapView._leaflet) {
        const layer = L.geoJSON(geoJson, {
            style: {
                color: style.strokeColor,
                weight: style.strokeWidth,
                opacity: 1,
                fillColor: style.fillColor,
                fillOpacity: style.fillOpacity,
                dashArray: style.strokeDashArray.join(',')
            },
            pointToLayer: (feature, latlng) => {
                return L.circleMarker(latlng, {
                    radius: 6,
                    fillColor: style.fillColor,
                    color: style.strokeColor,
                    weight: style.strokeWidth,
                    opacity: 1,
                    fillOpacity: style.fillOpacity
                });
            }
        });

        layer.addTo(mapView._leaflet);

        // Fit bounds to preview layer
        if (layer.getBounds && layer.getBounds().isValid()) {
            mapView._leaflet.fitBounds(layer.getBounds(), { padding: [50, 50] });
        }

        return layer._leaflet_id.toString();
    }

    // Check if using MapLibre/Mapbox
    else if (mapView.map) {
        const sourceId = `preview-source-${Date.now()}`;
        const layerId = `preview-layer-${Date.now()}`;

        mapView.map.addSource(sourceId, {
            type: 'geojson',
            data: geoJson
        });

        // Add fill layer
        mapView.map.addLayer({
            id: `${layerId}-fill`,
            type: 'fill',
            source: sourceId,
            paint: {
                'fill-color': style.fillColor,
                'fill-opacity': style.fillOpacity
            }
        });

        // Add line layer
        mapView.map.addLayer({
            id: `${layerId}-line`,
            type: 'line',
            source: sourceId,
            paint: {
                'line-color': style.strokeColor,
                'line-width': style.strokeWidth,
                'line-dasharray': style.strokeDashArray
            }
        });

        // Fit bounds
        const bounds = turf.bbox(geoJson);
        mapView.map.fitBounds(bounds, { padding: 50 });

        return layerId;
    }

    throw new Error('Unsupported map library');
}

/**
 * Updates an existing preview layer with new features.
 */
async function updatePreviewLayer(mapView, layerId, features, style) {
    const geoJson = { type: 'FeatureCollection', features };

    if (mapView._leaflet && previewLayers.has(layerId)) {
        const existingLayer = mapView._leaflet._layers[previewLayers.get(layerId)];
        if (existingLayer) {
            existingLayer.clearLayers();
            existingLayer.addData(geoJson);
        }
    } else if (mapView.map) {
        const sourceId = `preview-source-${layerId}`;
        const source = mapView.map.getSource(sourceId);
        if (source) {
            source.setData(geoJson);
        }
    }
}

/**
 * Adds a label to the map showing preview information.
 */
function addPreviewLabel(mapView, message) {
    if (mapView._leaflet) {
        const control = L.control({ position: 'topright' });
        control.onAdd = function() {
            const div = L.DomUtil.create('div', 'preview-label');
            div.innerHTML = `
                <div style="background: rgba(59, 130, 246, 0.9); color: white; padding: 8px 12px;
                            border-radius: 4px; font-size: 13px; box-shadow: 0 2px 4px rgba(0,0,0,0.2);">
                    <strong>Preview Mode</strong><br>
                    ${message}
                </div>
            `;
            return div;
        };
        control.addTo(mapView._leaflet);
    }
}

/**
 * Clears a preview layer from the map.
 * @param {string} mapViewId - Map view ID
 * @param {string} layerId - Preview layer ID
 */
export async function clearPreview(mapViewId, layerId) {
    const mapView = window.honuaMapViews?.get(mapViewId);
    if (!mapView) return;

    const actualLayerId = previewLayers.get(layerId);
    if (!actualLayerId) return;

    if (mapView._leaflet) {
        const layer = mapView._leaflet._layers[actualLayerId];
        if (layer) {
            mapView._leaflet.removeLayer(layer);
        }
    } else if (mapView.map) {
        const fillLayerId = `${actualLayerId}-fill`;
        const lineLayerId = `${actualLayerId}-line`;
        const sourceId = `preview-source-${actualLayerId}`;

        if (mapView.map.getLayer(fillLayerId)) {
            mapView.map.removeLayer(fillLayerId);
        }
        if (mapView.map.getLayer(lineLayerId)) {
            mapView.map.removeLayer(lineLayerId);
        }
        if (mapView.map.getSource(sourceId)) {
            mapView.map.removeSource(sourceId);
        }
    }

    previewLayers.delete(layerId);
}

/**
 * Detects the operation type from parameters.
 */
function detectOperationType(parameters) {
    if (parameters.distance !== undefined) return 'buffer';
    if (parameters.clipGeometry !== undefined) return 'clip';
    if (parameters.dissolveField !== undefined) return 'dissolve';
    if (parameters.intersectGeometry !== undefined) return 'intersect';
    return 'default';
}

/**
 * Gets the current preview style for an operation type.
 */
export function getPreviewStyle(operationType) {
    return previewStyles[operationType] || previewStyles.default;
}

/**
 * Updates preview styles.
 */
export function setPreviewStyle(operationType, style) {
    previewStyles[operationType] = { ...previewStyles[operationType], ...style };
}
