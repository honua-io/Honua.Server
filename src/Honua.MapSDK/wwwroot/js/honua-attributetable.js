/**
 * HonuaAttributeTable - JavaScript interop for attribute table component
 * Provides map integration, feature highlighting, zooming, and export capabilities
 */

window.HonuaAttributeTable = {
    /**
     * Highlight features on the map
     * @param {string} mapId - Map component ID
     * @param {string[]} featureIds - Array of feature IDs to highlight
     * @param {string} layerId - Layer ID containing the features
     */
    highlightFeatures: function (mapId, featureIds, layerId) {
        const map = window.HonuaMaps?.[mapId];
        if (!map) {
            console.warn(`Map ${mapId} not found for highlighting features`);
            return;
        }

        try {
            // Remove existing highlight layer
            if (map.getLayer('attribute-table-highlight')) {
                map.removeLayer('attribute-table-highlight');
            }
            if (map.getSource('attribute-table-highlight')) {
                map.removeSource('attribute-table-highlight');
            }

            // Create highlight layer
            const features = [];
            const sourceData = map.getSource(layerId)?._data;

            if (sourceData && sourceData.features) {
                sourceData.features.forEach(feature => {
                    if (featureIds.includes(feature.id || feature.properties.id)) {
                        features.push(feature);
                    }
                });
            }

            if (features.length > 0) {
                map.addSource('attribute-table-highlight', {
                    type: 'geojson',
                    data: {
                        type: 'FeatureCollection',
                        features: features
                    }
                });

                // Add highlight styling based on geometry type
                const geometryType = features[0].geometry.type;

                if (geometryType === 'Point' || geometryType === 'MultiPoint') {
                    map.addLayer({
                        id: 'attribute-table-highlight',
                        type: 'circle',
                        source: 'attribute-table-highlight',
                        paint: {
                            'circle-radius': 10,
                            'circle-color': '#ff6b35',
                            'circle-opacity': 0.6,
                            'circle-stroke-width': 3,
                            'circle-stroke-color': '#ff6b35',
                            'circle-stroke-opacity': 1
                        }
                    });
                } else if (geometryType === 'LineString' || geometryType === 'MultiLineString') {
                    map.addLayer({
                        id: 'attribute-table-highlight',
                        type: 'line',
                        source: 'attribute-table-highlight',
                        paint: {
                            'line-color': '#ff6b35',
                            'line-width': 4,
                            'line-opacity': 0.8
                        }
                    });
                } else if (geometryType === 'Polygon' || geometryType === 'MultiPolygon') {
                    map.addLayer({
                        id: 'attribute-table-highlight',
                        type: 'fill',
                        source: 'attribute-table-highlight',
                        paint: {
                            'fill-color': '#ff6b35',
                            'fill-opacity': 0.3
                        }
                    });
                    map.addLayer({
                        id: 'attribute-table-highlight-outline',
                        type: 'line',
                        source: 'attribute-table-highlight',
                        paint: {
                            'line-color': '#ff6b35',
                            'line-width': 3,
                            'line-opacity': 1
                        }
                    });
                }
            }
        } catch (error) {
            console.error('Error highlighting features:', error);
        }
    },

    /**
     * Clear feature highlights from the map
     * @param {string} mapId - Map component ID
     */
    clearHighlight: function (mapId) {
        const map = window.HonuaMaps?.[mapId];
        if (!map) {
            console.warn(`Map ${mapId} not found for clearing highlights`);
            return;
        }

        try {
            if (map.getLayer('attribute-table-highlight')) {
                map.removeLayer('attribute-table-highlight');
            }
            if (map.getLayer('attribute-table-highlight-outline')) {
                map.removeLayer('attribute-table-highlight-outline');
            }
            if (map.getSource('attribute-table-highlight')) {
                map.removeSource('attribute-table-highlight');
            }
        } catch (error) {
            console.error('Error clearing highlights:', error);
        }
    },

    /**
     * Zoom to a single feature on the map
     * @param {string} mapId - Map component ID
     * @param {string} featureId - Feature ID to zoom to
     * @param {string} layerId - Layer ID containing the feature
     */
    zoomToFeature: function (mapId, featureId, layerId) {
        const map = window.HonuaMaps?.[mapId];
        if (!map) {
            console.warn(`Map ${mapId} not found for zooming to feature`);
            return;
        }

        try {
            const sourceData = map.getSource(layerId)?._data;
            if (!sourceData || !sourceData.features) {
                console.warn(`Source ${layerId} not found or has no features`);
                return;
            }

            const feature = sourceData.features.find(f =>
                (f.id || f.properties?.id) === featureId
            );

            if (feature && feature.geometry) {
                const bounds = this._calculateBounds([feature]);
                map.fitBounds(bounds, {
                    padding: 100,
                    duration: 1000,
                    maxZoom: 16
                });
            }
        } catch (error) {
            console.error('Error zooming to feature:', error);
        }
    },

    /**
     * Zoom to multiple features on the map
     * @param {string} mapId - Map component ID
     * @param {string[]} featureIds - Array of feature IDs to zoom to
     * @param {string} layerId - Layer ID containing the features
     */
    zoomToFeatures: function (mapId, featureIds, layerId) {
        const map = window.HonuaMaps?.[mapId];
        if (!map) {
            console.warn(`Map ${mapId} not found for zooming to features`);
            return;
        }

        try {
            const sourceData = map.getSource(layerId)?._data;
            if (!sourceData || !sourceData.features) {
                console.warn(`Source ${layerId} not found or has no features`);
                return;
            }

            const features = sourceData.features.filter(f =>
                featureIds.includes(f.id || f.properties?.id)
            );

            if (features.length > 0) {
                const bounds = this._calculateBounds(features);
                map.fitBounds(bounds, {
                    padding: 100,
                    duration: 1000
                });
            }
        } catch (error) {
            console.error('Error zooming to features:', error);
        }
    },

    /**
     * Calculate bounding box for features
     * @param {Array} features - GeoJSON features
     * @returns {Array} Bounds [west, south, east, north]
     */
    _calculateBounds: function (features) {
        let minLng = Infinity, minLat = Infinity;
        let maxLng = -Infinity, maxLat = -Infinity;

        features.forEach(feature => {
            const coords = this._extractCoordinates(feature.geometry);
            coords.forEach(([lng, lat]) => {
                minLng = Math.min(minLng, lng);
                minLat = Math.min(minLat, lat);
                maxLng = Math.max(maxLng, lng);
                maxLat = Math.max(maxLat, lat);
            });
        });

        return [minLng, minLat, maxLng, maxLat];
    },

    /**
     * Extract all coordinates from a geometry
     * @param {Object} geometry - GeoJSON geometry
     * @returns {Array} Array of [lng, lat] coordinates
     */
    _extractCoordinates: function (geometry) {
        const coords = [];

        function extract(geom) {
            if (geom.type === 'Point') {
                coords.push(geom.coordinates);
            } else if (geom.type === 'MultiPoint' || geom.type === 'LineString') {
                geom.coordinates.forEach(coord => coords.push(coord));
            } else if (geom.type === 'MultiLineString' || geom.type === 'Polygon') {
                geom.coordinates.forEach(ring =>
                    ring.forEach(coord => coords.push(coord))
                );
            } else if (geom.type === 'MultiPolygon') {
                geom.coordinates.forEach(polygon =>
                    polygon.forEach(ring =>
                        ring.forEach(coord => coords.push(coord))
                    )
                );
            } else if (geom.type === 'GeometryCollection') {
                geom.geometries.forEach(g => extract(g));
            }
        }

        extract(geometry);
        return coords;
    },

    /**
     * Export data to CSV format
     * @param {Array} data - Array of objects to export
     * @param {string} filename - Output filename
     * @param {Array} columns - Column definitions
     */
    exportToCSV: function (data, filename, columns) {
        try {
            // Build CSV header
            const headers = columns.map(col => col.displayName || col.fieldName);
            const csvLines = [headers.join(',')];

            // Build CSV rows
            data.forEach(row => {
                const values = columns.map(col => {
                    const value = row[col.fieldName];
                    return this._escapeCSVValue(value);
                });
                csvLines.push(values.join(','));
            });

            const csvContent = csvLines.join('\n');
            this._downloadFile(filename, csvContent, 'text/csv');
        } catch (error) {
            console.error('Error exporting to CSV:', error);
        }
    },

    /**
     * Export data to Excel format (using CSV as fallback)
     * @param {Array} data - Array of objects to export
     * @param {string} filename - Output filename
     * @param {Array} columns - Column definitions
     */
    exportToExcel: function (data, filename, columns) {
        // For simplicity, we'll export as CSV with .xlsx extension
        // In a production environment, you'd use a library like SheetJS (xlsx)
        try {
            const csvFilename = filename.replace('.xlsx', '.csv');
            this.exportToCSV(data, csvFilename, columns);
            console.info('Exported to CSV format (Excel export requires additional library)');
        } catch (error) {
            console.error('Error exporting to Excel:', error);
        }
    },

    /**
     * Export data to JSON format
     * @param {Array} data - Array of objects to export
     * @param {string} filename - Output filename
     */
    exportToJSON: function (data, filename) {
        try {
            const jsonContent = JSON.stringify(data, null, 2);
            this._downloadFile(filename, jsonContent, 'application/json');
        } catch (error) {
            console.error('Error exporting to JSON:', error);
        }
    },

    /**
     * Export data to GeoJSON format
     * @param {Array} features - Array of feature records with geometry
     * @param {string} filename - Output filename
     */
    exportToGeoJSON: function (features, filename) {
        try {
            const featureCollection = {
                type: 'FeatureCollection',
                features: features.map(f => ({
                    type: 'Feature',
                    id: f.id,
                    properties: f.properties || {},
                    geometry: f.geometry
                }))
            };

            const geoJsonContent = JSON.stringify(featureCollection, null, 2);
            this._downloadFile(filename, geoJsonContent, 'application/geo+json');
        } catch (error) {
            console.error('Error exporting to GeoJSON:', error);
        }
    },

    /**
     * Copy selected rows to clipboard
     * @param {Array} data - Array of objects to copy
     * @param {Array} columns - Column definitions
     */
    copyToClipboard: function (data, columns) {
        try {
            // Build tab-delimited text (Excel-compatible)
            const headers = columns.map(col => col.displayName || col.fieldName);
            const lines = [headers.join('\t')];

            data.forEach(row => {
                const values = columns.map(col => {
                    const value = row[col.fieldName];
                    return value !== null && value !== undefined ? String(value) : '';
                });
                lines.push(values.join('\t'));
            });

            const text = lines.join('\n');
            navigator.clipboard.writeText(text)
                .then(() => console.log('Copied to clipboard'))
                .catch(err => console.error('Error copying to clipboard:', err));
        } catch (error) {
            console.error('Error copying to clipboard:', error);
        }
    },

    /**
     * Print table view
     * @param {string} tableId - Table element ID
     * @param {string} title - Print title
     */
    printTable: function (tableId, title) {
        try {
            const printWindow = window.open('', '_blank');
            if (!printWindow) {
                console.error('Could not open print window');
                return;
            }

            const tableElement = document.querySelector(`#${tableId}`);
            if (!tableElement) {
                console.error(`Table element ${tableId} not found`);
                return;
            }

            const html = `
                <!DOCTYPE html>
                <html>
                <head>
                    <title>${title}</title>
                    <style>
                        body { font-family: Arial, sans-serif; margin: 20px; }
                        h1 { margin-bottom: 20px; }
                        table { width: 100%; border-collapse: collapse; }
                        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
                        th { background-color: #f2f2f2; font-weight: bold; }
                        tr:nth-child(even) { background-color: #f9f9f9; }
                        @media print {
                            body { margin: 0; }
                        }
                    </style>
                </head>
                <body>
                    <h1>${title}</h1>
                    ${tableElement.innerHTML}
                    <script>
                        window.onload = function() {
                            window.print();
                            window.onafterprint = function() {
                                window.close();
                            };
                        };
                    </script>
                </body>
                </html>
            `;

            printWindow.document.write(html);
            printWindow.document.close();
        } catch (error) {
            console.error('Error printing table:', error);
        }
    },

    /**
     * Escape CSV value (handle quotes and commas)
     * @param {any} value - Value to escape
     * @returns {string} Escaped value
     */
    _escapeCSVValue: function (value) {
        if (value === null || value === undefined) {
            return '';
        }

        const str = String(value);

        // If contains comma, quote, or newline, wrap in quotes and escape quotes
        if (str.includes(',') || str.includes('"') || str.includes('\n')) {
            return `"${str.replace(/"/g, '""')}"`;
        }

        return str;
    },

    /**
     * Download file to user's computer
     * @param {string} filename - Output filename
     * @param {string} content - File content
     * @param {string} mimeType - MIME type
     */
    _downloadFile: function (filename, content, mimeType) {
        const blob = new Blob([content], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    },

    /**
     * Get features within map extent
     * @param {string} mapId - Map component ID
     * @param {string} layerId - Layer ID
     * @returns {Array} Features within current map extent
     */
    getFeaturesInExtent: function (mapId, layerId) {
        const map = window.HonuaMaps?.[mapId];
        if (!map) {
            console.warn(`Map ${mapId} not found`);
            return [];
        }

        try {
            const bounds = map.getBounds();
            const sourceData = map.getSource(layerId)?._data;

            if (!sourceData || !sourceData.features) {
                return [];
            }

            // Simple bounding box check
            return sourceData.features.filter(feature => {
                const coords = this._extractCoordinates(feature.geometry);
                return coords.some(([lng, lat]) =>
                    lng >= bounds.getWest() && lng <= bounds.getEast() &&
                    lat >= bounds.getSouth() && lat <= bounds.getNorth()
                );
            });
        } catch (error) {
            console.error('Error getting features in extent:', error);
            return [];
        }
    }
};

// Expose for Blazor interop
window.highlightFeatures = window.HonuaAttributeTable.highlightFeatures.bind(window.HonuaAttributeTable);
window.clearHighlight = window.HonuaAttributeTable.clearHighlight.bind(window.HonuaAttributeTable);
window.zoomToFeature = window.HonuaAttributeTable.zoomToFeature.bind(window.HonuaAttributeTable);
window.zoomToFeatures = window.HonuaAttributeTable.zoomToFeatures.bind(window.HonuaAttributeTable);
window.exportToCSV = window.HonuaAttributeTable.exportToCSV.bind(window.HonuaAttributeTable);
window.exportToExcel = window.HonuaAttributeTable.exportToExcel.bind(window.HonuaAttributeTable);
window.exportToJSON = window.HonuaAttributeTable.exportToJSON.bind(window.HonuaAttributeTable);
window.exportToGeoJSON = window.HonuaAttributeTable.exportToGeoJSON.bind(window.HonuaAttributeTable);
window.copyToClipboard = window.HonuaAttributeTable.copyToClipboard.bind(window.HonuaAttributeTable);
window.printTable = window.HonuaAttributeTable.printTable.bind(window.HonuaAttributeTable);
