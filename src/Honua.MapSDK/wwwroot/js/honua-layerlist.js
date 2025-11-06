// HonuaLayerList - Layer tree/table of contents for Honua.MapSDK
// Manages layer visibility, opacity, ordering, and metadata

/**
 * Get all layers from the map
 * @param {string} mapId - Map instance ID
 * @returns {string} JSON string of layer information
 */
export function getMapLayers(mapId) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return JSON.stringify([]);
        }

        const style = mapInstance.getStyle();
        if (!style || !style.layers) {
            return JSON.stringify([]);
        }

        const layers = style.layers
            .filter(layer => {
                // Filter out internal/system layers
                return !layer.id.startsWith('gl-') &&
                       !layer.id.startsWith('mapbox-') &&
                       layer.type !== 'background';
            })
            .map((layer, index) => {
                const layerInfo = {
                    id: layer.id,
                    name: layer.metadata?.name || layer.id,
                    type: layer.type,
                    sourceId: layer.source,
                    sourceLayer: layer['source-layer'],
                    visible: layer.layout?.visibility !== 'none',
                    opacity: getLayerOpacity(mapInstance, layer),
                    isLocked: layer.metadata?.locked === true,
                    groupId: layer.metadata?.group,
                    order: index,
                    minZoom: layer.minzoom,
                    maxZoom: layer.maxzoom,
                    extent: layer.metadata?.extent,
                    featureCount: layer.metadata?.featureCount,
                    description: layer.metadata?.description,
                    attribution: layer.metadata?.attribution,
                    isBasemap: layer.metadata?.isBasemap === true,
                    canRemove: layer.metadata?.canRemove !== false,
                    canRename: layer.metadata?.canRename !== false,
                    legendItems: layer.metadata?.legend || generateLegendFromPaint(layer),
                    metadata: layer.metadata || {}
                };

                return layerInfo;
            });

        return JSON.stringify(layers);
    } catch (error) {
        console.error('Error getting map layers:', error);
        return JSON.stringify([]);
    }
}

/**
 * Get opacity value for a layer
 * @param {object} map - Maplibre map instance
 * @param {object} layer - Layer definition
 * @returns {number} Opacity value (0-1)
 */
function getLayerOpacity(map, layer) {
    try {
        const paintProperty = `${layer.type}-opacity`;
        const opacity = map.getPaintProperty(layer.id, paintProperty);
        return opacity !== undefined ? opacity : 1.0;
    } catch {
        return 1.0;
    }
}

/**
 * Generate legend items from paint properties
 * @param {object} layer - Layer definition
 * @returns {array} Legend items
 */
function generateLegendFromPaint(layer) {
    const legendItems = [];

    try {
        if (layer.type === 'fill') {
            const color = layer.paint?.['fill-color'];
            if (color && typeof color === 'string') {
                legendItems.push({
                    label: layer.id,
                    color: color,
                    symbolType: 'polygon',
                    strokeColor: layer.paint?.['fill-outline-color'] || '#000',
                    strokeWidth: 1
                });
            }
        } else if (layer.type === 'line') {
            const color = layer.paint?.['line-color'];
            if (color && typeof color === 'string') {
                legendItems.push({
                    label: layer.id,
                    color: color,
                    symbolType: 'line',
                    strokeWidth: layer.paint?.['line-width'] || 1
                });
            }
        } else if (layer.type === 'circle') {
            const color = layer.paint?.['circle-color'];
            if (color && typeof color === 'string') {
                legendItems.push({
                    label: layer.id,
                    color: color,
                    symbolType: 'circle',
                    size: layer.paint?.['circle-radius'] || 5,
                    strokeColor: layer.paint?.['circle-stroke-color'],
                    strokeWidth: layer.paint?.['circle-stroke-width'] || 0
                });
            }
        }
    } catch (error) {
        console.error('Error generating legend:', error);
    }

    return legendItems;
}

/**
 * Set layer visibility
 * @param {string} mapId - Map instance ID
 * @param {string} layerId - Layer ID
 * @param {boolean} visible - Visibility state
 */
export function setLayerVisibility(mapId, layerId, visible) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return;
        }

        const layer = mapInstance.getLayer(layerId);
        if (!layer) {
            console.warn(`Layer not found: ${layerId}`);
            return;
        }

        mapInstance.setLayoutProperty(
            layerId,
            'visibility',
            visible ? 'visible' : 'none'
        );

        console.log(`Layer visibility set: ${layerId} = ${visible}`);
    } catch (error) {
        console.error('Error setting layer visibility:', error);
    }
}

/**
 * Set layer opacity
 * @param {string} mapId - Map instance ID
 * @param {string} layerId - Layer ID
 * @param {number} opacity - Opacity value (0-1)
 */
export function setLayerOpacity(mapId, layerId, opacity) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return;
        }

        const layer = mapInstance.getLayer(layerId);
        if (!layer) {
            console.warn(`Layer not found: ${layerId}`);
            return;
        }

        // Set opacity based on layer type
        const opacityProperty = `${layer.type}-opacity`;
        mapInstance.setPaintProperty(layerId, opacityProperty, opacity);

        console.log(`Layer opacity set: ${layerId} = ${opacity}`);
    } catch (error) {
        console.error('Error setting layer opacity:', error);
    }
}

/**
 * Move a layer in the rendering order
 * @param {string} mapId - Map instance ID
 * @param {string} layerId - Layer ID to move
 * @param {string} beforeId - Layer ID to insert before (null for top)
 */
export function moveLayer(mapId, layerId, beforeId) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return;
        }

        if (!mapInstance.getLayer(layerId)) {
            console.warn(`Layer not found: ${layerId}`);
            return;
        }

        if (beforeId && !mapInstance.getLayer(beforeId)) {
            console.warn(`Before layer not found: ${beforeId}`);
            return;
        }

        mapInstance.moveLayer(layerId, beforeId);
        console.log(`Layer moved: ${layerId} before ${beforeId || 'top'}`);
    } catch (error) {
        console.error('Error moving layer:', error);
    }
}

/**
 * Get the extent/bounding box of a layer
 * @param {string} mapId - Map instance ID
 * @param {string} layerId - Layer ID
 * @returns {array|null} Bounding box [west, south, east, north]
 */
export function getLayerExtent(mapId, layerId) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return null;
        }

        const layer = mapInstance.getLayer(layerId);
        if (!layer) {
            console.warn(`Layer not found: ${layerId}`);
            return null;
        }

        // Try to get from metadata first
        if (layer.metadata?.extent) {
            return layer.metadata.extent;
        }

        // Try to query features and calculate extent
        const sourceId = layer.source;
        if (sourceId) {
            const features = mapInstance.querySourceFeatures(sourceId, {
                sourceLayer: layer['source-layer']
            });

            if (features && features.length > 0) {
                return calculateExtentFromFeatures(features);
            }
        }

        return null;
    } catch (error) {
        console.error('Error getting layer extent:', error);
        return null;
    }
}

/**
 * Calculate extent from an array of features
 * @param {array} features - GeoJSON features
 * @returns {array} Bounding box [west, south, east, north]
 */
function calculateExtentFromFeatures(features) {
    let minLng = Infinity;
    let minLat = Infinity;
    let maxLng = -Infinity;
    let maxLat = -Infinity;

    features.forEach(feature => {
        if (!feature.geometry || !feature.geometry.coordinates) return;

        const coords = getCoordinatesFromGeometry(feature.geometry);
        coords.forEach(coord => {
            const [lng, lat] = coord;
            minLng = Math.min(minLng, lng);
            minLat = Math.min(minLat, lat);
            maxLng = Math.max(maxLng, lng);
            maxLat = Math.max(maxLat, lat);
        });
    });

    if (minLng === Infinity) return null;

    return [minLng, minLat, maxLng, maxLat];
}

/**
 * Extract all coordinates from a geometry
 * @param {object} geometry - GeoJSON geometry
 * @returns {array} Array of [lng, lat] coordinates
 */
function getCoordinatesFromGeometry(geometry) {
    const coords = [];

    function extract(c) {
        if (typeof c[0] === 'number') {
            coords.push(c);
        } else {
            c.forEach(extract);
        }
    }

    if (geometry.coordinates) {
        extract(geometry.coordinates);
    }

    return coords;
}

/**
 * Zoom map to layer extent
 * @param {string} mapId - Map instance ID
 * @param {string} layerId - Layer ID
 */
export function zoomToLayer(mapId, layerId) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return;
        }

        const extent = getLayerExtent(mapId, layerId);
        if (!extent) {
            console.warn(`Could not determine extent for layer: ${layerId}`);
            return;
        }

        const [west, south, east, north] = extent;

        // Fit bounds with padding
        mapInstance.fitBounds(
            [[west, south], [east, north]],
            {
                padding: 50,
                duration: 1000,
                maxZoom: 16
            }
        );

        console.log(`Zoomed to layer: ${layerId}`);
    } catch (error) {
        console.error('Error zooming to layer:', error);
    }
}

/**
 * Remove a layer from the map
 * @param {string} mapId - Map instance ID
 * @param {string} layerId - Layer ID
 */
export function removeLayer(mapId, layerId) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return;
        }

        if (!mapInstance.getLayer(layerId)) {
            console.warn(`Layer not found: ${layerId}`);
            return;
        }

        mapInstance.removeLayer(layerId);
        console.log(`Layer removed: ${layerId}`);
    } catch (error) {
        console.error('Error removing layer:', error);
    }
}

/**
 * Get layer feature count
 * @param {string} mapId - Map instance ID
 * @param {string} layerId - Layer ID
 * @returns {number} Feature count
 */
export function getLayerFeatureCount(mapId, layerId) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return 0;
        }

        const layer = mapInstance.getLayer(layerId);
        if (!layer) {
            console.warn(`Layer not found: ${layerId}`);
            return 0;
        }

        // Try to get from metadata
        if (layer.metadata?.featureCount) {
            return layer.metadata.featureCount;
        }

        // Try to query features
        const sourceId = layer.source;
        if (sourceId) {
            const features = mapInstance.querySourceFeatures(sourceId, {
                sourceLayer: layer['source-layer']
            });
            return features ? features.length : 0;
        }

        return 0;
    } catch (error) {
        console.error('Error getting layer feature count:', error);
        return 0;
    }
}

/**
 * Set layer rendering order by array of layer IDs
 * @param {string} mapId - Map instance ID
 * @param {array} layerIds - Ordered array of layer IDs (top to bottom)
 */
export function setLayerOrder(mapId, layerIds) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return;
        }

        // Move layers from bottom to top to maintain order
        for (let i = layerIds.length - 1; i >= 0; i--) {
            const layerId = layerIds[i];
            const beforeId = i < layerIds.length - 1 ? layerIds[i + 1] : undefined;

            if (mapInstance.getLayer(layerId)) {
                mapInstance.moveLayer(layerId, beforeId);
            }
        }

        console.log('Layer order updated');
    } catch (error) {
        console.error('Error setting layer order:', error);
    }
}

/**
 * Toggle all layers visibility
 * @param {string} mapId - Map instance ID
 * @param {boolean} visible - Visibility state
 */
export function toggleAllLayers(mapId, visible) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return;
        }

        const style = mapInstance.getStyle();
        if (!style || !style.layers) {
            return;
        }

        style.layers.forEach(layer => {
            if (!layer.id.startsWith('gl-') &&
                !layer.id.startsWith('mapbox-') &&
                layer.type !== 'background' &&
                layer.metadata?.locked !== true) {
                setLayerVisibility(mapId, layer.id, visible);
            }
        });

        console.log(`All layers ${visible ? 'shown' : 'hidden'}`);
    } catch (error) {
        console.error('Error toggling all layers:', error);
    }
}

/**
 * Get layer metadata
 * @param {string} mapId - Map instance ID
 * @param {string} layerId - Layer ID
 * @returns {object|null} Layer metadata
 */
export function getLayerMetadata(mapId, layerId) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return null;
        }

        const layer = mapInstance.getLayer(layerId);
        if (!layer) {
            console.warn(`Layer not found: ${layerId}`);
            return null;
        }

        return layer.metadata || {};
    } catch (error) {
        console.error('Error getting layer metadata:', error);
        return null;
    }
}

/**
 * Update layer metadata
 * @param {string} mapId - Map instance ID
 * @param {string} layerId - Layer ID
 * @param {object} metadata - Metadata to update
 */
export function updateLayerMetadata(mapId, layerId, metadata) {
    try {
        const mapInstance = window.honuaMaps?.get(mapId);
        if (!mapInstance) {
            console.error(`Map instance not found: ${mapId}`);
            return;
        }

        const layer = mapInstance.getLayer(layerId);
        if (!layer) {
            console.warn(`Layer not found: ${layerId}`);
            return;
        }

        // Update metadata
        layer.metadata = {
            ...(layer.metadata || {}),
            ...metadata
        };

        console.log(`Layer metadata updated: ${layerId}`);
    } catch (error) {
        console.error('Error updating layer metadata:', error);
    }
}

export default {
    getMapLayers,
    setLayerVisibility,
    setLayerOpacity,
    moveLayer,
    getLayerExtent,
    zoomToLayer,
    removeLayer,
    getLayerFeatureCount,
    setLayerOrder,
    toggleAllLayers,
    getLayerMetadata,
    updateLayerMetadata
};
