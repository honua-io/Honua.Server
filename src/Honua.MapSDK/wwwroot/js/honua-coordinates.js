/**
 * Honua Coordinate Display JavaScript Module
 * Handles mouse coordinate tracking, scale calculation, and map info
 */

const coordinateTrackers = new Map();
const mapScales = new Map();

/**
 * Initialize coordinate tracking for a map
 * @param {string} mapId - The map container ID
 * @param {object} dotNetRef - .NET object reference for callbacks
 * @returns {boolean} Success status
 */
export function initializeCoordinateTracking(mapId, dotNetRef) {
    try {
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer) {
            console.error(`Map container not found: ${mapId}`);
            return false;
        }

        // Wait for map to be available
        const checkMap = () => {
            const map = mapContainer._map;
            if (map) {
                setupTracking(mapId, map, dotNetRef);
            } else {
                setTimeout(checkMap, 100);
            }
        };
        checkMap();

        return true;
    } catch (error) {
        console.error('Error initializing coordinate tracking:', error);
        return false;
    }
}

/**
 * Setup tracking handlers for a map
 * @param {string} mapId - The map container ID
 * @param {object} map - MapLibre map instance
 * @param {object} dotNetRef - .NET object reference
 */
function setupTracking(mapId, map, dotNetRef) {
    const handlers = {
        mousemove: (e) => handleMouseMove(e, map, dotNetRef),
        click: (e) => handleClick(e, map, dotNetRef),
        zoom: () => handleMapChange(map, dotNetRef),
        move: () => handleMapChange(map, dotNetRef),
        rotate: () => handleMapChange(map, dotNetRef)
    };

    // Register handlers
    Object.entries(handlers).forEach(([event, handler]) => {
        map.on(event, handler);
    });

    // Store tracker info
    coordinateTrackers.set(mapId, {
        map: map,
        dotNetRef: dotNetRef,
        handlers: handlers,
        lastUpdate: 0
    });

    // Initial update
    handleMapChange(map, dotNetRef);

    console.log(`Coordinate tracking initialized for map: ${mapId}`);
}

/**
 * Handle mouse move events
 * @param {object} e - MapLibre mouse event
 * @param {object} map - MapLibre map instance
 * @param {object} dotNetRef - .NET object reference
 */
function handleMouseMove(e, map, dotNetRef) {
    try {
        const tracker = Array.from(coordinateTrackers.values()).find(t => t.map === map);
        if (!tracker) return;

        // Throttle updates to every 50ms
        const now = Date.now();
        if (now - tracker.lastUpdate < 50) return;
        tracker.lastUpdate = now;

        const lngLat = e.lngLat;
        const zoom = map.getZoom();
        const bearing = map.getBearing();
        const scale = calculateMapScale(map, lngLat.lat);

        // Query elevation if terrain is enabled
        let elevation = null;
        if (map.terrain) {
            const point = map.project(lngLat);
            elevation = map.queryTerrainElevation(lngLat, { exaggerated: false });
        }

        // Send to .NET
        dotNetRef.invokeMethodAsync('OnCoordinateUpdate', {
            longitude: lngLat.lng,
            latitude: lngLat.lat,
            elevation: elevation,
            zoom: zoom,
            bearing: bearing,
            scale: scale
        });
    } catch (error) {
        console.error('Error handling mouse move:', error);
    }
}

/**
 * Handle click events
 * @param {object} e - MapLibre mouse event
 * @param {object} map - MapLibre map instance
 * @param {object} dotNetRef - .NET object reference
 */
function handleClick(e, map, dotNetRef) {
    try {
        const lngLat = e.lngLat;

        // Query elevation if terrain is enabled
        let elevation = null;
        if (map.terrain) {
            elevation = map.queryTerrainElevation(lngLat, { exaggerated: false });
        }

        // Send to .NET
        dotNetRef.invokeMethodAsync('HandleCoordinateClick', {
            longitude: lngLat.lng,
            latitude: lngLat.lat,
            elevation: elevation
        });
    } catch (error) {
        console.error('Error handling click:', error);
    }
}

/**
 * Handle map change events (zoom, move, rotate)
 * @param {object} map - MapLibre map instance
 * @param {object} dotNetRef - .NET object reference
 */
function handleMapChange(map, dotNetRef) {
    try {
        const center = map.getCenter();
        const zoom = map.getZoom();
        const bearing = map.getBearing();
        const scale = calculateMapScale(map, center.lat);

        // Send to .NET
        dotNetRef.invokeMethodAsync('OnMapInfoUpdate', {
            zoom: zoom,
            bearing: bearing,
            scale: scale
        });
    } catch (error) {
        console.error('Error handling map change:', error);
    }
}

/**
 * Calculate map scale at a given latitude
 * @param {object} map - MapLibre map instance
 * @param {number} latitude - Latitude for scale calculation
 * @returns {number} Scale ratio (e.g., 25000 for 1:25,000)
 */
function calculateMapScale(map, latitude) {
    try {
        // Get map zoom level
        const zoom = map.getZoom();

        // Earth's radius in meters
        const earthRadius = 6378137;

        // Meters per pixel at given latitude and zoom
        const metersPerPixel = (2 * Math.PI * earthRadius * Math.cos(latitude * Math.PI / 180)) / (256 * Math.pow(2, zoom));

        // Assume 96 DPI for screen (standard)
        const pixelsPerInch = 96;
        const inchesPerMeter = 39.3701;

        // Calculate scale ratio
        const scale = metersPerPixel * pixelsPerInch * inchesPerMeter;

        // Store for later use
        const mapId = map.getContainer().id;
        mapScales.set(mapId, scale);

        return Math.round(scale);
    } catch (error) {
        console.error('Error calculating scale:', error);
        return 0;
    }
}

/**
 * Get current zoom level
 * @param {string} mapId - The map container ID
 * @returns {number|null} Zoom level
 */
export function getZoomLevel(mapId) {
    try {
        const tracker = coordinateTrackers.get(mapId);
        if (tracker && tracker.map) {
            return tracker.map.getZoom();
        }
        return null;
    } catch (error) {
        console.error('Error getting zoom level:', error);
        return null;
    }
}

/**
 * Get current map bearing
 * @param {string} mapId - The map container ID
 * @returns {number|null} Bearing in degrees
 */
export function getBearing(mapId) {
    try {
        const tracker = coordinateTrackers.get(mapId);
        if (tracker && tracker.map) {
            return tracker.map.getBearing();
        }
        return null;
    } catch (error) {
        console.error('Error getting bearing:', error);
        return null;
    }
}

/**
 * Get current map scale
 * @param {string} mapId - The map container ID
 * @returns {number|null} Scale ratio
 */
export function getMapScale(mapId) {
    try {
        const scale = mapScales.get(mapId);
        if (scale) {
            return scale;
        }

        const tracker = coordinateTrackers.get(mapId);
        if (tracker && tracker.map) {
            const center = tracker.map.getCenter();
            return calculateMapScale(tracker.map, center.lat);
        }

        return null;
    } catch (error) {
        console.error('Error getting scale:', error);
        return null;
    }
}

/**
 * Query elevation at a specific coordinate
 * @param {string} mapId - The map container ID
 * @param {number} longitude - Longitude
 * @param {number} latitude - Latitude
 * @returns {number|null} Elevation in meters
 */
export function queryElevation(mapId, longitude, latitude) {
    try {
        const tracker = coordinateTrackers.get(mapId);
        if (tracker && tracker.map && tracker.map.terrain) {
            return tracker.map.queryTerrainElevation({ lng: longitude, lat: latitude }, { exaggerated: false });
        }
        return null;
    } catch (error) {
        console.error('Error querying elevation:', error);
        return null;
    }
}

/**
 * Calculate distance between two coordinates (Haversine formula)
 * @param {number} lng1 - First longitude
 * @param {number} lat1 - First latitude
 * @param {number} lng2 - Second longitude
 * @param {number} lat2 - Second latitude
 * @returns {number} Distance in meters
 */
export function calculateDistance(lng1, lat1, lng2, lat2) {
    const R = 6371000; // Earth's radius in meters
    const φ1 = lat1 * Math.PI / 180;
    const φ2 = lat2 * Math.PI / 180;
    const Δφ = (lat2 - lat1) * Math.PI / 180;
    const Δλ = (lng2 - lng1) * Math.PI / 180;

    const a = Math.sin(Δφ / 2) * Math.sin(Δφ / 2) +
        Math.cos(φ1) * Math.cos(φ2) *
        Math.sin(Δλ / 2) * Math.sin(Δλ / 2);

    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));

    return R * c;
}

/**
 * Format distance with appropriate unit
 * @param {number} meters - Distance in meters
 * @param {string} unit - Unit system (metric, imperial, nautical)
 * @returns {string} Formatted distance
 */
export function formatDistance(meters, unit = 'metric') {
    if (unit === 'imperial') {
        const feet = meters * 3.28084;
        if (feet < 5280) {
            return `${feet.toFixed(0)} ft`;
        } else {
            const miles = feet / 5280;
            return `${miles.toFixed(2)} mi`;
        }
    } else if (unit === 'nautical') {
        const nauticalMiles = meters / 1852;
        if (nauticalMiles < 0.1) {
            return `${meters.toFixed(0)} m`;
        } else {
            return `${nauticalMiles.toFixed(2)} nm`;
        }
    } else {
        // Metric (default)
        if (meters < 1000) {
            return `${meters.toFixed(0)} m`;
        } else {
            const km = meters / 1000;
            return `${km.toFixed(2)} km`;
        }
    }
}

/**
 * Copy text to clipboard
 * @param {string} text - Text to copy
 * @returns {Promise<boolean>} Success status
 */
export async function copyToClipboard(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (error) {
        console.error('Error copying to clipboard:', error);
        // Fallback for older browsers
        try {
            const textarea = document.createElement('textarea');
            textarea.value = text;
            textarea.style.position = 'fixed';
            textarea.style.opacity = '0';
            document.body.appendChild(textarea);
            textarea.select();
            document.execCommand('copy');
            document.body.removeChild(textarea);
            return true;
        } catch (fallbackError) {
            console.error('Fallback copy failed:', fallbackError);
            return false;
        }
    }
}

/**
 * Stop coordinate tracking for a map
 * @param {string} mapId - The map container ID
 * @returns {boolean} Success status
 */
export function stopTracking(mapId) {
    try {
        const tracker = coordinateTrackers.get(mapId);
        if (tracker) {
            // Remove event handlers
            Object.entries(tracker.handlers).forEach(([event, handler]) => {
                tracker.map.off(event, handler);
            });

            // Clean up
            coordinateTrackers.delete(mapId);
            mapScales.delete(mapId);

            console.log(`Coordinate tracking stopped for map: ${mapId}`);
            return true;
        }
        return false;
    } catch (error) {
        console.error('Error stopping tracking:', error);
        return false;
    }
}

/**
 * Cleanup all trackers
 */
export function cleanup() {
    coordinateTrackers.forEach((tracker, mapId) => {
        stopTracking(mapId);
    });
}
