// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Honua Geoprocessing Module
 * Client-side spatial analysis using Turf.js
 * Provides high-performance geometric operations directly in the browser
 */

import * as turf from 'https://cdn.skypack.dev/@turf/turf@7.1.0';

// Store for geoprocessing results displayed on map
const geoprocessingLayers = new Map();
let mapInstance = null;

/**
 * Initialize geoprocessing for a map
 * @param {string} mapId - Map identifier
 * @param {object} options - Configuration options
 */
export function initializeGeoprocessing(mapId, options = {}) {
    mapInstance = window.honuaMaps?.get(mapId);
    if (!mapInstance) {
        console.warn(`Map ${mapId} not found for geoprocessing initialization`);
        return;
    }

    console.log(`Geoprocessing initialized for map ${mapId}`);
}

// ========== Geometric Operations ==========

/**
 * Creates a buffer around input features
 * @param {object} input - GeoJSON feature or geometry
 * @param {number} distance - Buffer distance
 * @param {string} units - Distance units (meters, kilometers, miles, etc.)
 * @returns {object} Buffered geometry as GeoJSON
 */
export function buffer(input, distance, units = 'meters') {
    const startTime = performance.now();
    try {
        const result = turf.buffer(input, distance, { units, steps: 64 });
        const executionTime = performance.now() - startTime;
        console.log(`Buffer operation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Buffer operation failed:', error);
        throw new Error(`Buffer failed: ${error.message}`);
    }
}

/**
 * Finds the geometric intersection of two layers
 * @param {object} layer1 - First GeoJSON layer
 * @param {object} layer2 - Second GeoJSON layer
 * @returns {object} Intersection result as GeoJSON
 */
export function intersect(layer1, layer2) {
    const startTime = performance.now();
    try {
        const result = turf.intersect(
            turf.featureCollection([ensureFeature(layer1), ensureFeature(layer2)])
        );
        const executionTime = performance.now() - startTime;
        console.log(`Intersect operation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Intersect operation failed:', error);
        throw new Error(`Intersect failed: ${error.message}`);
    }
}

/**
 * Combines multiple features into a single geometry
 * @param {Array<object>} layers - Array of GeoJSON features to union
 * @returns {object} Unioned geometry as GeoJSON
 */
export function union(layers) {
    const startTime = performance.now();
    try {
        if (!layers || layers.length === 0) {
            throw new Error('Union requires at least one feature');
        }

        let result = ensureFeature(layers[0]);
        for (let i = 1; i < layers.length; i++) {
            result = turf.union(turf.featureCollection([result, ensureFeature(layers[i])]));
        }

        const executionTime = performance.now() - startTime;
        console.log(`Union operation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Union operation failed:', error);
        throw new Error(`Union failed: ${error.message}`);
    }
}

/**
 * Removes areas of layer2 from layer1
 * @param {object} layer1 - Base layer (GeoJSON)
 * @param {object} layer2 - Layer to subtract (GeoJSON)
 * @returns {object} Difference result as GeoJSON
 */
export function difference(layer1, layer2) {
    const startTime = performance.now();
    try {
        const result = turf.difference(
            turf.featureCollection([ensureFeature(layer1), ensureFeature(layer2)])
        );
        const executionTime = performance.now() - startTime;
        console.log(`Difference operation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Difference operation failed:', error);
        throw new Error(`Difference failed: ${error.message}`);
    }
}

/**
 * Clips features to a clipping boundary
 * @param {object} clip - Clipping boundary (GeoJSON polygon)
 * @param {object} subject - Features to clip (GeoJSON)
 * @returns {object} Clipped features as GeoJSON
 */
export function clip(clip, subject) {
    const startTime = performance.now();
    try {
        const clipPolygon = ensureFeature(clip);
        const subjectFeature = ensureFeature(subject);

        const result = turf.bboxClip(subjectFeature, turf.bbox(clipPolygon));
        const executionTime = performance.now() - startTime;
        console.log(`Clip operation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Clip operation failed:', error);
        throw new Error(`Clip failed: ${error.message}`);
    }
}

/**
 * Simplifies a geometry by reducing vertices
 * @param {object} geometry - Input geometry (GeoJSON)
 * @param {number} tolerance - Simplification tolerance
 * @param {boolean} highQuality - Use higher quality algorithm
 * @returns {object} Simplified geometry as GeoJSON
 */
export function simplify(geometry, tolerance = 0.01, highQuality = false) {
    const startTime = performance.now();
    try {
        const result = turf.simplify(geometry, {
            tolerance,
            highQuality,
            mutate: false
        });
        const executionTime = performance.now() - startTime;
        console.log(`Simplify operation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Simplify operation failed:', error);
        throw new Error(`Simplify failed: ${error.message}`);
    }
}

// ========== Measurements ==========

/**
 * Calculates the area of a polygon
 * @param {object} polygon - Input polygon (GeoJSON)
 * @param {string} units - Area units (meters, kilometers, etc.)
 * @returns {number} Area in specified units
 */
export function area(polygon, units = 'meters') {
    const startTime = performance.now();
    try {
        const areaInMeters = turf.area(polygon);
        const result = convertArea(areaInMeters, 'meters', units);
        const executionTime = performance.now() - startTime;
        console.log(`Area calculation completed in ${executionTime.toFixed(2)}ms: ${result} ${units}`);
        return result;
    } catch (error) {
        console.error('Area calculation failed:', error);
        throw new Error(`Area calculation failed: ${error.message}`);
    }
}

/**
 * Calculates the length of a line
 * @param {object} line - Input line (GeoJSON LineString)
 * @param {string} units - Length units (meters, kilometers, etc.)
 * @returns {number} Length in specified units
 */
export function length(line, units = 'meters') {
    const startTime = performance.now();
    try {
        const result = turf.length(line, { units });
        const executionTime = performance.now() - startTime;
        console.log(`Length calculation completed in ${executionTime.toFixed(2)}ms: ${result} ${units}`);
        return result;
    } catch (error) {
        console.error('Length calculation failed:', error);
        throw new Error(`Length calculation failed: ${error.message}`);
    }
}

/**
 * Calculates the distance between two points
 * @param {object} point1 - First point {longitude, latitude}
 * @param {object} point2 - Second point {longitude, latitude}
 * @param {string} units - Distance units (meters, kilometers, etc.)
 * @returns {number} Distance in specified units
 */
export function distance(point1, point2, units = 'meters') {
    const startTime = performance.now();
    try {
        const from = turf.point([point1.longitude, point1.latitude]);
        const to = turf.point([point2.longitude, point2.latitude]);
        const result = turf.distance(from, to, { units });
        const executionTime = performance.now() - startTime;
        console.log(`Distance calculation completed in ${executionTime.toFixed(2)}ms: ${result} ${units}`);
        return result;
    } catch (error) {
        console.error('Distance calculation failed:', error);
        throw new Error(`Distance calculation failed: ${error.message}`);
    }
}

/**
 * Calculates the perimeter of a polygon
 * @param {object} polygon - Input polygon (GeoJSON)
 * @param {string} units - Length units (meters, kilometers, etc.)
 * @returns {number} Perimeter in specified units
 */
export function perimeter(polygon, units = 'meters') {
    const startTime = performance.now();
    try {
        // Get the boundary of the polygon
        const boundary = turf.polygonToLine(polygon);
        const result = turf.length(boundary, { units });
        const executionTime = performance.now() - startTime;
        console.log(`Perimeter calculation completed in ${executionTime.toFixed(2)}ms: ${result} ${units}`);
        return result;
    } catch (error) {
        console.error('Perimeter calculation failed:', error);
        throw new Error(`Perimeter calculation failed: ${error.message}`);
    }
}

// ========== Spatial Relationships ==========

/**
 * Tests if container contains the geometry
 * @param {object} container - Container geometry (GeoJSON)
 * @param {object} contained - Geometry to test (GeoJSON)
 * @returns {boolean} True if container contains the geometry
 */
export function contains(container, contained) {
    const startTime = performance.now();
    try {
        const containerFeature = ensureFeature(container);
        const containedFeature = ensureFeature(contained);
        const result = turf.booleanContains(containerFeature, containedFeature);
        const executionTime = performance.now() - startTime;
        console.log(`Contains test completed in ${executionTime.toFixed(2)}ms: ${result}`);
        return result;
    } catch (error) {
        console.error('Contains test failed:', error);
        throw new Error(`Contains test failed: ${error.message}`);
    }
}

/**
 * Tests if two geometries intersect
 * @param {object} geometry1 - First geometry (GeoJSON)
 * @param {object} geometry2 - Second geometry (GeoJSON)
 * @returns {boolean} True if geometries intersect
 */
export function intersects(geometry1, geometry2) {
    const startTime = performance.now();
    try {
        const feature1 = ensureFeature(geometry1);
        const feature2 = ensureFeature(geometry2);
        const result = turf.booleanIntersects(feature1, feature2);
        const executionTime = performance.now() - startTime;
        console.log(`Intersects test completed in ${executionTime.toFixed(2)}ms: ${result}`);
        return result;
    } catch (error) {
        console.error('Intersects test failed:', error);
        throw new Error(`Intersects test failed: ${error.message}`);
    }
}

/**
 * Tests if inner geometry is within outer geometry
 * @param {object} inner - Inner geometry (GeoJSON)
 * @param {object} outer - Outer geometry (GeoJSON)
 * @returns {boolean} True if inner is within outer
 */
export function within(inner, outer) {
    const startTime = performance.now();
    try {
        const innerFeature = ensureFeature(inner);
        const outerFeature = ensureFeature(outer);
        const result = turf.booleanWithin(innerFeature, outerFeature);
        const executionTime = performance.now() - startTime;
        console.log(`Within test completed in ${executionTime.toFixed(2)}ms: ${result}`);
        return result;
    } catch (error) {
        console.error('Within test failed:', error);
        throw new Error(`Within test failed: ${error.message}`);
    }
}

/**
 * Tests if two geometries overlap
 * @param {object} geometry1 - First geometry (GeoJSON)
 * @param {object} geometry2 - Second geometry (GeoJSON)
 * @returns {boolean} True if geometries overlap
 */
export function overlaps(geometry1, geometry2) {
    const startTime = performance.now();
    try {
        const feature1 = ensureFeature(geometry1);
        const feature2 = ensureFeature(geometry2);
        const result = turf.booleanOverlap(feature1, feature2);
        const executionTime = performance.now() - startTime;
        console.log(`Overlaps test completed in ${executionTime.toFixed(2)}ms: ${result}`);
        return result;
    } catch (error) {
        console.error('Overlaps test failed:', error);
        throw new Error(`Overlaps test failed: ${error.message}`);
    }
}

// ========== Geometric Calculations ==========

/**
 * Calculates the centroid of a feature
 * @param {object} geometry - Input geometry (GeoJSON)
 * @returns {object} Centroid as GeoJSON Point
 */
export function centroid(geometry) {
    const startTime = performance.now();
    try {
        const result = turf.centroid(geometry);
        const executionTime = performance.now() - startTime;
        console.log(`Centroid calculation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Centroid calculation failed:', error);
        throw new Error(`Centroid calculation failed: ${error.message}`);
    }
}

/**
 * Calculates the convex hull of features
 * @param {object} points - Input points or geometries (GeoJSON)
 * @returns {object} Convex hull as GeoJSON Polygon
 */
export function convexHull(points) {
    const startTime = performance.now();
    try {
        const result = turf.convex(points);
        const executionTime = performance.now() - startTime;
        console.log(`Convex hull calculation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Convex hull calculation failed:', error);
        throw new Error(`Convex hull calculation failed: ${error.message}`);
    }
}

/**
 * Calculates the bounding box of a feature
 * @param {object} geometry - Input geometry (GeoJSON)
 * @returns {Array<number>} Bounding box as [minLng, minLat, maxLng, maxLat]
 */
export function bbox(geometry) {
    const startTime = performance.now();
    try {
        const result = turf.bbox(geometry);
        const executionTime = performance.now() - startTime;
        console.log(`Bbox calculation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Bbox calculation failed:', error);
        throw new Error(`Bbox calculation failed: ${error.message}`);
    }
}

/**
 * Calculates the envelope (bounding box polygon) of a feature
 * @param {object} geometry - Input geometry (GeoJSON)
 * @returns {object} Envelope as GeoJSON Polygon
 */
export function envelope(geometry) {
    const startTime = performance.now();
    try {
        const box = turf.bbox(geometry);
        const result = turf.bboxPolygon(box);
        const executionTime = performance.now() - startTime;
        console.log(`Envelope calculation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Envelope calculation failed:', error);
        throw new Error(`Envelope calculation failed: ${error.message}`);
    }
}

// ========== Advanced Operations ==========

/**
 * Creates Voronoi polygons from points
 * @param {object} points - Input points (GeoJSON FeatureCollection)
 * @param {Array<number>} boundingBox - Optional bounding box
 * @returns {object} Voronoi polygons as GeoJSON FeatureCollection
 */
export function voronoi(points, boundingBox = null) {
    const startTime = performance.now();
    try {
        const options = boundingBox ? { bbox: boundingBox } : {};
        const result = turf.voronoi(points, options);
        const executionTime = performance.now() - startTime;
        console.log(`Voronoi calculation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Voronoi calculation failed:', error);
        throw new Error(`Voronoi calculation failed: ${error.message}`);
    }
}

/**
 * Dissolves features based on a property
 * @param {object} features - Input features (GeoJSON FeatureCollection)
 * @param {string} propertyName - Property name to dissolve by
 * @returns {object} Dissolved features as GeoJSON FeatureCollection
 */
export function dissolve(features, propertyName = null) {
    const startTime = performance.now();
    try {
        const options = propertyName ? { propertyName } : {};
        const result = turf.dissolve(features, options);
        const executionTime = performance.now() - startTime;
        console.log(`Dissolve operation completed in ${executionTime.toFixed(2)}ms`);
        return result;
    } catch (error) {
        console.error('Dissolve operation failed:', error);
        throw new Error(`Dissolve failed: ${error.message}`);
    }
}

/**
 * Transforms coordinates of a feature
 * @param {object} geometry - Input geometry (GeoJSON)
 * @param {Function} transformFn - Transform function
 * @returns {object} Transformed geometry as GeoJSON
 */
export function transform(geometry, transformFn) {
    const startTime = performance.now();
    try {
        const result = turf.coordEach(geometry, (coord) => {
            const transformed = transformFn(coord);
            coord[0] = transformed[0];
            coord[1] = transformed[1];
            if (transformed[2] !== undefined) coord[2] = transformed[2];
        });
        const executionTime = performance.now() - startTime;
        console.log(`Transform operation completed in ${executionTime.toFixed(2)}ms`);
        return geometry;
    } catch (error) {
        console.error('Transform operation failed:', error);
        throw new Error(`Transform failed: ${error.message}`);
    }
}

// ========== Display Operations ==========

/**
 * Displays geoprocessing result on map
 * @param {string} mapId - Map identifier
 * @param {string} layerId - Layer identifier
 * @param {object} geometry - GeoJSON geometry
 * @param {object} style - Display style
 */
export function displayResult(mapId, layerId, geometry, style = {}) {
    const map = window.honuaMaps?.get(mapId);
    if (!map) {
        console.warn(`Map ${mapId} not found`);
        return;
    }

    try {
        // Remove existing layer
        if (map.getLayer(layerId)) {
            map.removeLayer(layerId);
        }
        if (map.getSource(layerId)) {
            map.removeSource(layerId);
        }

        // Add new layer
        map.addSource(layerId, {
            type: 'geojson',
            data: geometry
        });

        const geometryType = geometry.geometry?.type || geometry.type;
        const defaultStyle = getDefaultStyle(geometryType, style);

        if (geometryType === 'Point' || geometryType === 'MultiPoint') {
            map.addLayer({
                id: layerId,
                type: 'circle',
                source: layerId,
                paint: defaultStyle.paint
            });
        } else if (geometryType === 'LineString' || geometryType === 'MultiLineString') {
            map.addLayer({
                id: layerId,
                type: 'line',
                source: layerId,
                paint: defaultStyle.paint
            });
        } else {
            map.addLayer({
                id: layerId,
                type: 'fill',
                source: layerId,
                paint: defaultStyle.paint
            });
            map.addLayer({
                id: `${layerId}-outline`,
                type: 'line',
                source: layerId,
                paint: {
                    'line-color': defaultStyle.outlineColor,
                    'line-width': 2
                }
            });
        }

        geoprocessingLayers.set(layerId, { mapId, geometry, style });
        console.log(`Displayed geoprocessing result: ${layerId}`);
    } catch (error) {
        console.error('Failed to display result:', error);
        throw new Error(`Display failed: ${error.message}`);
    }
}

/**
 * Clears geoprocessing results from map
 * @param {string} mapId - Map identifier
 * @param {string} layerId - Layer identifier (optional, clears all if not provided)
 */
export function clearResults(mapId, layerId = null) {
    const map = window.honuaMaps?.get(mapId);
    if (!map) return;

    if (layerId) {
        removeLayer(map, layerId);
        geoprocessingLayers.delete(layerId);
    } else {
        // Clear all geoprocessing layers
        for (const [id, info] of geoprocessingLayers.entries()) {
            if (info.mapId === mapId) {
                removeLayer(map, id);
                geoprocessingLayers.delete(id);
            }
        }
    }
}

// ========== Helper Functions ==========

/**
 * Ensures input is a GeoJSON Feature
 * @param {object} input - Input geometry or feature
 * @returns {object} GeoJSON Feature
 */
function ensureFeature(input) {
    if (!input) {
        throw new Error('Input is null or undefined');
    }

    if (input.type === 'Feature') {
        return input;
    } else if (input.type === 'FeatureCollection' && input.features && input.features.length > 0) {
        return input.features[0];
    } else if (['Point', 'LineString', 'Polygon', 'MultiPoint', 'MultiLineString', 'MultiPolygon'].includes(input.type)) {
        return turf.feature(input);
    }

    throw new Error('Invalid GeoJSON input');
}

/**
 * Converts area between different units
 * @param {number} value - Area value
 * @param {string} fromUnit - Source unit
 * @param {string} toUnit - Target unit
 * @returns {number} Converted area
 */
function convertArea(value, fromUnit, toUnit) {
    const conversions = {
        'meters': 1,
        'squaremeters': 1,
        'kilometers': 1000000,
        'squarekilometers': 1000000,
        'miles': 2589988.110336,
        'squaremiles': 2589988.110336,
        'hectares': 10000,
        'acres': 4046.8564224
    };

    const normalizedFrom = fromUnit.toLowerCase().replace(/[^a-z]/g, '');
    const normalizedTo = toUnit.toLowerCase().replace(/[^a-z]/g, '');

    const fromFactor = conversions[normalizedFrom] || 1;
    const toFactor = conversions[normalizedTo] || 1;

    return value * (fromFactor / toFactor);
}

/**
 * Gets default style for geometry type
 * @param {string} geometryType - Geometry type
 * @param {object} customStyle - Custom style overrides
 * @returns {object} Style configuration
 */
function getDefaultStyle(geometryType, customStyle = {}) {
    const defaults = {
        'Point': {
            paint: {
                'circle-radius': customStyle.radius || 6,
                'circle-color': customStyle.color || '#3b82f6',
                'circle-opacity': customStyle.opacity || 0.8,
                'circle-stroke-width': 2,
                'circle-stroke-color': '#ffffff'
            }
        },
        'LineString': {
            paint: {
                'line-color': customStyle.color || '#3b82f6',
                'line-width': customStyle.width || 3,
                'line-opacity': customStyle.opacity || 0.8
            }
        },
        'Polygon': {
            paint: {
                'fill-color': customStyle.color || '#3b82f6',
                'fill-opacity': customStyle.opacity || 0.3
            },
            outlineColor: customStyle.outlineColor || '#1e40af'
        }
    };

    return defaults[geometryType] || defaults['Polygon'];
}

/**
 * Removes a layer from the map
 * @param {object} map - Map instance
 * @param {string} layerId - Layer identifier
 */
function removeLayer(map, layerId) {
    if (map.getLayer(layerId)) {
        map.removeLayer(layerId);
    }
    if (map.getLayer(`${layerId}-outline`)) {
        map.removeLayer(`${layerId}-outline`);
    }
    if (map.getSource(layerId)) {
        map.removeSource(layerId);
    }
}

// Export all functions
export default {
    initializeGeoprocessing,
    buffer,
    intersect,
    union,
    difference,
    clip,
    simplify,
    area,
    length,
    distance,
    perimeter,
    contains,
    intersects,
    within,
    overlaps,
    centroid,
    convexHull,
    bbox,
    envelope,
    voronoi,
    dissolve,
    transform,
    displayResult,
    clearResults
};
