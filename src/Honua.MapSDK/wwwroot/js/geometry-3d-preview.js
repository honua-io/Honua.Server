/**
 * Honua Geometry 3D Preview
 *
 * Renders 3D mesh previews using Deck.gl SimpleMeshLayer.
 * Provides interactive controls for rotation, zoom, and camera reset.
 *
 * @module geometry-3d-preview
 */

import { Deck } from 'https://cdn.jsdelivr.net/npm/@deck.gl/core@8.9.33/+esm';
import { SimpleMeshLayer } from 'https://cdn.jsdelivr.net/npm/@deck.gl/mesh-layers@8.9.33/+esm';
import { OrbitView } from 'https://cdn.jsdelivr.net/npm/@deck.gl/core@8.9.33/+esm';

// Store active Deck.gl instances
const deckInstances = new Map();

/**
 * Render a mesh preview on a canvas element
 *
 * @param {HTMLCanvasElement} canvas - Canvas element to render on
 * @param {string} apiUrl - API URL to fetch mesh data from
 * @param {object} options - Rendering options
 * @returns {Promise<object>} Preview metadata
 */
export async function renderMeshPreview(canvas, apiUrl, options = {}) {
    try {
        console.log('Loading mesh preview from:', apiUrl);

        // Fetch mesh data from API
        const response = await fetch(apiUrl);
        if (!response.ok) {
            throw new Error(`Failed to fetch mesh data: ${response.status} ${response.statusText}`);
        }

        const previewData = await response.json();
        console.log('Mesh preview data loaded:', previewData);

        // Extract mesh data based on format
        let meshData;
        if (previewData.format === 'simple' && previewData.meshData) {
            meshData = previewData.meshData;
        } else if (previewData.format === 'gltf' && previewData.gltfData) {
            // For glTF format, we would need to parse the glTF structure
            // For now, this is a placeholder
            throw new Error('glTF format preview not yet implemented');
        } else {
            throw new Error('Invalid preview data format');
        }

        // Create Deck.gl instance
        const deck = createDeckInstance(canvas, meshData, previewData, options);

        // Store instance for later cleanup
        deckInstances.set(canvas, deck);

        return {
            success: true,
            vertexCount: previewData.vertexCount,
            faceCount: previewData.faceCount,
            boundingBox: previewData.boundingBox,
            center: previewData.center
        };

    } catch (error) {
        console.error('Error rendering mesh preview:', error);
        throw error;
    }
}

/**
 * Create a Deck.gl instance for mesh rendering
 *
 * @param {HTMLCanvasElement} canvas - Canvas element
 * @param {object} meshData - Mesh data (positions, normals, indices)
 * @param {object} previewData - Full preview response
 * @param {object} options - Rendering options
 * @returns {Deck} Deck.gl instance
 */
function createDeckInstance(canvas, meshData, previewData, options) {
    const { positions, normals, indices, colors } = meshData;
    const { center, boundingBox } = previewData;

    // Calculate bounding sphere radius for camera setup
    const dx = boundingBox.maxX - boundingBox.minX;
    const dy = boundingBox.maxY - boundingBox.minY;
    const dz = boundingBox.maxZ - boundingBox.minZ;
    const radius = Math.sqrt(dx * dx + dy * dy + dz * dz) / 2;

    // Convert flat arrays to format expected by SimpleMeshLayer
    const mesh = {
        positions: { value: new Float32Array(positions), size: 3 },
        normals: { value: new Float32Array(normals), size: 3 },
        indices: { value: new Uint32Array(indices), size: 1 }
    };

    // Add colors if available
    if (colors && colors.length > 0) {
        mesh.colors = { value: new Uint8Array(colors), size: 4 };
    }

    // Create SimpleMeshLayer
    const layer = new SimpleMeshLayer({
        id: 'mesh-preview-layer',
        data: [{ position: [center.longitude, center.latitude, center.altitude] }],
        mesh: mesh,
        getPosition: d => d.position,
        getColor: colors && colors.length > 0 ? undefined : [200, 200, 200, 255],
        getOrientation: [0, 0, 0],
        material: {
            ambient: 0.4,
            diffuse: 0.6,
            shininess: 32,
            specularColor: [255, 255, 255]
        },
        pickable: false
    });

    // Calculate initial camera position
    const cameraDistance = radius * 3;
    const initialViewState = {
        target: [center.longitude, center.latitude, center.altitude],
        rotationX: 30,
        rotationOrbit: 30,
        zoom: 0,
        minZoom: -5,
        maxZoom: 5
    };

    // Create Deck.gl instance
    const deck = new Deck({
        canvas: canvas,
        views: [new OrbitView({ orbitAxis: 'Y' })],
        initialViewState: initialViewState,
        controller: {
            inertia: true,
            scrollZoom: true,
            dragRotate: true,
            dragPan: true,
            keyboard: true
        },
        layers: [layer],
        parameters: {
            depthTest: true,
            clearColor: [0.95, 0.95, 0.95, 1.0]
        },
        onLoad: () => {
            console.log('Deck.gl instance loaded');
        },
        onError: (error) => {
            console.error('Deck.gl error:', error);
        }
    });

    return deck;
}

/**
 * Rotate the model by a specified angle
 *
 * @param {HTMLCanvasElement} canvas - Canvas element
 * @param {number} degrees - Rotation angle in degrees (positive = clockwise)
 */
export function rotateModel(canvas, degrees) {
    const deck = deckInstances.get(canvas);
    if (!deck) {
        console.warn('No Deck.gl instance found for canvas');
        return;
    }

    const viewState = deck.viewState;
    const newRotation = (viewState.rotationOrbit || 0) + degrees;

    deck.setProps({
        initialViewState: {
            ...viewState,
            rotationOrbit: newRotation
        }
    });
}

/**
 * Zoom the model by a specified factor
 *
 * @param {HTMLCanvasElement} canvas - Canvas element
 * @param {number} factor - Zoom factor (> 1 = zoom in, < 1 = zoom out)
 */
export function zoomModel(canvas, factor) {
    const deck = deckInstances.get(canvas);
    if (!deck) {
        console.warn('No Deck.gl instance found for canvas');
        return;
    }

    const viewState = deck.viewState;
    const currentZoom = viewState.zoom || 0;
    const zoomDelta = Math.log2(factor);
    const newZoom = Math.max(viewState.minZoom || -5, Math.min(viewState.maxZoom || 5, currentZoom + zoomDelta));

    deck.setProps({
        initialViewState: {
            ...viewState,
            zoom: newZoom
        }
    });
}

/**
 * Reset camera to initial position
 *
 * @param {HTMLCanvasElement} canvas - Canvas element
 */
export function resetCamera(canvas) {
    const deck = deckInstances.get(canvas);
    if (!deck) {
        console.warn('No Deck.gl instance found for canvas');
        return;
    }

    const viewState = deck.viewState;

    deck.setProps({
        initialViewState: {
            target: viewState.target,
            rotationX: 30,
            rotationOrbit: 30,
            zoom: 0,
            minZoom: viewState.minZoom,
            maxZoom: viewState.maxZoom
        }
    });
}

/**
 * Dispose of the Deck.gl instance and clean up resources
 *
 * @param {HTMLCanvasElement} canvas - Canvas element
 */
export function dispose(canvas) {
    const deck = deckInstances.get(canvas);
    if (deck) {
        deck.finalize();
        deckInstances.delete(canvas);
        console.log('Deck.gl instance disposed');
    }
}

/**
 * Get current camera state
 *
 * @param {HTMLCanvasElement} canvas - Canvas element
 * @returns {object|null} Camera state or null if not found
 */
export function getCameraState(canvas) {
    const deck = deckInstances.get(canvas);
    if (!deck) {
        return null;
    }

    return deck.viewState;
}

/**
 * Update layer properties (e.g., material, color)
 *
 * @param {HTMLCanvasElement} canvas - Canvas element
 * @param {object} properties - Properties to update
 */
export function updateLayerProperties(canvas, properties) {
    const deck = deckInstances.get(canvas);
    if (!deck) {
        console.warn('No Deck.gl instance found for canvas');
        return;
    }

    const currentLayers = deck.props.layers;
    const updatedLayers = currentLayers.map(layer => {
        if (layer.id === 'mesh-preview-layer') {
            return layer.clone(properties);
        }
        return layer;
    });

    deck.setProps({ layers: updatedLayers });
}

// Export for Node.js testing environments
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        renderMeshPreview,
        rotateModel,
        zoomModel,
        resetCamera,
        dispose,
        getCameraState,
        updateLayerProperties
    };
}
