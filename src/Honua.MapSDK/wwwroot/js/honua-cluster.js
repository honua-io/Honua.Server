// Honua Cluster JavaScript Module
// MapLibre GL JS clustering with Supercluster integration for Blazor

import Supercluster from 'https://cdn.jsdelivr.net/npm/supercluster@8.0.1/+esm';

const clusters = new Map();
const mapInstances = new Map();
const superclusterInstances = new Map();

/**
 * Initializes the cluster module with a map instance
 * @param {string} mapId - Map container ID
 * @param {Object} mapInstance - MapLibre GL map instance
 */
export function initializeCluster(mapId, mapInstance) {
    if (!mapInstance) {
        console.error('Map instance is required');
        return;
    }
    mapInstances.set(mapId, mapInstance);
}

/**
 * Creates a new cluster layer with Supercluster
 * @param {string} mapId - Map container ID
 * @param {Object} options - Cluster configuration options
 * @param {Object} dotNetRef - Reference to .NET component for callbacks
 * @returns {Object} Cluster API object
 */
export function createCluster(mapId, options, dotNetRef) {
    const map = mapInstances.get(mapId);
    if (!map) {
        console.error(`Map ${mapId} not found. Call initializeCluster first.`);
        return null;
    }

    const sourceId = options.sourceId || `cluster-source-${Date.now()}`;
    const layerId = options.layerId || `cluster-layer-${sourceId}`;
    const unclusteredLayerId = options.unclusteredLayerId || `unclustered-layer-${sourceId}`;

    // Initialize Supercluster
    const supercluster = new Supercluster({
        radius: options.clusterRadius || 50,
        maxZoom: options.clusterMaxZoom || 16,
        minZoom: options.minZoom || 0,
        extent: 512,
        nodeSize: 64,
        log: false,
        // Custom cluster properties aggregation
        reduce: options.clusterProperties ? createReduceFunction(options.clusterProperties) : null,
        map: options.clusterProperties ? createMapFunction(options.clusterProperties) : null
    });

    superclusterInstances.set(sourceId, supercluster);

    // Add empty source initially
    if (!map.getSource(sourceId)) {
        map.addSource(sourceId, {
            type: 'geojson',
            data: {
                type: 'FeatureCollection',
                features: []
            },
            cluster: false // We handle clustering with Supercluster
        });
    }

    // Create cluster circle layer
    const clusterLayer = {
        id: layerId,
        type: 'circle',
        source: sourceId,
        filter: ['has', 'point_count'],
        paint: {
            'circle-color': buildColorExpression(options.style.colorScale),
            'circle-radius': buildSizeExpression(options.style.sizeScale),
            'circle-stroke-width': options.style.strokeWidth || 2,
            'circle-stroke-color': options.style.strokeColor || '#ffffff',
            'circle-opacity': options.style.opacity || 0.8
        }
    };

    // Apply zoom limits if specified
    if (options.minZoom !== undefined && options.minZoom !== null) {
        clusterLayer.minzoom = options.minZoom;
    }
    if (options.maxZoom !== undefined && options.maxZoom !== null) {
        clusterLayer.maxzoom = options.maxZoom;
    }

    try {
        map.addLayer(clusterLayer);

        // Add cluster count label layer
        if (options.style.showCountLabel !== false) {
            map.addLayer({
                id: `${layerId}-count`,
                type: 'symbol',
                source: sourceId,
                filter: ['has', 'point_count'],
                layout: {
                    'text-field': '{point_count_abbreviated}',
                    'text-font': ['Open Sans Semibold', 'Arial Unicode MS Bold'],
                    'text-size': options.style.labelFontSize || 12
                },
                paint: {
                    'text-color': options.style.labelColor || '#ffffff'
                }
            });
        }

        // Add unclustered point layer
        map.addLayer({
            id: unclusteredLayerId,
            type: 'circle',
            source: sourceId,
            filter: ['!', ['has', 'point_count']],
            paint: {
                'circle-color': options.style.unclusteredColor || '#11b4da',
                'circle-radius': options.style.unclusteredRadius || 6,
                'circle-stroke-width': options.style.unclusteredStrokeWidth || 1,
                'circle-stroke-color': options.style.unclusteredStrokeColor || '#ffffff'
            }
        });

        // Store cluster metadata
        const clusterData = {
            mapId,
            sourceId,
            layerId,
            unclusteredLayerId,
            options,
            dotNetRef,
            map,
            supercluster,
            spiderfyLayer: null,
            extentLayer: null
        };

        clusters.set(layerId, clusterData);

        // Setup event handlers
        setupClusterHandlers(clusterData);

        console.log(`Cluster layer ${layerId} created successfully`);
        return createClusterAPI(layerId);
    } catch (error) {
        console.error('Error creating cluster:', error);
        return null;
    }
}

/**
 * Creates reduce function for custom cluster properties
 */
function createReduceFunction(clusterProperties) {
    if (!clusterProperties) return null;

    return (accumulated, properties) => {
        for (const [key, aggregation] of Object.entries(clusterProperties)) {
            const value = properties[key];
            if (value !== undefined && value !== null) {
                switch (aggregation.toLowerCase()) {
                    case 'sum':
                        accumulated[key] = (accumulated[key] || 0) + value;
                        break;
                    case 'max':
                        accumulated[key] = Math.max(accumulated[key] || -Infinity, value);
                        break;
                    case 'min':
                        accumulated[key] = Math.min(accumulated[key] || Infinity, value);
                        break;
                    case 'mean':
                        accumulated[`${key}_sum`] = (accumulated[`${key}_sum`] || 0) + value;
                        accumulated[`${key}_count`] = (accumulated[`${key}_count`] || 0) + 1;
                        accumulated[key] = accumulated[`${key}_sum`] / accumulated[`${key}_count`];
                        break;
                }
            }
        }
    };
}

/**
 * Creates map function for custom cluster properties
 */
function createMapFunction(clusterProperties) {
    if (!clusterProperties) return null;

    return (properties) => {
        const mapped = {};
        for (const key of Object.keys(clusterProperties)) {
            mapped[key] = properties[key];
        }
        return mapped;
    };
}

/**
 * Builds MapLibre color expression from color scale
 */
function buildColorExpression(colorScale) {
    if (!colorScale) {
        return '#51bbd6';
    }

    const sortedThresholds = Object.entries(colorScale)
        .map(([threshold, color]) => [parseInt(threshold), color])
        .sort((a, b) => a[0] - b[0]);

    const expression = ['step', ['get', 'point_count']];

    // Default color (for clusters with fewer points than the first threshold)
    expression.push(sortedThresholds[0][1]);

    // Add thresholds and colors
    for (let i = 1; i < sortedThresholds.length; i++) {
        expression.push(sortedThresholds[i][0]);
        expression.push(sortedThresholds[i][1]);
    }

    return expression;
}

/**
 * Builds MapLibre size expression from size scale
 */
function buildSizeExpression(sizeScale) {
    if (!sizeScale) {
        return 20;
    }

    const sortedThresholds = Object.entries(sizeScale)
        .map(([threshold, size]) => [parseInt(threshold), size])
        .sort((a, b) => a[0] - b[0]);

    const expression = ['step', ['get', 'point_count']];

    // Default size (for clusters with fewer points than the first threshold)
    expression.push(sortedThresholds[0][1]);

    // Add thresholds and sizes
    for (let i = 1; i < sortedThresholds.length; i++) {
        expression.push(sortedThresholds[i][0]);
        expression.push(sortedThresholds[i][1]);
    }

    return expression;
}

/**
 * Setup event handlers for cluster interactions
 */
function setupClusterHandlers(clusterData) {
    const { map, layerId, unclusteredLayerId, options, dotNetRef, supercluster, sourceId } = clusterData;

    // Cluster click handler
    map.on('click', layerId, async (e) => {
        const features = map.queryRenderedFeatures(e.point, { layers: [layerId] });
        if (features.length === 0) return;

        const feature = features[0];
        const clusterId = feature.properties.cluster_id;
        const pointCount = feature.properties.point_count;
        const coordinates = feature.geometry.coordinates.slice();

        // Get expansion zoom
        const expansionZoom = supercluster.getClusterExpansionZoom(clusterId);

        // Notify .NET component
        if (dotNetRef) {
            await dotNetRef.invokeMethodAsync(
                'OnClusterClickedInternal',
                clusterId,
                pointCount,
                coordinates,
                expansionZoom,
                feature.properties
            );
        }

        // Handle zoom on click
        if (options.zoomOnClick !== false) {
            if (pointCount <= (options.spiderfyMaxPoints || 50) && options.enableSpiderfy !== false) {
                // Spider-fy the cluster
                await spiderfyCluster(clusterData, clusterId, coordinates);
            } else {
                // Zoom to expansion zoom
                map.easeTo({
                    center: coordinates,
                    zoom: expansionZoom,
                    duration: options.animateTransitions !== false ? 500 : 0
                });
            }
        }
    });

    // Cluster hover handler for extent
    if (options.showClusterExtent !== false) {
        map.on('mouseenter', layerId, (e) => {
            map.getCanvas().style.cursor = 'pointer';

            const features = map.queryRenderedFeatures(e.point, { layers: [layerId] });
            if (features.length === 0) return;

            const feature = features[0];
            const clusterId = feature.properties.cluster_id;

            // Get cluster leaves (children)
            const leaves = supercluster.getLeaves(clusterId, Infinity);
            if (leaves.length === 0) return;

            // Calculate bounds
            const bounds = leaves.reduce((bbox, leaf) => {
                const [lng, lat] = leaf.geometry.coordinates;
                return [
                    Math.min(bbox[0], lng),
                    Math.min(bbox[1], lat),
                    Math.max(bbox[2], lng),
                    Math.max(bbox[3], lat)
                ];
            }, [Infinity, Infinity, -Infinity, -Infinity]);

            // Show extent box
            showClusterExtent(clusterData, clusterId, bounds);
        });

        map.on('mouseleave', layerId, () => {
            map.getCanvas().style.cursor = '';
            hideClusterExtent(clusterData);
        });
    }

    // Unclustered point click handler
    map.on('click', unclusteredLayerId, (e) => {
        const features = map.queryRenderedFeatures(e.point, { layers: [unclusteredLayerId] });
        if (features.length > 0) {
            const feature = features[0];
            // Could notify about unclustered point click
            console.log('Unclustered point clicked:', feature.properties);
        }
    });

    // Change cursor on hover
    map.on('mouseenter', layerId, () => {
        map.getCanvas().style.cursor = 'pointer';
    });

    map.on('mouseleave', layerId, () => {
        map.getCanvas().style.cursor = '';
    });

    map.on('mouseenter', unclusteredLayerId, () => {
        map.getCanvas().style.cursor = 'pointer';
    });

    map.on('mouseleave', unclusteredLayerId, () => {
        map.getCanvas().style.cursor = '';
    });

    // Update clusters on zoom
    map.on('zoom', () => {
        updateClusters(clusterData);
    });

    map.on('move', () => {
        updateClusters(clusterData);
    });
}

/**
 * Updates cluster data based on current map view
 */
function updateClusters(clusterData) {
    const { map, sourceId, supercluster } = clusterData;

    if (!supercluster) return;

    const bounds = map.getBounds();
    const zoom = Math.floor(map.getZoom());

    // Get clusters in current view
    const bbox = [bounds.getWest(), bounds.getSouth(), bounds.getEast(), bounds.getNorth()];
    const clusterFeatures = supercluster.getClusters(bbox, zoom);

    // Update source data
    const source = map.getSource(sourceId);
    if (source) {
        source.setData({
            type: 'FeatureCollection',
            features: clusterFeatures
        });
    }
}

/**
 * Spider-fy a cluster (expand to show individual points)
 */
async function spiderfyCluster(clusterData, clusterId, center) {
    const { map, supercluster, dotNetRef, sourceId } = clusterData;

    // Get cluster leaves
    const leaves = supercluster.getLeaves(clusterId, Infinity);
    if (leaves.length === 0) return;

    // Calculate spiral positions
    const positions = calculateSpiralPositions(leaves.length, center, map);

    // Create spider features
    const spiderFeatures = leaves.map((leaf, i) => {
        return {
            type: 'Feature',
            geometry: {
                type: 'Point',
                coordinates: positions[i]
            },
            properties: leaf.properties
        };
    });

    // Create spider legs (lines from cluster center to points)
    const legFeatures = positions.map(pos => ({
        type: 'Feature',
        geometry: {
            type: 'LineString',
            coordinates: [center, pos]
        },
        properties: {}
    }));

    // Add spider layer
    const spiderSourceId = `${sourceId}-spider`;
    const spiderLayerId = `${sourceId}-spider-layer`;
    const legLayerId = `${sourceId}-spider-legs`;

    // Remove existing spider layers if any
    removeSpiderfyLayers(clusterData);

    // Add spider legs source and layer
    map.addSource(`${spiderSourceId}-legs`, {
        type: 'geojson',
        data: {
            type: 'FeatureCollection',
            features: legFeatures
        }
    });

    map.addLayer({
        id: legLayerId,
        type: 'line',
        source: `${spiderSourceId}-legs`,
        paint: {
            'line-color': '#888888',
            'line-width': 1,
            'line-opacity': 0.5
        }
    });

    // Add spider points source and layer
    map.addSource(spiderSourceId, {
        type: 'geojson',
        data: {
            type: 'FeatureCollection',
            features: spiderFeatures
        }
    });

    map.addLayer({
        id: spiderLayerId,
        type: 'circle',
        source: spiderSourceId,
        paint: {
            'circle-color': '#11b4da',
            'circle-radius': 6,
            'circle-stroke-width': 2,
            'circle-stroke-color': '#ffffff'
        }
    });

    // Store spider layer info
    clusterData.spiderfyLayer = {
        sourceId: spiderSourceId,
        layerId: spiderLayerId,
        legSourceId: `${spiderSourceId}-legs`,
        legLayerId: legLayerId
    };

    // Notify .NET component
    if (dotNetRef) {
        await dotNetRef.invokeMethodAsync(
            'OnClusterSpiderfiedInternal',
            clusterId,
            leaves.length,
            center
        );
    }

    // Click anywhere to close spider
    const closeSpider = () => {
        removeSpiderfyLayers(clusterData);
        map.off('click', closeSpider);
    };
    setTimeout(() => map.on('click', closeSpider), 100);
}

/**
 * Calculate spiral positions for spiderfied points
 */
function calculateSpiralPositions(count, center, map) {
    const positions = [];
    const pixelCenter = map.project(center);

    const legLengthStart = 15;
    const legLengthFactor = 5;
    const spiralLengthFactor = 50;

    let angle = 0;
    let legLength = legLengthStart;
    let i = 0;

    while (i < count) {
        angle += spiralLengthFactor / legLength + i * 0.0005;
        const pt = {
            x: pixelCenter.x + legLength * Math.cos(angle),
            y: pixelCenter.y + legLength * Math.sin(angle)
        };
        const coord = map.unproject(pt);
        positions.push([coord.lng, coord.lat]);
        legLength += (2 * Math.PI * legLengthFactor) / angle;
        i++;
    }

    return positions;
}

/**
 * Remove spiderfy layers
 */
function removeSpiderfyLayers(clusterData) {
    const { map, spiderfyLayer } = clusterData;

    if (!spiderfyLayer) return;

    if (map.getLayer(spiderfyLayer.layerId)) {
        map.removeLayer(spiderfyLayer.layerId);
    }
    if (map.getSource(spiderfyLayer.sourceId)) {
        map.removeSource(spiderfyLayer.sourceId);
    }
    if (map.getLayer(spiderfyLayer.legLayerId)) {
        map.removeLayer(spiderfyLayer.legLayerId);
    }
    if (map.getSource(spiderfyLayer.legSourceId)) {
        map.removeSource(spiderfyLayer.legSourceId);
    }

    clusterData.spiderfyLayer = null;
}

/**
 * Show cluster extent box
 */
function showClusterExtent(clusterData, clusterId, bounds) {
    const { map, sourceId, dotNetRef } = clusterData;

    const extentSourceId = `${sourceId}-extent`;
    const extentLayerId = `${sourceId}-extent-layer`;

    // Remove existing extent if any
    hideClusterExtent(clusterData);

    // Create rectangle from bounds
    const [west, south, east, north] = bounds;
    const rectangle = {
        type: 'Feature',
        geometry: {
            type: 'Polygon',
            coordinates: [[
                [west, north],
                [east, north],
                [east, south],
                [west, south],
                [west, north]
            ]]
        },
        properties: {}
    };

    map.addSource(extentSourceId, {
        type: 'geojson',
        data: rectangle
    });

    map.addLayer({
        id: extentLayerId,
        type: 'line',
        source: extentSourceId,
        paint: {
            'line-color': '#ff6b6b',
            'line-width': 2,
            'line-dasharray': [2, 2],
            'line-opacity': 0.7
        }
    });

    clusterData.extentLayer = {
        sourceId: extentSourceId,
        layerId: extentLayerId
    };

    // Notify .NET component
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync(
            'OnClusterExtentShownInternal',
            clusterId,
            bounds
        );
    }
}

/**
 * Hide cluster extent box
 */
function hideClusterExtent(clusterData) {
    const { map, extentLayer } = clusterData;

    if (!extentLayer) return;

    if (map.getLayer(extentLayer.layerId)) {
        map.removeLayer(extentLayer.layerId);
    }
    if (map.getSource(extentLayer.sourceId)) {
        map.removeSource(extentLayer.sourceId);
    }

    clusterData.extentLayer = null;
}

/**
 * Updates cluster data
 */
export function updateClusterData(mapId, sourceId, geojson) {
    const map = mapInstances.get(mapId);
    if (!map) return;

    const supercluster = superclusterInstances.get(sourceId);
    if (!supercluster) return;

    try {
        // Load data into Supercluster
        const features = geojson.type === 'FeatureCollection'
            ? geojson.features
            : [geojson];

        supercluster.load(features);

        // Update clusters for current view
        const clusterData = Array.from(clusters.values()).find(c => c.sourceId === sourceId);
        if (clusterData) {
            updateClusters(clusterData);
        }
    } catch (error) {
        console.error('Error updating cluster data:', error);
    }
}

/**
 * Toggles cluster layer visibility
 */
export function setClusterVisibility(mapId, layerId, visible) {
    const map = mapInstances.get(mapId);
    if (!map) return;

    try {
        const visibility = visible ? 'visible' : 'none';

        if (map.getLayer(layerId)) {
            map.setLayoutProperty(layerId, 'visibility', visibility);
        }
        if (map.getLayer(`${layerId}-count`)) {
            map.setLayoutProperty(`${layerId}-count`, 'visibility', visibility);
        }

        const clusterData = clusters.get(layerId);
        if (clusterData && map.getLayer(clusterData.unclusteredLayerId)) {
            map.setLayoutProperty(clusterData.unclusteredLayerId, 'visibility', visibility);
        }
    } catch (error) {
        console.error('Error setting cluster visibility:', error);
    }
}

/**
 * Calculates cluster statistics
 */
export function calculateClusterStatistics(mapId, sourceId) {
    const map = mapInstances.get(mapId);
    const supercluster = superclusterInstances.get(sourceId);

    if (!map || !supercluster) return null;

    try {
        const zoom = Math.floor(map.getZoom());
        const bounds = map.getBounds();
        const bbox = [bounds.getWest(), bounds.getSouth(), bounds.getEast(), bounds.getNorth()];

        const clusterFeatures = supercluster.getClusters(bbox, zoom);

        let clusterCount = 0;
        let unclusteredCount = 0;
        let maxClusterSize = 0;
        let totalClusterSize = 0;

        clusterFeatures.forEach(feature => {
            if (feature.properties.cluster) {
                clusterCount++;
                const pointCount = feature.properties.point_count;
                maxClusterSize = Math.max(maxClusterSize, pointCount);
                totalClusterSize += pointCount;
            } else {
                unclusteredCount++;
            }
        });

        const averageClusterSize = clusterCount > 0 ? totalClusterSize / clusterCount : 0;

        // Get all points for total count and bounds
        const allPoints = supercluster.points;
        let minLng = Infinity, minLat = Infinity;
        let maxLng = -Infinity, maxLat = -Infinity;

        allPoints.forEach(point => {
            const [lng, lat] = point.geometry.coordinates;
            minLng = Math.min(minLng, lng);
            minLat = Math.min(minLat, lat);
            maxLng = Math.max(maxLng, lng);
            maxLat = Math.max(maxLat, lat);
        });

        return {
            totalPoints: allPoints.length,
            clusterCount,
            unclusteredCount,
            zoomLevel: zoom,
            maxClusterSize,
            averageClusterSize,
            bounds: isFinite(minLng) ? [minLng, minLat, maxLng, maxLat] : null
        };
    } catch (error) {
        console.error('Error calculating statistics:', error);
        return null;
    }
}

/**
 * Removes cluster layer
 */
export function removeCluster(mapId, layerId) {
    const map = mapInstances.get(mapId);
    if (!map) return;

    const clusterData = clusters.get(layerId);
    if (!clusterData) return;

    try {
        // Remove spiderfy and extent layers
        removeSpiderfyLayers(clusterData);
        hideClusterExtent(clusterData);

        // Remove cluster layers
        if (map.getLayer(`${layerId}-count`)) {
            map.removeLayer(`${layerId}-count`);
        }
        if (map.getLayer(layerId)) {
            map.removeLayer(layerId);
        }
        if (map.getLayer(clusterData.unclusteredLayerId)) {
            map.removeLayer(clusterData.unclusteredLayerId);
        }

        // Remove source
        if (map.getSource(clusterData.sourceId)) {
            map.removeSource(clusterData.sourceId);
        }

        // Clean up
        superclusterInstances.delete(clusterData.sourceId);
        clusters.delete(layerId);

        console.log(`Cluster layer ${layerId} removed`);
    } catch (error) {
        console.error('Error removing cluster:', error);
    }
}

/**
 * Creates the public API for a cluster instance
 */
function createClusterAPI(layerId) {
    const clusterData = clusters.get(layerId);
    if (!clusterData) return null;

    return {
        updateOption: (property, value) => {
            clusterData.options[property] = value;

            // Re-initialize if cluster parameters changed
            if (property === 'clusterRadius' || property === 'clusterMaxZoom') {
                const { supercluster, sourceId, options } = clusterData;
                const newSupercluster = new Supercluster({
                    radius: options.clusterRadius || 50,
                    maxZoom: options.clusterMaxZoom || 16,
                    minZoom: options.minZoom || 0,
                    extent: 512,
                    nodeSize: 64
                });

                // Reload data
                if (supercluster.points) {
                    newSupercluster.load(supercluster.points);
                }

                superclusterInstances.set(sourceId, newSupercluster);
                clusterData.supercluster = newSupercluster;
                updateClusters(clusterData);
            }
        },
        updateConfiguration: (config) => {
            Object.assign(clusterData.options, config);
            updateClusters(clusterData);
        },
        updateData: (geojson) => {
            updateClusterData(clusterData.mapId, clusterData.sourceId, geojson);
        },
        setVisibility: (visible) => {
            setClusterVisibility(clusterData.mapId, layerId, visible);
        },
        getStatistics: () => {
            return calculateClusterStatistics(clusterData.mapId, clusterData.sourceId);
        },
        remove: () => {
            removeCluster(clusterData.mapId, layerId);
        },
        dispose: () => {
            removeCluster(clusterData.mapId, layerId);
        }
    };
}

/**
 * Gets cluster instance by ID (for debugging)
 */
export function getCluster(layerId) {
    return clusters.get(layerId);
}

/**
 * Gets all active clusters for a map
 */
export function getMapClusters(mapId) {
    return Array.from(clusters.values()).filter(c => c.mapId === mapId);
}
