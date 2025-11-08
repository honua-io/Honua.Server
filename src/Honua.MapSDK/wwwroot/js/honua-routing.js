// Honua.MapSDK - Routing JavaScript Module
// Handles turn-by-turn routing and directions on MapLibre GL JS maps

export function initializeRouting(mapId, options) {
    const map = window.honuaMaps?.[mapId];
    if (!map) {
        console.error(`Map ${mapId} not found`);
        return false;
    }

    if (!window.honuaRouting) {
        window.honuaRouting = {};
    }

    window.honuaRouting[mapId] = {
        waypoints: [],
        routes: [],
        markers: [],
        currentRoute: null,
        alternatives: [],
        options: options || {}
    };

    // Add click handler for adding waypoints if enabled
    if (options.allowMapClick) {
        map.on('click', (e) => {
            window.honuaRoutingClickHandler?.(mapId, e.lngLat.lng, e.lngLat.lat);
        });
    }

    console.log(`Routing initialized for map ${mapId}`);
    return true;
}

export function addWaypoint(mapId, waypoint) {
    const routing = window.honuaRouting?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!routing || !map) return null;

    const waypointData = {
        id: waypoint.id,
        longitude: waypoint.longitude,
        latitude: waypoint.latitude,
        name: waypoint.name,
        address: waypoint.address,
        type: waypoint.type || 'via',
        label: waypoint.label || getWaypointLabel(routing.waypoints.length)
    };

    routing.waypoints.push(waypointData);

    // Create marker
    const markerEl = createWaypointMarker(waypointData);
    const marker = new maplibregl.Marker({ element: markerEl, draggable: true })
        .setLngLat([waypointData.longitude, waypointData.latitude])
        .addTo(map);

    // Handle marker drag
    marker.on('dragend', () => {
        const lngLat = marker.getLngLat();
        updateWaypointPosition(mapId, waypointData.id, lngLat.lng, lngLat.lat);
    });

    routing.markers.push({ id: waypointData.id, marker });

    return waypointData;
}

export function removeWaypoint(mapId, waypointId) {
    const routing = window.honuaRouting?.[mapId];
    if (!routing) return false;

    // Remove waypoint
    const index = routing.waypoints.findIndex(w => w.id === waypointId);
    if (index === -1) return false;

    routing.waypoints.splice(index, 1);

    // Remove marker
    const markerIndex = routing.markers.findIndex(m => m.id === waypointId);
    if (markerIndex !== -1) {
        routing.markers[markerIndex].marker.remove();
        routing.markers.splice(markerIndex, 1);
    }

    // Update labels
    updateWaypointLabels(mapId);

    return true;
}

export function clearWaypoints(mapId) {
    const routing = window.honuaRouting?.[mapId];
    if (!routing) return false;

    // Remove all markers
    routing.markers.forEach(m => m.marker.remove());
    routing.markers = [];
    routing.waypoints = [];

    return true;
}

export function displayRoute(mapId, route, options = {}) {
    const routing = window.honuaRouting?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!routing || !map) return false;

    const routeId = route.id;
    const isAlternative = route.isAlternative || false;
    const alternativeIndex = route.alternativeIndex || 0;

    // Remove existing route layer if not alternative
    if (!isAlternative && map.getLayer('route-line')) {
        map.removeLayer('route-line');
        map.removeSource('route-line');
    }

    // Prepare layer ID
    const layerId = isAlternative ? `route-alt-${alternativeIndex}` : 'route-line';
    const sourceId = isAlternative ? `route-alt-${alternativeIndex}` : 'route-line';

    // Remove if exists
    if (map.getLayer(layerId)) {
        map.removeLayer(layerId);
    }
    if (map.getSource(sourceId)) {
        map.removeSource(sourceId);
    }

    // Add route source
    map.addSource(sourceId, {
        type: 'geojson',
        data: {
            type: 'Feature',
            properties: {
                routeId: routeId,
                isAlternative: isAlternative
            },
            geometry: route.geometry
        }
    });

    // Get route color based on travel mode and alternative status
    const color = getRouteColor(route.travelMode, isAlternative, alternativeIndex);
    const width = isAlternative ? 4 : 6;
    const dashArray = isAlternative ? [2, 2] : null;

    // Add route layer
    map.addLayer({
        id: layerId,
        type: 'line',
        source: sourceId,
        layout: {
            'line-join': 'round',
            'line-cap': 'round'
        },
        paint: {
            'line-color': color,
            'line-width': width,
            'line-opacity': isAlternative ? 0.6 : 0.8,
            ...(dashArray && { 'line-dasharray': dashArray })
        }
    });

    // Add click handler for route
    map.on('click', layerId, (e) => {
        window.honuaRouteClickHandler?.(mapId, routeId, e.lngLat);
    });

    // Store route
    if (!isAlternative) {
        routing.currentRoute = route;
    } else {
        routing.alternatives[alternativeIndex] = route;
    }

    // Fit bounds to route if requested
    if (options.fitBounds && route.geometry) {
        fitRouteToView(map, route.geometry);
    }

    return true;
}

export function displayRouteInstructions(mapId, instructions) {
    const routing = window.honuaRouting?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!routing || !map) return false;

    // Remove existing instruction markers
    if (routing.instructionMarkers) {
        routing.instructionMarkers.forEach(m => m.remove());
    }
    routing.instructionMarkers = [];

    // Add markers for key maneuvers
    instructions.forEach((instruction, index) => {
        if (shouldShowInstructionMarker(instruction)) {
            const markerEl = createInstructionMarker(instruction);
            const marker = new maplibregl.Marker({ element: markerEl })
                .setLngLat(instruction.coordinate)
                .addTo(map);

            routing.instructionMarkers.push(marker);
        }
    });

    return true;
}

export function highlightRouteSegment(mapId, segmentIndex) {
    const routing = window.honuaRouting?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!routing || !map) return false;

    // Remove existing highlight
    if (map.getLayer('route-segment-highlight')) {
        map.removeLayer('route-segment-highlight');
        map.removeSource('route-segment-highlight');
    }

    const route = routing.currentRoute;
    if (!route || !route.instructions || segmentIndex >= route.instructions.length) {
        return false;
    }

    const instruction = route.instructions[segmentIndex];

    // Extract segment geometry (simplified approach)
    const startIndex = instruction.geometryStartIndex || segmentIndex;
    const endIndex = instruction.geometryEndIndex || segmentIndex + 1;

    if (route.geometry && route.geometry.coordinates) {
        const segmentCoords = route.geometry.coordinates.slice(
            Math.max(0, startIndex - 5),
            Math.min(route.geometry.coordinates.length, endIndex + 5)
        );

        if (segmentCoords.length > 0) {
            map.addSource('route-segment-highlight', {
                type: 'geojson',
                data: {
                    type: 'Feature',
                    geometry: {
                        type: 'LineString',
                        coordinates: segmentCoords
                    }
                }
            });

            map.addLayer({
                id: 'route-segment-highlight',
                type: 'line',
                source: 'route-segment-highlight',
                paint: {
                    'line-color': '#FF6B00',
                    'line-width': 8,
                    'line-opacity': 0.9
                }
            });

            // Pan to segment
            map.easeTo({
                center: instruction.coordinate,
                zoom: Math.max(map.getZoom(), 15),
                duration: 500
            });
        }
    }

    return true;
}

export function clearRoute(mapId, clearWaypoints = false) {
    const routing = window.honuaRouting?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!routing || !map) return false;

    // Remove route layers
    const routeLayers = ['route-line', 'route-segment-highlight'];
    for (let i = 0; i < 5; i++) {
        routeLayers.push(`route-alt-${i}`);
    }

    routeLayers.forEach(layerId => {
        if (map.getLayer(layerId)) {
            map.removeLayer(layerId);
        }
        if (map.getSource(layerId)) {
            map.removeSource(layerId);
        }
    });

    // Remove instruction markers
    if (routing.instructionMarkers) {
        routing.instructionMarkers.forEach(m => m.remove());
        routing.instructionMarkers = [];
    }

    // Clear route data
    routing.currentRoute = null;
    routing.alternatives = [];

    // Clear waypoints if requested
    if (clearWaypoints) {
        clearWaypoints(mapId);
    }

    return true;
}

export function displayIsochrone(mapId, isochroneResult) {
    const map = window.honuaMaps?.[mapId];
    if (!map) return false;

    // Remove existing isochrones
    for (let i = 0; i < 10; i++) {
        const layerId = `isochrone-${i}`;
        if (map.getLayer(layerId)) {
            map.removeLayer(layerId);
        }
        if (map.getSource(layerId)) {
            map.removeSource(layerId);
        }
    }

    // Add isochrone polygons
    isochroneResult.polygons.forEach((polygon, index) => {
        const layerId = `isochrone-${index}`;
        const sourceId = `isochrone-${index}`;

        map.addSource(sourceId, {
            type: 'geojson',
            data: {
                type: 'Feature',
                properties: {
                    interval: polygon.interval,
                    color: polygon.color
                },
                geometry: polygon.geometry
            }
        });

        map.addLayer({
            id: layerId,
            type: 'fill',
            source: sourceId,
            paint: {
                'fill-color': polygon.color,
                'fill-opacity': polygon.opacity || 0.3,
                'fill-outline-color': polygon.color
            }
        });

        // Add outline
        map.addLayer({
            id: `${layerId}-outline`,
            type: 'line',
            source: sourceId,
            paint: {
                'line-color': polygon.color,
                'line-width': 2,
                'line-opacity': 0.8
            }
        });
    });

    // Add center marker
    if (isochroneResult.center) {
        const markerEl = document.createElement('div');
        markerEl.className = 'isochrone-center-marker';
        markerEl.innerHTML = '📍';
        markerEl.style.fontSize = '24px';

        new maplibregl.Marker({ element: markerEl })
            .setLngLat(isochroneResult.center)
            .addTo(map);
    }

    return true;
}

export function exportRoute(mapId, format) {
    const routing = window.honuaRouting?.[mapId];
    if (!routing || !routing.currentRoute) return null;

    const route = routing.currentRoute;

    switch (format.toLowerCase()) {
        case 'geojson':
            return exportAsGeoJson(route);
        case 'gpx':
            return exportAsGpx(route);
        case 'kml':
            return exportAsKml(route);
        default:
            console.error(`Unsupported export format: ${format}`);
            return null;
    }
}

// Helper functions

function createWaypointMarker(waypoint) {
    const el = document.createElement('div');
    el.className = 'waypoint-marker';
    el.dataset.type = waypoint.type;

    const icon = getWaypointIcon(waypoint.type);
    const label = waypoint.label || '';

    // Create elements safely without innerHTML to prevent XSS
    const innerDiv = document.createElement('div');
    innerDiv.className = 'waypoint-marker-inner';

    const iconDiv = document.createElement('div');
    iconDiv.className = 'waypoint-icon';
    iconDiv.textContent = icon;

    const labelDiv = document.createElement('div');
    labelDiv.className = 'waypoint-label';
    labelDiv.textContent = label;

    innerDiv.appendChild(iconDiv);
    innerDiv.appendChild(labelDiv);
    el.appendChild(innerDiv);

    return el;
}

function createInstructionMarker(instruction) {
    const el = document.createElement('div');
    el.className = 'instruction-marker';

    const icon = getManeuverIcon(instruction.maneuver);

    // Create element safely without innerHTML to prevent XSS
    const iconDiv = document.createElement('div');
    iconDiv.className = 'instruction-icon';
    iconDiv.textContent = icon;
    el.appendChild(iconDiv);

    return el;
}

function getWaypointLabel(index) {
    return String.fromCharCode(65 + index); // A, B, C, etc.
}

function getWaypointIcon(type) {
    switch (type) {
        case 'start':
            return '🚩';
        case 'end':
            return '🏁';
        default:
            return '📍';
    }
}

function getManeuverIcon(maneuver) {
    const icons = {
        'TurnLeft': '⬅️',
        'TurnRight': '➡️',
        'TurnSlightLeft': '↖️',
        'TurnSlightRight': '↗️',
        'TurnSharpLeft': '⬅️',
        'TurnSharpRight': '➡️',
        'UTurn': '↩️',
        'Straight': '⬆️',
        'Continue': '⬆️',
        'Merge': '🔀',
        'Fork': '🔱',
        'Roundabout': '⭕',
        'Arrive': '🏁',
        'Depart': '🚩'
    };
    return icons[maneuver] || '➡️';
}

function getRouteColor(travelMode, isAlternative, altIndex) {
    if (isAlternative) {
        const altColors = ['#9E9E9E', '#757575', '#616161'];
        return altColors[altIndex % altColors.length];
    }

    const colors = {
        'Driving': '#1976D2',
        'Walking': '#4CAF50',
        'Cycling': '#FF9800',
        'Transit': '#9C27B0',
        'DrivingTraffic': '#F44336'
    };
    return colors[travelMode] || '#1976D2';
}

function shouldShowInstructionMarker(instruction) {
    const showFor = [
        'TurnLeft', 'TurnRight', 'TurnSharpLeft', 'TurnSharpRight',
        'Roundabout', 'Fork', 'Merge'
    ];
    return showFor.includes(instruction.maneuver);
}

function updateWaypointPosition(mapId, waypointId, lng, lat) {
    const routing = window.honuaRouting?.[mapId];
    if (!routing) return;

    const waypoint = routing.waypoints.find(w => w.id === waypointId);
    if (waypoint) {
        waypoint.longitude = lng;
        waypoint.latitude = lat;

        // Notify C# of position change
        window.honuaWaypointDraggedHandler?.(mapId, waypointId, lng, lat);
    }
}

function updateWaypointLabels(mapId) {
    const routing = window.honuaRouting?.[mapId];
    if (!routing) return;

    routing.waypoints.forEach((waypoint, index) => {
        waypoint.label = getWaypointLabel(index);

        // Update marker label
        const markerData = routing.markers.find(m => m.id === waypoint.id);
        if (markerData) {
            const labelEl = markerData.marker.getElement().querySelector('.waypoint-label');
            if (labelEl) {
                labelEl.textContent = waypoint.label;
            }
        }
    });
}

function fitRouteToView(map, geometry) {
    if (!geometry || !geometry.coordinates || geometry.coordinates.length === 0) {
        return;
    }

    const bounds = new maplibregl.LngLatBounds();
    geometry.coordinates.forEach(coord => {
        bounds.extend(coord);
    });

    map.fitBounds(bounds, {
        padding: { top: 50, bottom: 50, left: 50, right: 50 },
        duration: 1000
    });
}

function exportAsGeoJson(route) {
    return {
        type: 'Feature',
        properties: {
            id: route.id,
            distance: route.distance,
            duration: route.duration,
            travelMode: route.travelMode
        },
        geometry: route.geometry
    };
}

function exportAsGpx(route) {
    const coords = route.geometry.coordinates;
    const waypoints = route.waypoints || [];

    let gpx = `<?xml version="1.0" encoding="UTF-8"?>
<gpx version="1.1" creator="Honua.MapSDK">
  <metadata>
    <name>${route.id}</name>
    <desc>Route generated by Honua.MapSDK</desc>
    <time>${new Date().toISOString()}</time>
  </metadata>
  <trk>
    <name>Route</name>
    <trkseg>`;

    coords.forEach(coord => {
        gpx += `
      <trkpt lat="${coord[1]}" lon="${coord[0]}">
        <ele>0</ele>
      </trkpt>`;
    });

    gpx += `
    </trkseg>
  </trk>`;

    waypoints.forEach((waypoint, index) => {
        gpx += `
  <wpt lat="${waypoint.latitude}" lon="${waypoint.longitude}">
    <name>${waypoint.name || waypoint.label || `Waypoint ${index + 1}`}</name>
  </wpt>`;
    });

    gpx += `
</gpx>`;

    return gpx;
}

function exportAsKml(route) {
    const coords = route.geometry.coordinates;

    let coordString = coords.map(c => `${c[0]},${c[1]},0`).join(' ');

    return `<?xml version="1.0" encoding="UTF-8"?>
<kml xmlns="http://www.opengis.net/kml/2.2">
  <Document>
    <name>${route.id}</name>
    <description>Route generated by Honua.MapSDK</description>
    <Placemark>
      <name>Route</name>
      <LineString>
        <coordinates>${coordString}</coordinates>
      </LineString>
    </Placemark>
  </Document>
</kml>`;
}

// Register click handler setter
export function setRoutingClickHandler(handler) {
    window.honuaRoutingClickHandler = handler;
}

export function setRouteClickHandler(handler) {
    window.honuaRouteClickHandler = handler;
}

export function setWaypointDraggedHandler(handler) {
    window.honuaWaypointDraggedHandler = handler;
}
