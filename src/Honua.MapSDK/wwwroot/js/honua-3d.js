/**
 * Honua 3D
 *
 * Deck.gl integration layer for 3D geospatial visualization.
 * Provides high-performance WebGL rendering for 3D geometries with MapLibre GL JS.
 *
 * Dependencies:
 * - deck.gl (@deck.gl/core, @deck.gl/layers)
 * - MapLibre GL JS
 * - honua-geometry-3d.js
 *
 * @module Honua3D
 */

window.Honua3D = {
    _deckInstances: new Map(),
    _deckLoaded: false,

    /**
     * Check if Deck.gl is loaded.
     * Note: In production, load Deck.gl from CDN or bundle it.
     *
     * @returns {boolean} True if Deck.gl is available
     */
    isDeckGLAvailable() {
        return typeof deck !== 'undefined' && deck.Deck;
    },

    /**
     * Initialize 3D rendering for a map.
     * Creates a Deck.gl overlay synced with MapLibre GL map.
     *
     * @param {string} mapId - Map container ID
     * @param {object} mapLibreMap - MapLibre GL map instance
     * @param {object} options - Initialization options
     * @returns {object} Deck instance
     *
     * @example
     * const deckInstance = Honua3D.initialize('map', mapLibreMap, {
     *   enablePicking: true,
     *   enableLighting: true
     * });
     */
    initialize(mapId, mapLibreMap, options = {}) {
        if (!this.isDeckGLAvailable()) {
            console.warn('Deck.gl not loaded. 3D features will be unavailable. Load from: https://unpkg.com/deck.gl@^8.9.0/dist.min.js');
            return null;
        }

        // Create canvas for Deck.gl overlay
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer) {
            console.error(`Map container '${mapId}' not found`);
            return null;
        }

        // Check if already initialized
        if (this._deckInstances.has(mapId)) {
            console.warn(`Deck.gl already initialized for map '${mapId}'`);
            return this._deckInstances.get(mapId).deck;
        }

        const deckCanvas = document.createElement('canvas');
        deckCanvas.id = `${mapId}-deck-canvas`;
        deckCanvas.style.position = 'absolute';
        deckCanvas.style.top = '0';
        deckCanvas.style.left = '0';
        deckCanvas.style.width = '100%';
        deckCanvas.style.height = '100%';
        deckCanvas.style.pointerEvents = 'none'; // Allow map interactions to pass through
        mapContainer.appendChild(deckCanvas);

        // Create Deck instance
        const deckInstance = new deck.Deck({
            canvas: deckCanvas,
            width: '100%',
            height: '100%',
            initialViewState: {
                longitude: mapLibreMap.getCenter().lng,
                latitude: mapLibreMap.getCenter().lat,
                zoom: mapLibreMap.getZoom(),
                pitch: mapLibreMap.getPitch(),
                bearing: mapLibreMap.getBearing()
            },
            controller: false, // MapLibre handles controls
            layers: [],

            // Lighting (optional)
            effects: options.enableLighting ? [new deck.LightingEffect({
                shadowColor: [0, 0, 0, 0.5]
            })] : [],

            // Event handlers
            onHover: options.onHover,
            onClick: options.onClick
        });

        // Sync view state with MapLibre
        const syncViewState = () => {
            deckInstance.setProps({
                viewState: {
                    longitude: mapLibreMap.getCenter().lng,
                    latitude: mapLibreMap.getCenter().lat,
                    zoom: mapLibreMap.getZoom(),
                    pitch: mapLibreMap.getPitch(),
                    bearing: mapLibreMap.getBearing()
                }
            });
        };

        mapLibreMap.on('move', syncViewState);
        mapLibreMap.on('zoom', syncViewState);
        mapLibreMap.on('rotate', syncViewState);
        mapLibreMap.on('pitch', syncViewState);

        // Store instance
        this._deckInstances.set(mapId, {
            deck: deckInstance,
            map: mapLibreMap,
            layers: [],
            canvas: deckCanvas,
            syncViewState
        });

        this._deckLoaded = true;
        console.log(`Deck.gl initialized for map '${mapId}'`);
        return deckInstance;
    },

    /**
     * Add 3D GeoJSON layer to map.
     *
     * @param {string} mapId - Map ID
     * @param {string} layerId - Unique layer ID
     * @param {object} geojson - GeoJSON data
     * @param {object} options - Layer styling options
     * @returns {object|null} Created layer
     *
     * @example
     * Honua3D.addGeoJsonLayer('map', 'buildings', geojson, {
     *   extruded: true,
     *   fillColor: [160, 160, 180, 200],
     *   lineColor: [80, 80, 80],
     *   getElevation: f => f.properties.base_elevation,
     *   getHeight: f => f.properties.height
     * });
     */
    addGeoJsonLayer(mapId, layerId, geojson, options = {}) {
        const instance = this._deckInstances.get(mapId);
        if (!instance || !this.isDeckGLAvailable()) {
            console.error('Deck.gl not initialized for map:', mapId);
            return null;
        }

        // Parse 3D GeoJSON to extract metadata
        const parsed = window.HonuaGeometry3D.parse3DGeoJSON(geojson);

        const layer = new deck.GeoJsonLayer({
            id: layerId,
            data: parsed,

            // 3D rendering options
            extruded: options.extruded !== undefined ? options.extruded : true,
            wireframe: options.wireframe || false,

            // Elevation handling
            getElevation: options.getElevation || (f => {
                const coords = f.geometry.coordinates;
                return window.HonuaGeometry3D.getZ(coords) || 0;
            }),

            // Height for extrusion (buildings, etc.)
            getLineWidth: options.lineWidth || 1,
            getFillColor: options.fillColor || [160, 160, 180, 200],
            getLineColor: options.lineColor || [80, 80, 80, 255],

            // Performance
            pickable: options.pickable !== undefined ? options.pickable : true,
            autoHighlight: options.autoHighlight !== undefined ? options.autoHighlight : true,
            highlightColor: options.highlightColor || [255, 255, 0, 100],

            // Material (lighting)
            material: options.material || {
                ambient: 0.35,
                diffuse: 0.6,
                specular: 0.8,
                shininess: 32
            },

            // Callbacks
            onClick: options.onClick,
            onHover: options.onHover
        });

        instance.layers.push(layer);
        instance.deck.setProps({ layers: instance.layers });

        console.log(`Added 3D GeoJSON layer '${layerId}' with ${parsed.metadata.total} features (${parsed.metadata.with3D} 3D, ${parsed.metadata.without3D} 2D)`);
        return layer;
    },

    /**
     * Add 3D point cloud layer (optimized for millions of points).
     *
     * @param {string} mapId - Map ID
     * @param {string} layerId - Layer ID
     * @param {Array} points - Array of [lon, lat, z] coordinates
     * @param {object} options - Styling options
     * @returns {object|null} Created layer
     *
     * @example
     * const points = [
     *   [-122.4, 37.8, 50],
     *   [-122.5, 37.9, 100],
     *   // ... millions more
     * ];
     * Honua3D.addPointCloudLayer('map', 'sensors', points, {
     *   radius: 5,
     *   color: [255, 140, 0]
     * });
     */
    addPointCloudLayer(mapId, layerId, points, options = {}) {
        const instance = this._deckInstances.get(mapId);
        if (!instance || !this.isDeckGLAvailable()) {
            return null;
        }

        const layer = new deck.ScatterplotLayer({
            id: layerId,
            data: points,

            // Position using Z coordinate for elevation
            getPosition: d => d, // [lon, lat, z]
            getRadius: options.radius || 5,
            getFillColor: options.color || [255, 140, 0],

            // Performance optimizations
            radiusMinPixels: 1,
            radiusMaxPixels: 30,

            // Picking
            pickable: options.pickable !== undefined ? options.pickable : true,
            onClick: options.onClick,
            onHover: options.onHover
        });

        instance.layers.push(layer);
        instance.deck.setProps({ layers: instance.layers });

        console.log(`Added point cloud layer '${layerId}' with ${points.length} points`);
        return layer;
    },

    /**
     * Add 3D path layer (for flight paths, routes, etc.).
     *
     * @param {string} mapId - Map ID
     * @param {string} layerId - Layer ID
     * @param {Array} paths - Array of path objects with coordinates
     * @param {object} options - Styling options
     * @returns {object|null} Created layer
     */
    addPathLayer(mapId, layerId, paths, options = {}) {
        const instance = this._deckInstances.get(mapId);
        if (!instance || !this.isDeckGLAvailable()) {
            return null;
        }

        const layer = new deck.PathLayer({
            id: layerId,
            data: paths,

            getPath: d => d.coordinates || d,
            getColor: options.color || [255, 140, 0],
            getWidth: options.width || 5,

            widthMinPixels: 1,
            widthMaxPixels: 20,

            pickable: options.pickable !== undefined ? options.pickable : true,
            onClick: options.onClick,
            onHover: options.onHover
        });

        instance.layers.push(layer);
        instance.deck.setProps({ layers: instance.layers });

        return layer;
    },

    /**
     * Remove a layer from the map.
     *
     * @param {string} mapId - Map ID
     * @param {string} layerId - Layer ID to remove
     */
    removeLayer(mapId, layerId) {
        const instance = this._deckInstances.get(mapId);
        if (!instance) {
            return;
        }

        instance.layers = instance.layers.filter(l => l.id !== layerId);
        instance.deck.setProps({ layers: instance.layers });
        console.log(`Removed layer '${layerId}'`);
    },

    /**
     * Update layer data.
     *
     * @param {string} mapId - Map ID
     * @param {string} layerId - Layer ID
     * @param {*} newData - New data for the layer
     */
    updateLayer(mapId, layerId, newData) {
        const instance = this._deckInstances.get(mapId);
        if (!instance) {
            return;
        }

        const layerIndex = instance.layers.findIndex(l => l.id === layerId);
        if (layerIndex === -1) {
            console.warn(`Layer '${layerId}' not found`);
            return;
        }

        // Create new layer with updated data
        const oldLayer = instance.layers[layerIndex];
        const newLayer = oldLayer.clone({ data: newData });
        instance.layers[layerIndex] = newLayer;
        instance.deck.setProps({ layers: [...instance.layers] });

        console.log(`Updated layer '${layerId}'`);
    },

    /**
     * Set camera position for 3D viewing.
     *
     * @param {string} mapId - Map ID
     * @param {object} camera - Camera configuration
     * @param {number} camera.pitch - Pitch angle (0-85 degrees)
     * @param {number} camera.bearing - Bearing/rotation (0-360 degrees)
     * @param {number} camera.zoom - Zoom level
     * @param {Array} camera.center - [longitude, latitude]
     * @param {object} options - Animation options
     */
    setCamera3D(mapId, camera, options = {}) {
        const instance = this._deckInstances.get(mapId);
        if (!instance) {
            return;
        }

        const animationOptions = {
            duration: options.duration || 1000,
            ...options
        };

        instance.map.easeTo({
            center: camera.center,
            zoom: camera.zoom,
            pitch: camera.pitch,
            bearing: camera.bearing,
            ...animationOptions
        });
    },

    /**
     * Get all layers for a map.
     *
     * @param {string} mapId - Map ID
     * @returns {Array} Array of layers
     */
    getLayers(mapId) {
        const instance = this._deckInstances.get(mapId);
        return instance ? instance.layers : [];
    },

    /**
     * Dispose of Deck.gl instance and cleanup resources.
     *
     * @param {string} mapId - Map ID
     */
    dispose(mapId) {
        const instance = this._deckInstances.get(mapId);
        if (!instance) {
            return;
        }

        // Remove event listeners
        instance.map.off('move', instance.syncViewState);
        instance.map.off('zoom', instance.syncViewState);
        instance.map.off('rotate', instance.syncViewState);
        instance.map.off('pitch', instance.syncViewState);

        // Finalize Deck instance
        instance.deck.finalize();

        // Remove canvas
        if (instance.canvas && instance.canvas.parentNode) {
            instance.canvas.parentNode.removeChild(instance.canvas);
        }

        this._deckInstances.delete(mapId);
        console.log(`Disposed Deck.gl instance for map '${mapId}'`);
    }
};

// Export for Node.js environments (testing)
if (typeof module !== 'undefined' && module.exports) {
    module.exports = window.Honua3D;
}
