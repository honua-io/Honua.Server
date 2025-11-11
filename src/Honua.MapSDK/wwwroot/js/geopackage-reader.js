// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * GeoPackage Reader - Client-side GPKG file reading using sql.js
 *
 * This module provides functionality to read and query GeoPackage files
 * entirely in the browser using SQLite compiled to WebAssembly (sql.js).
 *
 * Features:
 * - Read GPKG file structure (layers, metadata)
 * - Query features with spatial and attribute filters
 * - Convert GPKG geometries to GeoJSON
 * - Support for spatial indexes (R-tree)
 * - Tile extraction for raster layers
 */

window.HonuaGeoPackage = window.HonuaGeoPackage || {};

(function() {
    'use strict';

    const GPKG_APPLICATION_ID = 0x47504B47; // 'GPKG' in hex
    const WKB_GEOMETRY_TYPES = {
        1: 'Point', 2: 'LineString', 3: 'Polygon',
        4: 'MultiPoint', 5: 'MultiLineString', 6: 'MultiPolygon',
        7: 'GeometryCollection'
    };

    /**
     * GeoPackage Reader class
     */
    class GeoPackageReader {
        constructor() {
            this.db = null;
            this.fileName = null;
            this.sqlInstance = null;
        }

        /**
         * Initialize sql.js library
         */
        async initSqlJs() {
            if (!window.initSqlJs) {
                throw new Error('sql.js library not loaded. Include sql.js from CDN or npm package.');
            }

            if (!this.sqlInstance) {
                this.sqlInstance = await window.initSqlJs({
                    locateFile: file => `https://cdnjs.cloudflare.com/ajax/libs/sql.js/1.8.0/${file}`
                });
            }
            return this.sqlInstance;
        }

        /**
         * Load a GeoPackage file from bytes
         * @param {Uint8Array} data - GPKG file data
         * @param {string} fileName - File name
         */
        async loadFromBytes(data, fileName) {
            const SQL = await this.initSqlJs();

            try {
                this.db = new SQL.Database(data);
                this.fileName = fileName || 'unknown.gpkg';

                // Validate it's a GeoPackage
                await this.validateGeoPackage();

                return {
                    success: true,
                    message: `GeoPackage '${this.fileName}' loaded successfully`
                };
            } catch (error) {
                return {
                    success: false,
                    error: `Failed to load GeoPackage: ${error.message}`
                };
            }
        }

        /**
         * Load a GeoPackage file from a URL
         * @param {string} url - URL to GPKG file
         */
        async loadFromUrl(url) {
            try {
                const response = await fetch(url);
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }

                const arrayBuffer = await response.arrayBuffer();
                const data = new Uint8Array(arrayBuffer);

                const fileName = url.split('/').pop() || 'download.gpkg';
                return await this.loadFromBytes(data, fileName);
            } catch (error) {
                return {
                    success: false,
                    error: `Failed to load from URL: ${error.message}`
                };
            }
        }

        /**
         * Validate that the database is a valid GeoPackage
         */
        validateGeoPackage() {
            if (!this.db) {
                throw new Error('Database not loaded');
            }

            // Check application_id
            const appIdResult = this.db.exec('PRAGMA application_id;');
            if (appIdResult.length === 0 || appIdResult[0].values[0][0] !== GPKG_APPLICATION_ID) {
                throw new Error('Invalid GeoPackage: application_id mismatch');
            }

            // Check for required tables
            const requiredTables = ['gpkg_contents', 'gpkg_spatial_ref_sys'];
            for (const table of requiredTables) {
                const result = this.db.exec(
                    `SELECT name FROM sqlite_master WHERE type='table' AND name='${table}';`
                );
                if (result.length === 0 || result[0].values.length === 0) {
                    throw new Error(`Invalid GeoPackage: missing table '${table}'`);
                }
            }
        }

        /**
         * Get GeoPackage metadata and structure
         * @returns {Object} GeoPackage info
         */
        getInfo() {
            if (!this.db) {
                throw new Error('Database not loaded');
            }

            const info = {
                fileName: this.fileName,
                fileSize: 0, // Not available in-memory
                applicationId: this.getApplicationId(),
                userVersion: this.getUserVersion(),
                layers: this.getLayers(),
                spatialReferences: this.getSpatialReferences(),
                boundingBox: null,
                parsedAt: new Date().toISOString()
            };

            // Calculate overall bounding box
            const bounds = this.calculateOverallBounds(info.layers);
            if (bounds) {
                info.boundingBox = bounds;
            }

            return info;
        }

        /**
         * Get application ID
         */
        getApplicationId() {
            const result = this.db.exec('PRAGMA application_id;');
            return result.length > 0 ? result[0].values[0][0] : 0;
        }

        /**
         * Get user version
         */
        getUserVersion() {
            const result = this.db.exec('PRAGMA user_version;');
            return result.length > 0 ? result[0].values[0][0] : 0;
        }

        /**
         * Get list of layers from gpkg_contents
         */
        getLayers() {
            const sql = `
                SELECT
                    c.table_name,
                    c.data_type,
                    c.identifier,
                    c.description,
                    c.last_change,
                    c.min_x,
                    c.min_y,
                    c.max_x,
                    c.max_y,
                    c.srs_id,
                    gc.column_name,
                    gc.geometry_type_name,
                    gc.z,
                    gc.m
                FROM gpkg_contents c
                LEFT JOIN gpkg_geometry_columns gc ON c.table_name = gc.table_name
                ORDER BY c.table_name;
            `;

            const result = this.db.exec(sql);
            if (result.length === 0) {
                return [];
            }

            const layers = [];
            const rows = result[0].values;

            for (const row of rows) {
                const layer = {
                    tableName: row[0],
                    dataType: row[1],
                    identifier: row[2] || row[0],
                    description: row[3],
                    lastChange: row[4],
                    boundingBox: (row[5] !== null && row[6] !== null && row[7] !== null && row[8] !== null)
                        ? [row[5], row[6], row[7], row[8]]
                        : null,
                    srsId: row[9] || 0,
                    geometryColumn: row[10],
                    geometryType: row[11],
                    hasZ: row[12] === 1,
                    hasM: row[13] === 1,
                    featureCount: 0,
                    fields: [],
                    statistics: {
                        totalCount: 0,
                        nullGeometryCount: 0,
                        extent: null,
                        geometryTypeCounts: {},
                        fieldStats: {}
                    }
                };

                // Get feature count for feature layers
                if (layer.dataType === 'features') {
                    layer.featureCount = this.getFeatureCount(layer.tableName);
                    layer.statistics.totalCount = layer.featureCount;

                    // Get field definitions
                    layer.fields = this.getTableFields(layer.tableName);
                }

                layers.push(layer);
            }

            return layers;
        }

        /**
         * Get spatial reference systems
         */
        getSpatialReferences() {
            const sql = `
                SELECT
                    srs_id,
                    srs_name,
                    organization,
                    organization_coordsys_id,
                    definition,
                    description
                FROM gpkg_spatial_ref_sys
                ORDER BY srs_id;
            `;

            const result = this.db.exec(sql);
            if (result.length === 0) {
                return [];
            }

            return result[0].values.map(row => ({
                srsId: row[0],
                srsName: row[1],
                organization: row[2],
                organizationCoordsysId: row[3],
                definition: row[4],
                description: row[5]
            }));
        }

        /**
         * Get field definitions for a table
         */
        getTableFields(tableName) {
            const sql = `PRAGMA table_info("${tableName}");`;
            const result = this.db.exec(sql);

            if (result.length === 0) {
                return [];
            }

            return result[0].values.map(row => ({
                name: row[1],
                type: row[2],
                nullable: row[3] === 0,
                defaultValue: row[4],
                isPrimaryKey: row[5] === 1
            }));
        }

        /**
         * Get feature count for a layer
         */
        getFeatureCount(tableName) {
            try {
                const sql = `SELECT COUNT(*) FROM "${tableName}";`;
                const result = this.db.exec(sql);
                return result.length > 0 ? result[0].values[0][0] : 0;
            } catch (error) {
                console.warn(`Failed to get feature count for ${tableName}:`, error);
                return 0;
            }
        }

        /**
         * Calculate overall bounding box from all layers
         */
        calculateOverallBounds(layers) {
            const bounds = layers
                .filter(l => l.boundingBox && l.boundingBox.length === 4)
                .map(l => l.boundingBox);

            if (bounds.length === 0) {
                return null;
            }

            return [
                Math.min(...bounds.map(b => b[0])),
                Math.min(...bounds.map(b => b[1])),
                Math.max(...bounds.map(b => b[2])),
                Math.max(...bounds.map(b => b[3]))
            ];
        }

        /**
         * Query features from a layer and convert to GeoJSON
         * @param {Object} request - Feature request parameters
         * @returns {Object} GeoJSON FeatureCollection
         */
        async queryFeatures(request) {
            if (!this.db) {
                throw new Error('Database not loaded');
            }

            const {
                layerName,
                maxFeatures = 1000,
                boundingBox = null,
                attributeFilter = null,
                fields = null,
                offset = 0,
                sortBy = null,
                sortDirection = 'ASC'
            } = request;

            // Get layer info
            const layer = this.getLayers().find(l => l.tableName === layerName);
            if (!layer) {
                throw new Error(`Layer '${layerName}' not found`);
            }

            if (layer.dataType !== 'features') {
                throw new Error(`Layer '${layerName}' is not a feature layer`);
            }

            // Build query
            const geomColumn = layer.geometryColumn || 'geom';
            const fieldList = fields && fields.length > 0
                ? fields.join(', ')
                : '*';

            let whereClauses = [];

            // Spatial filter using R-tree if available
            if (boundingBox && boundingBox.length === 4) {
                const rtreeTable = `rtree_${layerName}_${geomColumn}`;
                const hasRtree = this.tableExists(rtreeTable);

                if (hasRtree) {
                    whereClauses.push(
                        `"${layerName}".rowid IN (` +
                        `SELECT id FROM "${rtreeTable}" ` +
                        `WHERE minx <= ${boundingBox[2]} AND maxx >= ${boundingBox[0]} ` +
                        `AND miny <= ${boundingBox[3]} AND maxy >= ${boundingBox[1]})`
                    );
                }
            }

            // Attribute filter
            if (attributeFilter) {
                whereClauses.push(`(${attributeFilter})`);
            }

            const whereClause = whereClauses.length > 0
                ? 'WHERE ' + whereClauses.join(' AND ')
                : '';

            const orderClause = sortBy
                ? `ORDER BY "${sortBy}" ${sortDirection}`
                : '';

            const limitClause = `LIMIT ${maxFeatures} OFFSET ${offset}`;

            const sql = `
                SELECT ${fieldList}
                FROM "${layerName}"
                ${whereClause}
                ${orderClause}
                ${limitClause};
            `;

            // Execute query
            const result = this.db.exec(sql);

            if (result.length === 0) {
                return {
                    geoJson: JSON.stringify({
                        type: 'FeatureCollection',
                        features: []
                    }),
                    featureCount: 0,
                    totalFeatures: layer.featureCount,
                    boundingBox: null
                };
            }

            // Convert to GeoJSON
            const columns = result[0].columns;
            const rows = result[0].values;

            const geomColumnIndex = columns.indexOf(geomColumn);

            const features = [];
            let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;

            for (const row of rows) {
                const properties = {};

                // Extract properties
                for (let i = 0; i < columns.length; i++) {
                    if (i !== geomColumnIndex) {
                        properties[columns[i]] = row[i];
                    }
                }

                // Parse geometry
                let geometry = null;
                if (geomColumnIndex >= 0 && row[geomColumnIndex]) {
                    const wkb = row[geomColumnIndex];
                    geometry = this.parseGpkgGeometry(wkb);

                    // Update bounds
                    if (geometry && geometry.coordinates) {
                        const coords = this.extractCoordinates(geometry);
                        for (const coord of coords) {
                            minX = Math.min(minX, coord[0]);
                            minY = Math.min(minY, coord[1]);
                            maxX = Math.max(maxX, coord[0]);
                            maxY = Math.max(maxY, coord[1]);
                        }
                    }
                }

                features.push({
                    type: 'Feature',
                    geometry: geometry,
                    properties: properties
                });
            }

            const featureCollection = {
                type: 'FeatureCollection',
                features: features
            };

            return {
                geoJson: JSON.stringify(featureCollection),
                featureCount: features.length,
                totalFeatures: layer.featureCount,
                boundingBox: features.length > 0 && minX !== Infinity
                    ? [minX, minY, maxX, maxY]
                    : null
            };
        }

        /**
         * Check if a table exists
         */
        tableExists(tableName) {
            const sql = `SELECT name FROM sqlite_master WHERE type='table' AND name='${tableName}';`;
            const result = this.db.exec(sql);
            return result.length > 0 && result[0].values.length > 0;
        }

        /**
         * Parse GeoPackage geometry blob (GP header + WKB)
         * @param {Uint8Array} blob - Geometry blob
         * @returns {Object} GeoJSON geometry
         */
        parseGpkgGeometry(blob) {
            if (!blob || blob.length < 8) {
                return null;
            }

            try {
                // Parse GPKG header
                const magic = String.fromCharCode(blob[0]) + String.fromCharCode(blob[1]);
                if (magic !== 'GP') {
                    console.warn('Invalid GeoPackage geometry: missing GP magic bytes');
                    return null;
                }

                const flags = blob[3];
                const isLittleEndian = (flags & 0x01) === 0x01;
                const envelopeType = (flags >> 1) & 0x07;

                // Skip to WKB (after header and optional envelope)
                let offset = 8; // Magic(2) + Version(1) + Flags(1) + SRID(4)

                // Skip envelope if present
                const envelopeSizes = [0, 32, 48, 48, 64];
                if (envelopeType > 0 && envelopeType < envelopeSizes.length) {
                    offset += envelopeSizes[envelopeType];
                }

                // Parse WKB
                const wkb = blob.slice(offset);
                return this.parseWkb(wkb, isLittleEndian);
            } catch (error) {
                console.error('Failed to parse GPKG geometry:', error);
                return null;
            }
        }

        /**
         * Parse WKB (Well-Known Binary) to GeoJSON geometry
         * @param {Uint8Array} wkb - WKB data
         * @param {boolean} isLittleEndian - Byte order
         * @returns {Object} GeoJSON geometry
         */
        parseWkb(wkb, isLittleEndian) {
            if (!wkb || wkb.length < 5) {
                return null;
            }

            const view = new DataView(wkb.buffer, wkb.byteOffset, wkb.byteLength);
            let offset = 0;

            // Read byte order (should match GPKG header, but WKB also specifies it)
            const byteOrder = view.getUint8(offset++);
            const littleEndian = byteOrder === 1;

            // Read geometry type
            const geomType = view.getUint32(offset, littleEndian);
            offset += 4;

            const baseType = geomType % 1000; // Remove Z/M flags

            switch (baseType) {
                case 1: // Point
                    return this.parseWkbPoint(view, offset, littleEndian);
                case 2: // LineString
                    return this.parseWkbLineString(view, offset, littleEndian);
                case 3: // Polygon
                    return this.parseWkbPolygon(view, offset, littleEndian);
                case 4: // MultiPoint
                    return this.parseWkbMultiPoint(view, offset, littleEndian);
                case 5: // MultiLineString
                    return this.parseWkbMultiLineString(view, offset, littleEndian);
                case 6: // MultiPolygon
                    return this.parseWkbMultiPolygon(view, offset, littleEndian);
                default:
                    console.warn(`Unsupported geometry type: ${geomType}`);
                    return null;
            }
        }

        parseWkbPoint(view, offset, littleEndian) {
            const x = view.getFloat64(offset, littleEndian);
            const y = view.getFloat64(offset + 8, littleEndian);
            return {
                type: 'Point',
                coordinates: [x, y]
            };
        }

        parseWkbLineString(view, offset, littleEndian) {
            const numPoints = view.getUint32(offset, littleEndian);
            offset += 4;

            const coordinates = [];
            for (let i = 0; i < numPoints; i++) {
                const x = view.getFloat64(offset, littleEndian);
                const y = view.getFloat64(offset + 8, littleEndian);
                coordinates.push([x, y]);
                offset += 16;
            }

            return {
                type: 'LineString',
                coordinates: coordinates
            };
        }

        parseWkbPolygon(view, offset, littleEndian) {
            const numRings = view.getUint32(offset, littleEndian);
            offset += 4;

            const coordinates = [];
            for (let i = 0; i < numRings; i++) {
                const numPoints = view.getUint32(offset, littleEndian);
                offset += 4;

                const ring = [];
                for (let j = 0; j < numPoints; j++) {
                    const x = view.getFloat64(offset, littleEndian);
                    const y = view.getFloat64(offset + 8, littleEndian);
                    ring.push([x, y]);
                    offset += 16;
                }
                coordinates.push(ring);
            }

            return {
                type: 'Polygon',
                coordinates: coordinates
            };
        }

        parseWkbMultiPoint(view, offset, littleEndian) {
            const numPoints = view.getUint32(offset, littleEndian);
            offset += 4;

            const coordinates = [];
            for (let i = 0; i < numPoints; i++) {
                // Skip byte order and type (already know it's a point)
                offset += 5;
                const x = view.getFloat64(offset, littleEndian);
                const y = view.getFloat64(offset + 8, littleEndian);
                coordinates.push([x, y]);
                offset += 16;
            }

            return {
                type: 'MultiPoint',
                coordinates: coordinates
            };
        }

        parseWkbMultiLineString(view, offset, littleEndian) {
            const numLineStrings = view.getUint32(offset, littleEndian);
            offset += 4;

            const coordinates = [];
            for (let i = 0; i < numLineStrings; i++) {
                offset += 5; // Skip byte order and type
                const numPoints = view.getUint32(offset, littleEndian);
                offset += 4;

                const lineString = [];
                for (let j = 0; j < numPoints; j++) {
                    const x = view.getFloat64(offset, littleEndian);
                    const y = view.getFloat64(offset + 8, littleEndian);
                    lineString.push([x, y]);
                    offset += 16;
                }
                coordinates.push(lineString);
            }

            return {
                type: 'MultiLineString',
                coordinates: coordinates
            };
        }

        parseWkbMultiPolygon(view, offset, littleEndian) {
            const numPolygons = view.getUint32(offset, littleEndian);
            offset += 4;

            const coordinates = [];
            for (let i = 0; i < numPolygons; i++) {
                offset += 5; // Skip byte order and type
                const numRings = view.getUint32(offset, littleEndian);
                offset += 4;

                const polygon = [];
                for (let j = 0; j < numRings; j++) {
                    const numPoints = view.getUint32(offset, littleEndian);
                    offset += 4;

                    const ring = [];
                    for (let k = 0; k < numPoints; k++) {
                        const x = view.getFloat64(offset, littleEndian);
                        const y = view.getFloat64(offset + 8, littleEndian);
                        ring.push([x, y]);
                        offset += 16;
                    }
                    polygon.push(ring);
                }
                coordinates.push(polygon);
            }

            return {
                type: 'MultiPolygon',
                coordinates: coordinates
            };
        }

        /**
         * Extract all coordinates from a geometry (for bounds calculation)
         */
        extractCoordinates(geometry) {
            if (!geometry || !geometry.coordinates) {
                return [];
            }

            const coords = [];

            const extract = (coord) => {
                if (Array.isArray(coord)) {
                    if (typeof coord[0] === 'number') {
                        coords.push(coord);
                    } else {
                        coord.forEach(extract);
                    }
                }
            };

            extract(geometry.coordinates);
            return coords;
        }

        /**
         * Close the database
         */
        close() {
            if (this.db) {
                this.db.close();
                this.db = null;
            }
        }
    }

    // Export to global namespace
    window.HonuaGeoPackage.GeoPackageReader = GeoPackageReader;

    /**
     * Create a new GeoPackage reader instance
     */
    window.HonuaGeoPackage.createReader = function() {
        return new GeoPackageReader();
    };

    /**
     * DotNetHelper for Blazor interop
     */
    window.HonuaGeoPackage.DotNetHelper = {
        readers: new Map(),
        nextId: 1,

        async createReader() {
            const id = this.nextId++;
            const reader = new GeoPackageReader();
            this.readers.set(id, reader);
            return id;
        },

        async loadFromBytes(readerId, data, fileName) {
            const reader = this.readers.get(readerId);
            if (!reader) {
                throw new Error(`Reader ${readerId} not found`);
            }
            return await reader.loadFromBytes(new Uint8Array(data), fileName);
        },

        async loadFromUrl(readerId, url) {
            const reader = this.readers.get(readerId);
            if (!reader) {
                throw new Error(`Reader ${readerId} not found`);
            }
            return await reader.loadFromUrl(url);
        },

        getInfo(readerId) {
            const reader = this.readers.get(readerId);
            if (!reader) {
                throw new Error(`Reader ${readerId} not found`);
            }
            return reader.getInfo();
        },

        async queryFeatures(readerId, request) {
            const reader = this.readers.get(readerId);
            if (!reader) {
                throw new Error(`Reader ${readerId} not found`);
            }
            return await reader.queryFeatures(request);
        },

        closeReader(readerId) {
            const reader = this.readers.get(readerId);
            if (reader) {
                reader.close();
                this.readers.delete(readerId);
            }
        }
    };

})();
