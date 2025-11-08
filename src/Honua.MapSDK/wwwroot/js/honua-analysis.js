// HonuaAnalysis - Spatial analysis tools for Honua.MapSDK
// Requires: Turf.js (https://turfjs.org/)
// Note: Turf.js should be included via CDN or bundled with the application

const analysisInstances = new Map();
const analysisResults = new Map();
const dotNetRefs = new Map();

/**
 * Initialize analysis component
 */
export function initializeAnalysis(mapId, componentId, config, dotNetRef) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return;
        }

        // Check if Turf.js is available
        if (typeof turf === 'undefined') {
            console.error('Turf.js is not loaded. Please include it via CDN or bundle.');
            console.info('Add to your app: <script src="https://cdn.jsdelivr.net/npm/@turf/turf@latest/turf.min.js"></script>');
            return;
        }

        // Store references
        dotNetRefs.set(componentId, dotNetRef);

        const instance = {
            mapId,
            mapInstance,
            config,
            resultLayers: new Map(),
            previewLayer: null
        };

        analysisInstances.set(componentId, instance);

        console.log(`Analysis initialized for component: ${componentId}`);
    } catch (error) {
        console.error('Error initializing analysis:', error);
    }
}

/**
 * Perform buffer analysis
 */
export function performBuffer(componentId, featureJson, distance, unit, steps) {
    try {
        const feature = JSON.parse(featureJson);
        const units = convertUnit(unit);

        let result;
        if (feature.geometry.type === 'Point') {
            result = turf.buffer(feature, distance, { units, steps: steps || 8 });
        } else if (feature.geometry.type === 'LineString') {
            result = turf.buffer(feature, distance, { units, steps: steps || 8 });
        } else if (feature.geometry.type === 'Polygon') {
            result = turf.buffer(feature, distance, { units, steps: steps || 8 });
        } else {
            throw new Error(`Unsupported geometry type for buffer: ${feature.geometry.type}`);
        }

        // Calculate statistics
        const area = turf.area(result);

        return {
            success: true,
            result: result,
            statistics: {
                area: area,
                areaHectares: area / 10000,
                areaAcres: area / 4046.86
            },
            featureCount: 1
        };
    } catch (error) {
        console.error('Error performing buffer:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Perform multi-ring buffer analysis
 */
export function performMultiRingBuffer(componentId, featureJson, distances, unit) {
    try {
        const feature = JSON.parse(featureJson);
        const units = convertUnit(unit);

        const rings = distances.map((distance, index) => {
            const buffered = turf.buffer(feature, distance, { units, steps: 8 });
            buffered.properties = {
                ...buffered.properties,
                ringIndex: index,
                distance: distance,
                unit: unit
            };
            return buffered;
        });

        const featureCollection = turf.featureCollection(rings);

        return {
            success: true,
            result: featureCollection,
            featureCount: rings.length,
            statistics: {
                ringCount: rings.length,
                minDistance: Math.min(...distances),
                maxDistance: Math.max(...distances)
            }
        };
    } catch (error) {
        console.error('Error performing multi-ring buffer:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Perform intersection analysis
 */
export function performIntersect(componentId, feature1Json, feature2Json) {
    try {
        const feature1 = JSON.parse(feature1Json);
        const feature2 = JSON.parse(feature2Json);

        const result = turf.intersect(feature1, feature2);

        if (!result) {
            return {
                success: true,
                result: null,
                featureCount: 0,
                statistics: {
                    area: 0
                }
            };
        }

        const area = turf.area(result);

        return {
            success: true,
            result: result,
            featureCount: 1,
            statistics: {
                area: area,
                areaHectares: area / 10000
            }
        };
    } catch (error) {
        console.error('Error performing intersect:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Perform union analysis
 */
export function performUnion(componentId, featuresJson) {
    try {
        const features = JSON.parse(featuresJson);

        if (!Array.isArray(features) || features.length === 0) {
            throw new Error('Features must be a non-empty array');
        }

        let result = features[0];
        for (let i = 1; i < features.length; i++) {
            result = turf.union(result, features[i]);
        }

        const area = turf.area(result);

        return {
            success: true,
            result: result,
            featureCount: 1,
            statistics: {
                area: area,
                inputFeatureCount: features.length
            }
        };
    } catch (error) {
        console.error('Error performing union:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Perform difference analysis
 */
export function performDifference(componentId, feature1Json, feature2Json) {
    try {
        const feature1 = JSON.parse(feature1Json);
        const feature2 = JSON.parse(feature2Json);

        const result = turf.difference(feature1, feature2);

        if (!result) {
            return {
                success: true,
                result: null,
                featureCount: 0,
                statistics: { area: 0 }
            };
        }

        const area = turf.area(result);

        return {
            success: true,
            result: result,
            featureCount: 1,
            statistics: {
                area: area
            }
        };
    } catch (error) {
        console.error('Error performing difference:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Perform dissolve operation
 */
export function performDissolve(componentId, featuresJson, propertyName) {
    try {
        const features = JSON.parse(featuresJson);

        // Group features by property value
        const groups = {};
        features.forEach(feature => {
            const value = feature.properties[propertyName];
            if (!groups[value]) {
                groups[value] = [];
            }
            groups[value].push(feature);
        });

        // Union features in each group
        const dissolved = [];
        for (const [value, groupFeatures] of Object.entries(groups)) {
            let union = groupFeatures[0];
            for (let i = 1; i < groupFeatures.length; i++) {
                union = turf.union(union, groupFeatures[i]);
            }
            union.properties = { [propertyName]: value };
            dissolved.push(union);
        }

        const featureCollection = turf.featureCollection(dissolved);

        return {
            success: true,
            result: featureCollection,
            featureCount: dissolved.length,
            statistics: {
                inputFeatureCount: features.length,
                outputFeatureCount: dissolved.length,
                groupCount: Object.keys(groups).length
            }
        };
    } catch (error) {
        console.error('Error performing dissolve:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Find points within polygon
 */
export function performPointsWithinPolygon(componentId, pointsJson, polygonJson) {
    try {
        const points = JSON.parse(pointsJson);
        const polygon = JSON.parse(polygonJson);

        const pointsFC = Array.isArray(points) ? turf.featureCollection(points) : points;
        const result = turf.pointsWithinPolygon(pointsFC, polygon);

        return {
            success: true,
            result: result,
            featureCount: result.features.length,
            statistics: {
                inputPointCount: pointsFC.features.length,
                outputPointCount: result.features.length
            }
        };
    } catch (error) {
        console.error('Error performing points within polygon:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Find nearest neighbor(s)
 */
export function performNearestNeighbor(componentId, targetJson, candidatesJson, count) {
    try {
        const target = JSON.parse(targetJson);
        const candidates = JSON.parse(candidatesJson);

        const candidatesFC = Array.isArray(candidates) ? turf.featureCollection(candidates) : candidates;

        // Calculate distances to all candidates
        const distances = candidatesFC.features.map(candidate => {
            const distance = turf.distance(target, candidate);
            return {
                feature: candidate,
                distance: distance
            };
        });

        // Sort by distance and take top N
        distances.sort((a, b) => a.distance - b.distance);
        const nearest = distances.slice(0, count || 1);

        // Add distance to properties
        const results = nearest.map(item => {
            const feature = { ...item.feature };
            feature.properties = {
                ...feature.properties,
                distance: item.distance,
                distanceKm: item.distance,
                distanceMi: item.distance * 0.621371
            };
            return feature;
        });

        const featureCollection = turf.featureCollection(results);

        return {
            success: true,
            result: featureCollection,
            featureCount: results.length,
            statistics: {
                nearestDistance: nearest[0]?.distance || 0,
                farthestDistance: nearest[nearest.length - 1]?.distance || 0,
                candidateCount: candidatesFC.features.length
            }
        };
    } catch (error) {
        console.error('Error performing nearest neighbor:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Find features within distance
 */
export function performWithinDistance(componentId, targetJson, candidatesJson, distance, unit) {
    try {
        const target = JSON.parse(targetJson);
        const candidates = JSON.parse(candidatesJson);
        const units = convertUnit(unit);

        const candidatesFC = Array.isArray(candidates) ? turf.featureCollection(candidates) : candidates;

        const withinFeatures = candidatesFC.features.filter(candidate => {
            const dist = turf.distance(target, candidate, { units });
            return dist <= distance;
        });

        const featureCollection = turf.featureCollection(withinFeatures);

        return {
            success: true,
            result: featureCollection,
            featureCount: withinFeatures.length,
            statistics: {
                distance: distance,
                unit: unit,
                candidateCount: candidatesFC.features.length,
                withinCount: withinFeatures.length
            }
        };
    } catch (error) {
        console.error('Error performing within distance:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Calculate area
 */
export function calculateArea(componentId, featureJson, unit) {
    try {
        const feature = JSON.parse(featureJson);

        const areaSqMeters = turf.area(feature);
        const convertedArea = convertArea(areaSqMeters, unit);

        return {
            success: true,
            result: convertedArea,
            statistics: {
                area: convertedArea,
                areaSquareMeters: areaSqMeters,
                areaHectares: areaSqMeters / 10000,
                areaAcres: areaSqMeters / 4046.86,
                areaSquareMiles: areaSqMeters / 2589988.11,
                unit: unit
            },
            featureCount: 1
        };
    } catch (error) {
        console.error('Error calculating area:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Calculate length/perimeter
 */
export function calculateLength(componentId, featureJson, unit) {
    try {
        const feature = JSON.parse(featureJson);
        const units = convertUnit(unit);

        let length;
        if (feature.geometry.type === 'LineString' || feature.geometry.type === 'MultiLineString') {
            length = turf.length(feature, { units });
        } else if (feature.geometry.type === 'Polygon' || feature.geometry.type === 'MultiPolygon') {
            // Calculate perimeter
            const line = turf.polygonToLine(feature);
            length = turf.length(line, { units });
        } else {
            throw new Error(`Cannot calculate length for geometry type: ${feature.geometry.type}`);
        }

        return {
            success: true,
            result: length,
            statistics: {
                length: length,
                lengthKm: convertUnit(unit) === 'kilometers' ? length : length * getUnitMultiplier(unit, 'kilometers'),
                lengthMi: convertUnit(unit) === 'miles' ? length : length * getUnitMultiplier(unit, 'miles'),
                unit: unit
            },
            featureCount: 1
        };
    } catch (error) {
        console.error('Error calculating length:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Calculate centroid
 */
export function calculateCentroid(componentId, featureJson) {
    try {
        const feature = JSON.parse(featureJson);
        const centroid = turf.centroid(feature);

        return {
            success: true,
            result: centroid,
            statistics: {
                longitude: centroid.geometry.coordinates[0],
                latitude: centroid.geometry.coordinates[1]
            },
            featureCount: 1
        };
    } catch (error) {
        console.error('Error calculating centroid:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Calculate bounding box
 */
export function calculateBoundingBox(componentId, featureJson) {
    try {
        const feature = JSON.parse(featureJson);
        const bbox = turf.bbox(feature);

        // Create polygon from bbox
        const bboxPolygon = turf.bboxPolygon(bbox);

        return {
            success: true,
            result: bboxPolygon,
            statistics: {
                minLongitude: bbox[0],
                minLatitude: bbox[1],
                maxLongitude: bbox[2],
                maxLatitude: bbox[3],
                width: bbox[2] - bbox[0],
                height: bbox[3] - bbox[1]
            },
            featureCount: 1
        };
    } catch (error) {
        console.error('Error calculating bounding box:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Calculate bearing between two points
 */
export function calculateBearing(componentId, point1Json, point2Json) {
    try {
        const point1 = JSON.parse(point1Json);
        const point2 = JSON.parse(point2Json);

        const bearing = turf.bearing(point1, point2);
        const distance = turf.distance(point1, point2);

        return {
            success: true,
            result: bearing,
            statistics: {
                bearing: bearing,
                distance: distance,
                distanceKm: distance,
                distanceMi: distance * 0.621371
            },
            featureCount: 1
        };
    } catch (error) {
        console.error('Error calculating bearing:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Check spatial relationship
 */
export function checkSpatialRelationship(componentId, feature1Json, feature2Json, relationship) {
    try {
        const feature1 = JSON.parse(feature1Json);
        const feature2 = JSON.parse(feature2Json);

        let result;
        switch (relationship.toLowerCase()) {
            case 'contains':
                result = turf.booleanContains(feature1, feature2);
                break;
            case 'within':
                result = turf.booleanWithin(feature1, feature2);
                break;
            case 'crosses':
                result = turf.booleanCrosses(feature1, feature2);
                break;
            case 'overlaps':
                result = turf.booleanOverlap(feature1, feature2);
                break;
            case 'disjoint':
                result = turf.booleanDisjoint(feature1, feature2);
                break;
            default:
                throw new Error(`Unknown relationship: ${relationship}`);
        }

        return {
            success: true,
            result: result,
            statistics: {
                relationship: relationship,
                matches: result
            },
            featureCount: 1
        };
    } catch (error) {
        console.error('Error checking spatial relationship:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Add result to map as a new layer
 */
export function addResultToMap(componentId, resultJson, layerName, style) {
    try {
        const instance = analysisInstances.get(componentId);
        if (!instance) {
            throw new Error('Analysis instance not found');
        }

        const result = JSON.parse(resultJson);
        const layerId = `analysis-result-${Date.now()}`;

        // Add source
        instance.mapInstance.addSource(layerId, {
            type: 'geojson',
            data: result
        });

        // Determine geometry type
        const geomType = result.geometry?.type || result.features?.[0]?.geometry?.type;

        // Add appropriate layers based on geometry type
        if (geomType === 'Polygon' || geomType === 'MultiPolygon') {
            instance.mapInstance.addLayer({
                id: `${layerId}-fill`,
                type: 'fill',
                source: layerId,
                paint: {
                    'fill-color': style?.fillColor || '#3B82F6',
                    'fill-opacity': style?.fillOpacity || 0.2
                }
            });

            instance.mapInstance.addLayer({
                id: `${layerId}-line`,
                type: 'line',
                source: layerId,
                paint: {
                    'line-color': style?.strokeColor || '#3B82F6',
                    'line-width': style?.strokeWidth || 2
                }
            });
        } else if (geomType === 'LineString' || geomType === 'MultiLineString') {
            instance.mapInstance.addLayer({
                id: `${layerId}-line`,
                type: 'line',
                source: layerId,
                paint: {
                    'line-color': style?.strokeColor || '#3B82F6',
                    'line-width': style?.strokeWidth || 2
                }
            });
        } else if (geomType === 'Point' || geomType === 'MultiPoint') {
            instance.mapInstance.addLayer({
                id: `${layerId}-circle`,
                type: 'circle',
                source: layerId,
                paint: {
                    'circle-color': style?.fillColor || '#3B82F6',
                    'circle-radius': 6,
                    'circle-stroke-color': style?.strokeColor || '#FFFFFF',
                    'circle-stroke-width': 2
                }
            });
        }

        instance.resultLayers.set(layerId, {
            name: layerName,
            result: result
        });

        return {
            success: true,
            layerId: layerId
        };
    } catch (error) {
        console.error('Error adding result to map:', error);
        return {
            success: false,
            errorMessage: error.message
        };
    }
}

/**
 * Remove result layer from map
 */
export function removeResultLayer(componentId, layerId) {
    try {
        const instance = analysisInstances.get(componentId);
        if (!instance) return;

        // Remove all layer variations
        ['fill', 'line', 'circle'].forEach(suffix => {
            const fullLayerId = `${layerId}-${suffix}`;
            if (instance.mapInstance.getLayer(fullLayerId)) {
                instance.mapInstance.removeLayer(fullLayerId);
            }
        });

        // Remove source
        if (instance.mapInstance.getSource(layerId)) {
            instance.mapInstance.removeSource(layerId);
        }

        instance.resultLayers.delete(layerId);
    } catch (error) {
        console.error('Error removing result layer:', error);
    }
}

/**
 * Clear all result layers
 */
export function clearAllResults(componentId) {
    try {
        const instance = analysisInstances.get(componentId);
        if (!instance) return;

        for (const layerId of instance.resultLayers.keys()) {
            removeResultLayer(componentId, layerId);
        }
    } catch (error) {
        console.error('Error clearing results:', error);
    }
}

/**
 * Convert unit name to Turf.js format
 */
function convertUnit(unit) {
    const unitMap = {
        'meters': 'meters',
        'kilometers': 'kilometers',
        'miles': 'miles',
        'feet': 'feet',
        'nauticalmiles': 'nauticalmiles',
        'yards': 'yards'
    };
    return unitMap[unit.toLowerCase()] || 'meters';
}

/**
 * Convert area to specified unit
 */
function convertArea(sqMeters, unit) {
    switch (unit.toLowerCase()) {
        case 'squaremeters':
        case 'meters':
            return sqMeters;
        case 'hectares':
            return sqMeters / 10000;
        case 'acres':
            return sqMeters / 4046.86;
        case 'squarekilometers':
        case 'kilometers':
            return sqMeters / 1000000;
        case 'squaremiles':
        case 'miles':
            return sqMeters / 2589988.11;
        default:
            return sqMeters;
    }
}

/**
 * Get unit multiplier
 */
function getUnitMultiplier(fromUnit, toUnit) {
    const toMeters = {
        'meters': 1,
        'kilometers': 1000,
        'miles': 1609.34,
        'feet': 0.3048,
        'nauticalmiles': 1852,
        'yards': 0.9144
    };

    const from = toMeters[fromUnit.toLowerCase()] || 1;
    const to = toMeters[toUnit.toLowerCase()] || 1;
    return from / to;
}

/**
 * Cleanup analysis instance
 */
export function cleanup(componentId) {
    try {
        clearAllResults(componentId);
        analysisInstances.delete(componentId);
        dotNetRefs.delete(componentId);
    } catch (error) {
        console.error('Error cleaning up analysis:', error);
    }
}

// Make functions available globally for debugging
if (typeof window !== 'undefined') {
    window.honuaAnalysis = {
        initializeAnalysis,
        performBuffer,
        performMultiRingBuffer,
        performIntersect,
        performUnion,
        performDifference,
        performDissolve,
        performPointsWithinPolygon,
        performNearestNeighbor,
        performWithinDistance,
        calculateArea,
        calculateLength,
        calculateCentroid,
        calculateBoundingBox,
        calculateBearing,
        checkSpatialRelationship,
        addResultToMap,
        removeResultLayer,
        clearAllResults,
        cleanup
    };
}
