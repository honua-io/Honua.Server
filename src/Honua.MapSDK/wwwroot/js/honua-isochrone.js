// Honua.MapSDK - Isochrone JavaScript Module
// Handles isochrone (travel time polygon) visualization on MapLibre GL JS maps

export function initializeIsochrone(mapId, options) {
    const map = window.honuaMaps?.[mapId];
    if (!map) {
        console.error(`Map ${mapId} not found`);
        return false;
    }

    if (!window.honuaIsochrones) {
        window.honuaIsochrones = {};
    }

    window.honuaIsochrones[mapId] = {
        results: [],
        originMarker: null,
        visible: true,
        options: options || {}
    };

    // Add click handler for origin selection if enabled
    if (options?.enableMapClick) {
        map.on('click', (e) => {
            window.honuaIsochroneClickHandler?.(mapId, e.lngLat.lng, e.lngLat.lat);
        });
    }

    console.log(`Isochrone initialized for map ${mapId}`);
    return true;
}

export function setOriginPoint(mapId, longitude, latitude) {
    const isochrone = window.honuaIsochrones?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!isochrone || !map) return false;

    // Remove existing origin marker
    if (isochrone.originMarker) {
        isochrone.originMarker.remove();
    }

    // Create new origin marker
    const markerEl = createOriginMarker();
    isochrone.originMarker = new maplibregl.Marker({
        element: markerEl,
        draggable: true
    })
        .setLngLat([longitude, latitude])
        .addTo(map);

    // Handle marker drag
    isochrone.originMarker.on('dragend', () => {
        const lngLat = isochrone.originMarker.getLngLat();
        window.honuaIsochroneOriginDraggedHandler?.(mapId, lngLat.lng, lngLat.lat);
    });

    return true;
}

export function displayIsochrone(mapId, isochroneResult) {
    const isochrone = window.honuaIsochrones?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!isochrone || !map) return false;

    // Clear existing isochrones
    clearIsochrone(mapId);

    const resultId = `isochrone-${Date.now()}`;
    isochrone.results.push({ id: resultId, data: isochroneResult });

    // Add isochrone polygons (render largest first for proper layering)
    const sortedPolygons = [...isochroneResult.polygons].sort((a, b) => b.interval - a.interval);

    sortedPolygons.forEach((polygon, index) => {
        const layerId = `${resultId}-polygon-${index}`;
        const sourceId = `${resultId}-source-${index}`;

        // Add source
        map.addSource(sourceId, {
            type: 'geojson',
            data: {
                type: 'Feature',
                properties: {
                    interval: polygon.interval,
                    color: polygon.color,
                    opacity: polygon.opacity,
                    area: polygon.area || 0
                },
                geometry: polygon.geometry
            }
        });

        // Add fill layer
        map.addLayer({
            id: layerId,
            type: 'fill',
            source: sourceId,
            paint: {
                'fill-color': polygon.color,
                'fill-opacity': polygon.opacity || 0.3
            }
        });

        // Add outline layer
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

        // Add click handler for polygon
        map.on('click', layerId, (e) => {
            const properties = e.features[0].properties;
            window.honuaIsochronePolygonClickHandler?.(mapId, resultId, properties);
        });

        // Add hover effect
        map.on('mouseenter', layerId, () => {
            map.getCanvas().style.cursor = 'pointer';
        });

        map.on('mouseleave', layerId, () => {
            map.getCanvas().style.cursor = '';
        });
    });

    // Set origin marker if provided
    if (isochroneResult.center) {
        setOriginPoint(mapId, isochroneResult.center[0], isochroneResult.center[1]);
    }

    // Fit map to isochrone bounds
    if (isochroneResult.polygons.length > 0) {
        fitIsochroneToView(map, isochroneResult.polygons);
    }

    console.log(`Displayed isochrone with ${isochroneResult.polygons.length} polygons`);
    return true;
}

export function clearIsochrone(mapId) {
    const isochrone = window.honuaIsochrones?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!isochrone || !map) return false;

    // Remove all isochrone layers and sources
    isochrone.results.forEach(result => {
        result.data.polygons.forEach((polygon, index) => {
            const layerId = `${result.id}-polygon-${index}`;
            const sourceId = `${result.id}-source-${index}`;

            // Remove layers
            if (map.getLayer(layerId)) {
                map.removeLayer(layerId);
            }
            if (map.getLayer(`${layerId}-outline`)) {
                map.removeLayer(`${layerId}-outline`);
            }

            // Remove source
            if (map.getSource(sourceId)) {
                map.removeSource(sourceId);
            }
        });
    });

    // Clear results array
    isochrone.results = [];

    console.log('Cleared all isochrones');
    return true;
}

export function toggleIsochroneVisibility(mapId, visible) {
    const isochrone = window.honuaIsochrones?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!isochrone || !map) return false;

    isochrone.visible = visible;
    const visibility = visible ? 'visible' : 'none';

    // Toggle visibility of all layers
    isochrone.results.forEach(result => {
        result.data.polygons.forEach((polygon, index) => {
            const layerId = `${result.id}-polygon-${index}`;
            if (map.getLayer(layerId)) {
                map.setLayoutProperty(layerId, 'visibility', visibility);
            }
            if (map.getLayer(`${layerId}-outline`)) {
                map.setLayoutProperty(`${layerId}-outline`, 'visibility', visibility);
            }
        });
    });

    // Toggle origin marker
    if (isochrone.originMarker) {
        const markerEl = isochrone.originMarker.getElement();
        markerEl.style.display = visible ? 'block' : 'none';
    }

    return true;
}

export function exportIsochroneAsGeoJson(mapId) {
    const isochrone = window.honuaIsochrones?.[mapId];
    if (!isochrone || isochrone.results.length === 0) return null;

    const latestResult = isochrone.results[isochrone.results.length - 1];

    const features = latestResult.data.polygons.map(polygon => ({
        type: 'Feature',
        properties: {
            interval: polygon.interval,
            color: polygon.color,
            opacity: polygon.opacity,
            area: polygon.area || 0,
            travelMode: latestResult.data.travelMode
        },
        geometry: polygon.geometry
    }));

    const featureCollection = {
        type: 'FeatureCollection',
        properties: {
            center: latestResult.data.center,
            travelMode: latestResult.data.travelMode,
            generatedAt: new Date().toISOString()
        },
        features
    };

    return JSON.stringify(featureCollection, null, 2);
}

export function updateIsochroneColors(mapId, intervalColors) {
    const isochrone = window.honuaIsochrones?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!isochrone || !map) return false;

    isochrone.results.forEach(result => {
        result.data.polygons.forEach((polygon, index) => {
            const layerId = `${result.id}-polygon-${index}`;
            const color = intervalColors[polygon.interval] || polygon.color;

            if (map.getLayer(layerId)) {
                map.setPaintProperty(layerId, 'fill-color', color);
                map.setPaintProperty(`${layerId}-outline`, 'line-color', color);
            }

            // Update stored color
            polygon.color = color;
        });
    });

    return true;
}

export function updateIsochroneOpacity(mapId, opacity) {
    const isochrone = window.honuaIsochrones?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!isochrone || !map) return false;

    isochrone.results.forEach(result => {
        result.data.polygons.forEach((polygon, index) => {
            const layerId = `${result.id}-polygon-${index}`;

            if (map.getLayer(layerId)) {
                map.setPaintProperty(layerId, 'fill-opacity', opacity);
            }

            // Update stored opacity
            polygon.opacity = opacity;
        });
    });

    return true;
}

export function highlightIsochroneInterval(mapId, interval) {
    const isochrone = window.honuaIsochrones?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!isochrone || !map) return false;

    isochrone.results.forEach(result => {
        result.data.polygons.forEach((polygon, index) => {
            const layerId = `${result.id}-polygon-${index}`;

            if (map.getLayer(layerId)) {
                const isHighlighted = polygon.interval === interval;
                const opacity = isHighlighted ? (polygon.opacity * 1.5) : (polygon.opacity * 0.5);
                const lineWidth = isHighlighted ? 3 : 2;

                map.setPaintProperty(layerId, 'fill-opacity', opacity);
                map.setPaintProperty(`${layerId}-outline`, 'line-width', lineWidth);
            }
        });
    });

    return true;
}

export function clearHighlight(mapId) {
    const isochrone = window.honuaIsochrones?.[mapId];
    const map = window.honuaMaps?.[mapId];
    if (!isochrone || !map) return false;

    isochrone.results.forEach(result => {
        result.data.polygons.forEach((polygon, index) => {
            const layerId = `${result.id}-polygon-${index}`;

            if (map.getLayer(layerId)) {
                map.setPaintProperty(layerId, 'fill-opacity', polygon.opacity);
                map.setPaintProperty(`${layerId}-outline`, 'line-width', 2);
            }
        });
    });

    return true;
}

export function getIsochroneStats(mapId) {
    const isochrone = window.honuaIsochrones?.[mapId];
    if (!isochrone || isochrone.results.length === 0) return null;

    const latestResult = isochrone.results[isochrone.results.length - 1];

    return {
        center: latestResult.data.center,
        travelMode: latestResult.data.travelMode,
        polygonCount: latestResult.data.polygons.length,
        intervals: latestResult.data.polygons.map(p => p.interval),
        totalArea: latestResult.data.polygons.reduce((sum, p) => sum + (p.area || 0), 0)
    };
}

// Helper functions

function createOriginMarker() {
    const el = document.createElement('div');
    el.className = 'isochrone-origin-marker';
    el.style.cssText = `
        width: 32px;
        height: 32px;
        background-color: #FF4444;
        border: 3px solid white;
        border-radius: 50%;
        box-shadow: 0 2px 8px rgba(0,0,0,0.3);
        cursor: pointer;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 16px;
    `;
    el.textContent = '📍';
    return el;
}

function fitIsochroneToView(map, polygons) {
    if (!polygons || polygons.length === 0) return;

    const bounds = new maplibregl.LngLatBounds();

    // Use the largest polygon (last in sorted array) for bounds
    const largestPolygon = polygons.reduce((max, p) =>
        p.interval > max.interval ? p : max, polygons[0]);

    if (largestPolygon.geometry && largestPolygon.geometry.coordinates) {
        const coords = largestPolygon.geometry.coordinates;

        // Handle MultiPolygon or Polygon
        const coordArray = largestPolygon.geometry.type === 'MultiPolygon'
            ? coords.flat(2)
            : coords.flat();

        coordArray.forEach(coord => {
            if (Array.isArray(coord) && coord.length === 2) {
                bounds.extend(coord);
            }
        });

        map.fitBounds(bounds, {
            padding: { top: 80, bottom: 80, left: 80, right: 80 },
            duration: 1000,
            maxZoom: 14
        });
    }
}

// Register callback handlers
export function setIsochroneClickHandler(handler) {
    window.honuaIsochroneClickHandler = handler;
}

export function setIsochronePolygonClickHandler(handler) {
    window.honuaIsochronePolygonClickHandler = handler;
}

export function setIsochroneOriginDraggedHandler(handler) {
    window.honuaIsochroneOriginDraggedHandler = handler;
}
