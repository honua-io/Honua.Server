// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * MapLibre GL JS Interop Module for Blazor
 * Provides JavaScript interop functionality for the HonuaMapLibre component.
 *
 * This module uses MapLibre GL JS v4.x
 * CDN: https://unpkg.com/maplibre-gl@4/dist/maplibre-gl.js
 */

// Ensure MapLibre GL is loaded
let maplibreLoaded = false;
let maplibreLoadPromise = null;

/**
 * Load MapLibre GL JS library dynamically
 */
async function ensureMapLibreLoaded() {
    if (maplibreLoaded) return;
    if (maplibreLoadPromise) return maplibreLoadPromise;

    maplibreLoadPromise = new Promise((resolve, reject) => {
        // Check if already loaded
        if (window.maplibregl) {
            maplibreLoaded = true;
            resolve();
            return;
        }

        // Load CSS
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = 'https://unpkg.com/maplibre-gl@4/dist/maplibre-gl.css';
        document.head.appendChild(link);

        // Load JS
        const script = document.createElement('script');
        script.src = 'https://unpkg.com/maplibre-gl@4/dist/maplibre-gl.js';
        script.onload = () => {
            maplibreLoaded = true;
            resolve();
        };
        script.onerror = () => reject(new Error('Failed to load MapLibre GL JS'));
        document.head.appendChild(script);
    });

    return maplibreLoadPromise;
}

/**
 * Map instance wrapper with all MapLibre functionality
 */
class MapLibreInstance {
    constructor(map, element, dotNetRef) {
        this.map = map;
        this.element = element;
        this.dotNetRef = dotNetRef;
        this.markers = new Map();
        this.popups = new Map();
        this.eventHandlers = new Map();

        this.setupEventListeners();
    }

    /**
     * Setup map event listeners
     */
    setupEventListeners() {
        // Load event
        this.map.on('load', () => {
            const center = this.map.getCenter();
            const zoom = this.map.getZoom();
            const bearing = this.map.getBearing();
            const pitch = this.map.getPitch();
            const bounds = this.map.getBounds().toArray().flat();

            this.dotNetRef.invokeMethodAsync('OnMapLoadedCallback',
                [center.lng, center.lat], zoom, bearing, pitch, bounds)
                .catch(err => console.error('Error invoking OnMapLoadedCallback:', err));
        });

        // Click event
        this.map.on('click', (e) => {
            const features = this.map.queryRenderedFeatures(e.point);
            const mappedFeatures = features.slice(0, 10).map(f => ({
                id: f.id,
                type: f.geometry?.type,
                sourceLayer: f.sourceLayer,
                layer: f.layer?.id,
                properties: f.properties,
                geometry: f.geometry
            }));

            this.dotNetRef.invokeMethodAsync('OnMapClickCallback',
                [e.lngLat.lng, e.lngLat.lat],
                [e.point.x, e.point.y],
                mappedFeatures)
                .catch(err => console.error('Error invoking OnMapClickCallback:', err));
        });

        // Move events (debounced)
        let moveTimeout;
        const handleMove = () => {
            clearTimeout(moveTimeout);
            moveTimeout = setTimeout(() => {
                const center = this.map.getCenter();
                const zoom = this.map.getZoom();
                const bearing = this.map.getBearing();
                const pitch = this.map.getPitch();

                this.dotNetRef.invokeMethodAsync('OnMapMoveCallback',
                    [center.lng, center.lat], zoom, bearing, pitch)
                    .catch(err => console.error('Error invoking OnMapMoveCallback:', err));
            }, 100);
        };

        this.map.on('moveend', handleMove);
        this.map.on('zoomend', handleMove);
        this.map.on('rotateend', handleMove);
        this.map.on('pitchend', handleMove);

        // Viewport change events
        const handleViewportChange = (eventType) => {
            const center = this.map.getCenter();
            const zoom = this.map.getZoom();
            const bearing = this.map.getBearing();
            const pitch = this.map.getPitch();
            const bounds = this.map.getBounds().toArray().flat();

            this.dotNetRef.invokeMethodAsync('OnViewportChangeCallback',
                [center.lng, center.lat], zoom, bearing, pitch, bounds, eventType)
                .catch(err => console.error('Error invoking OnViewportChangeCallback:', err));
        };

        this.map.on('moveend', () => handleViewportChange('move'));
        this.map.on('zoomend', () => handleViewportChange('zoom'));
        this.map.on('rotateend', () => handleViewportChange('rotate'));
        this.map.on('pitchend', () => handleViewportChange('pitch'));

        // Style events
        this.map.on('style.load', () => {
            this.dotNetRef.invokeMethodAsync('OnStyleLoadCallback')
                .catch(err => console.error('Error invoking OnStyleLoadCallback:', err));
        });

        // Error event
        this.map.on('error', (e) => {
            this.dotNetRef.invokeMethodAsync('OnErrorCallback', e.error?.message || 'Unknown error')
                .catch(err => console.error('Error invoking OnErrorCallback:', err));
        });
    }

    /**
     * Fly to location with animation
     */
    flyTo(options) {
        this.map.flyTo(options);
    }

    /**
     * Jump to location without animation
     */
    jumpTo(options) {
        this.map.jumpTo(options);
    }

    /**
     * Fit map to bounds
     */
    fitBounds(bounds, options) {
        this.map.fitBounds(bounds, options);
    }

    /**
     * Set map style
     */
    setStyle(style) {
        this.map.setStyle(style);
    }

    /**
     * Add source to map
     */
    addSource(sourceId, source) {
        if (!this.map.getSource(sourceId)) {
            this.map.addSource(sourceId, source);
        }
    }

    /**
     * Remove source from map
     */
    removeSource(sourceId) {
        if (this.map.getSource(sourceId)) {
            this.map.removeSource(sourceId);
        }
    }

    /**
     * Add layer to map
     */
    addLayer(layer, beforeId) {
        if (!this.map.getLayer(layer.id)) {
            this.map.addLayer(layer, beforeId);
        }
    }

    /**
     * Remove layer from map
     */
    removeLayer(layerId) {
        if (this.map.getLayer(layerId)) {
            this.map.removeLayer(layerId);
        }
    }

    /**
     * Set layer visibility
     */
    setLayerVisibility(layerId, visible) {
        if (this.map.getLayer(layerId)) {
            this.map.setLayoutProperty(layerId, 'visibility', visible ? 'visible' : 'none');
        }
    }

    /**
     * Set layer opacity
     */
    setLayerOpacity(layerId, opacity) {
        const layer = this.map.getLayer(layerId);
        if (!layer) return;

        const layerType = layer.type;
        const opacityProperty = {
            'fill': 'fill-opacity',
            'line': 'line-opacity',
            'symbol': 'icon-opacity',
            'circle': 'circle-opacity',
            'fill-extrusion': 'fill-extrusion-opacity',
            'raster': 'raster-opacity',
            'heatmap': 'heatmap-opacity'
        }[layerType];

        if (opacityProperty) {
            this.map.setPaintProperty(layerId, opacityProperty, opacity);
        }
    }

    /**
     * Add marker to map
     */
    addMarker(markerConfig) {
        const markerId = markerConfig.id || `marker-${Date.now()}`;

        const markerOptions = {
            color: markerConfig.color,
            scale: markerConfig.scale || 1.0,
            rotation: markerConfig.rotation || 0,
            rotationAlignment: markerConfig.rotationAlignment || 'auto',
            pitchAlignment: markerConfig.pitchAlignment || 'auto',
            draggable: markerConfig.draggable || false
        };

        if (markerConfig.anchor) {
            markerOptions.anchor = markerConfig.anchor;
        }

        if (markerConfig.offset) {
            markerOptions.offset = markerConfig.offset;
        }

        const marker = new maplibregl.Marker(markerOptions)
            .setLngLat(markerConfig.position)
            .addTo(this.map);

        // Add popup if configured
        if (markerConfig.popup) {
            const popup = new maplibregl.Popup({
                maxWidth: markerConfig.popup.maxWidth || '240px',
                closeButton: markerConfig.popup.closeButton !== false,
                closeOnClick: markerConfig.popup.closeOnClick !== false,
                closeOnMove: markerConfig.popup.closeOnMove || false,
                anchor: markerConfig.popup.anchor,
                offset: markerConfig.popup.offset,
                className: markerConfig.popup.className
            });

            if (markerConfig.popup.html) {
                popup.setHTML(markerConfig.popup.html);
            } else if (markerConfig.popup.text) {
                popup.setText(markerConfig.popup.text);
            }

            marker.setPopup(popup);
            this.popups.set(markerId, popup);
        }

        this.markers.set(markerId, marker);
        return markerId;
    }

    /**
     * Remove marker from map
     */
    removeMarker(markerId) {
        const marker = this.markers.get(markerId);
        if (marker) {
            marker.remove();
            this.markers.delete(markerId);
            this.popups.delete(markerId);
        }
    }

    /**
     * Update marker position
     */
    updateMarkerPosition(markerId, position) {
        const marker = this.markers.get(markerId);
        if (marker) {
            marker.setLngLat(position);
        }
    }

    /**
     * Get current viewport
     */
    getViewport() {
        const center = this.map.getCenter();
        const bounds = this.map.getBounds().toArray().flat();
        return {
            center: [center.lng, center.lat],
            zoom: this.map.getZoom(),
            bearing: this.map.getBearing(),
            pitch: this.map.getPitch(),
            bounds: bounds
        };
    }

    /**
     * Get current bounds
     */
    getBounds() {
        return this.map.getBounds().toArray().flat();
    }

    /**
     * Get current center
     */
    getCenter() {
        const center = this.map.getCenter();
        return [center.lng, center.lat];
    }

    /**
     * Get current zoom
     */
    getZoom() {
        return this.map.getZoom();
    }

    /**
     * Resize map
     */
    resize() {
        this.map.resize();
    }

    /**
     * Load GeoJSON data
     */
    loadGeoJson(sourceId, geoJson, layer) {
        // Add or update source
        if (this.map.getSource(sourceId)) {
            this.map.getSource(sourceId).setData(geoJson);
        } else {
            this.map.addSource(sourceId, {
                type: 'geojson',
                data: geoJson
            });
        }

        // Add layer if provided
        if (layer && !this.map.getLayer(layer.id)) {
            this.map.addLayer({
                ...layer,
                source: sourceId
            });
        }
    }

    /**
     * Query rendered features at a point
     */
    queryRenderedFeatures(point, layerIds) {
        const options = layerIds ? { layers: layerIds } : {};
        const features = this.map.queryRenderedFeatures(point, options);

        return features.map(f => ({
            id: f.id,
            type: f.geometry?.type,
            sourceLayer: f.sourceLayer,
            layer: f.layer?.id,
            properties: f.properties,
            geometry: f.geometry
        }));
    }

    /**
     * Query rendered features in a bounding box
     */
    queryRenderedFeaturesInBounds(bbox, layerIds) {
        const options = layerIds ? { layers: layerIds } : {};
        const features = this.map.queryRenderedFeatures(bbox, options);

        return features.map(f => ({
            id: f.id,
            type: f.geometry?.type,
            sourceLayer: f.sourceLayer,
            layer: f.layer?.id,
            properties: f.properties,
            geometry: f.geometry
        }));
    }

    /**
     * Add navigation control
     */
    addNavigationControl(position) {
        const nav = new maplibregl.NavigationControl();
        this.map.addControl(nav, position);
    }

    /**
     * Add scale control
     */
    addScaleControl(position) {
        const scale = new maplibregl.ScaleControl({
            maxWidth: 100,
            unit: 'metric'
        });
        this.map.addControl(scale, position);
    }

    /**
     * Add fullscreen control
     */
    addFullscreenControl(position) {
        const fullscreen = new maplibregl.FullscreenControl();
        this.map.addControl(fullscreen, position);
    }

    /**
     * Add geolocate control
     */
    addGeolocateControl(position) {
        const geolocate = new maplibregl.GeolocateControl({
            positionOptions: {
                enableHighAccuracy: true
            },
            trackUserLocation: true,
            showUserHeading: true
        });
        this.map.addControl(geolocate, position);
    }

    /**
     * Dispose map instance
     */
    dispose() {
        // Remove all markers
        this.markers.forEach(marker => marker.remove());
        this.markers.clear();
        this.popups.clear();

        // Remove map
        if (this.map) {
            this.map.remove();
            this.map = null;
        }
    }
}

/**
 * Initialize MapLibre map
 * @param {HTMLElement} element - Map container element
 * @param {object} options - Map initialization options
 * @param {object} dotNetRef - .NET object reference for callbacks
 * @returns {MapLibreInstance} Map instance wrapper
 */
export async function initializeMap(element, options, dotNetRef) {
    try {
        // Ensure MapLibre is loaded
        await ensureMapLibreLoaded();

        // Create map
        const map = new maplibregl.Map({
            container: element,
            ...options,
            // Accessibility
            preserveDrawingBuffer: true,
            trackResize: true
        });

        // Create and return instance wrapper
        const instance = new MapLibreInstance(map, element, dotNetRef);
        return instance;
    } catch (error) {
        console.error('Error initializing MapLibre:', error);
        throw error;
    }
}

// Export for module usage
export default {
    initializeMap
};
