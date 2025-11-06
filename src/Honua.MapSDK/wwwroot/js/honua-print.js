/**
 * HonuaPrint - JavaScript interop for print functionality
 * Handles map screenshot capture, map state retrieval, and file downloads
 */

window.honuaPrint = {
    /**
     * Capture a screenshot of the map
     * @param {string} mapId - Map component ID
     * @returns {Promise<string>} Base64-encoded image data URL
     */
    async captureMap(mapId) {
        const mapElement = this.findMapElement(mapId);
        if (!mapElement) {
            console.error(`Map element not found: ${mapId}`);
            return null;
        }

        try {
            // Get MapLibre map instance
            const map = mapElement.__maplibreMap;
            if (!map) {
                console.error('MapLibre instance not found on map element');
                return null;
            }

            // Wait for map to finish rendering
            await this.waitForMapIdle(map);

            // Get the canvas
            const canvas = map.getCanvas();
            if (!canvas) {
                console.error('Map canvas not found');
                return null;
            }

            // Convert canvas to data URL
            const dataUrl = canvas.toDataURL('image/png');
            return dataUrl;
        } catch (error) {
            console.error('Failed to capture map:', error);
            return null;
        }
    },

    /**
     * Get current map state (center, zoom, bearing, pitch)
     * @param {string} mapId - Map component ID
     * @returns {object} Map state object
     */
    getMapState(mapId) {
        const mapElement = this.findMapElement(mapId);
        if (!mapElement) {
            console.error(`Map element not found: ${mapId}`);
            return null;
        }

        const map = mapElement.__maplibreMap;
        if (!map) {
            console.error('MapLibre instance not found on map element');
            return null;
        }

        const center = map.getCenter();
        const zoom = map.getZoom();
        const bearing = map.getBearing();
        const pitch = map.getPitch();
        const bounds = map.getBounds();

        return {
            center: [center.lng, center.lat],
            zoom: zoom,
            bearing: bearing,
            pitch: pitch,
            bounds: [
                bounds.getWest(),
                bounds.getSouth(),
                bounds.getEast(),
                bounds.getNorth()
            ]
        };
    },

    /**
     * Download a file to the user's computer
     * @param {Uint8Array} data - File data as byte array
     * @param {string} fileName - Name for the downloaded file
     */
    downloadFile(data, fileName) {
        try {
            // Convert byte array to Blob
            const blob = new Blob([data], { type: 'application/octet-stream' });

            // Create download link
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = fileName;

            // Trigger download
            document.body.appendChild(link);
            link.click();

            // Cleanup
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        } catch (error) {
            console.error('Failed to download file:', error);
        }
    },

    /**
     * Download from a URL
     * @param {string} url - URL to download
     * @param {string} fileName - Name for the downloaded file
     */
    async downloadFromUrl(url, fileName) {
        try {
            const response = await fetch(url);
            const blob = await response.blob();
            const objectUrl = URL.createObjectURL(blob);

            const link = document.createElement('a');
            link.href = objectUrl;
            link.download = fileName;

            document.body.appendChild(link);
            link.click();

            document.body.removeChild(link);
            URL.revokeObjectURL(objectUrl);
        } catch (error) {
            console.error('Failed to download from URL:', error);
        }
    },

    /**
     * Find map element by ID
     * @param {string} mapId - Map component ID
     * @returns {HTMLElement|null} Map container element
     */
    findMapElement(mapId) {
        // Try direct ID first
        let element = document.getElementById(mapId);
        if (element) return element;

        // Try with map- prefix
        element = document.getElementById(`map-${mapId}`);
        if (element) return element;

        // Try finding by class
        element = document.querySelector(`.honua-map-${mapId}`);
        if (element) return element;

        // Try finding the first map
        element = document.querySelector('.honua-map');
        if (element) {
            console.warn(`Using first map element found as fallback for ID: ${mapId}`);
            return element;
        }

        return null;
    },

    /**
     * Wait for map to finish rendering
     * @param {maplibregl.Map} map - MapLibre map instance
     * @returns {Promise<void>}
     */
    waitForMapIdle(map) {
        return new Promise((resolve) => {
            if (map.loaded() && !map.isMoving()) {
                resolve();
            } else {
                map.once('idle', () => resolve());
            }
        });
    },

    /**
     * Capture map at specific resolution
     * @param {string} mapId - Map component ID
     * @param {number} width - Target width in pixels
     * @param {number} height - Target height in pixels
     * @param {number} dpi - Target DPI (default 150)
     * @returns {Promise<string>} Base64-encoded image data URL
     */
    async captureMapAtResolution(mapId, width, height, dpi = 150) {
        const mapElement = this.findMapElement(mapId);
        if (!mapElement) {
            console.error(`Map element not found: ${mapId}`);
            return null;
        }

        const map = mapElement.__maplibreMap;
        if (!map) {
            console.error('MapLibre instance not found');
            return null;
        }

        try {
            // Calculate scale factor for DPI
            const scaleFactor = dpi / 96; // 96 DPI is browser default

            // Get current map state
            const originalCenter = map.getCenter();
            const originalZoom = map.getZoom();
            const originalBearing = map.getBearing();
            const originalPitch = map.getPitch();

            // Create offscreen canvas
            const canvas = document.createElement('canvas');
            canvas.width = width * scaleFactor;
            canvas.height = height * scaleFactor;

            // Create temporary container
            const container = document.createElement('div');
            container.style.width = `${width}px`;
            container.style.height = `${height}px`;
            container.style.position = 'absolute';
            container.style.left = '-9999px';
            document.body.appendChild(container);

            // Create temporary map
            const tempMap = new maplibregl.Map({
                container: container,
                style: map.getStyle(),
                center: originalCenter,
                zoom: originalZoom,
                bearing: originalBearing,
                pitch: originalPitch,
                preserveDrawingBuffer: true,
                interactive: false
            });

            // Wait for map to load
            await new Promise((resolve) => tempMap.once('load', resolve));
            await this.waitForMapIdle(tempMap);

            // Capture canvas
            const tempCanvas = tempMap.getCanvas();
            const ctx = canvas.getContext('2d');
            ctx.drawImage(tempCanvas, 0, 0, canvas.width, canvas.height);

            // Convert to data URL
            const dataUrl = canvas.toDataURL('image/png');

            // Cleanup
            tempMap.remove();
            document.body.removeChild(container);

            return dataUrl;
        } catch (error) {
            console.error('Failed to capture map at resolution:', error);
            return null;
        }
    },

    /**
     * Get layer visibility states
     * @param {string} mapId - Map component ID
     * @returns {object} Map of layer IDs to visibility states
     */
    getLayerVisibility(mapId) {
        const mapElement = this.findMapElement(mapId);
        if (!mapElement) return {};

        const map = mapElement.__maplibreMap;
        if (!map) return {};

        const style = map.getStyle();
        if (!style || !style.layers) return {};

        const visibility = {};
        style.layers.forEach((layer) => {
            visibility[layer.id] = map.getLayoutProperty(layer.id, 'visibility') !== 'none';
        });

        return visibility;
    },

    /**
     * Export map as image blob
     * @param {string} mapId - Map component ID
     * @param {string} format - Image format ('png', 'jpeg')
     * @param {number} quality - JPEG quality 0-1
     * @returns {Promise<Blob>} Image blob
     */
    async exportMapAsBlob(mapId, format = 'png', quality = 0.92) {
        const mapElement = this.findMapElement(mapId);
        if (!mapElement) {
            throw new Error(`Map element not found: ${mapId}`);
        }

        const map = mapElement.__maplibreMap;
        if (!map) {
            throw new Error('MapLibre instance not found');
        }

        await this.waitForMapIdle(map);

        const canvas = map.getCanvas();
        const mimeType = format === 'jpeg' ? 'image/jpeg' : 'image/png';

        return new Promise((resolve, reject) => {
            canvas.toBlob(
                (blob) => {
                    if (blob) {
                        resolve(blob);
                    } else {
                        reject(new Error('Failed to create blob'));
                    }
                },
                mimeType,
                quality
            );
        });
    },

    /**
     * Get map extent as GeoJSON bbox
     * @param {string} mapId - Map component ID
     * @returns {array} Bounding box [west, south, east, north]
     */
    getMapExtent(mapId) {
        const mapElement = this.findMapElement(mapId);
        if (!mapElement) return null;

        const map = mapElement.__maplibreMap;
        if (!map) return null;

        const bounds = map.getBounds();
        return [
            bounds.getWest(),
            bounds.getSouth(),
            bounds.getEast(),
            bounds.getNorth()
        ];
    },

    /**
     * Calculate print scale based on map extent and paper size
     * @param {string} mapId - Map component ID
     * @param {number} paperWidthMM - Paper width in millimeters
     * @returns {number} Map scale denominator (e.g., 25000 for 1:25,000)
     */
    calculatePrintScale(mapId, paperWidthMM) {
        const mapElement = this.findMapElement(mapId);
        if (!mapElement) return null;

        const map = mapElement.__maplibreMap;
        if (!map) return null;

        // Get map bounds
        const bounds = map.getBounds();
        const ne = bounds.getNorthEast();
        const sw = bounds.getSouthWest();

        // Calculate ground distance in meters (approximate)
        const groundDistance = this.haversineDistance(
            sw.lat, sw.lng,
            sw.lat, ne.lng
        );

        // Convert paper width to meters
        const paperWidthMeters = paperWidthMM / 1000;

        // Calculate scale
        const scale = Math.round(groundDistance / paperWidthMeters);

        return scale;
    },

    /**
     * Calculate distance between two points using Haversine formula
     * @param {number} lat1 - Latitude of point 1
     * @param {number} lon1 - Longitude of point 1
     * @param {number} lat2 - Latitude of point 2
     * @param {number} lon2 - Longitude of point 2
     * @returns {number} Distance in meters
     */
    haversineDistance(lat1, lon1, lat2, lon2) {
        const R = 6371000; // Earth's radius in meters
        const φ1 = lat1 * Math.PI / 180;
        const φ2 = lat2 * Math.PI / 180;
        const Δφ = (lat2 - lat1) * Math.PI / 180;
        const Δλ = (lon2 - lon1) * Math.PI / 180;

        const a = Math.sin(Δφ / 2) * Math.sin(Δφ / 2) +
            Math.cos(φ1) * Math.cos(φ2) *
            Math.sin(Δλ / 2) * Math.sin(Δλ / 2);

        const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));

        return R * c;
    }
};

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = window.honuaPrint;
}
