// Honua.MapSDK - Navigation Manager JavaScript Module
// Handles navigation visualization, camera tracking, and progress display

let navigationSessions = {};

/**
 * Initialize navigation for a map
 * @param {string} mapId - Map identifier
 * @param {Object} options - Navigation options
 */
export function initializeNavigation(mapId, options = {}) {
    const map = window.honuaMaps?.[mapId];
    if (!map) {
        console.error(`Map ${mapId} not found`);
        return false;
    }

    navigationSessions[mapId] = {
        map: map,
        route: null,
        currentLocation: null,
        currentHeading: 0,
        markers: {
            current: null,
            destination: null,
            waypoints: []
        },
        layers: {
            route: 'nav-route',
            routeProgress: 'nav-route-progress',
            routeRemaining: 'nav-route-remaining',
            currentLocation: 'nav-current-location'
        },
        options: {
            cameraFollowMode: options.cameraFollowMode !== false,
            navigationZoom: options.navigationZoom || 16,
            cameraPitch: options.cameraPitch || 45,
            showProgressLine: options.showProgressLine !== false,
            ...options
        },
        isNavigating: false,
        locationWatchId: null
    };

    console.log(`Navigation initialized for map ${mapId}`);
    return true;
}

/**
 * Start navigation with a route
 * @param {string} mapId - Map identifier
 * @param {Object} route - Route object with geometry and instructions
 */
export function startNavigation(mapId, route) {
    const session = navigationSessions[mapId];
    if (!session) {
        console.error(`Navigation session not found for map ${mapId}`);
        return false;
    }

    session.route = route;
    session.isNavigating = true;

    // Clear any existing route visualization
    clearNavigationLayers(mapId);

    // Display the route
    displayNavigationRoute(mapId, route);

    // Add destination marker
    if (route.waypoints && route.waypoints.length > 0) {
        const destination = route.waypoints[route.waypoints.length - 1];
        addDestinationMarker(mapId, [destination.longitude, destination.latitude]);
    }

    // Start location tracking
    if (session.options.cameraFollowMode) {
        startLocationTracking(mapId);
    }

    console.log(`Navigation started for map ${mapId}`);
    return true;
}

/**
 * Update current location during navigation
 * @param {string} mapId - Map identifier
 * @param {number} lng - Longitude
 * @param {number} lat - Latitude
 * @param {number} heading - Heading in degrees (optional)
 * @param {Object} progress - Navigation progress data (optional)
 */
export function updateNavigationLocation(mapId, lng, lat, heading = null, progress = null) {
    const session = navigationSessions[mapId];
    if (!session || !session.isNavigating) {
        return;
    }

    const location = [lng, lat];
    session.currentLocation = location;
    if (heading !== null) {
        session.currentHeading = heading;
    }

    // Update current location marker
    updateCurrentLocationMarker(mapId, location, heading);

    // Update progress line if available
    if (progress && session.options.showProgressLine) {
        updateProgressLine(mapId, progress);
    }

    // Update camera if follow mode is enabled
    if (session.options.cameraFollowMode) {
        updateCamera(mapId, location, heading);
    }
}

/**
 * Display navigation route on map
 * @param {string} mapId - Map identifier
 * @param {Object} route - Route object
 */
function displayNavigationRoute(mapId, route) {
    const session = navigationSessions[mapId];
    if (!session) return;

    const map = session.map;
    const routeLayerId = session.layers.route;

    // Remove existing layer
    if (map.getLayer(routeLayerId)) {
        map.removeLayer(routeLayerId);
    }
    if (map.getSource(routeLayerId)) {
        map.removeSource(routeLayerId);
    }

    // Add route source
    map.addSource(routeLayerId, {
        type: 'geojson',
        data: {
            type: 'Feature',
            properties: {},
            geometry: route.geometry
        }
    });

    // Add route layer (outline)
    map.addLayer({
        id: `${routeLayerId}-outline`,
        type: 'line',
        source: routeLayerId,
        layout: {
            'line-join': 'round',
            'line-cap': 'round'
        },
        paint: {
            'line-color': '#ffffff',
            'line-width': 10,
            'line-opacity': 0.8
        }
    });

    // Add route layer (main)
    map.addLayer({
        id: routeLayerId,
        type: 'line',
        source: routeLayerId,
        layout: {
            'line-join': 'round',
            'line-cap': 'round'
        },
        paint: {
            'line-color': '#4285F4',
            'line-width': 6,
            'line-opacity': 1.0
        }
    });

    // Fit bounds to route
    if (route.geometry && route.geometry.coordinates) {
        fitRouteToView(map, route.geometry);
    }
}

/**
 * Update progress line showing completed vs remaining route
 * @param {string} mapId - Map identifier
 * @param {Object} progress - Progress data
 */
function updateProgressLine(mapId, progress) {
    const session = navigationSessions[mapId];
    if (!session || !session.route) return;

    const map = session.map;
    const progressLayerId = session.layers.routeProgress;
    const remainingLayerId = session.layers.routeRemaining;

    // Calculate split point in route based on progress
    const routeCoords = session.route.geometry.coordinates;
    const progressPct = progress.progressPercentage || 0;
    const splitIndex = Math.floor((routeCoords.length * progressPct) / 100);

    if (splitIndex > 0 && splitIndex < routeCoords.length) {
        // Completed portion
        const completedCoords = routeCoords.slice(0, splitIndex + 1);
        updateOrCreateLine(map, progressLayerId, completedCoords, '#34A853', 6);

        // Remaining portion
        const remainingCoords = routeCoords.slice(splitIndex);
        updateOrCreateLine(map, remainingLayerId, remainingCoords, '#FBBC04', 6);
    }
}

/**
 * Update or create a line layer
 */
function updateOrCreateLine(map, layerId, coordinates, color, width) {
    if (coordinates.length < 2) return;

    const geojson = {
        type: 'Feature',
        geometry: {
            type: 'LineString',
            coordinates: coordinates
        }
    };

    if (map.getSource(layerId)) {
        map.getSource(layerId).setData(geojson);
    } else {
        map.addSource(layerId, {
            type: 'geojson',
            data: geojson
        });

        map.addLayer({
            id: layerId,
            type: 'line',
            source: layerId,
            layout: {
                'line-join': 'round',
                'line-cap': 'round'
            },
            paint: {
                'line-color': color,
                'line-width': width,
                'line-opacity': 0.9
            }
        });
    }
}

/**
 * Update current location marker
 * @param {string} mapId - Map identifier
 * @param {Array} location - [lng, lat]
 * @param {number} heading - Heading in degrees
 */
function updateCurrentLocationMarker(mapId, location, heading) {
    const session = navigationSessions[mapId];
    if (!session) return;

    const map = session.map;

    // Remove existing marker
    if (session.markers.current) {
        session.markers.current.remove();
    }

    // Create marker element with heading indicator
    const el = document.createElement('div');
    el.className = 'navigation-location-marker';
    el.innerHTML = `
        <div class="marker-outer-ring"></div>
        <div class="marker-inner-circle"></div>
        ${heading !== null ? `<div class="marker-heading-arrow" style="transform: rotate(${heading}deg)"></div>` : ''}
    `;

    // Add styles
    const style = document.createElement('style');
    style.textContent = `
        .navigation-location-marker {
            position: relative;
            width: 40px;
            height: 40px;
        }
        .marker-outer-ring {
            position: absolute;
            width: 40px;
            height: 40px;
            border-radius: 50%;
            background: rgba(66, 133, 244, 0.2);
            border: 2px solid #4285F4;
        }
        .marker-inner-circle {
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            width: 16px;
            height: 16px;
            border-radius: 50%;
            background: #4285F4;
            border: 3px solid white;
            box-shadow: 0 2px 4px rgba(0,0,0,0.3);
        }
        .marker-heading-arrow {
            position: absolute;
            top: -8px;
            left: 50%;
            transform-origin: center 24px;
            width: 0;
            height: 0;
            border-left: 8px solid transparent;
            border-right: 8px solid transparent;
            border-bottom: 16px solid #4285F4;
            margin-left: -8px;
        }
    `;
    if (!document.getElementById('nav-marker-styles')) {
        style.id = 'nav-marker-styles';
        document.head.appendChild(style);
    }

    // Create and add marker
    const marker = new maplibregl.Marker({ element: el, anchor: 'center' })
        .setLngLat(location)
        .addTo(map);

    session.markers.current = marker;
}

/**
 * Add destination marker
 * @param {string} mapId - Map identifier
 * @param {Array} location - [lng, lat]
 */
function addDestinationMarker(mapId, location) {
    const session = navigationSessions[mapId];
    if (!session) return;

    const map = session.map;

    // Remove existing marker
    if (session.markers.destination) {
        session.markers.destination.remove();
    }

    // Create marker element
    const el = document.createElement('div');
    el.className = 'navigation-destination-marker';
    el.innerHTML = '🏁';
    el.style.cssText = 'font-size: 32px; cursor: pointer;';

    const marker = new maplibregl.Marker({ element: el, anchor: 'bottom' })
        .setLngLat(location)
        .addTo(map);

    session.markers.destination = marker;
}

/**
 * Update camera to follow user
 * @param {string} mapId - Map identifier
 * @param {Array} location - [lng, lat]
 * @param {number} heading - Heading in degrees
 */
function updateCamera(mapId, location, heading) {
    const session = navigationSessions[mapId];
    if (!session) return;

    const map = session.map;
    const options = session.options;

    // Smooth camera movement
    map.easeTo({
        center: location,
        zoom: options.navigationZoom,
        pitch: options.cameraPitch,
        bearing: heading !== null ? heading : map.getBearing(),
        duration: 1000,
        essential: true
    });
}

/**
 * Start tracking user location
 * @param {string} mapId - Map identifier
 */
function startLocationTracking(mapId) {
    const session = navigationSessions[mapId];
    if (!session || session.locationWatchId !== null) return;

    if (!navigator.geolocation) {
        console.error('Geolocation not supported');
        return;
    }

    session.locationWatchId = navigator.geolocation.watchPosition(
        (position) => {
            const { longitude, latitude, heading } = position.coords;
            updateNavigationLocation(mapId, longitude, latitude, heading);

            // Notify C# of location update
            window.honuaNavigationLocationUpdated?.(mapId, longitude, latitude, heading);
        },
        (error) => {
            console.error('Geolocation error:', error);
        },
        {
            enableHighAccuracy: true,
            maximumAge: 1000,
            timeout: 5000
        }
    );
}

/**
 * Stop tracking user location
 * @param {string} mapId - Map identifier
 */
function stopLocationTracking(mapId) {
    const session = navigationSessions[mapId];
    if (!session || session.locationWatchId === null) return;

    navigator.geolocation.clearWatch(session.locationWatchId);
    session.locationWatchId = null;
}

/**
 * Display turn arrow at upcoming maneuver
 * @param {string} mapId - Map identifier
 * @param {Object} instruction - Route instruction
 */
export function displayTurnArrow(mapId, instruction) {
    const session = navigationSessions[mapId];
    if (!session) return;

    // This would create a visual arrow indicator on the map
    // Implementation depends on specific requirements
    console.log('Display turn arrow:', instruction.maneuver);
}

/**
 * Show lane guidance
 * @param {string} mapId - Map identifier
 * @param {Object} laneGuidance - Lane guidance data
 */
export function showLaneGuidance(mapId, laneGuidance) {
    const session = navigationSessions[mapId];
    if (!session) return;

    // This would display lane arrows on the map or in UI
    // Implementation depends on specific requirements
    console.log('Show lane guidance:', laneGuidance);
}

/**
 * Stop navigation
 * @param {string} mapId - Map identifier
 */
export function stopNavigation(mapId) {
    const session = navigationSessions[mapId];
    if (!session) return;

    session.isNavigating = false;

    // Stop location tracking
    stopLocationTracking(mapId);

    // Clear navigation layers and markers
    clearNavigationLayers(mapId);
    clearNavigationMarkers(mapId);

    console.log(`Navigation stopped for map ${mapId}`);
}

/**
 * Clear navigation layers
 */
function clearNavigationLayers(mapId) {
    const session = navigationSessions[mapId];
    if (!session) return;

    const map = session.map;
    const layerIds = Object.values(session.layers);

    layerIds.forEach(layerId => {
        // Remove both the layer and outline
        [layerId, `${layerId}-outline`].forEach(id => {
            if (map.getLayer(id)) {
                map.removeLayer(id);
            }
        });
        if (map.getSource(layerId)) {
            map.removeSource(layerId);
        }
    });
}

/**
 * Clear navigation markers
 */
function clearNavigationMarkers(mapId) {
    const session = navigationSessions[mapId];
    if (!session) return;

    Object.values(session.markers).forEach(marker => {
        if (marker && marker.remove) {
            marker.remove();
        }
    });

    session.markers = {
        current: null,
        destination: null,
        waypoints: []
    };
}

/**
 * Fit route to view
 */
function fitRouteToView(map, geometry) {
    if (!geometry || !geometry.coordinates || geometry.coordinates.length === 0) {
        return;
    }

    const bounds = new maplibregl.LngLatBounds();
    geometry.coordinates.forEach(coord => {
        bounds.extend(coord);
    });

    map.fitBounds(bounds, {
        padding: { top: 100, bottom: 100, left: 100, right: 100 },
        duration: 1000
    });
}

/**
 * Toggle camera follow mode
 * @param {string} mapId - Map identifier
 * @param {boolean} enabled - Enable/disable follow mode
 */
export function setCameraFollowMode(mapId, enabled) {
    const session = navigationSessions[mapId];
    if (!session) return;

    session.options.cameraFollowMode = enabled;

    if (enabled && session.currentLocation) {
        updateCamera(mapId, session.currentLocation, session.currentHeading);
    }
}

/**
 * Set navigation zoom level
 * @param {string} mapId - Map identifier
 * @param {number} zoom - Zoom level
 */
export function setNavigationZoom(mapId, zoom) {
    const session = navigationSessions[mapId];
    if (!session) return;

    session.options.navigationZoom = zoom;
}

/**
 * Highlight route segment for current instruction
 * @param {string} mapId - Map identifier
 * @param {number} stepIndex - Step index
 */
export function highlightNavigationSegment(mapId, stepIndex) {
    const session = navigationSessions[mapId];
    if (!session || !session.route) return;

    // This would highlight a specific segment of the route
    // Similar to the routing module's highlightRouteSegment
    console.log('Highlight segment:', stepIndex);
}

/**
 * Get navigation session info
 * @param {string} mapId - Map identifier
 * @returns {Object} Session info
 */
export function getNavigationSession(mapId) {
    return navigationSessions[mapId] || null;
}

/**
 * Cleanup navigation session
 * @param {string} mapId - Map identifier
 */
export function cleanupNavigation(mapId) {
    const session = navigationSessions[mapId];
    if (!session) return;

    stopNavigation(mapId);
    delete navigationSessions[mapId];
    console.log(`Navigation session cleaned up for map ${mapId}`);
}

// Export for debugging
window.honuaNavigation = {
    initializeNavigation,
    startNavigation,
    updateNavigationLocation,
    displayTurnArrow,
    showLaneGuidance,
    stopNavigation,
    setCameraFollowMode,
    setNavigationZoom,
    highlightNavigationSegment,
    getNavigationSession,
    cleanupNavigation
};
