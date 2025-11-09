/**
 * Honua Geometry 3D
 *
 * Utilities for parsing, processing, and validating 3D GeoJSON geometries.
 * Handles Z coordinate extraction, dimension detection, and coordinate transformations.
 *
 * @module HonuaGeometry3D
 */

window.HonuaGeometry3D = {
    /**
     * Parse GeoJSON with 3D coordinates and extract Z information.
     *
     * @param {object} geojson - GeoJSON FeatureCollection or Feature
     * @returns {object} Parsed features with Z coordinate metadata
     *
     * @example
     * const geojson = {
     *   type: 'FeatureCollection',
     *   features: [{
     *     type: 'Feature',
     *     geometry: {
     *       type: 'Point',
     *       coordinates: [-122.4194, 37.7749, 50.0]
     *     }
     *   }]
     * };
     * const parsed = HonuaGeometry3D.parse3DGeoJSON(geojson);
     */
    parse3DGeoJSON(geojson) {
        const features = geojson.features || [geojson];
        const parsed = [];
        let total3D = 0;
        let total2D = 0;
        let globalZMin = Infinity;
        let globalZMax = -Infinity;

        for (const feature of features) {
            const geometry = feature.geometry;
            if (!geometry || !geometry.coordinates) {
                continue;
            }

            const dimension = this._detectDimension(geometry.coordinates);
            const hasZ = dimension >= 3;

            if (hasZ) {
                const { min, max } = this._getZRange(geometry.coordinates);
                globalZMin = Math.min(globalZMin, min);
                globalZMax = Math.max(globalZMax, max);
                total3D++;

                parsed.push({
                    ...feature,
                    properties: {
                        ...feature.properties,
                        _dimension: dimension,
                        _hasZ: true,
                        _zMin: min,
                        _zMax: max,
                        _zRange: max - min
                    }
                });
            } else {
                total2D++;
                parsed.push({
                    ...feature,
                    properties: {
                        ...feature.properties,
                        _dimension: dimension,
                        _hasZ: false
                    }
                });
            }
        }

        return {
            type: 'FeatureCollection',
            features: parsed,
            metadata: {
                total: parsed.length,
                with3D: total3D,
                without3D: total2D,
                zMin: globalZMin === Infinity ? null : globalZMin,
                zMax: globalZMax === -Infinity ? null : globalZMax,
                zRange: globalZMax === -Infinity ? null : globalZMax - globalZMin
            }
        };
    },

    /**
     * Extract Z coordinate from a position array.
     *
     * @param {number[]} position - [lon, lat, z?, m?]
     * @returns {number|null} Z coordinate or null if not present
     */
    getZ(position) {
        return position.length >= 3 ? position[2] : null;
    },

    /**
     * Set Z coordinate on a position array.
     *
     * @param {number[]} position - [lon, lat, z?]
     * @param {number} z - New Z value
     * @returns {number[]} - [lon, lat, z]
     */
    setZ(position, z) {
        if (position.length === 2) {
            return [...position, z];
        } else {
            const updated = [...position];
            updated[2] = z;
            return updated;
        }
    },

    /**
     * Remove Z coordinate from position array (convert 3D to 2D).
     *
     * @param {number[]} position - [lon, lat, z?, m?]
     * @returns {number[]} - [lon, lat]
     */
    removeZ(position) {
        return [position[0], position[1]];
    },

    /**
     * Detect coordinate dimension from nested coordinate array.
     * Navigates to first leaf coordinate to determine dimension.
     *
     * @param {Array} coords - GeoJSON coordinates (can be nested)
     * @returns {number} Dimension (2, 3, or 4)
     * @private
     */
    _detectDimension(coords) {
        // Navigate to first leaf coordinate
        let current = coords;
        while (Array.isArray(current[0])) {
            current = current[0];
        }
        return current.length; // 2, 3, or 4
    },

    /**
     * Get minimum and maximum Z values from coordinate array.
     *
     * @param {Array} coords - GeoJSON coordinates
     * @returns {{min: number, max: number}} Z range
     * @private
     */
    _getZRange(coords) {
        const zValues = [];
        this._collectZValues(coords, zValues);

        return {
            min: Math.min(...zValues),
            max: Math.max(...zValues)
        };
    },

    /**
     * Recursively collect all Z values from nested coordinates.
     *
     * @param {Array} coords - Coordinates to process
     * @param {number[]} result - Array to collect Z values
     * @private
     */
    _collectZValues(coords, result) {
        if (typeof coords[0] === 'number') {
            // Leaf coordinate: [lon, lat, z?]
            if (coords.length >= 3) {
                result.push(coords[2]);
            }
        } else if (Array.isArray(coords[0])) {
            // Nested array: recurse
            for (const item of coords) {
                this._collectZValues(item, result);
            }
        }
    },

    /**
     * Flatten nested coordinate arrays to single array.
     * Useful for efficient GPU processing.
     *
     * @param {Array} coords - Nested coordinates
     * @returns {number[]} Flattened coordinate array
     */
    _flattenCoordinates(coords, result = []) {
        for (const item of coords) {
            if (typeof item === 'number') {
                result.push(item);
            } else if (Array.isArray(item)) {
                this._flattenCoordinates(item, result);
            }
        }
        return result;
    },

    /**
     * Validate Z coordinate range.
     * Checks if elevations are within reasonable bounds.
     *
     * @param {number} z - Z coordinate to validate
     * @param {number} minElevation - Minimum valid elevation (default: -500m)
     * @param {number} maxElevation - Maximum valid elevation (default: 9000m)
     * @returns {boolean} True if valid
     */
    validateZ(z, minElevation = -500, maxElevation = 9000) {
        return z >= minElevation && z <= maxElevation;
    },

    /**
     * Convert coordinates from one dimension to another.
     *
     * @param {Array} coords - Source coordinates
     * @param {number} targetDimension - Target dimension (2 or 3)
     * @param {number} defaultZ - Default Z value for 2D->3D conversion
     * @returns {Array} Converted coordinates
     */
    convertDimension(coords, targetDimension, defaultZ = 0) {
        const sourceDim = this._detectDimension(coords);

        if (sourceDim === targetDimension) {
            return coords; // No conversion needed
        }

        if (targetDimension === 2) {
            // 3D to 2D: remove Z
            return this._mapCoordinates(coords, coord => this.removeZ(coord));
        } else if (targetDimension === 3) {
            // 2D to 3D: add Z
            return this._mapCoordinates(coords, coord => this.setZ(coord, defaultZ));
        }

        return coords;
    },

    /**
     * Map function over all coordinates in a geometry.
     * Handles nested coordinate structures (Point, LineString, Polygon, etc.)
     *
     * @param {Array} coords - Coordinates to map
     * @param {Function} fn - Mapping function
     * @returns {Array} Mapped coordinates
     * @private
     */
    _mapCoordinates(coords, fn) {
        if (typeof coords[0] === 'number') {
            // Leaf coordinate
            return fn(coords);
        } else if (Array.isArray(coords[0])) {
            // Nested array
            return coords.map(item => this._mapCoordinates(item, fn));
        }
        return coords;
    },

    /**
     * Calculate statistics for Z coordinates in a geometry.
     *
     * @param {object} geometry - GeoJSON geometry
     * @returns {object|null} Statistics or null if no Z coordinates
     */
    getZStatistics(geometry) {
        const dimension = this._detectDimension(geometry.coordinates);
        if (dimension < 3) {
            return null;
        }

        const zValues = [];
        this._collectZValues(geometry.coordinates, zValues);

        if (zValues.length === 0) {
            return null;
        }

        const sorted = [...zValues].sort((a, b) => a - b);
        const sum = zValues.reduce((a, b) => a + b, 0);

        return {
            min: sorted[0],
            max: sorted[sorted.length - 1],
            mean: sum / zValues.length,
            median: sorted[Math.floor(sorted.length / 2)],
            range: sorted[sorted.length - 1] - sorted[0],
            count: zValues.length,
            stdDev: this._calculateStdDev(zValues, sum / zValues.length)
        };
    },

    /**
     * Calculate standard deviation of Z values.
     *
     * @param {number[]} values - Z values
     * @param {number} mean - Mean value
     * @returns {number} Standard deviation
     * @private
     */
    _calculateStdDev(values, mean) {
        const squareDiffs = values.map(value => Math.pow(value - mean, 2));
        const avgSquareDiff = squareDiffs.reduce((a, b) => a + b, 0) / values.length;
        return Math.sqrt(avgSquareDiff);
    },

    /**
     * Check if a geometry has valid 3D coordinates.
     *
     * @param {object} geometry - GeoJSON geometry
     * @returns {boolean} True if geometry has valid Z coordinates
     */
    isValid3D(geometry) {
        if (!geometry || !geometry.coordinates) {
            return false;
        }

        const dimension = this._detectDimension(geometry.coordinates);
        if (dimension < 3) {
            return false;
        }

        const zValues = [];
        this._collectZValues(geometry.coordinates, zValues);

        // Check if all Z values are valid numbers
        return zValues.every(z => typeof z === 'number' && !isNaN(z) && isFinite(z));
    },

    /**
     * Get geometry type name with dimension suffix (OGC standard).
     *
     * @param {object} geometry - GeoJSON geometry
     * @returns {string} Type name (e.g., "PointZ", "LineStringZ")
     */
    getOgcTypeName(geometry) {
        const type = geometry.type;
        const dimension = this._detectDimension(geometry.coordinates);

        if (dimension === 3) {
            return `${type}Z`;
        } else if (dimension === 4) {
            return `${type}ZM`;
        }
        return type;
    }
};

// Export for Node.js environments (testing)
if (typeof module !== 'undefined' && module.exports) {
    module.exports = window.HonuaGeometry3D;
}
