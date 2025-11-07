// Honua Heatmap JavaScript Module
// MapLibre GL JS heatmap layer integration for Blazor

const heatmaps = new Map();
const mapInstances = new Map();

/**
 * Initializes the heatmap module with a map instance
 * @param {string} mapId - Map container ID
 * @param {Object} mapInstance - MapLibre GL map instance
 */
export function initializeHeatmap(mapId, mapInstance) {
    if (!mapInstance) {
        console.error('Map instance is required');
        return;
    }
    mapInstances.set(mapId, mapInstance);
}

/**
 * Creates a new heatmap layer
 * @param {string} mapId - Map container ID
 * @param {string} sourceId - GeoJSON source ID
 * @param {Object} options - Heatmap configuration options
 * @param {Object} dotNetRef - Reference to .NET component for callbacks
 * @returns {Object} Heatmap API object
 */
export function createHeatmap(mapId, sourceId, options, dotNetRef) {
    const map = mapInstances.get(mapId);
    if (!map) {
        console.error(`Map ${mapId} not found. Call initializeHeatmap first.`);
        return null;
    }

    const layerId = options.layerId || `heatmap-${sourceId}-${Date.now()}`;

    // Build gradient expression
    const gradient = buildGradientExpression(options.gradient, options.customGradient);

    // Build weight expression
    const weight = options.weightProperty
        ? ['get', options.weightProperty]
        : 1;

    // Create the heatmap layer
    const layer = {
        id: layerId,
        type: 'heatmap',
        source: sourceId,
        paint: {
            'heatmap-radius': options.radius || 30,
            'heatmap-weight': weight,
            'heatmap-intensity': options.intensity || 1.0,
            'heatmap-color': gradient,
            'heatmap-opacity': options.opacity || 0.6
        }
    };

    // Apply zoom limits if specified
    if (options.minZoom !== undefined && options.minZoom !== null) {
        layer.minzoom = options.minZoom;
    }
    if (options.maxZoom !== undefined && options.maxZoom !== null) {
        layer.maxzoom = options.maxZoom;
    }

    try {
        map.addLayer(layer);

        // Store heatmap metadata
        heatmaps.set(layerId, {
            mapId,
            sourceId,
            layerId,
            options,
            dotNetRef,
            map
        });

        console.log(`Heatmap layer ${layerId} created successfully`);
        return createHeatmapAPI(layerId);
    } catch (error) {
        console.error('Error creating heatmap:', error);
        return null;
    }
}

/**
 * Builds MapLibre gradient expression from gradient type
 */
function buildGradientExpression(gradientType, customGradient) {
    // Predefined gradient definitions
    const gradients = {
        hot: [
            0, 'rgba(0, 0, 255, 0)',
            0.2, 'rgba(0, 255, 255, 0.2)',
            0.4, 'rgba(0, 255, 0, 0.5)',
            0.6, 'rgba(255, 255, 0, 0.8)',
            0.8, 'rgba(255, 128, 0, 0.9)',
            1, 'rgba(255, 0, 0, 1)'
        ],
        cool: [
            0, 'rgba(0, 0, 255, 0)',
            0.2, 'rgba(0, 128, 255, 0.3)',
            0.4, 'rgba(0, 191, 255, 0.5)',
            0.6, 'rgba(64, 224, 208, 0.7)',
            0.8, 'rgba(127, 255, 212, 0.9)',
            1, 'rgba(0, 255, 255, 1)'
        ],
        rainbow: [
            0, 'rgba(138, 43, 226, 0)',
            0.16, 'rgba(75, 0, 130, 0.5)',
            0.33, 'rgba(0, 0, 255, 0.7)',
            0.5, 'rgba(0, 255, 0, 0.8)',
            0.66, 'rgba(255, 255, 0, 0.9)',
            0.83, 'rgba(255, 127, 0, 0.95)',
            1, 'rgba(255, 0, 0, 1)'
        ],
        viridis: [
            0, 'rgba(68, 1, 84, 0)',
            0.13, 'rgba(71, 44, 122, 0.3)',
            0.25, 'rgba(59, 81, 139, 0.5)',
            0.38, 'rgba(44, 113, 142, 0.6)',
            0.5, 'rgba(33, 144, 141, 0.7)',
            0.63, 'rgba(39, 173, 129, 0.8)',
            0.75, 'rgba(92, 200, 99, 0.9)',
            0.88, 'rgba(170, 220, 50, 0.95)',
            1, 'rgba(253, 231, 37, 1)'
        ],
        plasma: [
            0, 'rgba(13, 8, 135, 0)',
            0.13, 'rgba(75, 3, 161, 0.3)',
            0.25, 'rgba(125, 3, 168, 0.5)',
            0.38, 'rgba(168, 34, 150, 0.6)',
            0.5, 'rgba(203, 70, 121, 0.7)',
            0.63, 'rgba(229, 107, 93, 0.8)',
            0.75, 'rgba(248, 148, 65, 0.9)',
            0.88, 'rgba(253, 195, 40, 0.95)',
            1, 'rgba(240, 249, 33, 1)'
        ],
        inferno: [
            0, 'rgba(0, 0, 4, 0)',
            0.13, 'rgba(31, 12, 72, 0.3)',
            0.25, 'rgba(85, 15, 109, 0.5)',
            0.38, 'rgba(136, 34, 106, 0.6)',
            0.5, 'rgba(186, 54, 85, 0.7)',
            0.63, 'rgba(227, 89, 51, 0.8)',
            0.75, 'rgba(249, 140, 10, 0.9)',
            0.88, 'rgba(249, 201, 50, 0.95)',
            1, 'rgba(252, 255, 164, 1)'
        ]
    };

    let stops;

    if (gradientType?.toLowerCase() === 'custom' && customGradient) {
        // Build custom gradient from dictionary
        stops = [];
        const sortedStops = Object.entries(customGradient)
            .sort(([a], [b]) => parseFloat(a) - parseFloat(b));

        for (const [density, color] of sortedStops) {
            stops.push(parseFloat(density), color);
        }
    } else {
        // Use predefined gradient
        const gradientKey = gradientType?.toLowerCase() || 'hot';
        stops = gradients[gradientKey] || gradients.hot;
    }

    return [
        'interpolate',
        ['linear'],
        ['heatmap-density'],
        ...stops
    ];
}

/**
 * Updates heatmap radius
 * @param {string} mapId - Map container ID
 * @param {string} layerId - Heatmap layer ID
 * @param {number} radius - New radius value
 */
export function updateHeatmapRadius(mapId, layerId, radius) {
    const map = mapInstances.get(mapId);
    if (!map) return;

    try {
        map.setPaintProperty(layerId, 'heatmap-radius', radius);

        const heatmap = heatmaps.get(layerId);
        if (heatmap) {
            heatmap.options.radius = radius;
        }
    } catch (error) {
        console.error('Error updating heatmap radius:', error);
    }
}

/**
 * Updates heatmap intensity
 * @param {string} mapId - Map container ID
 * @param {string} layerId - Heatmap layer ID
 * @param {number} intensity - New intensity value (0-2)
 */
export function updateHeatmapIntensity(mapId, layerId, intensity) {
    const map = mapInstances.get(mapId);
    if (!map) return;

    try {
        map.setPaintProperty(layerId, 'heatmap-intensity', intensity);

        const heatmap = heatmaps.get(layerId);
        if (heatmap) {
            heatmap.options.intensity = intensity;
        }
    } catch (error) {
        console.error('Error updating heatmap intensity:', error);
    }
}

/**
 * Updates heatmap opacity
 * @param {string} mapId - Map container ID
 * @param {string} layerId - Heatmap layer ID
 * @param {number} opacity - New opacity value (0-1)
 */
export function updateHeatmapOpacity(mapId, layerId, opacity) {
    const map = mapInstances.get(mapId);
    if (!map) return;

    try {
        map.setPaintProperty(layerId, 'heatmap-opacity', opacity);

        const heatmap = heatmaps.get(layerId);
        if (heatmap) {
            heatmap.options.opacity = opacity;
        }
    } catch (error) {
        console.error('Error updating heatmap opacity:', error);
    }
}

/**
 * Updates heatmap color gradient
 * @param {string} mapId - Map container ID
 * @param {string} layerId - Heatmap layer ID
 * @param {string} gradientType - Gradient type name
 * @param {Object} customGradient - Custom gradient stops (optional)
 */
export function updateHeatmapGradient(mapId, layerId, gradientType, customGradient) {
    const map = mapInstances.get(mapId);
    if (!map) return;

    try {
        const gradient = buildGradientExpression(gradientType, customGradient);
        map.setPaintProperty(layerId, 'heatmap-color', gradient);

        const heatmap = heatmaps.get(layerId);
        if (heatmap) {
            heatmap.options.gradient = gradientType;
            heatmap.options.customGradient = customGradient;
        }
    } catch (error) {
        console.error('Error updating heatmap gradient:', error);
    }
}

/**
 * Sets the weight property for heatmap
 * @param {string} mapId - Map container ID
 * @param {string} layerId - Heatmap layer ID
 * @param {string} property - Property name to use for weighting (null for uniform)
 */
export function setHeatmapWeight(mapId, layerId, property) {
    const map = mapInstances.get(mapId);
    if (!map) return;

    try {
        const weight = property ? ['get', property] : 1;
        map.setPaintProperty(layerId, 'heatmap-weight', weight);

        const heatmap = heatmaps.get(layerId);
        if (heatmap) {
            heatmap.options.weightProperty = property;
        }
    } catch (error) {
        console.error('Error setting heatmap weight:', error);
    }
}

/**
 * Updates heatmap data source
 * @param {string} mapId - Map container ID
 * @param {string} sourceId - Source ID
 * @param {Object} geojson - GeoJSON FeatureCollection
 */
export function updateHeatmapData(mapId, sourceId, geojson) {
    const map = mapInstances.get(mapId);
    if (!map) return;

    try {
        const source = map.getSource(sourceId);
        if (source) {
            source.setData(geojson);
        } else {
            // Create source if it doesn't exist
            map.addSource(sourceId, {
                type: 'geojson',
                data: geojson
            });
        }
    } catch (error) {
        console.error('Error updating heatmap data:', error);
    }
}

/**
 * Toggles heatmap layer visibility
 * @param {string} mapId - Map container ID
 * @param {string} layerId - Heatmap layer ID
 * @param {boolean} visible - Visibility state
 */
export function setHeatmapVisibility(mapId, layerId, visible) {
    const map = mapInstances.get(mapId);
    if (!map) return;

    try {
        map.setLayoutProperty(layerId, 'visibility', visible ? 'visible' : 'none');
    } catch (error) {
        console.error('Error setting heatmap visibility:', error);
    }
}

/**
 * Calculates heatmap statistics
 * @param {string} mapId - Map container ID
 * @param {string} sourceId - Source ID
 * @param {string} weightProperty - Weight property name (optional)
 * @returns {Object} Statistics object
 */
export function calculateHeatmapStatistics(mapId, sourceId, weightProperty) {
    const map = mapInstances.get(mapId);
    if (!map) return null;

    try {
        const source = map.getSource(sourceId);
        if (!source) return null;

        const features = source._data?.features || [];

        const stats = {
            pointCount: features.length,
            maxDensity: 0,
            minDensity: 0,
            averageDensity: 0,
            totalWeight: null,
            maxWeight: null,
            minWeight: null,
            bounds: null
        };

        if (features.length === 0) return stats;

        // Calculate bounds
        let minLng = Infinity, minLat = Infinity;
        let maxLng = -Infinity, maxLat = -Infinity;

        features.forEach(feature => {
            if (feature.geometry?.type === 'Point') {
                const [lng, lat] = feature.geometry.coordinates;
                minLng = Math.min(minLng, lng);
                minLat = Math.min(minLat, lat);
                maxLng = Math.max(maxLng, lng);
                maxLat = Math.max(maxLat, lat);
            }
        });

        if (isFinite(minLng)) {
            stats.bounds = [minLng, minLat, maxLng, maxLat];
        }

        // Calculate weight statistics if weighted
        if (weightProperty) {
            const weights = features
                .map(f => f.properties?.[weightProperty])
                .filter(w => typeof w === 'number' && !isNaN(w));

            if (weights.length > 0) {
                stats.totalWeight = weights.reduce((sum, w) => sum + w, 0);
                stats.maxWeight = Math.max(...weights);
                stats.minWeight = Math.min(...weights);
            }
        }

        return stats;
    } catch (error) {
        console.error('Error calculating statistics:', error);
        return null;
    }
}

/**
 * Removes heatmap layer
 * @param {string} mapId - Map container ID
 * @param {string} layerId - Heatmap layer ID
 */
export function removeHeatmap(mapId, layerId) {
    const map = mapInstances.get(mapId);
    if (!map) return;

    try {
        if (map.getLayer(layerId)) {
            map.removeLayer(layerId);
        }

        heatmaps.delete(layerId);
        console.log(`Heatmap layer ${layerId} removed`);
    } catch (error) {
        console.error('Error removing heatmap:', error);
    }
}

/**
 * Exports heatmap as image (captures current map view)
 * @param {string} mapId - Map container ID
 * @returns {string} Base64 encoded image data
 */
export function exportHeatmapImage(mapId) {
    const map = mapInstances.get(mapId);
    if (!map) return null;

    try {
        const canvas = map.getCanvas();
        return canvas.toDataURL('image/png');
    } catch (error) {
        console.error('Error exporting heatmap image:', error);
        return null;
    }
}

/**
 * Creates the public API for a heatmap instance
 */
function createHeatmapAPI(layerId) {
    const heatmap = heatmaps.get(layerId);
    if (!heatmap) return null;

    return {
        updateRadius: (radius) => updateHeatmapRadius(heatmap.mapId, layerId, radius),
        updateIntensity: (intensity) => updateHeatmapIntensity(heatmap.mapId, layerId, intensity),
        updateOpacity: (opacity) => updateHeatmapOpacity(heatmap.mapId, layerId, opacity),
        updateGradient: (gradientType, customGradient) => updateHeatmapGradient(heatmap.mapId, layerId, gradientType, customGradient),
        setWeight: (property) => setHeatmapWeight(heatmap.mapId, layerId, property),
        setVisibility: (visible) => setHeatmapVisibility(heatmap.mapId, layerId, visible),
        updateData: (geojson) => updateHeatmapData(heatmap.mapId, heatmap.sourceId, geojson),
        getStatistics: () => calculateHeatmapStatistics(heatmap.mapId, heatmap.sourceId, heatmap.options.weightProperty),
        exportImage: () => exportHeatmapImage(heatmap.mapId),
        remove: () => removeHeatmap(heatmap.mapId, layerId),
        dispose: () => {
            removeHeatmap(heatmap.mapId, layerId);
        }
    };
}

/**
 * Gets heatmap instance by ID (for debugging)
 */
export function getHeatmap(layerId) {
    return heatmaps.get(layerId);
}

/**
 * Gets all active heatmaps for a map
 */
export function getMapHeatmaps(mapId) {
    return Array.from(heatmaps.values()).filter(h => h.mapId === mapId);
}
