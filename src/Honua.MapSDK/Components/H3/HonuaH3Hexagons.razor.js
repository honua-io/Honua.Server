// H3 Hexagonal Binning JavaScript Module
// Integrates with MapLibre GL JS for H3 hexagon visualization

export function initializeH3Layer(options) {
    const layer = {
        id: options.id,
        resolution: options.resolution,
        aggregation: options.aggregation,
        valueField: options.valueField,
        opacity: options.opacity,
        colorScheme: options.colorScheme,
        sourceLayer: options.sourceLayer,
        sourceId: `${options.id}-source`,
        layerId: `${options.id}-layer`,
        data: null,
        map: null
    };

    // Get map instance (assuming global window.honuaMap or similar)
    layer.map = window.honuaMap || window.map;

    return layer;
}

export async function refreshHexagons(layerRef, options) {
    if (!layerRef.map) {
        throw new Error('Map not initialized');
    }

    try {
        // Get source data
        const sourceData = await getSourceData(layerRef);

        if (!sourceData || !sourceData.features || sourceData.features.length === 0) {
            return {
                success: false,
                message: 'No source data available'
            };
        }

        // Bin points into H3 hexagons
        const hexBins = binPointsToH3(sourceData.features, options);

        // Create GeoJSON from hex bins
        const hexGeoJSON = createHexGeoJSON(hexBins, options.resolution);

        // Update or create map source
        updateMapSource(layerRef, hexGeoJSON);

        // Update or create map layer
        updateMapLayer(layerRef, options);

        // Calculate statistics
        const stats = calculateStats(hexBins);

        return {
            success: true,
            stats: stats
        };
    } catch (error) {
        console.error('Error refreshing H3 hexagons:', error);
        throw error;
    }
}

export function clearHexagons(layerRef) {
    if (!layerRef.map) return;

    // Remove layer if exists
    if (layerRef.map.getLayer(layerRef.layerId)) {
        layerRef.map.removeLayer(layerRef.layerId);
    }

    // Remove source if exists
    if (layerRef.map.getSource(layerRef.sourceId)) {
        layerRef.map.removeSource(layerRef.sourceId);
    }
}

export function updateOpacity(layerRef, opacity) {
    if (!layerRef.map || !layerRef.map.getLayer(layerRef.layerId)) return;

    layerRef.map.setPaintProperty(layerRef.layerId, 'fill-opacity', opacity);
    layerRef.map.setPaintProperty(layerRef.layerId, 'fill-outline-opacity', opacity * 0.5);
}

export function disposeH3Layer(layerRef) {
    clearHexagons(layerRef);
}

// Helper functions

async function getSourceData(layerRef) {
    // If source layer is specified, get data from that layer
    if (layerRef.sourceLayer && layerRef.map.getSource(layerRef.sourceLayer)) {
        const source = layerRef.map.getSource(layerRef.sourceLayer);

        if (source._data) {
            return source._data;
        }
    }

    // Otherwise, look for any GeoJSON source with point data
    const style = layerRef.map.getStyle();
    for (const [sourceId, source] of Object.entries(style.sources)) {
        if (source.type === 'geojson' && source.data) {
            const data = typeof source.data === 'string'
                ? await fetch(source.data).then(r => r.json())
                : source.data;

            if (data.features && data.features.some(f => f.geometry.type === 'Point')) {
                return data;
            }
        }
    }

    return null;
}

function binPointsToH3(features, options) {
    // Check if h3-js is available
    if (typeof h3 === 'undefined') {
        console.error('h3-js library not loaded. Please include: <script src="https://unpkg.com/h3-js"></script>');
        throw new Error('h3-js library required');
    }

    const hexBins = new Map();

    for (const feature of features) {
        if (feature.geometry.type !== 'Point') continue;

        const [lng, lat] = feature.geometry.coordinates;
        const h3Index = h3.latLngToCell(lat, lng, options.resolution);

        if (!hexBins.has(h3Index)) {
            hexBins.set(h3Index, {
                h3Index: h3Index,
                values: [],
                count: 0
            });
        }

        const bin = hexBins.get(h3Index);
        bin.count++;

        // Extract value if field is specified
        let value = 1.0;
        if (options.valueField && feature.properties && feature.properties[options.valueField]) {
            value = parseFloat(feature.properties[options.valueField]) || 1.0;
        }

        bin.values.push(value);
    }

    // Compute aggregations
    for (const [h3Index, bin] of hexBins) {
        bin.value = computeAggregation(bin.values, options.aggregation);
    }

    return hexBins;
}

function computeAggregation(values, aggregationType) {
    if (values.length === 0) return 0;

    switch (aggregationType.toLowerCase()) {
        case 'count':
            return values.length;
        case 'sum':
            return values.reduce((a, b) => a + b, 0);
        case 'average':
        case 'avg':
        case 'mean':
            return values.reduce((a, b) => a + b, 0) / values.length;
        case 'min':
        case 'minimum':
            return Math.min(...values);
        case 'max':
        case 'maximum':
            return Math.max(...values);
        case 'stddev':
        case 'std':
            const avg = values.reduce((a, b) => a + b, 0) / values.length;
            const squareDiffs = values.map(v => Math.pow(v - avg, 2));
            return Math.sqrt(squareDiffs.reduce((a, b) => a + b, 0) / values.length);
        case 'median':
        case 'med':
            const sorted = [...values].sort((a, b) => a - b);
            const mid = Math.floor(sorted.length / 2);
            return sorted.length % 2 === 0
                ? (sorted[mid - 1] + sorted[mid]) / 2
                : sorted[mid];
        default:
            return values.length;
    }
}

function createHexGeoJSON(hexBins, resolution) {
    if (typeof h3 === 'undefined') {
        throw new Error('h3-js library required');
    }

    const features = [];

    for (const [h3Index, bin] of hexBins) {
        const boundary = h3.cellToBoundary(h3Index);

        // Convert boundary to GeoJSON coordinates (close the ring)
        const coordinates = [boundary.map(([lat, lng]) => [lng, lat])];
        coordinates[0].push(coordinates[0][0]); // Close the ring

        features.push({
            type: 'Feature',
            properties: {
                h3Index: h3Index,
                count: bin.count,
                value: bin.value,
                resolution: resolution
            },
            geometry: {
                type: 'Polygon',
                coordinates: coordinates
            }
        });
    }

    return {
        type: 'FeatureCollection',
        features: features
    };
}

function updateMapSource(layerRef, geoJSON) {
    const map = layerRef.map;

    if (map.getSource(layerRef.sourceId)) {
        map.getSource(layerRef.sourceId).setData(geoJSON);
    } else {
        map.addSource(layerRef.sourceId, {
            type: 'geojson',
            data: geoJSON
        });
    }
}

function updateMapLayer(layerRef, options) {
    const map = layerRef.map;

    // Remove existing layer if it exists
    if (map.getLayer(layerRef.layerId)) {
        map.removeLayer(layerRef.layerId);
    }

    // Get color scheme
    const colorStops = getColorScheme(options.colorScheme);

    // Add new layer
    map.addLayer({
        id: layerRef.layerId,
        type: 'fill',
        source: layerRef.sourceId,
        paint: {
            'fill-color': [
                'interpolate',
                ['linear'],
                ['get', 'value'],
                ...colorStops
            ],
            'fill-opacity': options.opacity,
            'fill-outline-color': '#000000'
        }
    });

    // Add click handler
    map.on('click', layerRef.layerId, (e) => {
        if (e.features && e.features.length > 0) {
            const feature = e.features[0];
            new maplibregl.Popup()
                .setLngLat(e.lngLat)
                .setHTML(`
                    <strong>H3 Index:</strong> ${feature.properties.h3Index}<br>
                    <strong>Count:</strong> ${feature.properties.count}<br>
                    <strong>Value:</strong> ${feature.properties.value.toFixed(2)}<br>
                    <strong>Resolution:</strong> ${feature.properties.resolution}
                `)
                .addTo(map);
        }
    });

    // Change cursor on hover
    map.on('mouseenter', layerRef.layerId, () => {
        map.getCanvas().style.cursor = 'pointer';
    });

    map.on('mouseleave', layerRef.layerId, () => {
        map.getCanvas().style.cursor = '';
    });
}

function getColorScheme(scheme) {
    const schemes = {
        'YlOrRd': [
            0, '#ffffcc',
            0.2, '#ffeda0',
            0.4, '#fed976',
            0.6, '#feb24c',
            0.8, '#fd8d3c',
            1.0, '#f03b20'
        ],
        'Blues': [
            0, '#f7fbff',
            0.2, '#deebf7',
            0.4, '#c6dbef',
            0.6, '#9ecae1',
            0.8, '#6baed6',
            1.0, '#2171b5'
        ],
        'Greens': [
            0, '#f7fcf5',
            0.2, '#e5f5e0',
            0.4, '#c7e9c0',
            0.6, '#a1d99b',
            0.8, '#74c476',
            1.0, '#238b45'
        ],
        'Viridis': [
            0, '#440154',
            0.2, '#3b528b',
            0.4, '#21918c',
            0.6, '#5ec962',
            0.8, '#fde725',
            1.0, '#fde725'
        ],
        'Plasma': [
            0, '#0d0887',
            0.2, '#6a00a8',
            0.4, '#b12a90',
            0.6, '#e16462',
            0.8, '#fca636',
            1.0, '#f0f921'
        ],
        'Inferno': [
            0, '#000004',
            0.2, '#420a68',
            0.4, '#932667',
            0.6, '#dd513a',
            0.8, '#fca50a',
            1.0, '#fcffa4'
        ],
        'Turbo': [
            0, '#30123b',
            0.2, '#4777ef',
            0.4, '#1ac7c2',
            0.6, '#a0fc59',
            0.8, '#faba39',
            1.0, '#7a0403'
        ]
    };

    return schemes[scheme] || schemes['YlOrRd'];
}

function calculateStats(hexBins) {
    const values = Array.from(hexBins.values());
    const pointCount = values.reduce((sum, bin) => sum + bin.count, 0);

    // Get H3 average area (this is approximate - ideally would use h3.getHexagonAreaAvg)
    const avgArea = values.length > 0 && values[0].h3Index
        ? getApproximateH3Area(values[0].h3Index)
        : 0;

    const allValues = values.map(v => v.value);

    return {
        hexagonCount: hexBins.size,
        pointCount: pointCount,
        avgHexagonArea: avgArea,
        minValue: allValues.length > 0 ? Math.min(...allValues) : null,
        maxValue: allValues.length > 0 ? Math.max(...allValues) : null
    };
}

function getApproximateH3Area(h3Index) {
    if (typeof h3 === 'undefined') return 0;

    try {
        // Get resolution
        const resolution = h3.getResolution(h3Index);

        // Approximate areas in m² for each resolution
        const areas = [
            4250000000000, // res 0
            607000000000,  // res 1
            86700000000,   // res 2
            12400000000,   // res 3
            1770000000,    // res 4
            252000000,     // res 5
            36000000,      // res 6
            5160000,       // res 7
            737000,        // res 8
            105000,        // res 9
            15000,         // res 10
            2140,          // res 11
            305,           // res 12
            43.6,          // res 13
            6.2,           // res 14
            0.9            // res 15
        ];

        return areas[resolution] || 0;
    } catch {
        return 0;
    }
}
