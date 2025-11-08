// HonuaDraw - Drawing and measurement tools for Honua.MapSDK
// Requires: MapboxGL Draw, Turf.js

import MapboxDraw from '@mapbox/mapbox-gl-draw';
import * as turf from '@turf/turf';

const drawInstances = new Map();
const drawConfigs = new Map();
const dotNetRefs = new Map();

/**
 * Initialize drawing on a map
 */
export function initializeDrawing(mapId, componentId, config, dotNetRef) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return;
        }

        // Store DotNet reference
        dotNetRefs.set(componentId, dotNetRef);
        drawConfigs.set(componentId, config);

        // Create MapboxGL Draw instance
        const draw = new MapboxDraw({
            displayControlsDefault: false,
            controls: {},
            styles: getDrawStyles(config),
            modes: {
                ...MapboxDraw.modes,
                draw_circle: CircleMode,
                draw_rectangle: RectangleMode,
                draw_freehand: FreehandMode
            }
        });

        // Add draw control to map
        mapInstance.addControl(draw, 'top-left');

        // Store draw instance
        drawInstances.set(componentId, { draw, mapId, mapInstance });

        // Setup event listeners
        setupDrawEventListeners(mapInstance, draw, componentId, config);

        console.log(`Drawing initialized for component: ${componentId}`);
    } catch (error) {
        console.error('Error initializing drawing:', error);
    }
}

/**
 * Set drawing mode
 */
export function setDrawMode(mapId, mode) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = drawInstances.get(componentId);
    if (!instance) return;

    try {
        const { draw } = instance;

        if (!mode || mode === 'none') {
            draw.changeMode('simple_select');
        } else {
            draw.changeMode(mode);
        }
    } catch (error) {
        console.error('Error setting draw mode:', error);
    }
}

/**
 * Select a feature
 */
export function selectFeature(mapId, featureId) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = drawInstances.get(componentId);
    if (!instance) return;

    try {
        const { draw } = instance;

        // Deselect all first
        const selected = draw.getSelected();
        if (selected.features.length > 0) {
            draw.changeMode('simple_select');
        }

        // Select the feature
        draw.changeMode('simple_select', { featureIds: [featureId] });
    } catch (error) {
        console.error('Error selecting feature:', error);
    }
}

/**
 * Delete a feature
 */
export function deleteFeature(mapId, featureId) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = drawInstances.get(componentId);
    if (!instance) return;

    try {
        const { draw } = instance;
        draw.delete(featureId);
    } catch (error) {
        console.error('Error deleting feature:', error);
    }
}

/**
 * Set feature visibility
 */
export function setFeatureVisibility(mapId, featureId, visible) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = drawInstances.get(componentId);
    if (!instance) return;

    try {
        const { draw } = instance;
        const feature = draw.get(featureId);

        if (feature) {
            // Update feature properties to control visibility
            feature.properties = feature.properties || {};
            feature.properties.visible = visible;
            draw.add(feature);
        }
    } catch (error) {
        console.error('Error setting feature visibility:', error);
    }
}

/**
 * Clear all drawn features
 */
export function clearAll(mapId) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = drawInstances.get(componentId);
    if (!instance) return;

    try {
        const { draw } = instance;
        draw.deleteAll();
    } catch (error) {
        console.error('Error clearing features:', error);
    }
}

/**
 * Set features from JSON
 */
export function setFeatures(mapId, featuresJson) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = drawInstances.get(componentId);
    if (!instance) return;

    try {
        const { draw } = instance;
        const features = JSON.parse(featuresJson);

        draw.deleteAll();
        draw.add({
            type: 'FeatureCollection',
            features: features
        });
    } catch (error) {
        console.error('Error setting features:', error);
    }
}

/**
 * Set measurement unit
 */
export function setMeasurementUnit(mapId, unit) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const config = drawConfigs.get(componentId);
    if (config) {
        config.measurementUnit = unit;
    }
}

/**
 * Calculate measurements for a geometry
 */
export function calculateMeasurements(geometryJson, unit) {
    try {
        const geometry = JSON.parse(geometryJson);
        const measurements = {};

        switch (geometry.type) {
            case 'Point':
                measurements.coordinates = geometry.coordinates;
                break;

            case 'LineString':
                const line = turf.lineString(geometry.coordinates);
                measurements.distance = turf.length(line, { units: getUnits(unit) }) * getUnitMultiplier(unit);

                if (geometry.coordinates.length === 2) {
                    const bearing = turf.bearing(
                        turf.point(geometry.coordinates[0]),
                        turf.point(geometry.coordinates[1])
                    );
                    measurements.bearing = bearing;
                }
                break;

            case 'Polygon':
                const polygon = turf.polygon(geometry.coordinates);
                measurements.area = turf.area(polygon);

                const perimeter = turf.length(turf.polygonToLine(polygon), { units: getUnits(unit) }) * getUnitMultiplier(unit);
                measurements.perimeter = perimeter;
                break;

            case 'Circle':
                // Custom circle geometry
                if (geometry.properties?.radius) {
                    measurements.radius = geometry.properties.radius;
                    measurements.area = Math.PI * Math.pow(geometry.properties.radius, 2);
                }
                break;
        }

        return measurements;
    } catch (error) {
        console.error('Error calculating measurements:', error);
        return {};
    }
}

/**
 * Export features to various formats
 */
export async function exportFeatures(mapId, format) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = drawInstances.get(componentId);
    if (!instance) return;

    try {
        const { draw } = instance;
        const featureCollection = draw.getAll();

        let content, filename, mimeType;

        switch (format) {
            case 'geojson':
                content = JSON.stringify(featureCollection, null, 2);
                filename = 'drawn-features.geojson';
                mimeType = 'application/json';
                break;

            case 'csv':
                content = convertToCSV(featureCollection);
                filename = 'drawn-features.csv';
                mimeType = 'text/csv';
                break;

            case 'kml':
                content = convertToKML(featureCollection);
                filename = 'drawn-features.kml';
                mimeType = 'application/vnd.google-earth.kml+xml';
                break;

            default:
                console.error('Unknown export format:', format);
                return;
        }

        // Download file
        downloadFile(content, filename, mimeType);
    } catch (error) {
        console.error('Error exporting features:', error);
    }
}

/**
 * Setup event listeners for drawing
 */
function setupDrawEventListeners(map, draw, componentId, config) {
    const dotNetRef = dotNetRefs.get(componentId);
    if (!dotNetRef) return;

    // Feature created
    map.on('draw.create', async (e) => {
        try {
            for (const feature of e.features) {
                // Calculate measurements
                const measurements = calculateMeasurements(JSON.stringify(feature.geometry), config.measurementUnit);

                // Add measurements to feature properties
                feature.properties = {
                    ...feature.properties,
                    measurements: measurements
                };

                // Notify Blazor
                await dotNetRef.invokeMethodAsync('OnFeatureDrawnFromJS', JSON.stringify(feature));
            }
        } catch (error) {
            console.error('Error in draw.create handler:', error);
        }
    });

    // Feature updated
    map.on('draw.update', async (e) => {
        try {
            for (const feature of e.features) {
                await dotNetRef.invokeMethodAsync(
                    'OnFeatureEditedFromJS',
                    feature.id,
                    JSON.stringify(feature.geometry)
                );
            }
        } catch (error) {
            console.error('Error in draw.update handler:', error);
        }
    });

    // Selection changed
    map.on('draw.selectionchange', async (e) => {
        try {
            const selectedId = e.features.length > 0 ? e.features[0].id : null;
            await dotNetRef.invokeMethodAsync('OnFeatureSelectedFromJS', selectedId);
        } catch (error) {
            console.error('Error in draw.selectionchange handler:', error);
        }
    });

    // Live measurement updates while drawing
    map.on('draw.render', () => {
        try {
            const data = draw.getAll();
            if (data.features.length > 0) {
                const feature = data.features[data.features.length - 1];
                const measurements = calculateMeasurements(JSON.stringify(feature.geometry), config.measurementUnit);

                // Update live measurements
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync(
                        'OnMeasurementUpdate',
                        measurements.distance || null,
                        measurements.area || null,
                        measurements.perimeter || null,
                        measurements.radius || null,
                        measurements.bearing || null,
                        measurements.coordinates || null
                    ).catch(err => {
                        // Silently ignore errors during render events
                    });
                }
            }
        } catch (error) {
            // Silently ignore errors during render events
        }
    });
}

/**
 * Get component ID for a map
 */
function getComponentIdForMap(mapId) {
    for (const [componentId, instance] of drawInstances.entries()) {
        if (instance.mapId === mapId) {
            return componentId;
        }
    }
    return null;
}

/**
 * Get drawing styles
 */
function getDrawStyles(config) {
    return [
        // Line stroke
        {
            'id': 'gl-draw-line',
            'type': 'line',
            'filter': ['all', ['==', '$type', 'LineString'], ['!=', 'mode', 'static']],
            'layout': {
                'line-cap': 'round',
                'line-join': 'round'
            },
            'paint': {
                'line-color': config.strokeColor || '#3B82F6',
                'line-width': config.strokeWidth || 2,
                'line-opacity': config.strokeOpacity || 1
            }
        },
        // Polygon fill
        {
            'id': 'gl-draw-polygon-fill',
            'type': 'fill',
            'filter': ['all', ['==', '$type', 'Polygon'], ['!=', 'mode', 'static']],
            'paint': {
                'fill-color': config.fillColor || '#3B82F6',
                'fill-opacity': config.fillOpacity || 0.2
            }
        },
        // Polygon stroke
        {
            'id': 'gl-draw-polygon-stroke',
            'type': 'line',
            'filter': ['all', ['==', '$type', 'Polygon'], ['!=', 'mode', 'static']],
            'layout': {
                'line-cap': 'round',
                'line-join': 'round'
            },
            'paint': {
                'line-color': config.strokeColor || '#3B82F6',
                'line-width': config.strokeWidth || 2,
                'line-opacity': config.strokeOpacity || 1
            }
        },
        // Vertices
        {
            'id': 'gl-draw-polygon-and-line-vertex-halo-active',
            'type': 'circle',
            'filter': ['all', ['==', 'meta', 'vertex'], ['==', '$type', 'Point']],
            'paint': {
                'circle-radius': 7,
                'circle-color': '#FFF'
            }
        },
        {
            'id': 'gl-draw-polygon-and-line-vertex-active',
            'type': 'circle',
            'filter': ['all', ['==', 'meta', 'vertex'], ['==', '$type', 'Point']],
            'paint': {
                'circle-radius': 5,
                'circle-color': config.strokeColor || '#3B82F6'
            }
        },
        // Points
        {
            'id': 'gl-draw-point',
            'type': 'circle',
            'filter': ['all', ['==', '$type', 'Point'], ['!=', 'meta', 'vertex']],
            'paint': {
                'circle-radius': 6,
                'circle-color': config.strokeColor || '#3B82F6',
                'circle-stroke-color': '#FFF',
                'circle-stroke-width': 2
            }
        }
    ];
}

/**
 * Custom Circle drawing mode
 */
const CircleMode = {
    ...MapboxDraw.modes.draw_circle,
    // Implementation would go here
};

/**
 * Custom Rectangle drawing mode
 */
const RectangleMode = {
    ...MapboxDraw.modes.draw_rectangle,
    // Implementation would go here
};

/**
 * Custom Freehand drawing mode
 */
const FreehandMode = {
    ...MapboxDraw.modes.draw_line_string,
    // Implementation would go here
};

/**
 * Convert FeatureCollection to CSV
 */
function convertToCSV(featureCollection) {
    const headers = ['id', 'type', 'geometry', 'properties'];
    const rows = [headers.join(',')];

    for (const feature of featureCollection.features) {
        const row = [
            feature.id || '',
            feature.geometry.type,
            JSON.stringify(feature.geometry).replace(/"/g, '""'),
            JSON.stringify(feature.properties || {}).replace(/"/g, '""')
        ];
        rows.push(row.map(v => `"${v}"`).join(','));
    }

    return rows.join('\n');
}

/**
 * Convert FeatureCollection to KML
 */
function convertToKML(featureCollection) {
    const kml = ['<?xml version="1.0" encoding="UTF-8"?>'];
    kml.push('<kml xmlns="http://www.opengis.net/kml/2.2">');
    kml.push('<Document>');
    kml.push('<name>Drawn Features</name>');

    for (const feature of featureCollection.features) {
        kml.push('<Placemark>');

        if (feature.properties?.name) {
            kml.push(`<name>${escapeXml(feature.properties.name)}</name>`);
        }

        // Convert geometry to KML format
        switch (feature.geometry.type) {
            case 'Point':
                kml.push('<Point>');
                kml.push(`<coordinates>${feature.geometry.coordinates.join(',')}</coordinates>`);
                kml.push('</Point>');
                break;

            case 'LineString':
                kml.push('<LineString>');
                const lineCoords = feature.geometry.coordinates
                    .map(coord => coord.join(','))
                    .join(' ');
                kml.push(`<coordinates>${lineCoords}</coordinates>`);
                kml.push('</LineString>');
                break;

            case 'Polygon':
                kml.push('<Polygon>');
                kml.push('<outerBoundaryIs>');
                kml.push('<LinearRing>');
                const polyCoords = feature.geometry.coordinates[0]
                    .map(coord => coord.join(','))
                    .join(' ');
                kml.push(`<coordinates>${polyCoords}</coordinates>`);
                kml.push('</LinearRing>');
                kml.push('</outerBoundaryIs>');
                kml.push('</Polygon>');
                break;
        }

        kml.push('</Placemark>');
    }

    kml.push('</Document>');
    kml.push('</kml>');

    return kml.join('\n');
}

/**
 * Download file helper
 */
function downloadFile(content, filename, mimeType) {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
}

/**
 * Helper to escape XML special characters
 */
function escapeXml(text) {
    return text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&apos;');
}

/**
 * Get Turf.js units
 */
function getUnits(unit) {
    switch (unit) {
        case 'imperial':
            return 'miles';
        case 'nautical':
            return 'nauticalmiles';
        default:
            return 'kilometers';
    }
}

/**
 * Get unit multiplier for display
 */
function getUnitMultiplier(unit) {
    switch (unit) {
        case 'imperial':
            return 5280; // feet per mile
        case 'nautical':
            return 6076.12; // feet per nautical mile
        default:
            return 1000; // meters per kilometer
    }
}

/**
 * Cleanup drawing instance
 */
export function cleanup(mapId) {
    const componentId = getComponentIdForMap(mapId);
    if (!componentId) return;

    const instance = drawInstances.get(componentId);
    if (instance) {
        const { draw, mapInstance } = instance;

        try {
            mapInstance.removeControl(draw);
        } catch (error) {
            console.error('Error removing draw control:', error);
        }

        drawInstances.delete(componentId);
        drawConfigs.delete(componentId);
        dotNetRefs.delete(componentId);
    }
}

// Make functions available globally for debugging
if (typeof window !== 'undefined') {
    window.honuaDraw = {
        initializeDrawing,
        setDrawMode,
        selectFeature,
        deleteFeature,
        setFeatureVisibility,
        clearAll,
        setFeatures,
        setMeasurementUnit,
        calculateMeasurements,
        exportFeatures,
        cleanup
    };
}
