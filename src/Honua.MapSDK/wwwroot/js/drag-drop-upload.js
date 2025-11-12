// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Drag and drop upload functionality
 */

export function initializeDragDrop(dropZoneElement, dotNetHelper) {
    if (!dropZoneElement) {
        console.warn('Drop zone element not found');
        return;
    }

    // Prevent default drag behaviors on document
    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
        document.body.addEventListener(eventName, preventDefaults, false);
    });

    function preventDefaults(e) {
        e.preventDefault();
        e.stopPropagation();
    }

    // Handle drag events on drop zone
    dropZoneElement.addEventListener('dragenter', handleDragEnter, false);
    dropZoneElement.addEventListener('dragover', handleDragOver, false);
    dropZoneElement.addEventListener('dragleave', handleDragLeave, false);
    dropZoneElement.addEventListener('drop', handleDrop, false);

    function handleDragEnter(e) {
        preventDefaults(e);
        if (dotNetHelper) {
            dotNetHelper.invokeMethodAsync('HandleDragEnter');
        }
    }

    function handleDragOver(e) {
        preventDefaults(e);
        e.dataTransfer.dropEffect = 'copy';
    }

    function handleDragLeave(e) {
        preventDefaults(e);
        // Only trigger if leaving the drop zone itself
        if (e.target === dropZoneElement) {
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('HandleDragLeave');
            }
        }
    }

    function handleDrop(e) {
        preventDefaults(e);

        if (dotNetHelper) {
            dotNetHelper.invokeMethodAsync('HandleDragLeave');
        }

        const files = e.dataTransfer.files;
        if (files.length > 0) {
            // Trigger the InputFile component
            const inputFile = dropZoneElement.parentElement.querySelector('input[type="file"]');
            if (inputFile) {
                // Create a new DataTransfer object and add the dropped files
                const dataTransfer = new DataTransfer();
                for (let i = 0; i < files.length; i++) {
                    dataTransfer.items.add(files[i]);
                }
                inputFile.files = dataTransfer.files;

                // Trigger change event
                const event = new Event('change', { bubbles: true });
                inputFile.dispatchEvent(event);
            }
        }
    }

    // Return cleanup function
    return {
        dispose: function () {
            ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
                document.body.removeEventListener(eventName, preventDefaults, false);
                dropZoneElement.removeEventListener(eventName, preventDefaults, false);
            });
        }
    };
}

/**
 * Visualize GeoJSON data on a map
 */
export function visualizeGeoJSON(mapId, geojsonData, style, options) {
    const map = window.honuaMaps?.[mapId];
    if (!map) {
        console.error('Map not found:', mapId);
        return null;
    }

    try {
        const sourceId = options?.sourceId || `upload-${Date.now()}`;
        const layerId = options?.layerId || `upload-layer-${Date.now()}`;

        // Add source
        if (!map.getSource(sourceId)) {
            map.addSource(sourceId, {
                type: 'geojson',
                data: geojsonData
            });
        } else {
            map.getSource(sourceId).setData(geojsonData);
        }

        // Determine geometry type
        const geometryType = detectGeometryType(geojsonData);

        // Add layers based on geometry type and style
        const layers = [];

        if (geometryType === 'Point' || geometryType === 'mixed') {
            // Add point layer
            const pointLayerId = `${layerId}-points`;
            if (!map.getLayer(pointLayerId)) {
                map.addLayer({
                    id: pointLayerId,
                    type: 'circle',
                    source: sourceId,
                    filter: ['==', ['geometry-type'], 'Point'],
                    paint: {
                        'circle-radius': style?.pointStyle?.radius || 6,
                        'circle-color': style?.pointStyle?.fillColor || '#3b82f6',
                        'circle-opacity': style?.pointStyle?.fillOpacity || 0.8,
                        'circle-stroke-width': style?.pointStyle?.strokeWidth || 1,
                        'circle-stroke-color': style?.pointStyle?.strokeColor || '#2563eb',
                        'circle-stroke-opacity': style?.pointStyle?.strokeOpacity || 1
                    }
                });
                layers.push(pointLayerId);
            }
        }

        if (geometryType === 'LineString' || geometryType === 'mixed') {
            // Add line layer
            const lineLayerId = `${layerId}-lines`;
            if (!map.getLayer(lineLayerId)) {
                map.addLayer({
                    id: lineLayerId,
                    type: 'line',
                    source: sourceId,
                    filter: ['==', ['geometry-type'], 'LineString'],
                    layout: {
                        'line-cap': style?.lineStyle?.lineCap || 'round',
                        'line-join': style?.lineStyle?.lineJoin || 'round'
                    },
                    paint: {
                        'line-color': style?.lineStyle?.color || '#3b82f6',
                        'line-width': style?.lineStyle?.width || 3,
                        'line-opacity': style?.lineStyle?.opacity || 0.8
                    }
                });
                layers.push(lineLayerId);
            }
        }

        if (geometryType === 'Polygon' || geometryType === 'mixed') {
            // Add polygon fill layer
            const fillLayerId = `${layerId}-fill`;
            if (!map.getLayer(fillLayerId)) {
                map.addLayer({
                    id: fillLayerId,
                    type: 'fill',
                    source: sourceId,
                    filter: ['==', ['geometry-type'], 'Polygon'],
                    paint: {
                        'fill-color': style?.polygonStyle?.fillColor || '#3b82f6',
                        'fill-opacity': style?.polygonStyle?.fillOpacity || 0.4
                    }
                });
                layers.push(fillLayerId);
            }

            // Add polygon outline layer
            const outlineLayerId = `${layerId}-outline`;
            if (!map.getLayer(outlineLayerId)) {
                map.addLayer({
                    id: outlineLayerId,
                    type: 'line',
                    source: sourceId,
                    filter: ['==', ['geometry-type'], 'Polygon'],
                    paint: {
                        'line-color': style?.polygonStyle?.strokeColor || '#2563eb',
                        'line-width': style?.polygonStyle?.strokeWidth || 2,
                        'line-opacity': style?.polygonStyle?.strokeOpacity || 0.8
                    }
                });
                layers.push(outlineLayerId);
            }
        }

        // Fit map to data bounds if requested
        if (options?.autoZoom !== false) {
            const bounds = calculateBounds(geojsonData);
            if (bounds) {
                map.fitBounds(bounds, {
                    padding: 50,
                    maxZoom: 16,
                    duration: 1000
                });
            }
        }

        // Add popup on click if template provided
        if (style?.popupTemplate) {
            layers.forEach(layerId => {
                map.on('click', layerId, (e) => {
                    const coordinates = e.features[0].geometry.coordinates.slice();
                    const properties = e.features[0].properties;

                    // Replace template variables
                    let content = style.popupTemplate;
                    for (const [key, value] of Object.entries(properties)) {
                        content = content.replace(new RegExp(`{{${key}}}`, 'g'), value || 'N/A');
                    }

                    new maplibregl.Popup()
                        .setLngLat(e.lngLat)
                        .setHTML(content)
                        .addTo(map);
                });

                map.on('mouseenter', layerId, () => {
                    map.getCanvas().style.cursor = 'pointer';
                });

                map.on('mouseleave', layerId, () => {
                    map.getCanvas().style.cursor = '';
                });
            });
        }

        return {
            sourceId,
            layers,
            remove: function () {
                layers.forEach(layerId => {
                    if (map.getLayer(layerId)) {
                        map.removeLayer(layerId);
                    }
                });
                if (map.getSource(sourceId)) {
                    map.removeSource(sourceId);
                }
            }
        };

    } catch (error) {
        console.error('Error visualizing GeoJSON:', error);
        return null;
    }
}

function detectGeometryType(geojsonData) {
    if (!geojsonData || !geojsonData.features) {
        return 'unknown';
    }

    const types = new Set();
    geojsonData.features.forEach(feature => {
        if (feature.geometry && feature.geometry.type) {
            let type = feature.geometry.type;
            // Normalize multi-geometries
            if (type.startsWith('Multi')) {
                type = type.substring(5);
            }
            types.add(type);
        }
    });

    if (types.size === 1) {
        return Array.from(types)[0];
    } else if (types.size > 1) {
        return 'mixed';
    }

    return 'unknown';
}

function calculateBounds(geojsonData) {
    if (!geojsonData || !geojsonData.features || geojsonData.features.length === 0) {
        return null;
    }

    let minLng = Infinity, minLat = Infinity;
    let maxLng = -Infinity, maxLat = -Infinity;

    function processCoordinates(coords) {
        if (typeof coords[0] === 'number') {
            // Single coordinate pair
            minLng = Math.min(minLng, coords[0]);
            maxLng = Math.max(maxLng, coords[0]);
            minLat = Math.min(minLat, coords[1]);
            maxLat = Math.max(maxLat, coords[1]);
        } else {
            // Nested coordinates
            coords.forEach(processCoordinates);
        }
    }

    geojsonData.features.forEach(feature => {
        if (feature.geometry && feature.geometry.coordinates) {
            processCoordinates(feature.geometry.coordinates);
        }
    });

    if (minLng === Infinity) {
        return null;
    }

    return [[minLng, minLat], [maxLng, maxLat]];
}

// Initialize global maps registry if not exists
if (!window.honuaMaps) {
    window.honuaMaps = {};
}
