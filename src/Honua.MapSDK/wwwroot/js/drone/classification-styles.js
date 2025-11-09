/**
 * LAS Classification Styles and Utilities
 * Standard ASPRS classification codes and color schemes
 */

/**
 * ASPRS Standard LAS Classification Codes
 */
export const LAS_CLASSIFICATIONS = {
    NEVER_CLASSIFIED: 0,
    UNCLASSIFIED: 1,
    GROUND: 2,
    LOW_VEGETATION: 3,
    MEDIUM_VEGETATION: 4,
    HIGH_VEGETATION: 5,
    BUILDING: 6,
    LOW_POINT: 7,
    RESERVED_8: 8,
    WATER: 9,
    RAIL: 10,
    ROAD_SURFACE: 11,
    RESERVED_12: 12,
    WIRE_GUARD: 13,
    WIRE_CONDUCTOR: 14,
    TRANSMISSION_TOWER: 15,
    WIRE_CONNECTOR: 16,
    BRIDGE_DECK: 17,
    HIGH_NOISE: 18
};

/**
 * Classification names
 */
export const CLASSIFICATION_NAMES = {
    0: 'Never Classified',
    1: 'Unclassified',
    2: 'Ground',
    3: 'Low Vegetation',
    4: 'Medium Vegetation',
    5: 'High Vegetation',
    6: 'Building',
    7: 'Low Point (Noise)',
    8: 'Reserved',
    9: 'Water',
    10: 'Rail',
    11: 'Road Surface',
    12: 'Reserved',
    13: 'Wire - Guard (Shield)',
    14: 'Wire - Conductor (Phase)',
    15: 'Transmission Tower',
    16: 'Wire-Structure Connector',
    17: 'Bridge Deck',
    18: 'High Noise'
};

/**
 * Standard classification color scheme
 */
export const CLASSIFICATION_COLORS = {
    0: [128, 128, 128],    // Never Classified - Gray
    1: [128, 128, 128],    // Unclassified - Gray
    2: [139, 69, 19],      // Ground - Brown
    3: [34, 139, 34],      // Low Vegetation - Green
    4: [0, 128, 0],        // Medium Vegetation - Dark Green
    5: [0, 255, 0],        // High Vegetation - Bright Green
    6: [255, 0, 0],        // Building - Red
    7: [255, 255, 0],      // Low Point (Noise) - Yellow
    8: [128, 128, 128],    // Reserved - Gray
    9: [0, 0, 255],        // Water - Blue
    10: [128, 0, 128],     // Rail - Purple
    11: [64, 64, 64],      // Road Surface - Dark Gray
    12: [128, 128, 128],   // Reserved - Gray
    13: [255, 165, 0],     // Wire Guard - Orange
    14: [255, 140, 0],     // Wire Conductor - Dark Orange
    15: [255, 20, 147],    // Transmission Tower - Deep Pink
    16: [255, 105, 180],   // Wire Connector - Hot Pink
    17: [255, 255, 0],     // Bridge Deck - Yellow
    18: [255, 0, 255]      // High Noise - Magenta
};

/**
 * Alternative color schemes
 */
export const COLOR_SCHEMES = {
    // Standard ASPRS colors
    standard: CLASSIFICATION_COLORS,

    // High contrast colors
    contrast: {
        0: [128, 128, 128],
        1: [200, 200, 200],
        2: [101, 67, 33],
        3: [60, 179, 113],
        4: [34, 139, 34],
        5: [0, 255, 127],
        6: [220, 20, 60],
        7: [255, 215, 0],
        9: [30, 144, 255],
        10: [138, 43, 226],
        11: [105, 105, 105],
        17: [255, 255, 102],
        18: [255, 0, 255]
    },

    // Natural colors (for vegetation and terrain)
    natural: {
        0: [160, 160, 160],
        1: [180, 180, 180],
        2: [139, 90, 43],      // Soil brown
        3: [107, 142, 35],     // Olive green
        4: [34, 139, 34],      // Forest green
        5: [50, 205, 50],      // Lime green
        6: [178, 34, 34],      // Fire brick red
        7: [255, 255, 153],    // Light yellow
        9: [65, 105, 225],     // Royal blue
        11: [105, 105, 105],   // Dim gray
        17: [255, 228, 181]    // Moccasin
    },

    // Grayscale by height (useful for elevation visualization)
    grayscale: null  // Special case, handled by elevation coloring
};

/**
 * Get color for a classification code
 */
export function getClassificationColor(classification, scheme = 'standard') {
    const colors = COLOR_SCHEMES[scheme] || CLASSIFICATION_COLORS;
    return colors[classification] || [200, 200, 200];
}

/**
 * Get classification name
 */
export function getClassificationName(classification) {
    return CLASSIFICATION_NAMES[classification] || `Custom (${classification})`;
}

/**
 * Get all available classifications in a point cloud
 */
export function extractAvailableClassifications(points) {
    const classifications = new Set();

    points.forEach(point => {
        if (point.classification !== undefined) {
            classifications.add(point.classification);
        }
    });

    return Array.from(classifications).sort((a, b) => a - b);
}

/**
 * Create classification legend
 */
export function createClassificationLegend(classifications, scheme = 'standard') {
    return classifications.map(code => ({
        code,
        name: getClassificationName(code),
        color: getClassificationColor(code, scheme)
    }));
}

/**
 * Filter configuration for common use cases
 */
export const CLASSIFICATION_FILTERS = {
    // Ground and terrain
    terrain: [2],

    // All vegetation
    vegetation: [3, 4, 5],

    // Buildings only
    buildings: [6],

    // Water features
    water: [9],

    // Infrastructure
    infrastructure: [6, 10, 11, 17],

    // Noise points
    noise: [7, 18],

    // Overhead features (wires, towers)
    overhead: [13, 14, 15, 16]
};

/**
 * Get filter preset
 */
export function getFilterPreset(presetName) {
    return CLASSIFICATION_FILTERS[presetName] || [];
}

/**
 * Elevation-based color ramp
 */
export function getElevationColor(z, minZ = -100, maxZ = 100) {
    // Normalize elevation to 0-1
    const normalized = Math.max(0, Math.min(1, (z - minZ) / (maxZ - minZ)));

    // Color ramp: blue (low) -> cyan -> green -> yellow -> red (high)
    if (normalized < 0.25) {
        const t = normalized * 4;
        return [0, t * 255, 255];  // Blue to cyan
    } else if (normalized < 0.5) {
        const t = (normalized - 0.25) * 4;
        return [0, 255, (1 - t) * 255];  // Cyan to green
    } else if (normalized < 0.75) {
        const t = (normalized - 0.5) * 4;
        return [t * 255, 255, 0];  // Green to yellow
    } else {
        const t = (normalized - 0.75) * 4;
        return [255, (1 - t) * 255, 0];  // Yellow to red
    }
}

/**
 * Intensity-based grayscale
 */
export function getIntensityColor(intensity, maxIntensity = 65535) {
    const normalized = Math.max(0, Math.min(1, intensity / maxIntensity));
    const value = normalized * 255;
    return [value, value, value];
}

/**
 * Export for window global access
 */
if (typeof window !== 'undefined') {
    window.LasClassifications = {
        CLASSIFICATIONS: LAS_CLASSIFICATIONS,
        NAMES: CLASSIFICATION_NAMES,
        COLORS: CLASSIFICATION_COLORS,
        COLOR_SCHEMES,
        getClassificationColor,
        getClassificationName,
        extractAvailableClassifications,
        createClassificationLegend,
        getFilterPreset,
        getElevationColor,
        getIntensityColor
    };
}
