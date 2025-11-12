/**
 * Honua Sky Layer Manager JavaScript Module
 * Handles MapLibre GL JS sky layer and atmospheric effects
 * Provides day/night cycle, sun position, and dynamic sky colors
 */

let mapInstance = null;
let dotNetRef = null;
let currentSkyConfig = null;
let animationFrameId = null;
let transitionProgress = 0;
let isTransitioning = false;
let targetSkyConfig = null;
let startSkyConfig = null;

/**
 * Initialize sky layer functionality
 * @param {object} map - MapLibre GL JS map instance
 * @param {object} dotNetReference - .NET object reference for callbacks
 * @returns {boolean} Success status
 */
export function initializeSkyLayer(map, dotNetReference) {
    try {
        if (!map) {
            console.error('Map instance is required');
            return false;
        }

        mapInstance = map;
        dotNetRef = dotNetReference;

        console.log('Sky layer manager initialized');
        return true;
    } catch (error) {
        console.error('Error initializing sky layer:', error);
        return false;
    }
}

/**
 * Check if sky layer is supported by the current MapLibre version
 * @returns {boolean} True if sky layer is supported
 */
export function isSkyLayerSupported() {
    try {
        // Sky layer requires MapLibre GL JS v3.0+
        if (typeof maplibregl !== 'undefined' && maplibregl.version) {
            const version = maplibregl.version;
            const majorVersion = parseInt(version.split('.')[0]);
            return majorVersion >= 3;
        }
        return true; // Assume supported if version can't be detected
    } catch (error) {
        console.error('Error checking sky layer support:', error);
        return false;
    }
}

/**
 * Update sky layer configuration
 * @param {object} config - Sky configuration object
 * @param {number} transitionDuration - Transition duration in milliseconds
 * @returns {Promise<boolean>} Success status
 */
export async function updateSkyLayer(config, transitionDuration = 1000) {
    if (!mapInstance) {
        console.error('Map instance not initialized');
        return false;
    }

    try {
        // If transitioning, set target config for smooth transition
        if (transitionDuration > 0) {
            startSkyConfig = currentSkyConfig || getDefaultSkyConfig();
            targetSkyConfig = config;
            transitionProgress = 0;
            isTransitioning = true;

            // Start transition animation
            await animateTransition(transitionDuration);
        } else {
            // Apply immediately without transition
            applyImmediateSkyConfig(config);
        }

        currentSkyConfig = config;
        return true;
    } catch (error) {
        console.error('Error updating sky layer:', error);
        return false;
    }
}

/**
 * Apply sky configuration immediately without transition
 * @param {object} config - Sky configuration
 */
function applyImmediateSkyConfig(config) {
    if (!mapInstance) return;

    try {
        const style = mapInstance.getStyle();
        if (!style) return;

        // Configure sky layer based on type
        let skyLayer = {
            'id': 'sky',
            'type': 'sky',
            'paint': {}
        };

        switch (config.skyType?.toLowerCase()) {
            case 'gradient':
                skyLayer.paint = {
                    'sky-type': 'gradient',
                    'sky-gradient': [
                        'interpolate',
                        ['linear'],
                        ['sky-radial-progress'],
                        0.0, config.horizonColor || '#FFA07A',
                        config.horizonBlend || 0.1, config.skyColor || '#87CEEB',
                        1.0, config.skyColor || '#87CEEB'
                    ],
                    'sky-gradient-center': [0, 0],
                    'sky-gradient-radius': 90,
                    'sky-opacity': config.atmosphereIntensity || 1.0
                };
                break;

            case 'atmosphere':
                skyLayer.paint = {
                    'sky-type': 'atmosphere',
                    'sky-atmosphere-sun': [config.sunPosition?.x || 0, config.sunPosition?.y || 45],
                    'sky-atmosphere-sun-intensity': config.atmosphereIntensity || 1.0,
                    'sky-atmosphere-color': config.atmosphereColor || '#87CEEB',
                    'sky-atmosphere-halo-color': config.horizonColor || '#FFA07A',
                    'sky-opacity': 1.0
                };
                break;

            case 'solid':
                skyLayer.paint = {
                    'sky-type': 'gradient',
                    'sky-gradient': [
                        'interpolate',
                        ['linear'],
                        ['sky-radial-progress'],
                        0.0, config.skyColor || '#87CEEB',
                        1.0, config.skyColor || '#87CEEB'
                    ],
                    'sky-gradient-center': [0, 0],
                    'sky-gradient-radius': 90,
                    'sky-opacity': config.atmosphereIntensity || 1.0
                };
                break;

            default: // custom
                skyLayer.paint = {
                    'sky-type': 'atmosphere',
                    'sky-atmosphere-sun': [config.sunPosition?.x || 0, config.sunPosition?.y || 45],
                    'sky-atmosphere-sun-intensity': config.atmosphereIntensity || 1.0,
                    'sky-opacity': 1.0
                };
                break;
        }

        // Remove existing sky layer if present
        if (style.layers && style.layers.some(layer => layer.id === 'sky')) {
            mapInstance.removeLayer('sky');
        }

        // Add sky layer
        mapInstance.addLayer(skyLayer);

        console.log('Sky layer applied:', config.skyType);
    } catch (error) {
        console.error('Error applying sky configuration:', error);
    }
}

/**
 * Animate transition between sky configurations
 * @param {number} duration - Transition duration in milliseconds
 * @returns {Promise<void>}
 */
function animateTransition(duration) {
    return new Promise((resolve) => {
        const startTime = performance.now();

        function animate(currentTime) {
            if (!isTransitioning) {
                resolve();
                return;
            }

            const elapsed = currentTime - startTime;
            transitionProgress = Math.min(elapsed / duration, 1.0);

            // Apply easing function (ease-in-out)
            const easedProgress = easeInOutCubic(transitionProgress);

            // Interpolate between start and target configs
            const interpolatedConfig = interpolateSkyConfigs(startSkyConfig, targetSkyConfig, easedProgress);
            applyImmediateSkyConfig(interpolatedConfig);

            if (transitionProgress < 1.0) {
                animationFrameId = requestAnimationFrame(animate);
            } else {
                isTransitioning = false;
                resolve();
            }
        }

        animationFrameId = requestAnimationFrame(animate);
    });
}

/**
 * Interpolate between two sky configurations
 * @param {object} start - Start configuration
 * @param {object} target - Target configuration
 * @param {number} progress - Progress (0 to 1)
 * @returns {object} Interpolated configuration
 */
function interpolateSkyConfigs(start, target, progress) {
    return {
        skyType: target.skyType,
        skyColor: interpolateColor(start.skyColor, target.skyColor, progress),
        horizonColor: interpolateColor(start.horizonColor, target.horizonColor, progress),
        horizonBlend: lerp(start.horizonBlend || 0.1, target.horizonBlend || 0.1, progress),
        enableAtmosphere: target.enableAtmosphere,
        atmosphereIntensity: lerp(start.atmosphereIntensity || 1.0, target.atmosphereIntensity || 1.0, progress),
        atmosphereColor: interpolateColor(start.atmosphereColor, target.atmosphereColor, progress),
        enableStars: target.enableStars,
        sunPosition: {
            x: lerp(start.sunPosition?.x || 0, target.sunPosition?.x || 0, progress),
            y: lerp(start.sunPosition?.y || 45, target.sunPosition?.y || 45, progress)
        }
    };
}

/**
 * Update sun position
 * @param {number} azimuth - Sun azimuth in degrees (0-360)
 * @param {number} altitude - Sun altitude in degrees (-90 to 90)
 * @param {number} transitionDuration - Transition duration in milliseconds
 * @returns {Promise<boolean>} Success status
 */
export async function updateSunPosition(azimuth, altitude, transitionDuration = 1000) {
    if (!mapInstance) {
        console.error('Map instance not initialized');
        return false;
    }

    try {
        const sunPosition = { x: azimuth, y: altitude };

        if (currentSkyConfig) {
            // Update current config with new sun position
            const newConfig = { ...currentSkyConfig, sunPosition };
            await updateSkyLayer(newConfig, transitionDuration);
        } else {
            // Create default atmosphere config with sun position
            const defaultConfig = {
                ...getDefaultSkyConfig(),
                sunPosition
            };
            await updateSkyLayer(defaultConfig, transitionDuration);
        }

        // Notify .NET component
        if (dotNetRef) {
            try {
                await dotNetRef.invokeMethodAsync('OnSunPositionUpdated', azimuth, altitude);
            } catch (error) {
                console.error('Error notifying .NET about sun position update:', error);
            }
        }

        return true;
    } catch (error) {
        console.error('Error updating sun position:', error);
        return false;
    }
}

/**
 * Get current sun position from sky layer
 * @returns {object} Current sun position {azimuth, altitude}
 */
export function getCurrentSunPosition() {
    if (!currentSkyConfig || !currentSkyConfig.sunPosition) {
        return { azimuth: 180, altitude: 45 };
    }
    return {
        azimuth: currentSkyConfig.sunPosition.x,
        altitude: currentSkyConfig.sunPosition.y
    };
}

/**
 * Apply sky preset by name
 * @param {string} presetName - Name of the preset
 * @param {number} transitionDuration - Transition duration in milliseconds
 * @returns {Promise<boolean>} Success status
 */
export async function applySkyPreset(presetName, transitionDuration = 1000) {
    // Presets are defined in C# and passed to JS
    console.log('Apply sky preset:', presetName);
    return true;
}

/**
 * Enable or disable sky layer
 * @param {boolean} enabled - Whether to enable sky layer
 * @returns {boolean} Success status
 */
export function setSkyEnabled(enabled) {
    if (!mapInstance) {
        console.error('Map instance not initialized');
        return false;
    }

    try {
        const style = mapInstance.getStyle();
        if (!style) return false;

        if (enabled) {
            // Add sky layer if not present
            if (!style.layers || !style.layers.some(layer => layer.id === 'sky')) {
                const defaultConfig = getDefaultSkyConfig();
                applyImmediateSkyConfig(defaultConfig);
                currentSkyConfig = defaultConfig;
            }
        } else {
            // Remove sky layer
            if (style.layers && style.layers.some(layer => layer.id === 'sky')) {
                mapInstance.removeLayer('sky');
            }
        }

        return true;
    } catch (error) {
        console.error('Error setting sky enabled:', error);
        return false;
    }
}

/**
 * Get default sky configuration
 * @returns {object} Default sky configuration
 */
function getDefaultSkyConfig() {
    return {
        skyType: 'atmosphere',
        skyColor: '#87CEEB',
        horizonColor: '#B0E0E6',
        horizonBlend: 0.1,
        enableAtmosphere: true,
        atmosphereIntensity: 1.0,
        atmosphereColor: '#87CEEB',
        enableStars: false,
        sunPosition: { x: 180, y: 45 }
    };
}

/**
 * Linear interpolation
 * @param {number} a - Start value
 * @param {number} b - End value
 * @param {number} t - Progress (0 to 1)
 * @returns {number} Interpolated value
 */
function lerp(a, b, t) {
    return a + (b - a) * t;
}

/**
 * Interpolate between two colors
 * @param {string} color1 - Start color (hex)
 * @param {string} color2 - End color (hex)
 * @param {number} t - Progress (0 to 1)
 * @returns {string} Interpolated color (hex)
 */
function interpolateColor(color1, color2, t) {
    if (!color1 || !color2) return color2 || color1 || '#87CEEB';

    const rgb1 = parseHexColor(color1);
    const rgb2 = parseHexColor(color2);

    const r = Math.round(lerp(rgb1.r, rgb2.r, t));
    const g = Math.round(lerp(rgb1.g, rgb2.g, t));
    const b = Math.round(lerp(rgb1.b, rgb2.b, t));

    return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
}

/**
 * Parse hex color to RGB
 * @param {string} hex - Hex color string
 * @returns {object} RGB components {r, g, b}
 */
function parseHexColor(hex) {
    hex = hex.replace('#', '');
    if (hex.length === 3) {
        hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
    }
    return {
        r: parseInt(hex.substring(0, 2), 16),
        g: parseInt(hex.substring(2, 4), 16),
        b: parseInt(hex.substring(4, 6), 16)
    };
}

/**
 * Ease-in-out cubic easing function
 * @param {number} t - Progress (0 to 1)
 * @returns {number} Eased progress
 */
function easeInOutCubic(t) {
    return t < 0.5
        ? 4 * t * t * t
        : 1 - Math.pow(-2 * t + 2, 3) / 2;
}

/**
 * Get current sky configuration
 * @returns {object} Current sky configuration
 */
export function getCurrentSkyConfig() {
    return currentSkyConfig || getDefaultSkyConfig();
}

/**
 * Check if sky layer is currently active
 * @returns {boolean} True if sky layer is active
 */
export function isSkyLayerActive() {
    if (!mapInstance) return false;

    try {
        const style = mapInstance.getStyle();
        return style && style.layers && style.layers.some(layer => layer.id === 'sky');
    } catch (error) {
        console.error('Error checking sky layer status:', error);
        return false;
    }
}

/**
 * Get sky layer capabilities
 * @returns {object} Sky layer capabilities
 */
export function getSkyLayerCapabilities() {
    return {
        supported: isSkyLayerSupported(),
        active: isSkyLayerActive(),
        currentConfig: getCurrentSkyConfig(),
        sunPosition: getCurrentSunPosition(),
        isTransitioning: isTransitioning
    };
}

/**
 * Calculate sky color based on sun altitude (automatic mode)
 * @param {number} altitude - Sun altitude in degrees
 * @returns {string} Sky color (hex)
 */
export function calculateSkyColorFromAltitude(altitude) {
    // Night
    if (altitude < -18) return '#0B1026';
    // Astronomical twilight
    if (altitude < -12) return '#1B2A49';
    // Nautical twilight
    if (altitude < -6) return '#2B3A69';
    // Civil twilight
    if (altitude < 0) return '#4B5A89';
    // Sunrise/sunset
    if (altitude < 6) {
        const t = altitude / 6.0;
        return interpolateColor('#FF8C00', '#87CEEB', t);
    }
    // Day
    return '#87CEEB';
}

/**
 * Cleanup and remove event listeners
 */
export function cleanup() {
    try {
        // Cancel any ongoing animation
        if (animationFrameId) {
            cancelAnimationFrame(animationFrameId);
            animationFrameId = null;
        }

        isTransitioning = false;
        transitionProgress = 0;
        currentSkyConfig = null;
        startSkyConfig = null;
        targetSkyConfig = null;
        mapInstance = null;
        dotNetRef = null;

        console.log('Sky layer manager cleaned up');
    } catch (error) {
        console.error('Error cleaning up sky layer manager:', error);
    }
}
