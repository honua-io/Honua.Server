// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Deck.gl Integration for Honua.MapSDK
 * Provides WebGL-powered big data visualization on top of MapLibre GL JS
 *
 * Supports:
 * - ScatterplotLayer
 * - HexagonLayer
 * - ArcLayer
 * - GridLayer
 * - ScreenGridLayer
 * - Real-time data updates
 * - Custom styling
 */

import { Deck } from 'https://cdn.jsdelivr.net/npm/@deck.gl/core@8.9.33/+esm';
import { ScatterplotLayer } from 'https://cdn.jsdelivr.net/npm/@deck.gl/layers@8.9.33/+esm';
import { HexagonLayer, GridLayer, ScreenGridLayer } from 'https://cdn.jsdelivr.net/npm/@deck.gl/aggregation-layers@8.9.33/+esm';
import { ArcLayer } from 'https://cdn.jsdelivr.net/npm/@deck.gl/layers@8.9.33/+esm';

/**
 * Deck.gl Layer Manager
 * Manages Deck.gl overlay on MapLibre maps
 */
export class DeckLayerManager {
    constructor(mapInstance, mapElement, dotNetRef) {
        this.mapInstance = mapInstance;
        this.map = mapInstance.map;
        this.mapElement = mapElement;
        this.dotNetRef = dotNetRef;
        this.deckOverlay = null;
        this.layers = new Map();

        this.initialize();
    }

    /**
     * Initialize Deck.gl overlay
     */
    initialize() {
        // Create Deck.gl overlay using MapLibre's canvas
        this.deckOverlay = new Deck({
            canvas: 'deck-canvas',
            width: '100%',
            height: '100%',
            initialViewState: {
                longitude: this.map.getCenter().lng,
                latitude: this.map.getCenter().lat,
                zoom: this.map.getZoom(),
                bearing: this.map.getBearing(),
                pitch: this.map.getPitch()
            },
            controller: false, // MapLibre controls camera
            layers: [],
            // Sync with MapLibre
            onViewStateChange: null
        });

        // Create canvas overlay
        this._createCanvasOverlay();

        // Sync Deck.gl view with MapLibre
        this._syncViewState();
        this.map.on('move', () => this._syncViewState());
        this.map.on('moveend', () => this._syncViewState());

        console.log('Deck.gl overlay initialized');
    }

    /**
     * Create canvas overlay for Deck.gl
     */
    _createCanvasOverlay() {
        // Get map container
        const mapContainer = this.mapElement;

        // Create canvas overlay container
        let canvasContainer = document.getElementById('deck-canvas-container');
        if (!canvasContainer) {
            canvasContainer = document.createElement('div');
            canvasContainer.id = 'deck-canvas-container';
            canvasContainer.style.position = 'absolute';
            canvasContainer.style.top = '0';
            canvasContainer.style.left = '0';
            canvasContainer.style.width = '100%';
            canvasContainer.style.height = '100%';
            canvasContainer.style.pointerEvents = 'none'; // Allow map interactions

            // Create canvas
            const canvas = document.createElement('canvas');
            canvas.id = 'deck-canvas';
            canvas.style.width = '100%';
            canvas.style.height = '100%';
            canvasContainer.appendChild(canvas);

            mapContainer.appendChild(canvasContainer);
        }
    }

    /**
     * Sync Deck.gl view state with MapLibre
     */
    _syncViewState() {
        const center = this.map.getCenter();
        const zoom = this.map.getZoom();
        const bearing = this.map.getBearing();
        const pitch = this.map.getPitch();

        this.deckOverlay.setProps({
            viewState: {
                longitude: center.lng,
                latitude: center.lat,
                zoom: zoom,
                bearing: bearing,
                pitch: pitch
            }
        });
    }

    /**
     * Add or update a Deck.gl layer
     */
    addLayer(layerConfig) {
        const layerId = layerConfig.id;
        const layerType = layerConfig.type;

        console.log(`Adding Deck.gl layer: ${layerId} (${layerType})`);

        // Create layer based on type
        let layer;
        switch (layerType) {
            case 'scatterplot':
                layer = this._createScatterplotLayer(layerConfig);
                break;
            case 'hexagon':
                layer = this._createHexagonLayer(layerConfig);
                break;
            case 'arc':
                layer = this._createArcLayer(layerConfig);
                break;
            case 'grid':
                layer = this._createGridLayer(layerConfig);
                break;
            case 'screengrid':
                layer = this._createScreenGridLayer(layerConfig);
                break;
            default:
                console.error(`Unknown Deck.gl layer type: ${layerType}`);
                return;
        }

        // Store layer
        this.layers.set(layerId, layer);

        // Update Deck.gl
        this._updateDeckLayers();
    }

    /**
     * Create ScatterplotLayer
     */
    _createScatterplotLayer(config) {
        return new ScatterplotLayer({
            id: config.id,
            data: config.data || [],
            pickable: config.pickable !== false,
            opacity: config.opacity || 0.8,
            stroked: config.stroked !== false,
            filled: config.filled !== false,
            radiusScale: config.radiusScale || 1,
            radiusMinPixels: config.radiusMinPixels || 1,
            radiusMaxPixels: config.radiusMaxPixels || 100,
            lineWidthMinPixels: config.lineWidthMinPixels || 1,
            getPosition: d => config.getPosition ? this._evaluateAccessor(config.getPosition, d) : d.position,
            getRadius: d => config.getRadius ? this._evaluateAccessor(config.getRadius, d) : d.radius || 10,
            getFillColor: d => config.getFillColor ? this._evaluateAccessor(config.getFillColor, d) : d.color || [255, 140, 0],
            getLineColor: d => config.getLineColor ? this._evaluateAccessor(config.getLineColor, d) : d.lineColor || [0, 0, 0],
            onClick: (info) => this._handleLayerClick(config.id, info),
            onHover: (info) => this._handleLayerHover(config.id, info)
        });
    }

    /**
     * Create HexagonLayer
     */
    _createHexagonLayer(config) {
        return new HexagonLayer({
            id: config.id,
            data: config.data || [],
            pickable: config.pickable !== false,
            extruded: config.extruded !== false,
            radius: config.radius || 1000,
            elevationScale: config.elevationScale || 4,
            elevationRange: config.elevationRange || [0, 3000],
            coverage: config.coverage || 1,
            upperPercentile: config.upperPercentile || 100,
            colorRange: config.colorRange || [
                [1, 152, 189],
                [73, 227, 206],
                [216, 254, 181],
                [254, 237, 177],
                [254, 173, 84],
                [209, 55, 78]
            ],
            getPosition: d => config.getPosition ? this._evaluateAccessor(config.getPosition, d) : d.position,
            getWeight: d => config.getWeight ? this._evaluateAccessor(config.getWeight, d) : 1,
            onClick: (info) => this._handleLayerClick(config.id, info),
            onHover: (info) => this._handleLayerHover(config.id, info)
        });
    }

    /**
     * Create ArcLayer
     */
    _createArcLayer(config) {
        return new ArcLayer({
            id: config.id,
            data: config.data || [],
            pickable: config.pickable !== false,
            getWidth: config.getWidth || 5,
            opacity: config.opacity || 0.8,
            getSourcePosition: d => config.getSourcePosition ? this._evaluateAccessor(config.getSourcePosition, d) : d.sourcePosition,
            getTargetPosition: d => config.getTargetPosition ? this._evaluateAccessor(config.getTargetPosition, d) : d.targetPosition,
            getSourceColor: d => config.getSourceColor ? this._evaluateAccessor(config.getSourceColor, d) : d.sourceColor || [255, 140, 0],
            getTargetColor: d => config.getTargetColor ? this._evaluateAccessor(config.getTargetColor, d) : d.targetColor || [0, 128, 255],
            getTilt: d => config.getTilt ? this._evaluateAccessor(config.getTilt, d) : 0,
            getHeight: d => config.getHeight ? this._evaluateAccessor(config.getHeight, d) : 0.5,
            onClick: (info) => this._handleLayerClick(config.id, info),
            onHover: (info) => this._handleLayerHover(config.id, info)
        });
    }

    /**
     * Create GridLayer
     */
    _createGridLayer(config) {
        return new GridLayer({
            id: config.id,
            data: config.data || [],
            pickable: config.pickable !== false,
            extruded: config.extruded !== false,
            cellSize: config.cellSize || 1000,
            elevationScale: config.elevationScale || 4,
            elevationRange: config.elevationRange || [0, 3000],
            coverage: config.coverage || 1,
            upperPercentile: config.upperPercentile || 100,
            colorRange: config.colorRange || [
                [1, 152, 189],
                [73, 227, 206],
                [216, 254, 181],
                [254, 237, 177],
                [254, 173, 84],
                [209, 55, 78]
            ],
            getPosition: d => config.getPosition ? this._evaluateAccessor(config.getPosition, d) : d.position,
            getWeight: d => config.getWeight ? this._evaluateAccessor(config.getWeight, d) : 1,
            onClick: (info) => this._handleLayerClick(config.id, info),
            onHover: (info) => this._handleLayerHover(config.id, info)
        });
    }

    /**
     * Create ScreenGridLayer
     */
    _createScreenGridLayer(config) {
        return new ScreenGridLayer({
            id: config.id,
            data: config.data || [],
            pickable: config.pickable !== false,
            opacity: config.opacity || 0.8,
            cellSizePixels: config.cellSizePixels || 50,
            colorRange: config.colorRange || [
                [0, 25, 0, 25],
                [0, 85, 0, 85],
                [0, 127, 0, 127],
                [0, 170, 0, 170],
                [0, 190, 0, 190],
                [0, 255, 0, 255]
            ],
            getPosition: d => config.getPosition ? this._evaluateAccessor(config.getPosition, d) : d.position,
            getWeight: d => config.getWeight ? this._evaluateAccessor(config.getWeight, d) : 1,
            onClick: (info) => this._handleLayerClick(config.id, info),
            onHover: (info) => this._handleLayerHover(config.id, info)
        });
    }

    /**
     * Evaluate accessor function or property
     */
    _evaluateAccessor(accessor, data) {
        if (typeof accessor === 'function') {
            return accessor(data);
        } else if (typeof accessor === 'string') {
            // Property path (e.g., "coordinates" or "properties.value")
            return accessor.split('.').reduce((obj, key) => obj?.[key], data);
        }
        return accessor;
    }

    /**
     * Update Deck.gl with current layers
     */
    _updateDeckLayers() {
        const layerArray = Array.from(this.layers.values());
        this.deckOverlay.setProps({
            layers: layerArray
        });
        console.log(`Updated Deck.gl with ${layerArray.length} layers`);
    }

    /**
     * Remove a layer
     */
    removeLayer(layerId) {
        if (this.layers.has(layerId)) {
            this.layers.delete(layerId);
            this._updateDeckLayers();
            console.log(`Removed Deck.gl layer: ${layerId}`);
        }
    }

    /**
     * Update layer data
     */
    updateLayerData(layerId, data) {
        const layer = this.layers.get(layerId);
        if (layer) {
            // Get layer config
            const config = {
                id: layerId,
                type: layer.constructor.name.toLowerCase().replace('layer', ''),
                data: data,
                ...layer.props
            };

            // Recreate layer with new data
            this.addLayer(config);
            console.log(`Updated data for layer: ${layerId} (${data.length} items)`);
        }
    }

    /**
     * Update layer visibility
     */
    setLayerVisibility(layerId, visible) {
        const layer = this.layers.get(layerId);
        if (layer) {
            layer.props.visible = visible;
            this._updateDeckLayers();
            console.log(`Set layer ${layerId} visibility: ${visible}`);
        }
    }

    /**
     * Update layer opacity
     */
    setLayerOpacity(layerId, opacity) {
        const layer = this.layers.get(layerId);
        if (layer) {
            layer.props.opacity = opacity;
            this._updateDeckLayers();
            console.log(`Set layer ${layerId} opacity: ${opacity}`);
        }
    }

    /**
     * Handle layer click
     */
    _handleLayerClick(layerId, info) {
        if (info.object && this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnDeckLayerClickedInternal',
                layerId,
                info.object,
                info.x,
                info.y,
                [info.coordinate[0], info.coordinate[1]]
            ).catch(err => console.error('Error invoking OnDeckLayerClickedInternal:', err));
        }
    }

    /**
     * Handle layer hover
     */
    _handleLayerHover(layerId, info) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnDeckLayerHoveredInternal',
                layerId,
                info.object || null,
                info.x,
                info.y
            ).catch(err => console.error('Error invoking OnDeckLayerHoveredInternal:', err));
        }
    }

    /**
     * Get all layers
     */
    getLayers() {
        return Array.from(this.layers.keys());
    }

    /**
     * Clear all layers
     */
    clearAllLayers() {
        this.layers.clear();
        this._updateDeckLayers();
        console.log('Cleared all Deck.gl layers');
    }

    /**
     * Dispose resources
     */
    dispose() {
        if (this.deckOverlay) {
            this.deckOverlay.finalize();
            this.deckOverlay = null;
        }

        // Remove canvas overlay
        const canvasContainer = document.getElementById('deck-canvas-container');
        if (canvasContainer) {
            canvasContainer.remove();
        }

        this.layers.clear();
        console.log('Deck.gl overlay disposed');
    }
}

/**
 * Initialize Deck.gl overlay on a MapLibre map
 */
export function initializeDeckOverlay(mapInstance, mapElement, dotNetRef) {
    try {
        const manager = new DeckLayerManager(mapInstance, mapElement, dotNetRef);
        return manager;
    } catch (error) {
        console.error('Error initializing Deck.gl overlay:', error);
        throw error;
    }
}

/**
 * Utility: Load data from URL for Deck.gl layer
 */
export async function loadDeckLayerData(url) {
    try {
        const response = await fetch(url);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        const data = await response.json();
        console.log(`Loaded Deck.gl layer data from ${url}: ${data.length} items`);
        return data;
    } catch (error) {
        console.error(`Failed to load Deck.gl layer data from ${url}:`, error);
        throw error;
    }
}

// Export for module usage
export default {
    DeckLayerManager,
    initializeDeckOverlay,
    loadDeckLayerData
};
