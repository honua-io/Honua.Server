(function () {
    'use strict';

    const connector = tableau.makeConnector();

    // Configuration state
    let config = {
        serverUrl: '',
        dataSource: '',
        collectionId: '',
        authType: 'none',
        credentials: {}
    };

    /**
     * Initialize the connector and define the schema
     */
    connector.init = function (initCallback) {
        tableau.authType = tableau.authTypeEnum.custom;

        // Restore configuration from connection data
        if (tableau.connectionData) {
            try {
                config = JSON.parse(tableau.connectionData);
            } catch (e) {
                console.error('Failed to parse connection data:', e);
            }
        }

        initCallback();
    };

    /**
     * Define the schema for the data
     */
    connector.getSchema = function (schemaCallback) {
        const tables = [];

        if (config.dataSource === 'ogc-features') {
            tables.push({
                id: 'ogc_features',
                alias: 'OGC Features',
                columns: [
                    { id: 'feature_id', dataType: tableau.dataTypeEnum.string },
                    { id: 'geometry_type', dataType: tableau.dataTypeEnum.string },
                    { id: 'geometry_wkt', dataType: tableau.dataTypeEnum.string },
                    { id: 'latitude', dataType: tableau.dataTypeEnum.float },
                    { id: 'longitude', dataType: tableau.dataTypeEnum.float },
                    { id: 'properties', dataType: tableau.dataTypeEnum.string },
                    { id: 'bbox_minx', dataType: tableau.dataTypeEnum.float },
                    { id: 'bbox_miny', dataType: tableau.dataTypeEnum.float },
                    { id: 'bbox_maxx', dataType: tableau.dataTypeEnum.float },
                    { id: 'bbox_maxy', dataType: tableau.dataTypeEnum.float },
                    { id: 'collection_id', dataType: tableau.dataTypeEnum.string }
                ]
            });
        } else if (config.dataSource === 'stac') {
            tables.push({
                id: 'stac_items',
                alias: 'STAC Items',
                columns: [
                    { id: 'item_id', dataType: tableau.dataTypeEnum.string },
                    { id: 'collection', dataType: tableau.dataTypeEnum.string },
                    { id: 'geometry_type', dataType: tableau.dataTypeEnum.string },
                    { id: 'geometry_wkt', dataType: tableau.dataTypeEnum.string },
                    { id: 'latitude', dataType: tableau.dataTypeEnum.float },
                    { id: 'longitude', dataType: tableau.dataTypeEnum.float },
                    { id: 'datetime', dataType: tableau.dataTypeEnum.datetime },
                    { id: 'bbox_minx', dataType: tableau.dataTypeEnum.float },
                    { id: 'bbox_miny', dataType: tableau.dataTypeEnum.float },
                    { id: 'bbox_maxx', dataType: tableau.dataTypeEnum.float },
                    { id: 'bbox_maxy', dataType: tableau.dataTypeEnum.float },
                    { id: 'properties', dataType: tableau.dataTypeEnum.string },
                    { id: 'assets', dataType: tableau.dataTypeEnum.string }
                ]
            });
        }

        schemaCallback(tables);
    };

    /**
     * Fetch data from the API
     */
    connector.getData = function (table, doneCallback) {
        const baseUrl = config.serverUrl.replace(/\/$/, '');
        let apiUrl = '';

        // Build the appropriate API URL
        if (config.dataSource === 'ogc-features') {
            apiUrl = `${baseUrl}/ogc/features/v1/collections/${config.collectionId}/items?limit=1000`;
        } else if (config.dataSource === 'stac') {
            apiUrl = `${baseUrl}/stac/collections/${config.collectionId}/items?limit=1000`;
        }

        fetchAllPages(apiUrl, table, doneCallback);
    };

    /**
     * Fetch all pages of data (handle pagination)
     */
    function fetchAllPages(url, table, doneCallback) {
        const allData = [];
        let currentUrl = url;

        function fetchPage() {
            makeRequest(currentUrl, function (data) {
                if (!data) {
                    tableau.abortWithError('Failed to fetch data from API');
                    return;
                }

                // Process features/items
                const items = data.features || data.items || [];
                const processedData = items.map(item => processFeature(item, config.dataSource));
                allData.push(...processedData);

                // Check for next page
                const nextLink = data.links?.find(link => link.rel === 'next');
                if (nextLink && nextLink.href) {
                    currentUrl = nextLink.href;
                    tableau.reportProgress(`Fetched ${allData.length} records...`);
                    fetchPage();
                } else {
                    // Done fetching all pages
                    table.appendRows(allData);
                    doneCallback();
                }
            });
        }

        fetchPage();
    }

    /**
     * Process a single feature/item into table format
     */
    function processFeature(feature, sourceType) {
        const geometry = feature.geometry || {};
        const properties = feature.properties || {};
        const bbox = feature.bbox || [];

        // Extract centroid for mapping
        let latitude = null;
        let longitude = null;
        if (geometry.coordinates) {
            if (geometry.type === 'Point') {
                longitude = geometry.coordinates[0];
                latitude = geometry.coordinates[1];
            } else if (bbox.length >= 4) {
                // Use bbox center as fallback
                longitude = (bbox[0] + bbox[2]) / 2;
                latitude = (bbox[1] + bbox[3]) / 2;
            }
        }

        const baseData = {
            geometry_type: geometry.type || 'Unknown',
            geometry_wkt: geometryToWkt(geometry),
            latitude: latitude,
            longitude: longitude,
            bbox_minx: bbox[0] || null,
            bbox_miny: bbox[1] || null,
            bbox_maxx: bbox[2] || null,
            bbox_maxy: bbox[3] || null,
            properties: JSON.stringify(properties)
        };

        if (sourceType === 'ogc-features') {
            return {
                feature_id: feature.id || '',
                collection_id: config.collectionId,
                ...baseData
            };
        } else if (sourceType === 'stac') {
            return {
                item_id: feature.id || '',
                collection: feature.collection || config.collectionId,
                datetime: properties.datetime ? new Date(properties.datetime) : null,
                assets: JSON.stringify(feature.assets || {}),
                ...baseData
            };
        }
    }

    /**
     * Convert GeoJSON geometry to WKT (Well-Known Text)
     */
    function geometryToWkt(geometry) {
        if (!geometry || !geometry.type || !geometry.coordinates) {
            return null;
        }

        const coords = geometry.coordinates;
        const type = geometry.type;

        try {
            switch (type) {
                case 'Point':
                    return `POINT (${coords[0]} ${coords[1]})`;
                case 'LineString':
                    return `LINESTRING (${coordsToString(coords)})`;
                case 'Polygon':
                    return `POLYGON (${coords.map(ring => `(${coordsToString(ring)})`).join(', ')})`;
                case 'MultiPoint':
                    return `MULTIPOINT (${coords.map(c => `${c[0]} ${c[1]}`).join(', ')})`;
                case 'MultiLineString':
                    return `MULTILINESTRING (${coords.map(line => `(${coordsToString(line)})`).join(', ')})`;
                case 'MultiPolygon':
                    return `MULTIPOLYGON (${coords.map(poly =>
                        `(${poly.map(ring => `(${coordsToString(ring)})`).join(', ')})`
                    ).join(', ')})`;
                default:
                    return null;
            }
        } catch (e) {
            console.error('Error converting geometry to WKT:', e);
            return null;
        }
    }

    /**
     * Helper to convert coordinates array to WKT string
     */
    function coordsToString(coords) {
        return coords.map(c => `${c[0]} ${c[1]}`).join(', ');
    }

    /**
     * Make HTTP request with authentication
     */
    function makeRequest(url, callback) {
        const xhr = new XMLHttpRequest();
        xhr.open('GET', url);

        // Add authentication headers
        if (config.authType === 'bearer' && config.credentials.token) {
            xhr.setRequestHeader('Authorization', `Bearer ${config.credentials.token}`);
        } else if (config.authType === 'apikey' && config.credentials.apiKey) {
            xhr.setRequestHeader('X-API-Key', config.credentials.apiKey);
        } else if (config.authType === 'basic' && config.credentials.username && config.credentials.password) {
            const encoded = btoa(`${config.credentials.username}:${config.credentials.password}`);
            xhr.setRequestHeader('Authorization', `Basic ${encoded}`);
        }

        xhr.setRequestHeader('Accept', 'application/json');

        xhr.onload = function () {
            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    const data = JSON.parse(xhr.responseText);
                    callback(data);
                } catch (e) {
                    console.error('Failed to parse JSON response:', e);
                    callback(null);
                }
            } else {
                console.error('HTTP Error:', xhr.status, xhr.statusText);
                callback(null);
            }
        };

        xhr.onerror = function () {
            console.error('Network error occurred');
            callback(null);
        };

        xhr.send();
    }

    // Register the connector
    tableau.registerConnector(connector);

    // UI Event Handlers
    document.addEventListener('DOMContentLoaded', function () {
        const form = document.getElementById('configForm');
        const dataSourceSelect = document.getElementById('dataSource');
        const collectionGroup = document.getElementById('collectionGroup');
        const authTypeSelect = document.getElementById('authType');
        const tokenGroup = document.getElementById('tokenGroup');
        const apiKeyGroup = document.getElementById('apiKeyGroup');
        const usernameGroup = document.getElementById('usernameGroup');
        const passwordGroup = document.getElementById('passwordGroup');
        const errorMsg = document.getElementById('errorMsg');

        // Show/hide collection field based on data source
        dataSourceSelect.addEventListener('change', function () {
            if (this.value) {
                collectionGroup.style.display = 'block';
                document.getElementById('collectionId').required = true;
            } else {
                collectionGroup.style.display = 'none';
                document.getElementById('collectionId').required = false;
            }
        });

        // Show/hide auth fields based on auth type
        authTypeSelect.addEventListener('change', function () {
            const authType = this.value;
            tokenGroup.style.display = authType === 'bearer' ? 'block' : 'none';
            apiKeyGroup.style.display = authType === 'apikey' ? 'block' : 'none';
            usernameGroup.style.display = authType === 'basic' ? 'block' : 'none';
            passwordGroup.style.display = authType === 'basic' ? 'block' : 'none';
        });

        // Handle form submission
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            errorMsg.style.display = 'none';

            // Gather configuration
            config.serverUrl = document.getElementById('serverUrl').value.trim();
            config.dataSource = document.getElementById('dataSource').value;
            config.collectionId = document.getElementById('collectionId').value.trim();
            config.authType = document.getElementById('authType').value;

            // Gather credentials based on auth type
            config.credentials = {};
            if (config.authType === 'bearer') {
                config.credentials.token = document.getElementById('token').value.trim();
            } else if (config.authType === 'apikey') {
                config.credentials.apiKey = document.getElementById('apiKey').value.trim();
            } else if (config.authType === 'basic') {
                config.credentials.username = document.getElementById('username').value.trim();
                config.credentials.password = document.getElementById('password').value.trim();
            }

            // Validate
            if (!config.serverUrl || !config.dataSource || !config.collectionId) {
                showError('Please fill in all required fields');
                return;
            }

            try {
                new URL(config.serverUrl);
            } catch (e) {
                showError('Invalid server URL');
                return;
            }

            // Set connection data and name
            tableau.connectionData = JSON.stringify(config);
            tableau.connectionName = `Honua ${config.dataSource} - ${config.collectionId}`;

            // Submit the connector
            tableau.submit();
        });

        function showError(message) {
            errorMsg.textContent = message;
            errorMsg.style.display = 'block';
        }
    });
})();
