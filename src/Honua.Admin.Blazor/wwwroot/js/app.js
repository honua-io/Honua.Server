// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Downloads a file from base64 data
 * @param {string} filename - The name of the file to download
 * @param {string} contentType - The MIME type of the file
 * @param {string} base64Data - The base64-encoded file data
 */
window.downloadFile = function(filename, contentType, base64Data) {
    const linkElement = document.createElement('a');
    linkElement.href = `data:${contentType};base64,${base64Data}`;
    linkElement.download = filename;
    document.body.appendChild(linkElement);
    linkElement.click();
    document.body.removeChild(linkElement);
};

// MapLibre GL Map Management
window.honuaMapLibre = {
    maps: {},

    /**
     * Initialize a MapLibre GL map
     * @param {string} containerId - The ID of the container element
     * @param {object} options - Map initialization options
     * @returns {string} Map instance ID
     */
    initializeMap: function(containerId, options) {
        try {
            const mapId = containerId;

            // Default options
            const mapOptions = {
                container: containerId,
                style: options.style || {
                    version: 8,
                    sources: {},
                    layers: []
                },
                center: options.center || [0, 0],
                zoom: options.zoom || 2,
                minZoom: options.minZoom || 0,
                maxZoom: options.maxZoom || 22
            };

            // Create map
            const map = new maplibregl.Map(mapOptions);

            // Add navigation controls
            map.addControl(new maplibregl.NavigationControl(), 'top-right');

            // Add scale control
            map.addControl(new maplibregl.ScaleControl(), 'bottom-left');

            // Store map instance
            this.maps[mapId] = map;

            return mapId;
        } catch (error) {
            console.error('Failed to initialize MapLibre map:', error);
            throw error;
        }
    },

    /**
     * Update map style
     * @param {string} mapId - Map instance ID
     * @param {object} style - MapLibre style object
     */
    updateStyle: function(mapId, style) {
        const map = this.maps[mapId];
        if (!map) {
            console.error('Map not found:', mapId);
            return;
        }

        try {
            map.setStyle(style);
        } catch (error) {
            console.error('Failed to update map style:', error);
            throw error;
        }
    },

    /**
     * Add a GeoJSON source and layer to the map
     * @param {string} mapId - Map instance ID
     * @param {string} sourceId - Source ID
     * @param {object} geojson - GeoJSON data
     * @param {string} layerType - Layer type (fill, line, circle)
     * @param {object} paint - Paint properties
     */
    addGeoJsonLayer: function(mapId, sourceId, geojson, layerType, paint) {
        const map = this.maps[mapId];
        if (!map) {
            console.error('Map not found:', mapId);
            return;
        }

        try {
            map.on('load', function() {
                // Add source
                if (!map.getSource(sourceId)) {
                    map.addSource(sourceId, {
                        type: 'geojson',
                        data: geojson
                    });
                }

                // Add layer
                const layerId = `${sourceId}-layer`;
                if (!map.getLayer(layerId)) {
                    map.addLayer({
                        id: layerId,
                        type: layerType,
                        source: sourceId,
                        paint: paint || {}
                    });
                }

                // Fit bounds to data
                if (geojson.features && geojson.features.length > 0) {
                    const bounds = new maplibregl.LngLatBounds();
                    geojson.features.forEach(function(feature) {
                        if (feature.geometry.type === 'Point') {
                            bounds.extend(feature.geometry.coordinates);
                        } else if (feature.geometry.type === 'LineString') {
                            feature.geometry.coordinates.forEach(coord => bounds.extend(coord));
                        } else if (feature.geometry.type === 'Polygon') {
                            feature.geometry.coordinates[0].forEach(coord => bounds.extend(coord));
                        }
                    });

                    if (!bounds.isEmpty()) {
                        map.fitBounds(bounds, { padding: 50 });
                    }
                }
            });
        } catch (error) {
            console.error('Failed to add GeoJSON layer:', error);
            throw error;
        }
    },

    /**
     * Add a vector tile source and styled layers
     * @param {string} mapId - Map instance ID
     * @param {string} sourceId - Source ID
     * @param {string} tilesUrl - Vector tiles URL template
     * @param {string} sourceLayer - Source layer name
     * @param {object} style - Layer style definition
     */
    addVectorTileLayer: function(mapId, sourceId, tilesUrl, sourceLayer, style) {
        const map = this.maps[mapId];
        if (!map) {
            console.error('Map not found:', mapId);
            return;
        }

        try {
            map.on('load', function() {
                // Add source
                if (!map.getSource(sourceId)) {
                    map.addSource(sourceId, {
                        type: 'vector',
                        tiles: [tilesUrl]
                    });
                }

                // Add layers from style
                if (style && style.layers) {
                    style.layers.forEach(function(layer) {
                        const layerId = layer.id || `${sourceId}-${layer.type}`;
                        if (!map.getLayer(layerId)) {
                            const layerDef = {
                                ...layer,
                                id: layerId,
                                source: sourceId,
                                'source-layer': sourceLayer
                            };
                            map.addLayer(layerDef);
                        }
                    });
                }
            });
        } catch (error) {
            console.error('Failed to add vector tile layer:', error);
            throw error;
        }
    },

    /**
     * Fly to a specific location
     * @param {string} mapId - Map instance ID
     * @param {number[]} center - [lng, lat]
     * @param {number} zoom - Zoom level
     */
    flyTo: function(mapId, center, zoom) {
        const map = this.maps[mapId];
        if (!map) {
            console.error('Map not found:', mapId);
            return;
        }

        map.flyTo({
            center: center,
            zoom: zoom,
            essential: true
        });
    },

    /**
     * Resize the map (call after container size changes)
     * @param {string} mapId - Map instance ID
     */
    resize: function(mapId) {
        const map = this.maps[mapId];
        if (!map) {
            console.error('Map not found:', mapId);
            return;
        }

        map.resize();
    },

    /**
     * Destroy a map instance
     * @param {string} mapId - Map instance ID
     */
    destroyMap: function(mapId) {
        const map = this.maps[mapId];
        if (map) {
            map.remove();
            delete this.maps[mapId];
        }
    }
};
