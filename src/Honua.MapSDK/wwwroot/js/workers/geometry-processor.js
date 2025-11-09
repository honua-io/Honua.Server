// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Web Worker for processing 3D geometry data in the background.
 * Keeps the UI thread responsive during heavy computation.
 *
 * Supported operations:
 * - processGeoJSON: Parse and simplify GeoJSON features
 * - triangulate: Convert polygons to triangles for WebGL
 * - simplifyLOD: Apply Level-of-Detail simplification
 * - computeBounds: Calculate bounding boxes
 */

// Worker message handler
self.onmessage = async (e) => {
    const { id, type, data, options } = e.data;

    try {
        let result;

        switch (type) {
            case 'processGeoJSON':
                result = processGeoJSON(data, options);
                break;

            case 'triangulate':
                result = triangulatePolygons(data);
                break;

            case 'simplifyLOD':
                result = simplifyLOD(data, options?.tolerance || 0.0001);
                break;

            case 'computeBounds':
                result = computeBounds(data);
                break;

            case 'filterFeatures':
                result = filterFeatures(data, options);
                break;

            default:
                throw new Error(`Unknown operation type: ${type}`);
        }

        // Send result back to main thread
        const transferables = getTransferables(result);
        self.postMessage({ id, type, result, success: true }, transferables);

    } catch (error) {
        self.postMessage({
            id,
            type,
            error: error.message,
            success: false
        });
    }
};

/**
 * Process GeoJSON data with optional LOD simplification
 */
function processGeoJSON(geojson, options = {}) {
    const startTime = performance.now();
    const features = geojson.features || [geojson];

    console.log(`[Worker] Processing ${features.length} features`);

    let result = features;

    // Apply LOD simplification based on feature count
    if (options.autoLOD !== false) {
        if (features.length > 100000) {
            result = simplifyLOD(result, 0.001); // High simplification
            console.log(`[Worker] Applied high LOD (100K+ features)`);
        } else if (features.length > 10000) {
            result = simplifyLOD(result, 0.0001); // Medium simplification
            console.log(`[Worker] Applied medium LOD (10K+ features)`);
        }
    }

    // Apply custom tolerance if specified
    if (options.tolerance) {
        result = simplifyLOD(result, options.tolerance);
    }

    const duration = performance.now() - startTime;
    console.log(`[Worker] Processing complete in ${duration.toFixed(2)}ms`);

    return {
        type: 'FeatureCollection',
        features: result
    };
}

/**
 * Triangulate polygon features for WebGL rendering
 */
function triangulatePolygons(features) {
    // Simplified triangulation (real implementation would use earcut or similar)
    const triangles = [];

    for (const feature of features) {
        if (feature.geometry.type === 'Polygon') {
            const coords = feature.geometry.coordinates[0]; // exterior ring

            // Simple fan triangulation (for convex polygons only)
            for (let i = 1; i < coords.length - 2; i++) {
                triangles.push({
                    vertices: [
                        coords[0],
                        coords[i],
                        coords[i + 1]
                    ],
                    properties: feature.properties
                });
            }
        }
    }

    return triangles;
}

/**
 * Apply Level-of-Detail simplification using Douglas-Peucker algorithm
 */
function simplifyLOD(features, tolerance) {
    return features.map(feature => {
        if (feature.geometry.type === 'LineString') {
            return {
                ...feature,
                geometry: {
                    type: 'LineString',
                    coordinates: simplifyLineString(feature.geometry.coordinates, tolerance)
                }
            };
        } else if (feature.geometry.type === 'Polygon') {
            return {
                ...feature,
                geometry: {
                    type: 'Polygon',
                    coordinates: feature.geometry.coordinates.map(ring =>
                        simplifyLineString(ring, tolerance)
                    )
                }
            };
        }
        return feature;
    });
}

/**
 * Simplify a LineString using Douglas-Peucker algorithm
 */
function simplifyLineString(points, tolerance) {
    if (points.length <= 2) return points;

    // Find the point with maximum distance from line segment
    let maxDistance = 0;
    let maxIndex = 0;

    const start = points[0];
    const end = points[points.length - 1];

    for (let i = 1; i < points.length - 1; i++) {
        const distance = perpendicularDistance(points[i], start, end);
        if (distance > maxDistance) {
            maxDistance = distance;
            maxIndex = i;
        }
    }

    // If max distance is greater than tolerance, recursively simplify
    if (maxDistance > tolerance) {
        const left = simplifyLineString(points.slice(0, maxIndex + 1), tolerance);
        const right = simplifyLineString(points.slice(maxIndex), tolerance);

        // Combine results (remove duplicate middle point)
        return left.slice(0, -1).concat(right);
    }

    // Otherwise, just keep endpoints
    return [start, end];
}

/**
 * Calculate perpendicular distance from point to line segment
 */
function perpendicularDistance(point, lineStart, lineEnd) {
    const [x, y] = point;
    const [x1, y1] = lineStart;
    const [x2, y2] = lineEnd;

    const dx = x2 - x1;
    const dy = y2 - y1;

    if (dx === 0 && dy === 0) {
        // Line segment is a point
        return Math.sqrt((x - x1) ** 2 + (y - y1) ** 2);
    }

    // Calculate distance
    const numerator = Math.abs(dy * x - dx * y + x2 * y1 - y2 * x1);
    const denominator = Math.sqrt(dx ** 2 + dy ** 2);

    return numerator / denominator;
}

/**
 * Compute bounding box for features
 */
function computeBounds(features) {
    let minX = Infinity, minY = Infinity, minZ = Infinity;
    let maxX = -Infinity, maxY = -Infinity, maxZ = -Infinity;

    for (const feature of features) {
        const coords = getAllCoordinates(feature.geometry);

        for (const coord of coords) {
            const [x, y, z] = coord;

            minX = Math.min(minX, x);
            minY = Math.min(minY, y);
            maxX = Math.max(maxX, x);
            maxY = Math.max(maxY, y);

            if (z !== undefined) {
                minZ = Math.min(minZ, z);
                maxZ = Math.max(maxZ, z);
            }
        }
    }

    return {
        min: [minX, minY, minZ],
        max: [maxX, maxY, maxZ],
        center: [
            (minX + maxX) / 2,
            (minY + maxY) / 2,
            (minZ + maxZ) / 2
        ]
    };
}

/**
 * Filter features based on properties
 */
function filterFeatures(features, options) {
    if (!options.filter) return features;

    return features.filter(feature => {
        for (const [key, value] of Object.entries(options.filter)) {
            if (feature.properties[key] !== value) {
                return false;
            }
        }
        return true;
    });
}

/**
 * Extract all coordinates from a geometry (recursive)
 */
function getAllCoordinates(geometry) {
    const coords = [];

    function extract(geom) {
        if (Array.isArray(geom)) {
            if (typeof geom[0] === 'number') {
                // This is a coordinate
                coords.push(geom);
            } else {
                // This is an array of coordinates or geometries
                geom.forEach(extract);
            }
        }
    }

    if (geometry.type === 'GeometryCollection') {
        geometry.geometries.forEach(g => extract(g.coordinates));
    } else {
        extract(geometry.coordinates);
    }

    return coords;
}

/**
 * Get transferable objects from result (for zero-copy transfer)
 */
function getTransferables(result) {
    const transferables = [];

    // Look for typed arrays in the result
    function findTypedArrays(obj) {
        if (obj instanceof ArrayBuffer) {
            transferables.push(obj);
        } else if (obj instanceof Float32Array || obj instanceof Uint8Array || obj instanceof Uint32Array) {
            transferables.push(obj.buffer);
        } else if (obj && typeof obj === 'object') {
            Object.values(obj).forEach(findTypedArrays);
        }
    }

    findTypedArrays(result);

    return transferables;
}

console.log('[Worker] Geometry processor initialized');
