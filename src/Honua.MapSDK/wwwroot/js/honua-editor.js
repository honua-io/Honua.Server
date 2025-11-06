// HonuaEditor - Feature editing with full CRUD operations
// Integrates with MapLibre GL JS Draw plugin

import MapboxDraw from 'https://cdn.jsdelivr.net/npm/@mapbox/mapbox-gl-draw@1.4.3/+esm';

const editorInstances = new Map();

/**
 * Initialize the editor for a map
 */
export function initializeEditor(mapId, componentId, config, dotNetRef) {
    const mapInstance = window.mapInstances?.get(mapId);
    if (!mapInstance) {
        console.error(`Map ${mapId} not found`);
        return;
    }

    // Create Draw instance
    const draw = new MapboxDraw({
        displayControlsDefault: false,
        controls: {},
        styles: getEditorStyles(),
        modes: {
            ...MapboxDraw.modes,
            direct_select: DirectSelectMode,
            simple_select: SimpleSelectMode
        },
        userProperties: true
    });

    // Add to map
    mapInstance.addControl(draw, 'top-left');

    // Store instance
    editorInstances.set(componentId, {
        mapId,
        map: mapInstance,
        draw,
        config,
        dotNetRef,
        selectedFeatureId: null,
        editMode: 'none',
        history: [],
        historyIndex: -1
    });

    // Setup event listeners
    setupEditorEvents(componentId);

    console.log(`Editor initialized for map ${mapId}`);
}

/**
 * Start edit mode
 */
export function startEditMode(mapId) {
    const instance = getInstanceByMapId(mapId);
    if (!instance) return;

    // Enable cursor
    instance.map.getCanvas().style.cursor = 'crosshair';

    // Listen for feature clicks
    instance.map.on('click', instance.clickHandler = (e) => {
        const features = instance.map.queryRenderedFeatures(e.point, {
            layers: instance.config.editableLayers
        });

        if (features.length > 0) {
            const feature = features[0];
            selectFeature(mapId, feature.id, feature.layer.id);
        }
    });
}

/**
 * Stop edit mode
 */
export function stopEditMode(mapId) {
    const instance = getInstanceByMapId(mapId);
    if (!instance) return;

    // Reset cursor
    instance.map.getCanvas().style.cursor = '';

    // Remove click handler
    if (instance.clickHandler) {
        instance.map.off('click', instance.clickHandler);
        instance.clickHandler = null;
    }

    // Clear selection
    instance.draw.changeMode('simple_select');
    instance.selectedFeatureId = null;
}

/**
 * Enable draw mode for creating features
 */
export function enableDrawMode(mapId, geometryType) {
    const instance = getInstanceByMapId(mapId);
    if (!instance) return;

    const mode = {
        'point': 'draw_point',
        'line': 'draw_line_string',
        'polygon': 'draw_polygon'
    }[geometryType];

    if (mode) {
        instance.draw.changeMode(mode);
        instance.editMode = 'draw';
    }
}

/**
 * Disable draw mode
 */
export function disableDrawMode(mapId) {
    const instance = getInstanceByMapId(mapId);
    if (!instance) return;

    instance.draw.changeMode('simple_select');
    instance.editMode = 'none';
}

/**
 * Set edit mode (select, edit_vertices, move)
 */
export function setEditMode(mapId, mode, featureId) {
    const instance = getInstanceByMapId(mapId);
    if (!instance) return;

    instance.editMode = mode;

    switch (mode) {
        case 'select':
            instance.draw.changeMode('simple_select');
            break;

        case 'edit_vertices':
            if (featureId) {
                instance.draw.changeMode('direct_select', { featureId });
            }
            break;

        case 'move':
            if (featureId) {
                instance.draw.changeMode('simple_select', { featureIds: [featureId] });
            }
            break;

        default:
            instance.draw.changeMode('simple_select');
    }
}

/**
 * Select a feature
 */
function selectFeature(mapId, featureId, layerId) {
    const instance = getInstanceByMapId(mapId);
    if (!instance) return;

    instance.selectedFeatureId = featureId;

    // Get feature from map
    const features = instance.map.queryRenderedFeatures({
        layers: [layerId],
        filter: ['==', ['id'], featureId]
    });

    if (features.length > 0) {
        const feature = features[0];

        // Add to Draw for editing
        try {
            instance.draw.add({
                type: 'Feature',
                id: featureId,
                geometry: feature.geometry,
                properties: feature.properties || {}
            });

            instance.draw.changeMode('simple_select', { featureIds: [featureId] });
        } catch (e) {
            console.error('Error adding feature to draw:', e);
        }
    }
}

/**
 * Move a feature to new coordinates
 */
export function moveFeature(mapId, featureId, newCoordinates) {
    const instance = getInstanceByMapId(mapId);
    if (!instance) return;

    const feature = instance.draw.get(featureId);
    if (!feature) return;

    // Update coordinates
    feature.geometry.coordinates = newCoordinates;
    instance.draw.add(feature);

    // Notify Blazor
    const geometryJson = JSON.stringify(feature.geometry);
    instance.dotNetRef.invokeMethodAsync('OnGeometryUpdatedFromJS', featureId, geometryJson);

    addToHistory(instance, 'move', feature);
}

/**
 * Update feature geometry
 */
export function updateGeometry(mapId, featureId, geometry) {
    const instance = getInstanceByMapId(mapId);
    if (!instance) return;

    const feature = instance.draw.get(featureId);
    if (!feature) return;

    feature.geometry = geometry;
    instance.draw.add(feature);

    addToHistory(instance, 'reshape', feature);
}

/**
 * Delete a feature
 */
export function deleteFeature(mapId, featureId) {
    const instance = getInstanceByMapId(mapId);
    if (!instance) return;

    const feature = instance.draw.get(featureId);
    if (feature) {
        addToHistory(instance, 'delete', feature);
    }

    instance.draw.delete(featureId);
    instance.selectedFeatureId = null;
}

/**
 * Get edit history
 */
export function getEditHistory(mapId) {
    const instance = getInstanceByMapId(mapId);
    if (!instance) return [];

    return instance.history;
}

/**
 * Undo last operation
 */
export function undo(mapId) {
    const instance = getInstanceByMapId(mapId);
    if (!instance || instance.historyIndex < 0) return;

    const operation = instance.history[instance.historyIndex];

    switch (operation.type) {
        case 'create':
            instance.draw.delete(operation.feature.id);
            break;

        case 'update':
        case 'move':
        case 'reshape':
            if (operation.previousState) {
                instance.draw.add(operation.previousState);
            }
            break;

        case 'delete':
            instance.draw.add(operation.feature);
            break;
    }

    instance.historyIndex--;
}

/**
 * Redo next operation
 */
export function redo(mapId) {
    const instance = getInstanceByMapId(mapId);
    if (!instance || instance.historyIndex >= instance.history.length - 1) return;

    instance.historyIndex++;
    const operation = instance.history[instance.historyIndex];

    switch (operation.type) {
        case 'create':
            instance.draw.add(operation.feature);
            break;

        case 'update':
        case 'move':
        case 'reshape':
            instance.draw.add(operation.feature);
            break;

        case 'delete':
            instance.draw.delete(operation.feature.id);
            break;
    }
}

/**
 * Setup event listeners for the editor
 */
function setupEditorEvents(componentId) {
    const instance = editorInstances.get(componentId);
    if (!instance) return;

    const { map, draw, dotNetRef, config } = instance;

    // Feature created
    map.on('draw.create', (e) => {
        const feature = e.features[0];
        const layerId = config.editableLayers[0] || 'default';

        // Determine layer based on geometry type or use first editable layer
        const featureJson = JSON.stringify(feature);

        dotNetRef.invokeMethodAsync('OnFeatureCreatedFromJS', featureJson, layerId);

        addToHistory(instance, 'create', feature);
    });

    // Feature updated
    map.on('draw.update', (e) => {
        const feature = e.features[0];
        const geometryJson = JSON.stringify(feature.geometry);

        dotNetRef.invokeMethodAsync('OnGeometryUpdatedFromJS', feature.id, geometryJson);

        addToHistory(instance, 'update', feature);
    });

    // Selection changed
    map.on('draw.selectionchange', (e) => {
        if (e.features.length > 0) {
            const feature = e.features[0];
            instance.selectedFeatureId = feature.id;
        } else {
            instance.selectedFeatureId = null;
        }
    });

    // Mode changed
    map.on('draw.modechange', (e) => {
        console.log('Draw mode changed:', e.mode);
    });
}

/**
 * Add operation to history
 */
function addToHistory(instance, type, feature, previousState = null) {
    // Remove forward history
    if (instance.historyIndex < instance.history.length - 1) {
        instance.history = instance.history.slice(0, instance.historyIndex + 1);
    }

    // Add new operation
    instance.history.push({
        type,
        feature: JSON.parse(JSON.stringify(feature)), // Deep clone
        previousState: previousState ? JSON.parse(JSON.stringify(previousState)) : null,
        timestamp: new Date().toISOString()
    });

    instance.historyIndex = instance.history.length - 1;

    // Limit history size
    if (instance.history.length > 100) {
        instance.history.shift();
        instance.historyIndex--;
    }
}

/**
 * Get editor styles for Draw plugin
 */
function getEditorStyles() {
    return [
        // Polygon fill
        {
            'id': 'gl-draw-polygon-fill',
            'type': 'fill',
            'filter': ['all', ['==', '$type', 'Polygon'], ['!=', 'mode', 'static']],
            'paint': {
                'fill-color': '#3B82F6',
                'fill-outline-color': '#3B82F6',
                'fill-opacity': 0.2
            }
        },
        // Polygon outline
        {
            'id': 'gl-draw-polygon-stroke-active',
            'type': 'line',
            'filter': ['all', ['==', '$type', 'Polygon'], ['!=', 'mode', 'static']],
            'layout': {
                'line-cap': 'round',
                'line-join': 'round'
            },
            'paint': {
                'line-color': '#3B82F6',
                'line-width': 2
            }
        },
        // Line string
        {
            'id': 'gl-draw-line',
            'type': 'line',
            'filter': ['all', ['==', '$type', 'LineString'], ['!=', 'mode', 'static']],
            'layout': {
                'line-cap': 'round',
                'line-join': 'round'
            },
            'paint': {
                'line-color': '#3B82F6',
                'line-width': 2
            }
        },
        // Points
        {
            'id': 'gl-draw-point',
            'type': 'circle',
            'filter': ['all', ['==', '$type', 'Point'], ['!=', 'mode', 'static']],
            'paint': {
                'circle-radius': 6,
                'circle-color': '#3B82F6',
                'circle-stroke-width': 2,
                'circle-stroke-color': '#FFFFFF'
            }
        },
        // Vertex points
        {
            'id': 'gl-draw-polygon-and-line-vertex-active',
            'type': 'circle',
            'filter': ['all', ['==', 'meta', 'vertex'], ['==', '$type', 'Point']],
            'paint': {
                'circle-radius': 5,
                'circle-color': '#FFFFFF',
                'circle-stroke-width': 2,
                'circle-stroke-color': '#3B82F6'
            }
        },
        // Midpoints
        {
            'id': 'gl-draw-polygon-and-line-vertex-midpoint',
            'type': 'circle',
            'filter': ['all', ['==', 'meta', 'midpoint'], ['==', '$type', 'Point']],
            'paint': {
                'circle-radius': 4,
                'circle-color': '#FBBF24'
            }
        },
        // Selected/Active
        {
            'id': 'gl-draw-polygon-fill-active',
            'type': 'fill',
            'filter': ['all', ['==', 'active', 'true'], ['==', '$type', 'Polygon']],
            'paint': {
                'fill-color': '#FBBF24',
                'fill-outline-color': '#FBBF24',
                'fill-opacity': 0.3
            }
        }
    ];
}

/**
 * Custom Direct Select mode with enhanced vertex editing
 */
const DirectSelectMode = {
    ...MapboxDraw.modes.direct_select,

    onSetup(opts) {
        const mode = MapboxDraw.modes.direct_select.onSetup.call(this, opts);
        mode.snapEnabled = true;
        mode.snapTolerance = 10;
        return mode;
    },

    onDrag(state, e) {
        // Add snapping logic here if needed
        return MapboxDraw.modes.direct_select.onDrag.call(this, state, e);
    }
};

/**
 * Custom Simple Select mode
 */
const SimpleSelectMode = {
    ...MapboxDraw.modes.simple_select,

    onSetup(opts) {
        return MapboxDraw.modes.simple_select.onSetup.call(this, opts);
    },

    onClick(state, e) {
        const result = MapboxDraw.modes.simple_select.onClick.call(this, state, e);

        // Add custom click handling here
        return result;
    }
};

/**
 * Get instance by component ID
 */
function getInstance(componentId) {
    return editorInstances.get(componentId);
}

/**
 * Get instance by map ID
 */
function getInstanceByMapId(mapId) {
    for (const [, instance] of editorInstances) {
        if (instance.mapId === mapId) {
            return instance;
        }
    }
    return null;
}

/**
 * Cleanup editor instance
 */
export function cleanup(mapId) {
    for (const [componentId, instance] of editorInstances) {
        if (instance.mapId === mapId) {
            // Remove draw control
            if (instance.draw && instance.map) {
                instance.map.removeControl(instance.draw);
            }

            // Remove click handler
            if (instance.clickHandler) {
                instance.map.off('click', instance.clickHandler);
            }

            editorInstances.delete(componentId);
            console.log(`Editor cleaned up for map ${mapId}`);
            break;
        }
    }
}

// Export for debugging
if (typeof window !== 'undefined') {
    window.honuaEditor = {
        editorInstances,
        getInstance,
        getInstanceByMapId
    };
}
