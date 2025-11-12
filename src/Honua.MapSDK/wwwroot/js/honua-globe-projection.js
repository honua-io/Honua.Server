/**
 * Honua Globe Projection JavaScript Module
 * Handles MapLibre GL JS globe projection (v5.0+) with smooth transitions
 */

let mapInstance = null;
let dotNetRef = null;
let currentProjection = 'mercator';
let isTransitioning = false;

/**
 * Initialize globe projection functionality
 * @param {object} map - MapLibre GL JS map instance
 * @param {object} dotNetReference - .NET object reference for callbacks
 * @returns {boolean} Success status
 */
export function initializeGlobeProjection(map, dotNetReference) {
    try {
        if (!map) {
            console.error('Map instance is required');
            return false;
        }

        mapInstance = map;
        dotNetRef = dotNetReference;

        // Get current projection
        const style = map.getStyle();
        currentProjection = style?.projection?.type || 'mercator';

        console.log('Globe projection initialized. Current projection:', currentProjection);
        return true;
    } catch (error) {
        console.error('Error initializing globe projection:', error);
        return false;
    }
}

/**
 * Check if globe projection is supported
 * @returns {boolean} True if globe projection is supported
 */
export function isGlobeSupported() {
    try {
        // Check if MapLibre GL JS version supports globe projection
        // Globe projection was added in MapLibre GL JS v5.0
        if (typeof maplibregl !== 'undefined' && maplibregl.version) {
            const version = maplibregl.version;
            const majorVersion = parseInt(version.split('.')[0]);
            return majorVersion >= 5;
        }

        // If we can't detect version, assume it might be supported
        // and let the setProjection call handle any errors
        console.warn('MapLibre GL JS version could not be detected');
        return true;
    } catch (error) {
        console.error('Error checking globe support:', error);
        return false;
    }
}

/**
 * Get current projection type
 * @returns {string} Current projection type
 */
export function getCurrentProjection() {
    if (!mapInstance) {
        console.error('Map instance not initialized');
        return 'mercator';
    }

    try {
        const style = mapInstance.getStyle();
        return style?.projection?.type || 'mercator';
    } catch (error) {
        console.error('Error getting current projection:', error);
        return currentProjection;
    }
}

/**
 * Set map projection with smooth transition
 * @param {string} projectionType - Projection type ('mercator' or 'globe')
 * @param {object} options - Projection options
 * @returns {Promise<boolean>} Success status
 */
export async function setProjection(projectionType, options = {}) {
    if (!mapInstance) {
        console.error('Map instance not initialized');
        return false;
    }

    if (isTransitioning) {
        console.warn('Projection transition already in progress');
        return false;
    }

    try {
        isTransitioning = true;

        // Normalize projection type
        const projection = projectionType.toLowerCase();

        if (!['mercator', 'globe'].includes(projection)) {
            console.error('Invalid projection type:', projection);
            isTransitioning = false;
            return false;
        }

        // Check if already in target projection
        if (projection === currentProjection) {
            console.log('Already in', projection, 'projection');
            isTransitioning = false;
            return true;
        }

        // Build projection configuration
        const projectionConfig = {
            type: projection
        };

        // Add globe-specific options
        if (projection === 'globe') {
            projectionConfig.atmosphere = options.enableAtmosphere !== false;
            projectionConfig.atmosphereColor = options.atmosphereColor || '#87CEEB';
            projectionConfig.space = options.enableSpace !== false;
        }

        // Store old zoom for potential camera adjustment
        const oldZoom = mapInstance.getZoom();
        const oldCenter = mapInstance.getCenter();

        // Set projection
        mapInstance.setProjection(projectionConfig);

        // Auto-adjust camera when switching to globe
        if (projection === 'globe' && options.autoAdjustCamera !== false) {
            const targetZoom = options.globeDefaultZoom !== undefined ? options.globeDefaultZoom : 1.5;

            // If current zoom is too high, zoom out smoothly
            if (oldZoom > targetZoom) {
                await new Promise((resolve) => {
                    mapInstance.once('moveend', resolve);
                    mapInstance.easeTo({
                        zoom: targetZoom,
                        duration: options.transitionDuration || 1000,
                        easing: (t) => t * (2 - t) // ease-out
                    });
                });
            }
        }

        // Update current projection
        currentProjection = projection;

        // Notify .NET component
        if (dotNetRef) {
            try {
                await dotNetRef.invokeMethodAsync('OnProjectionChanged', projection);
            } catch (error) {
                console.error('Error notifying .NET about projection change:', error);
            }
        }

        console.log('Projection changed to:', projection);

        // Wait a bit for the transition to complete
        await new Promise(resolve => setTimeout(resolve, 100));

        isTransitioning = false;
        return true;
    } catch (error) {
        console.error('Error setting projection:', error);
        isTransitioning = false;
        return false;
    }
}

/**
 * Toggle between mercator and globe projections
 * @param {object} options - Projection options
 * @returns {Promise<boolean>} Success status
 */
export async function toggleProjection(options = {}) {
    const current = getCurrentProjection();
    const target = current === 'globe' ? 'mercator' : 'globe';
    return await setProjection(target, options);
}

/**
 * Set to globe projection
 * @param {object} options - Globe projection options
 * @returns {Promise<boolean>} Success status
 */
export async function setGlobeProjection(options = {}) {
    return await setProjection('globe', options);
}

/**
 * Set to mercator projection
 * @param {object} options - Projection options
 * @returns {Promise<boolean>} Success status
 */
export async function setMercatorProjection(options = {}) {
    return await setProjection('mercator', options);
}

/**
 * Update atmosphere settings (for globe projection)
 * @param {object} atmosphereOptions - Atmosphere configuration
 * @returns {boolean} Success status
 */
export function updateAtmosphere(atmosphereOptions = {}) {
    if (!mapInstance) {
        console.error('Map instance not initialized');
        return false;
    }

    try {
        const current = getCurrentProjection();

        if (current !== 'globe') {
            console.warn('Atmosphere can only be configured in globe projection');
            return false;
        }

        // Re-apply globe projection with new atmosphere settings
        const projectionConfig = {
            type: 'globe',
            atmosphere: atmosphereOptions.enabled !== false,
            atmosphereColor: atmosphereOptions.color || '#87CEEB',
            space: atmosphereOptions.space !== false
        };

        mapInstance.setProjection(projectionConfig);
        console.log('Atmosphere settings updated');
        return true;
    } catch (error) {
        console.error('Error updating atmosphere:', error);
        return false;
    }
}

/**
 * Get projection capabilities
 * @returns {object} Projection capabilities
 */
export function getProjectionCapabilities() {
    return {
        globeSupported: isGlobeSupported(),
        currentProjection: getCurrentProjection(),
        availableProjections: ['mercator', 'globe'],
        isTransitioning: isTransitioning
    };
}

/**
 * Check if currently in globe projection
 * @returns {boolean} True if in globe projection
 */
export function isGlobeProjection() {
    return getCurrentProjection() === 'globe';
}

/**
 * Check if currently in mercator projection
 * @returns {boolean} True if in mercator projection
 */
export function isMercatorProjection() {
    return getCurrentProjection() === 'mercator';
}

/**
 * Get recommended zoom level for globe view
 * @returns {number} Recommended zoom level
 */
export function getRecommendedGlobeZoom() {
    return 1.5; // Good default for seeing the whole earth
}

/**
 * Cleanup and remove event listeners
 */
export function cleanup() {
    try {
        mapInstance = null;
        dotNetRef = null;
        currentProjection = 'mercator';
        isTransitioning = false;
        console.log('Globe projection module cleaned up');
    } catch (error) {
        console.error('Error cleaning up globe projection module:', error);
    }
}
