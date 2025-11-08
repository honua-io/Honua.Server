// Honua Leaflet Interop Module
// Provides Leaflet integration with support for popular plugins

// Import Leaflet from CDN (will be loaded via link tags in the component)
// This assumes Leaflet is loaded globally via CDN

export function createLeafletMap(container, options, dotNetRef) {
    // Ensure Leaflet is loaded
    if (typeof L === 'undefined') {
        console.error('Leaflet library not loaded. Please ensure Leaflet is included in your page.');
        return null;
    }

    // Create the map
    const map = L.map(container, {
        center: options.center,
        zoom: options.zoom,
        minZoom: options.minZoom,
        maxZoom: options.maxZoom,
        maxBounds: options.maxBounds,
        zoomControl: true,
        attributionControl: true
    });

    // Store references
    map._dotNetRef = dotNetRef;
    map._honuaId = options.id;
    map._layers = new Map();
    map._markers = new Map();
    map._markerClusterGroup = null;
    map._highlightLayer = null;
    map._drawLayer = null;
    map._measureControl = null;

    // Add tile layer
    const tileLayer = L.tileLayer(options.tileUrl, {
        attribution: options.attribution,
        maxZoom: options.maxZoom || 19
    });
    tileLayer.addTo(map);
    map._baseLayer = tileLayer;

    // Setup optional features
    if (options.enableMarkerCluster && typeof L.markerClusterGroup !== 'undefined') {
        map._markerClusterGroup = L.markerClusterGroup({
            maxClusterRadius: options.maxClusterRadius || 80,
            spiderfyOnMaxZoom: true,
            showCoverageOnHover: false,
            zoomToBoundsOnClick: true
        });
        map.addLayer(map._markerClusterGroup);
    }

    if (options.enableFullscreen && typeof L.control.fullscreen !== 'undefined') {
        L.control.fullscreen({
            position: 'topright',
            title: 'Enter fullscreen',
            titleCancel: 'Exit fullscreen'
        }).addTo(map);
    }

    if (options.enableDraw && typeof L.Control.Draw !== 'undefined') {
        map._drawLayer = new L.FeatureGroup();
        map.addLayer(map._drawLayer);

        const drawControl = new L.Control.Draw({
            position: 'topright',
            draw: {
                polygon: {
                    allowIntersection: false,
                    shapeOptions: {
                        color: '#3388ff'
                    }
                },
                polyline: {
                    shapeOptions: {
                        color: '#3388ff'
                    }
                },
                rectangle: {
                    shapeOptions: {
                        color: '#3388ff'
                    }
                },
                circle: {
                    shapeOptions: {
                        color: '#3388ff'
                    }
                },
                marker: true,
                circlemarker: false
            },
            edit: {
                featureGroup: map._drawLayer,
                remove: true
            }
        });
        map.addControl(drawControl);

        // Handle draw events
        map.on(L.Draw.Event.CREATED, (e) => {
            const layer = e.layer;
            map._drawLayer.addLayer(layer);

            const geoJson = layer.toGeoJSON();
            dotNetRef.invokeMethodAsync('OnDrawCreatedInternal', {
                type: e.layerType,
                geometry: geoJson.geometry,
                area: layer.getLatLngs ? calculateArea(layer) : null,
                length: layer.getLatLngs ? calculateLength(layer) : null
            });
        });
    }

    if (options.enableMeasure && typeof L.Control.Measure !== 'undefined') {
        map._measureControl = L.control.measure({
            position: 'topright',
            primaryLengthUnit: 'meters',
            secondaryLengthUnit: 'kilometers',
            primaryAreaUnit: 'sqmeters',
            secondaryAreaUnit: 'hectares',
            activeColor: '#db4a29',
            completedColor: '#9b2d14'
        });
        map.addControl(map._measureControl);

        // Handle measure finish event
        map.on('measurefinish', (e) => {
            dotNetRef.invokeMethodAsync('OnMeasureCompleteInternal', {
                length: e.length,
                area: e.area,
                perimeter: e.perimeter
            });
        });
    }

    // Setup event handlers
    setupEventHandlers(map, dotNetRef);

    // Create API wrapper
    const api = createLeafletAPI(map);

    return api;
}

function setupEventHandlers(map, dotNetRef) {
    // Map ready
    map.whenReady(() => {
        console.log(`Honua Leaflet Map ${map._honuaId} ready`);
    });

    // Extent changed (debounced)
    let extentChangeTimeout;
    const notifyExtentChanged = () => {
        const bounds = map.getBounds();
        const center = map.getCenter();

        dotNetRef.invokeMethodAsync('OnExtentChangedInternal',
            [bounds.getSouth(), bounds.getWest(), bounds.getNorth(), bounds.getEast()],
            map.getZoom(),
            [center.lat, center.lng]
        );
    };

    map.on('moveend', () => {
        clearTimeout(extentChangeTimeout);
        extentChangeTimeout = setTimeout(notifyExtentChanged, 100);
    });

    map.on('zoomend', () => {
        clearTimeout(extentChangeTimeout);
        extentChangeTimeout = setTimeout(notifyExtentChanged, 100);
    });

    // Feature click handling
    map.on('click', (e) => {
        // Check if click was on a layer with properties
        const clickedLayers = [];
        map.eachLayer((layer) => {
            if (layer.feature && layer.getBounds && layer.getBounds().contains(e.latlng)) {
                clickedLayers.push(layer);
            }
        });

        if (clickedLayers.length > 0) {
            const layer = clickedLayers[0];
            const feature = layer.feature;

            dotNetRef.invokeMethodAsync('OnFeatureClickedInternal',
                layer._honuaLayerId || 'unknown',
                feature.id?.toString() || feature.properties?.id?.toString() || 'unknown',
                feature.properties || {},
                feature.geometry
            );
        }
    });
}

function createLeafletAPI(map) {
    return {
        // Navigation
        flyTo: (options) => {
            map.flyTo(options.center, options.zoom, {
                duration: (options.duration || 1000) / 1000 // Convert to seconds
            });
        },

        fitBounds: (bounds, padding) => {
            map.fitBounds(bounds, { padding: [padding || 50, padding || 50] });
        },

        getBounds: () => {
            const bounds = map.getBounds();
            return [bounds.getSouth(), bounds.getWest(), bounds.getNorth(), bounds.getEast()];
        },

        getCenter: () => {
            const center = map.getCenter();
            return [center.lat, center.lng];
        },

        getZoom: () => {
            return map.getZoom();
        },

        // Tile layer management
        setTileLayer: (url, attribution) => {
            if (map._baseLayer) {
                map.removeLayer(map._baseLayer);
            }

            map._baseLayer = L.tileLayer(url, {
                attribution: attribution,
                maxZoom: 19
            });
            map._baseLayer.addTo(map);
        },

        // GeoJSON layers
        addGeoJsonLayer: (layerId, geoJson, style) => {
            // Remove existing layer if present
            if (map._layers.has(layerId)) {
                const existingLayer = map._layers.get(layerId);
                map.removeLayer(existingLayer);
            }

            const defaultStyle = style || {
                color: '#3388ff',
                weight: 2,
                opacity: 0.8,
                fillOpacity: 0.4
            };

            const layer = L.geoJSON(geoJson, {
                style: (feature) => {
                    return typeof defaultStyle === 'function' ? defaultStyle(feature) : defaultStyle;
                },
                pointToLayer: (feature, latlng) => {
                    return L.circleMarker(latlng, {
                        radius: 8,
                        fillColor: defaultStyle.color || '#3388ff',
                        color: '#fff',
                        weight: 1,
                        opacity: 1,
                        fillOpacity: 0.8
                    });
                },
                onEachFeature: (feature, layer) => {
                    // Add click handler
                    layer.on('click', (e) => {
                        map._dotNetRef.invokeMethodAsync('OnFeatureClickedInternal',
                            layerId,
                            feature.id?.toString() || feature.properties?.id?.toString() || 'unknown',
                            feature.properties || {},
                            feature.geometry
                        );
                        L.DomEvent.stopPropagation(e);
                    });

                    // Add hover handler
                    layer.on('mouseover', (e) => {
                        layer.setStyle({
                            weight: 3,
                            opacity: 1
                        });
                        if (!L.Browser.ie && !L.Browser.opera && !L.Browser.edge) {
                            layer.bringToFront();
                        }
                    });

                    layer.on('mouseout', (e) => {
                        layer.setStyle(defaultStyle);
                    });

                    // Bind popup if feature has properties
                    if (feature.properties) {
                        const popupContent = createPopupContent(feature.properties);
                        layer.bindPopup(popupContent);
                    }
                }
            });

            layer._honuaLayerId = layerId;
            layer.addTo(map);
            map._layers.set(layerId, layer);
        },

        // WMS layers
        addWmsLayer: (layerId, url, options) => {
            if (map._layers.has(layerId)) {
                const existingLayer = map._layers.get(layerId);
                map.removeLayer(existingLayer);
            }

            const wmsLayer = L.tileLayer.wms(url, options);
            wmsLayer._honuaLayerId = layerId;
            wmsLayer.addTo(map);
            map._layers.set(layerId, wmsLayer);
        },

        // Layer management
        removeLayer: (layerId) => {
            if (map._layers.has(layerId)) {
                const layer = map._layers.get(layerId);
                map.removeLayer(layer);
                map._layers.delete(layerId);
            }
        },

        setLayerVisibility: (layerId, visible) => {
            if (map._layers.has(layerId)) {
                const layer = map._layers.get(layerId);
                if (visible) {
                    map.addLayer(layer);
                } else {
                    map.removeLayer(layer);
                }
            }
        },

        setLayerOpacity: (layerId, opacity) => {
            if (map._layers.has(layerId)) {
                const layer = map._layers.get(layerId);
                if (layer.setOpacity) {
                    layer.setOpacity(opacity);
                } else if (layer.setStyle) {
                    layer.setStyle({ opacity: opacity, fillOpacity: opacity * 0.5 });
                }
            }
        },

        // Markers
        addMarker: (markerId, position, popupContent, options) => {
            if (map._markers.has(markerId)) {
                map.removeMarker(markerId);
            }

            const markerOptions = options || {};
            const marker = L.marker(position, markerOptions);

            if (popupContent) {
                marker.bindPopup(popupContent);
            }

            marker._honuaMarkerId = markerId;

            if (map._markerClusterGroup) {
                map._markerClusterGroup.addLayer(marker);
            } else {
                marker.addTo(map);
            }

            map._markers.set(markerId, marker);
        },

        removeMarker: (markerId) => {
            if (map._markers.has(markerId)) {
                const marker = map._markers.get(markerId);
                if (map._markerClusterGroup) {
                    map._markerClusterGroup.removeLayer(marker);
                } else {
                    map.removeLayer(marker);
                }
                map._markers.delete(markerId);
            }
        },

        // Highlighting
        highlightFeature: (featureId, geometry) => {
            clearHighlights(map);

            if (!map._highlightLayer) {
                map._highlightLayer = L.geoJSON(null, {
                    style: {
                        color: '#ffff00',
                        weight: 4,
                        opacity: 1,
                        fillOpacity: 0.3,
                        fillColor: '#ffff00'
                    },
                    pointToLayer: (feature, latlng) => {
                        return L.circleMarker(latlng, {
                            radius: 10,
                            fillColor: '#ffff00',
                            color: '#ff0000',
                            weight: 2,
                            opacity: 1,
                            fillOpacity: 0.5
                        });
                    }
                }).addTo(map);
            }

            map._highlightLayer.addData({
                type: 'Feature',
                geometry: geometry,
                properties: { id: featureId }
            });

            // Fit bounds to highlighted feature
            const bounds = map._highlightLayer.getBounds();
            if (bounds.isValid()) {
                map.fitBounds(bounds, { padding: [50, 50] });
            }
        },

        highlightFeatures: (featureIds, layerId) => {
            clearHighlights(map);

            if (map._layers.has(layerId)) {
                const layer = map._layers.get(layerId);

                layer.eachLayer((subLayer) => {
                    if (subLayer.feature) {
                        const featureId = subLayer.feature.id?.toString() ||
                                        subLayer.feature.properties?.id?.toString();

                        if (featureIds.includes(featureId)) {
                            subLayer.setStyle({
                                color: '#ffff00',
                                weight: 4,
                                opacity: 1,
                                fillOpacity: 0.6
                            });
                        }
                    }
                });
            }
        },

        clearHighlights: () => {
            clearHighlights(map);
        },

        // Cleanup
        dispose: () => {
            map.remove();
        }
    };
}

// Helper functions
function clearHighlights(map) {
    if (map._highlightLayer) {
        map._highlightLayer.clearLayers();
    }

    // Reset styles for all layers
    map._layers.forEach((layer) => {
        if (layer.resetStyle) {
            layer.resetStyle();
        } else if (layer.eachLayer) {
            layer.eachLayer((subLayer) => {
                if (subLayer.setStyle && subLayer._originalStyle) {
                    subLayer.setStyle(subLayer._originalStyle);
                }
            });
        }
    });
}

function createPopupContent(properties) {
    let content = '<div class="leaflet-popup-content-honua">';

    for (const [key, value] of Object.entries(properties)) {
        if (key !== 'id' && value !== null && value !== undefined) {
            content += `<div><strong>${key}:</strong> ${value}</div>`;
        }
    }

    content += '</div>';
    return content;
}

function calculateArea(layer) {
    if (layer.getLatLngs) {
        const latlngs = layer.getLatLngs()[0];
        let area = 0;

        for (let i = 0; i < latlngs.length; i++) {
            const j = (i + 1) % latlngs.length;
            area += latlngs[i].lng * latlngs[j].lat;
            area -= latlngs[j].lng * latlngs[i].lat;
        }

        area = Math.abs(area / 2);

        // Convert to square meters (rough approximation)
        const metersPerDegree = 111000;
        return area * metersPerDegree * metersPerDegree;
    }

    return null;
}

function calculateLength(layer) {
    if (layer.getLatLngs) {
        const latlngs = layer.getLatLngs();
        let length = 0;

        for (let i = 0; i < latlngs.length - 1; i++) {
            length += latlngs[i].distanceTo(latlngs[i + 1]);
        }

        return length;
    }

    return null;
}
