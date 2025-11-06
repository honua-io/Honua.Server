// Honua Overview Map JavaScript Module
// Manages synchronization between main map and miniature overview map

import maplibregl from 'https://cdn.jsdelivr.net/npm/maplibre-gl@5.0.0/+esm';

export function createOverviewMap(container, mainMapId, options, dotNetRef) {
    const overviewMap = new maplibregl.Map({
        container: container,
        style: options.style,
        center: options.center,
        zoom: options.zoom,
        bearing: 0,
        pitch: 0,
        interactive: options.interactive !== false,
        attributionControl: false,
        logoControl: false,
        minZoom: options.minZoom,
        maxZoom: options.maxZoom,
        trackResize: true
    });

    // Store references
    overviewMap._dotNetRef = dotNetRef;
    overviewMap._mainMapId = mainMapId;
    overviewMap._options = options;
    overviewMap._extentSource = null;
    overviewMap._extentLayer = null;
    overviewMap._isDragging = false;
    overviewMap._updateThrottle = null;

    // Wait for map to load before setting up
    overviewMap.on('load', () => {
        setupExtentBox(overviewMap, options);
        setupEventHandlers(overviewMap, dotNetRef, options);
        console.log(`Honua Overview Map initialized for ${mainMapId}`);
    });

    // Create API wrapper
    const api = createOverviewMapAPI(overviewMap);

    return api;
}

function setupExtentBox(map, options) {
    // Add source for extent box
    map.addSource('extent-box', {
        type: 'geojson',
        data: {
            type: 'Feature',
            properties: {},
            geometry: {
                type: 'Polygon',
                coordinates: [[]]
            }
        }
    });

    // Add fill layer for extent box
    map.addLayer({
        id: 'extent-box-fill',
        type: 'fill',
        source: 'extent-box',
        paint: {
            'fill-color': options.extentBoxFillColor || '#FF4444',
            'fill-opacity': options.extentBoxFillOpacity || 0.1
        }
    });

    // Add line layer for extent box border
    map.addLayer({
        id: 'extent-box-outline',
        type: 'line',
        source: 'extent-box',
        paint: {
            'line-color': options.extentBoxColor || '#FF4444',
            'line-width': options.extentBoxWidth || 2,
            'line-opacity': options.extentBoxOpacity || 0.8
        }
    });

    map._extentSource = 'extent-box';
    map._extentLayer = 'extent-box-outline';
}

function setupEventHandlers(map, dotNetRef, options) {
    // Click to pan main map
    if (options.clickToPan) {
        map.on('click', (e) => {
            if (!map._isDragging) {
                const center = [e.lngLat.lng, e.lngLat.lat];
                dotNetRef.invokeMethodAsync('OnOverviewClickedInternal', center);
            }
        });
    }

    // Drag extent box to pan main map
    if (options.dragToPan) {
        let dragStartLngLat = null;

        map.on('mousedown', 'extent-box-fill', (e) => {
            e.preventDefault();
            map._isDragging = true;
            dragStartLngLat = e.lngLat;
            map.getCanvas().style.cursor = 'move';
        });

        map.on('mousemove', (e) => {
            if (map._isDragging && dragStartLngLat) {
                const center = [e.lngLat.lng, e.lngLat.lat];

                // Throttle updates for performance
                if (!map._updateThrottle) {
                    map._updateThrottle = setTimeout(() => {
                        dotNetRef.invokeMethodAsync('OnOverviewDraggedInternal', center);
                        map._updateThrottle = null;
                    }, options.updateThrottleMs || 50);
                }
            }
        });

        map.on('mouseup', () => {
            if (map._isDragging) {
                map._isDragging = false;
                dragStartLngLat = null;
                map.getCanvas().style.cursor = '';
            }
        });

        // Change cursor on hover
        map.on('mouseenter', 'extent-box-fill', () => {
            if (!map._isDragging) {
                map.getCanvas().style.cursor = 'move';
            }
        });

        map.on('mouseleave', 'extent-box-fill', () => {
            if (!map._isDragging) {
                map.getCanvas().style.cursor = '';
            }
        });
    }

    // Scroll to zoom main map
    if (options.scrollToZoom) {
        map.on('wheel', (e) => {
            e.preventDefault();
            const delta = e.originalEvent.deltaY;
            const zoomDelta = delta > 0 ? -0.5 : 0.5;
            dotNetRef.invokeMethodAsync('OnOverviewScrolledInternal', zoomDelta);
        });
    }

    // Rotate overview map with bearing (if enabled)
    if (options.rotateWithBearing) {
        // This will be updated from the main map via API
    }
}

function createOverviewMapAPI(map) {
    return {
        // Update extent box to show main map viewport
        updateExtent: (bounds, bearing) => {
            if (!map.getSource('extent-box')) return;

            // Convert bounds [west, south, east, north] to polygon coordinates
            const [west, south, east, north] = bounds;
            const coordinates = [[
                [west, north],
                [east, north],
                [east, south],
                [west, south],
                [west, north]
            ]];

            // Update source
            map.getSource('extent-box').setData({
                type: 'Feature',
                properties: {},
                geometry: {
                    type: 'Polygon',
                    coordinates: coordinates
                }
            });

            // Update bearing if rotation is enabled
            if (map._options.rotateWithBearing && bearing !== undefined) {
                map.setBearing(bearing);
            }
        },

        // Sync center and zoom with main map
        syncWithMainMap: (center, zoom, bearing) => {
            const overviewZoom = zoom + (map._options.zoomOffset || -5);

            // Fit to show the extent nicely
            map.jumpTo({
                center: center,
                zoom: Math.max(0, overviewZoom),
                bearing: map._options.rotateWithBearing ? bearing : 0
            });
        },

        // Fit overview to show main map bounds
        fitToExtent: (bounds, padding) => {
            map.fitBounds(bounds, {
                padding: padding || 20,
                duration: 0
            });
        },

        // Get current overview map state
        getState: () => {
            const center = map.getCenter();
            return {
                center: [center.lng, center.lat],
                zoom: map.getZoom(),
                bearing: map.getBearing()
            };
        },

        // Update style
        setStyle: (style) => {
            const currentExtentData = map.getSource('extent-box')?._data;

            map.setStyle(style);

            // Re-add extent box after style change
            map.once('styledata', () => {
                setupExtentBox(map, map._options);
                if (currentExtentData) {
                    map.getSource('extent-box').setData(currentExtentData);
                }
            });
        },

        // Update extent box styling
        updateExtentStyle: (color, width, opacity, fillColor, fillOpacity) => {
            if (map.getLayer('extent-box-outline')) {
                if (color) map.setPaintProperty('extent-box-outline', 'line-color', color);
                if (width !== undefined) map.setPaintProperty('extent-box-outline', 'line-width', width);
                if (opacity !== undefined) map.setPaintProperty('extent-box-outline', 'line-opacity', opacity);
            }
            if (map.getLayer('extent-box-fill')) {
                if (fillColor) map.setPaintProperty('extent-box-fill', 'fill-color', fillColor);
                if (fillOpacity !== undefined) map.setPaintProperty('extent-box-fill', 'fill-opacity', fillOpacity);
            }
        },

        // Resize map
        resize: () => {
            map.resize();
        },

        // Set interactive mode
        setInteractive: (interactive) => {
            if (interactive) {
                map.scrollZoom.enable();
                map.dragPan.enable();
            } else {
                map.scrollZoom.disable();
                map.dragPan.disable();
            }
        },

        // Cleanup
        dispose: () => {
            if (map._updateThrottle) {
                clearTimeout(map._updateThrottle);
            }
            map.remove();
        }
    };
}

// Utility function to calculate optimal zoom offset
export function calculateOptimalZoomOffset(mainMapZoom, mainMapBounds, overviewMapSize) {
    // Calculate the area of the main map viewport
    const [west, south, east, north] = mainMapBounds;
    const width = Math.abs(east - west);
    const height = Math.abs(north - south);
    const area = width * height;

    // Heuristic: larger viewport needs more zoom out
    if (area > 100) return -6;
    if (area > 50) return -5;
    if (area > 20) return -4;
    if (area > 10) return -3;
    return -2;
}

// Utility function to determine if point is inside polygon
export function isPointInPolygon(point, polygon) {
    const [x, y] = point;
    let inside = false;

    for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
        const [xi, yi] = polygon[i];
        const [xj, yj] = polygon[j];

        const intersect = ((yi > y) !== (yj > y))
            && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);

        if (intersect) inside = !inside;
    }

    return inside;
}
