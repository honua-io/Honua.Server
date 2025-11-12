// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Honua MapSDK - Esri REST API Integration
 * Supports ArcGIS FeatureServer and MapServer
 */

import { mapRegistry } from './honua-map.js';

/**
 * Create an Esri FeatureServer layer
 */
export function createEsriFeatureLayer(mapId, options, dotNetRef) {
    const map = mapRegistry.get(mapId);
    if (!map) {
        throw new Error(`Map with ID ${mapId} not found`);
    }

    const { sourceId, layerId, geoJson, opacity, minZoom, maxZoom, geometryType, drawingInfo } = options;

    // Parse GeoJSON
    const data = typeof geoJson === 'string' ? JSON.parse(geoJson) : geoJson;

    // Add source
    map.addSource(sourceId, {
        type: 'geojson',
        data: data
    });

    // Determine layer type and style from geometry type and drawing info
    const layerStyle = getLayerStyle(geometryType, drawingInfo, opacity);

    // Add layer
    map.addLayer({
        id: layerId,
        type: layerStyle.type,
        source: sourceId,
        paint: layerStyle.paint,
        layout: layerStyle.layout || {},
        minzoom: minZoom,
        maxzoom: maxZoom
    });

    // Add click handler
    map.on('click', layerId, (e) => {
        if (e.features && e.features.length > 0) {
            const feature = e.features[0];
            const esriFeature = {
                geometry: convertToEsriGeometry(feature.geometry),
                attributes: feature.properties || {}
            };
            dotNetRef.invokeMethodAsync('OnFeatureClickCallback', JSON.stringify(esriFeature));
        }
    });

    // Change cursor on hover
    map.on('mouseenter', layerId, () => {
        map.getCanvas().style.cursor = 'pointer';
    });

    map.on('mouseleave', layerId, () => {
        map.getCanvas().style.cursor = '';
    });

    return {
        sourceId,
        layerId,

        setOpacity(opacity) {
            const paintProperty = getPaintOpacityProperty(layerStyle.type);
            if (paintProperty) {
                map.setPaintProperty(layerId, paintProperty, opacity);
            }
        },

        setVisibility(visible) {
            map.setLayoutProperty(layerId, 'visibility', visible ? 'visible' : 'none');
        },

        updateData(geoJsonString) {
            const newData = JSON.parse(geoJsonString);
            const source = map.getSource(sourceId);
            if (source) {
                source.setData(newData);
            }
        },

        dispose() {
            if (map.getLayer(layerId)) {
                map.removeLayer(layerId);
            }
            if (map.getSource(sourceId)) {
                map.removeSource(sourceId);
            }
        }
    };
}

/**
 * Create an Esri MapServer layer (tiled or dynamic)
 */
export function createEsriMapServerLayer(mapId, options, dotNetRef) {
    const map = mapRegistry.get(mapId);
    if (!map) {
        throw new Error(`Map with ID ${mapId} not found`);
    }

    const { sourceId, layerId, serviceUrl, useTiles, layers, format, transparent, opacity, token, minZoom, maxZoom } = options;

    let tiles;
    if (useTiles) {
        // Use tile service
        tiles = [
            `${serviceUrl}/tile/{z}/{y}/{x}${token ? `?token=${token}` : ''}`
        ];
    } else {
        // Use export image (dynamic service)
        // This requires a custom tile URL that calls the export endpoint
        tiles = [
            buildExportTileUrl(serviceUrl, layers, format, transparent, token)
        ];
    }

    // Add source
    map.addSource(sourceId, {
        type: 'raster',
        tiles: tiles,
        tileSize: 256,
        scheme: 'xyz'
    });

    // Add layer
    map.addLayer({
        id: layerId,
        type: 'raster',
        source: sourceId,
        paint: {
            'raster-opacity': opacity
        },
        minzoom: minZoom,
        maxzoom: maxZoom
    });

    // Add click handler for identify
    map.on('click', (e) => {
        const canvas = map.getCanvas();
        const point = e.point;
        const bounds = map.getBounds();
        const bbox = `${bounds.getWest()},${bounds.getSouth()},${bounds.getEast()},${bounds.getNorth()}`;
        const mapExtent = bbox;

        dotNetRef.invokeMethodAsync('OnIdentifyCallback',
            e.lngLat.lng,
            e.lngLat.lat,
            canvas.width,
            canvas.height,
            bbox,
            mapExtent
        );
    });

    return {
        sourceId,
        layerId,

        setOpacity(opacity) {
            map.setPaintProperty(layerId, 'raster-opacity', opacity);
        },

        setVisibility(visible) {
            map.setLayoutProperty(layerId, 'visibility', visible ? 'visible' : 'none');
        },

        updateLayers(layersParam) {
            // For dynamic layers, we'd need to rebuild the tile URL
            // This is a simplified version
            console.log('Update layers:', layersParam);
        },

        refresh() {
            // Force refresh by removing and re-adding source
            const layer = map.getLayer(layerId);
            const source = map.getSource(sourceId);
            if (layer && source) {
                map.removeLayer(layerId);
                map.removeSource(sourceId);

                map.addSource(sourceId, {
                    type: 'raster',
                    tiles: tiles,
                    tileSize: 256,
                    scheme: 'xyz'
                });

                map.addLayer({
                    id: layerId,
                    type: 'raster',
                    source: sourceId,
                    paint: {
                        'raster-opacity': opacity
                    },
                    minzoom: minZoom,
                    maxzoom: maxZoom
                });
            }
        },

        dispose() {
            if (map.getLayer(layerId)) {
                map.removeLayer(layerId);
            }
            if (map.getSource(sourceId)) {
                map.removeSource(sourceId);
            }
        }
    };
}

/**
 * Get layer style from Esri geometry type and drawing info
 */
function getLayerStyle(geometryType, drawingInfo, opacity) {
    const baseStyle = {
        type: 'circle',
        paint: {}
    };

    // Determine layer type from geometry
    if (geometryType?.includes('Point')) {
        baseStyle.type = 'circle';
        baseStyle.paint = {
            'circle-radius': 6,
            'circle-color': '#3388ff',
            'circle-opacity': opacity,
            'circle-stroke-width': 2,
            'circle-stroke-color': '#ffffff'
        };
    } else if (geometryType?.includes('Polyline') || geometryType?.includes('Line')) {
        baseStyle.type = 'line';
        baseStyle.paint = {
            'line-color': '#3388ff',
            'line-width': 2,
            'line-opacity': opacity
        };
    } else if (geometryType?.includes('Polygon')) {
        // For polygons, we need both fill and outline
        baseStyle.type = 'fill';
        baseStyle.paint = {
            'fill-color': '#3388ff',
            'fill-opacity': opacity * 0.5,
            'fill-outline-color': '#3388ff'
        };
    }

    // Apply Esri renderer if available
    if (drawingInfo && drawingInfo.renderer) {
        applyEsriRenderer(baseStyle, drawingInfo.renderer, opacity);
    }

    return baseStyle;
}

/**
 * Apply Esri renderer to MapLibre style
 */
function applyEsriRenderer(style, renderer, opacity) {
    if (renderer.type === 'simple' && renderer.symbol) {
        const symbol = renderer.symbol;

        if (style.type === 'circle' && symbol.color) {
            const color = esriColorToMapLibre(symbol.color);
            style.paint['circle-color'] = color;
            if (symbol.size) {
                style.paint['circle-radius'] = symbol.size / 2;
            }
        } else if (style.type === 'line' && symbol.color) {
            const color = esriColorToMapLibre(symbol.color);
            style.paint['line-color'] = color;
            if (symbol.width) {
                style.paint['line-width'] = symbol.width;
            }
        } else if (style.type === 'fill' && symbol.color) {
            const color = esriColorToMapLibre(symbol.color);
            style.paint['fill-color'] = color;
            if (symbol.outline && symbol.outline.color) {
                style.paint['fill-outline-color'] = esriColorToMapLibre(symbol.outline.color);
            }
        }
    }
}

/**
 * Convert Esri color [r, g, b, a] to MapLibre rgba string
 */
function esriColorToMapLibre(esriColor) {
    if (Array.isArray(esriColor) && esriColor.length >= 3) {
        const r = esriColor[0];
        const g = esriColor[1];
        const b = esriColor[2];
        const a = esriColor.length > 3 ? esriColor[3] / 255 : 1;
        return `rgba(${r}, ${g}, ${b}, ${a})`;
    }
    return '#3388ff'; // Default color
}

/**
 * Get paint property name for opacity based on layer type
 */
function getPaintOpacityProperty(layerType) {
    const opacityMap = {
        'circle': 'circle-opacity',
        'line': 'line-opacity',
        'fill': 'fill-opacity',
        'symbol': 'icon-opacity',
        'raster': 'raster-opacity'
    };
    return opacityMap[layerType];
}

/**
 * Convert GeoJSON geometry to Esri geometry
 */
function convertToEsriGeometry(geoJsonGeometry) {
    if (!geoJsonGeometry) return null;

    const { type, coordinates } = geoJsonGeometry;

    switch (type) {
        case 'Point':
            return {
                x: coordinates[0],
                y: coordinates[1],
                z: coordinates.length > 2 ? coordinates[2] : null,
                spatialReference: { wkid: 4326 }
            };

        case 'MultiPoint':
            return {
                points: coordinates,
                spatialReference: { wkid: 4326 }
            };

        case 'LineString':
            return {
                paths: [coordinates],
                spatialReference: { wkid: 4326 }
            };

        case 'MultiLineString':
            return {
                paths: coordinates,
                spatialReference: { wkid: 4326 }
            };

        case 'Polygon':
            return {
                rings: coordinates,
                spatialReference: { wkid: 4326 }
            };

        case 'MultiPolygon':
            // Flatten multipolygon into single polygon with multiple rings
            return {
                rings: coordinates.flat(),
                spatialReference: { wkid: 4326 }
            };

        default:
            return null;
    }
}

/**
 * Build export tile URL for dynamic MapServer
 */
function buildExportTileUrl(serviceUrl, layers, format, transparent, token) {
    // This would build a URL template for the export endpoint
    // In practice, you'd need server-side support to convert tile coordinates to bbox
    // For now, return a basic tile template
    const baseUrl = serviceUrl.replace('/MapServer', '/MapServer/export');
    const params = new URLSearchParams({
        bbox: '{bbox-epsg-3857}',
        size: '256,256',
        format: `image/${format}`,
        transparent: transparent,
        f: 'image',
        layers: layers
    });

    if (token) {
        params.append('token', token);
    }

    return `${baseUrl}?${params.toString()}`;
}

export default {
    createEsriFeatureLayer,
    createEsriMapServerLayer
};
