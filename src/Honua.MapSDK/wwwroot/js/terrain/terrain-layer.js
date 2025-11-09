// Honua Terrain Layer Module
// Provides 3D terrain visualization using Deck.gl TerrainLayer
// Supports Mapbox Terrain-RGB, quantized mesh, and custom elevation sources

import { TerrainLayer } from 'https://cdn.jsdelivr.net/npm/@deck.gl/geo-layers@9/+esm';
import { COORDINATE_SYSTEM } from 'https://cdn.jsdelivr.net/npm/@deck.gl/core@9/+esm';

const terrainLayers = new Map();

/**
 * Add a terrain layer to the map
 * @param {string} mapId - Map identifier
 * @param {Object} options - Terrain layer options
 */
export function addTerrainLayer(mapId, options) {
    try {
        const deckInstance = getDeckInstance(mapId);
        if (!deckInstance) {
            console.error('Deck.gl instance not found for map:', mapId);
            return;
        }

        const layer = createTerrainLayer(options);
        terrainLayers.set(options.layerId, layer);

        // Add to existing layers
        const currentLayers = deckInstance.props.layers || [];
        deckInstance.setProps({
            layers: [...currentLayers, layer]
        });

        console.log('Terrain layer added:', options.layerId);
    } catch (error) {
        console.error('Error adding terrain layer:', error);
    }
}

/**
 * Remove a terrain layer
 */
export function removeTerrainLayer(mapId, layerId) {
    try {
        const deckInstance = getDeckInstance(mapId);
        if (!deckInstance) return;

        const currentLayers = deckInstance.props.layers || [];
        const filteredLayers = currentLayers.filter(l => l.id !== layerId);

        deckInstance.setProps({
            layers: filteredLayers
        });

        terrainLayers.delete(layerId);
        console.log('Terrain layer removed:', layerId);
    } catch (error) {
        console.error('Error removing terrain layer:', error);
    }
}

/**
 * Update terrain exaggeration
 */
export function updateTerrainExaggeration(mapId, layerId, exaggeration) {
    try {
        const layer = terrainLayers.get(layerId);
        if (!layer) return;

        const deckInstance = getDeckInstance(mapId);
        if (!deckInstance) return;

        const updatedLayer = layer.clone({
            elevationScale: exaggeration
        });

        terrainLayers.set(layerId, updatedLayer);

        const currentLayers = deckInstance.props.layers || [];
        const newLayers = currentLayers.map(l =>
            l.id === layerId ? updatedLayer : l
        );

        deckInstance.setProps({ layers: newLayers });
    } catch (error) {
        console.error('Error updating terrain exaggeration:', error);
    }
}

/**
 * Update terrain material properties
 */
export function updateTerrainMaterial(mapId, layerId, material) {
    try {
        const layer = terrainLayers.get(layerId);
        if (!layer) return;

        const deckInstance = getDeckInstance(mapId);
        if (!deckInstance) return;

        const updatedLayer = layer.clone({
            material: {
                ambient: material.ambient || 0.3,
                diffuse: material.diffuse || 0.6,
                shininess: material.shininess || 32,
                specularColor: [material.specularColor || 0.1, material.specularColor || 0.1, material.specularColor || 0.1]
            }
        });

        terrainLayers.set(layerId, updatedLayer);

        const currentLayers = deckInstance.props.layers || [];
        const newLayers = currentLayers.map(l =>
            l.id === layerId ? updatedLayer : l
        );

        deckInstance.setProps({ layers: newLayers });
    } catch (error) {
        console.error('Error updating terrain material:', error);
    }
}

/**
 * Toggle wireframe rendering
 */
export function toggleWireframe(mapId, layerId, enabled) {
    try {
        const layer = terrainLayers.get(layerId);
        if (!layer) return;

        const deckInstance = getDeckInstance(mapId);
        if (!deckInstance) return;

        const updatedLayer = layer.clone({
            wireframe: enabled
        });

        terrainLayers.set(layerId, updatedLayer);

        const currentLayers = deckInstance.props.layers || [];
        const newLayers = currentLayers.map(l =>
            l.id === layerId ? updatedLayer : l
        );

        deckInstance.setProps({ layers: newLayers });
    } catch (error) {
        console.error('Error toggling wireframe:', error);
    }
}

/**
 * Get elevation at a specific point
 */
export function getElevationAtPoint(mapId, layerId, longitude, latitude) {
    try {
        // This would query the terrain tile at the given coordinates
        // For now, return a placeholder
        return 0;
    } catch (error) {
        console.error('Error getting elevation:', error);
        return null;
    }
}

/**
 * Create a Deck.gl TerrainLayer
 */
function createTerrainLayer(options) {
    const {
        layerId,
        terrainSource,
        encoding,
        exaggeration,
        wireframe,
        material,
        enableLOD,
        maxLOD,
        colorMap
    } = options;

    // Determine terrain image URL provider based on encoding
    const elevationDecoder = getElevationDecoder(encoding);
    const getTileData = getTileDataProvider(terrainSource, encoding);

    return new TerrainLayer({
        id: layerId,
        minZoom: 0,
        maxZoom: maxLOD || 16,
        strategy: enableLOD ? 'best-available' : 'no-overlap',
        elevationDecoder,
        elevationData: getTileData,
        texture: getTextureProvider(colorMap),
        wireframe: wireframe || false,
        color: [255, 255, 255],
        elevationScale: exaggeration || 1.0,
        material: material || {
            ambient: 0.3,
            diffuse: 0.6,
            shininess: 32,
            specularColor: [0.1, 0.1, 0.1]
        },
        // Performance optimizations
        loadOptions: {
            terrain: {
                meshMaxError: 4.0,
                bounds: null
            }
        },
        onTileLoad: (tile) => {
            console.log('Terrain tile loaded:', tile);
        },
        onTileError: (error) => {
            console.error('Terrain tile error:', error);
        }
    });
}

/**
 * Get elevation decoder based on encoding type
 */
function getElevationDecoder(encoding) {
    switch (encoding) {
        case 'terrain-rgb':
        case 'mapbox':
            // Mapbox Terrain-RGB encoding
            // height = -10000 + ((R * 256 * 256 + G * 256 + B) * 0.1)
            return {
                rScaler: 256 * 256,
                gScaler: 256,
                bScaler: 1,
                offset: -10000
            };

        case 'terrarium':
            // Terrarium encoding
            // height = (R * 256 + G + B / 256) - 32768
            return {
                rScaler: 256,
                gScaler: 1,
                bScaler: 1 / 256,
                offset: -32768
            };

        default:
            // Default to Mapbox Terrain-RGB
            return {
                rScaler: 256 * 256,
                gScaler: 256,
                bScaler: 1,
                offset: -10000
            };
    }
}

/**
 * Get tile data provider function
 */
function getTileDataProvider(terrainSource, encoding) {
    if (!terrainSource) {
        // Default to Mapbox Terrain-RGB tiles
        return 'https://api.mapbox.com/v4/mapbox.terrain-rgb/{z}/{x}/{y}.png?access_token={accessToken}';
    }

    if (typeof terrainSource === 'string') {
        // URL template
        return terrainSource;
    }

    // Custom function
    return terrainSource;
}

/**
 * Get texture provider (for color mapping)
 */
function getTextureProvider(colorMap) {
    if (!colorMap) {
        // Use elevation data as texture
        return null;
    }

    // Custom color mapping function
    return (tile) => {
        // Would generate texture from elevation data using color map
        return null;
    };
}

/**
 * Get Deck.gl instance for a map
 */
function getDeckInstance(mapId) {
    // Access Deck.gl instance from global Honua3D object
    if (typeof window.Honua3D === 'undefined') {
        console.error('Honua3D not initialized');
        return null;
    }

    return window.Honua3D._deckInstances?.get(mapId);
}

/**
 * Create hillshade layer from elevation data
 */
export function addHillshadeLayer(mapId, layerId, options) {
    try {
        const {
            terrainSource,
            azimuth = 315,
            altitude = 45,
            exaggeration = 1.0,
            opacity = 0.5
        } = options;

        // Create custom layer that renders hillshade
        const layer = new TerrainLayer({
            id: layerId,
            elevationData: terrainSource,
            elevationDecoder: getElevationDecoder('terrain-rgb'),
            texture: null,
            color: [255, 255, 255],
            opacity: opacity,
            // Custom shader for hillshade
            getPolygonOffset: () => [0, -10],
            // Would inject custom shader code here
        });

        const deckInstance = getDeckInstance(mapId);
        if (deckInstance) {
            const currentLayers = deckInstance.props.layers || [];
            deckInstance.setProps({
                layers: [...currentLayers, layer]
            });
        }

        terrainLayers.set(layerId, layer);
        console.log('Hillshade layer added:', layerId);
    } catch (error) {
        console.error('Error adding hillshade layer:', error);
    }
}

/**
 * Create slope analysis layer
 */
export function addSlopeLayer(mapId, layerId, options) {
    try {
        const {
            terrainSource,
            colorRamp = [
                [0, [34, 139, 34]],      // 0-15%: green
                [15, [255, 255, 0]],     // 15-30%: yellow
                [30, [255, 165, 0]],     // 30-45%: orange
                [45, [255, 0, 0]]        // 45%+: red
            ]
        } = options;

        // Create layer with custom slope coloring
        const layer = new TerrainLayer({
            id: layerId,
            elevationData: terrainSource,
            elevationDecoder: getElevationDecoder('terrain-rgb'),
            // Custom color function based on slope
            // Would implement slope calculation in shader
        });

        const deckInstance = getDeckInstance(mapId);
        if (deckInstance) {
            const currentLayers = deckInstance.props.layers || [];
            deckInstance.setProps({
                layers: [...currentLayers, layer]
            });
        }

        terrainLayers.set(layerId, layer);
        console.log('Slope layer added:', layerId);
    } catch (error) {
        console.error('Error adding slope layer:', error);
    }
}

/**
 * Add contour lines layer
 */
export function addContourLayer(mapId, layerId, options) {
    try {
        const {
            terrainSource,
            interval = 100,  // Contour interval in meters
            color = [139, 69, 19],
            width = 1
        } = options;

        // Would create contour lines from elevation data
        // This requires processing the elevation tiles to extract contours
        console.log('Contour layer creation not yet implemented');
    } catch (error) {
        console.error('Error adding contour layer:', error);
    }
}

console.log('Honua Terrain Layer module loaded');
