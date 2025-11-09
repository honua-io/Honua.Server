/**
 * Unit tests for HonuaGeometry3D module
 * Tests 3D GeoJSON parsing, coordinate extraction, and dimension detection
 */

// Load the module
const HonuaGeometry3D = require('../honua-geometry-3d.js');

describe('HonuaGeometry3D', () => {
    describe('getZ', () => {
        test('extracts Z coordinate from 3D position', () => {
            const position = [-122.4194, 37.7749, 50.0];
            const z = HonuaGeometry3D.getZ(position);
            expect(z).toBe(50.0);
        });

        test('returns null for 2D position', () => {
            const position = [-122.4194, 37.7749];
            const z = HonuaGeometry3D.getZ(position);
            expect(z).toBeNull();
        });

        test('extracts Z from 4D position', () => {
            const position = [-122.4194, 37.7749, 50.0, 100.0];
            const z = HonuaGeometry3D.getZ(position);
            expect(z).toBe(50.0);
        });
    });

    describe('setZ', () => {
        test('adds Z coordinate to 2D position', () => {
            const position = [-122.4194, 37.7749];
            const result = HonuaGeometry3D.setZ(position, 50.0);
            expect(result).toEqual([-122.4194, 37.7749, 50.0]);
        });

        test('updates Z coordinate in 3D position', () => {
            const position = [-122.4194, 37.7749, 30.0];
            const result = HonuaGeometry3D.setZ(position, 50.0);
            expect(result).toEqual([-122.4194, 37.7749, 50.0]);
        });

        test('does not modify original array when adding Z', () => {
            const position = [-122.4194, 37.7749];
            const result = HonuaGeometry3D.setZ(position, 50.0);
            expect(position).toEqual([-122.4194, 37.7749]);
            expect(result).not.toBe(position);
        });
    });

    describe('removeZ', () => {
        test('converts 3D position to 2D', () => {
            const position = [-122.4194, 37.7749, 50.0];
            const result = HonuaGeometry3D.removeZ(position);
            expect(result).toEqual([-122.4194, 37.7749]);
        });

        test('handles 2D position', () => {
            const position = [-122.4194, 37.7749];
            const result = HonuaGeometry3D.removeZ(position);
            expect(result).toEqual([-122.4194, 37.7749]);
        });
    });

    describe('_detectDimension', () => {
        test('detects 2D Point coordinates', () => {
            const coords = [-122.4194, 37.7749];
            const dimension = HonuaGeometry3D._detectDimension(coords);
            expect(dimension).toBe(2);
        });

        test('detects 3D Point coordinates', () => {
            const coords = [-122.4194, 37.7749, 50.0];
            const dimension = HonuaGeometry3D._detectDimension(coords);
            expect(dimension).toBe(3);
        });

        test('detects 4D Point coordinates', () => {
            const coords = [-122.4194, 37.7749, 50.0, 100.0];
            const dimension = HonuaGeometry3D._detectDimension(coords);
            expect(dimension).toBe(4);
        });

        test('detects dimension for LineString', () => {
            const coords = [
                [-122.4194, 37.7749, 50.0],
                [-122.4184, 37.7759, 60.0]
            ];
            const dimension = HonuaGeometry3D._detectDimension(coords);
            expect(dimension).toBe(3);
        });

        test('detects dimension for Polygon', () => {
            const coords = [[
                [-122.4194, 37.7749, 50.0],
                [-122.4184, 37.7749, 50.0],
                [-122.4184, 37.7759, 50.0],
                [-122.4194, 37.7749, 50.0]
            ]];
            const dimension = HonuaGeometry3D._detectDimension(coords);
            expect(dimension).toBe(3);
        });
    });

    describe('parse3DGeoJSON', () => {
        test('parses 3D Point feature', () => {
            const geojson = {
                type: 'Feature',
                geometry: {
                    type: 'Point',
                    coordinates: [-122.4194, 37.7749, 50.0]
                },
                properties: { name: 'Test Point' }
            };

            const parsed = HonuaGeometry3D.parse3DGeoJSON(geojson);

            expect(parsed.type).toBe('FeatureCollection');
            expect(parsed.features).toHaveLength(1);
            expect(parsed.features[0].properties._hasZ).toBe(true);
            expect(parsed.features[0].properties._dimension).toBe(3);
            expect(parsed.features[0].properties._zMin).toBe(50.0);
            expect(parsed.features[0].properties._zMax).toBe(50.0);
            expect(parsed.metadata.with3D).toBe(1);
            expect(parsed.metadata.without3D).toBe(0);
        });

        test('parses 2D Point feature', () => {
            const geojson = {
                type: 'Feature',
                geometry: {
                    type: 'Point',
                    coordinates: [-122.4194, 37.7749]
                },
                properties: { name: 'Test Point' }
            };

            const parsed = HonuaGeometry3D.parse3DGeoJSON(geojson);

            expect(parsed.features[0].properties._hasZ).toBe(false);
            expect(parsed.features[0].properties._dimension).toBe(2);
            expect(parsed.metadata.with3D).toBe(0);
            expect(parsed.metadata.without3D).toBe(1);
        });

        test('parses 3D LineString feature', () => {
            const geojson = {
                type: 'Feature',
                geometry: {
                    type: 'LineString',
                    coordinates: [
                        [-122.4194, 37.7749, 10.0],
                        [-122.4184, 37.7759, 20.0],
                        [-122.4174, 37.7769, 30.0]
                    ]
                }
            };

            const parsed = HonuaGeometry3D.parse3DGeoJSON(geojson);

            expect(parsed.features[0].properties._hasZ).toBe(true);
            expect(parsed.features[0].properties._zMin).toBe(10.0);
            expect(parsed.features[0].properties._zMax).toBe(30.0);
            expect(parsed.features[0].properties._zRange).toBe(20.0);
        });

        test('parses FeatureCollection with mixed 2D/3D features', () => {
            const geojson = {
                type: 'FeatureCollection',
                features: [
                    {
                        type: 'Feature',
                        geometry: {
                            type: 'Point',
                            coordinates: [-122.4194, 37.7749, 50.0]
                        }
                    },
                    {
                        type: 'Feature',
                        geometry: {
                            type: 'Point',
                            coordinates: [-122.5, 37.8]
                        }
                    }
                ]
            };

            const parsed = HonuaGeometry3D.parse3DGeoJSON(geojson);

            expect(parsed.metadata.total).toBe(2);
            expect(parsed.metadata.with3D).toBe(1);
            expect(parsed.metadata.without3D).toBe(1);
            expect(parsed.metadata.zMin).toBe(50.0);
            expect(parsed.metadata.zMax).toBe(50.0);
        });
    });

    describe('validateZ', () => {
        test('validates Z within default range', () => {
            expect(HonuaGeometry3D.validateZ(50.0)).toBe(true);
            expect(HonuaGeometry3D.validateZ(100.0)).toBe(true);
            expect(HonuaGeometry3D.validateZ(-100.0)).toBe(true);
        });

        test('rejects Z above maximum', () => {
            expect(HonuaGeometry3D.validateZ(10000.0)).toBe(false);
        });

        test('rejects Z below minimum', () => {
            expect(HonuaGeometry3D.validateZ(-600.0)).toBe(false);
        });

        test('validates Z with custom range', () => {
            expect(HonuaGeometry3D.validateZ(50.0, 0, 100)).toBe(true);
            expect(HonuaGeometry3D.validateZ(150.0, 0, 100)).toBe(false);
        });
    });

    describe('getZStatistics', () => {
        test('calculates statistics for 3D Point', () => {
            const geometry = {
                type: 'Point',
                coordinates: [-122.4194, 37.7749, 50.0]
            };

            const stats = HonuaGeometry3D.getZStatistics(geometry);

            expect(stats).not.toBeNull();
            expect(stats.min).toBe(50.0);
            expect(stats.max).toBe(50.0);
            expect(stats.mean).toBe(50.0);
            expect(stats.median).toBe(50.0);
            expect(stats.range).toBe(0);
            expect(stats.count).toBe(1);
        });

        test('calculates statistics for 3D LineString', () => {
            const geometry = {
                type: 'LineString',
                coordinates: [
                    [-122.4194, 37.7749, 10.0],
                    [-122.4184, 37.7759, 20.0],
                    [-122.4174, 37.7769, 30.0]
                ]
            };

            const stats = HonuaGeometry3D.getZStatistics(geometry);

            expect(stats.min).toBe(10.0);
            expect(stats.max).toBe(30.0);
            expect(stats.mean).toBe(20.0);
            expect(stats.range).toBe(20.0);
            expect(stats.count).toBe(3);
        });

        test('returns null for 2D geometry', () => {
            const geometry = {
                type: 'Point',
                coordinates: [-122.4194, 37.7749]
            };

            const stats = HonuaGeometry3D.getZStatistics(geometry);

            expect(stats).toBeNull();
        });
    });

    describe('isValid3D', () => {
        test('validates 3D Point with valid Z', () => {
            const geometry = {
                type: 'Point',
                coordinates: [-122.4194, 37.7749, 50.0]
            };

            expect(HonuaGeometry3D.isValid3D(geometry)).toBe(true);
        });

        test('rejects 2D geometry', () => {
            const geometry = {
                type: 'Point',
                coordinates: [-122.4194, 37.7749]
            };

            expect(HonuaGeometry3D.isValid3D(geometry)).toBe(false);
        });

        test('rejects geometry with invalid Z (NaN)', () => {
            const geometry = {
                type: 'Point',
                coordinates: [-122.4194, 37.7749, NaN]
            };

            expect(HonuaGeometry3D.isValid3D(geometry)).toBe(false);
        });

        test('rejects geometry with invalid Z (Infinity)', () => {
            const geometry = {
                type: 'Point',
                coordinates: [-122.4194, 37.7749, Infinity]
            };

            expect(HonuaGeometry3D.isValid3D(geometry)).toBe(false);
        });
    });

    describe('getOgcTypeName', () => {
        test('returns Point for 2D Point', () => {
            const geometry = {
                type: 'Point',
                coordinates: [-122.4194, 37.7749]
            };

            expect(HonuaGeometry3D.getOgcTypeName(geometry)).toBe('Point');
        });

        test('returns PointZ for 3D Point', () => {
            const geometry = {
                type: 'Point',
                coordinates: [-122.4194, 37.7749, 50.0]
            };

            expect(HonuaGeometry3D.getOgcTypeName(geometry)).toBe('PointZ');
        });

        test('returns PointZM for 4D Point', () => {
            const geometry = {
                type: 'Point',
                coordinates: [-122.4194, 37.7749, 50.0, 100.0]
            };

            expect(HonuaGeometry3D.getOgcTypeName(geometry)).toBe('PointZM');
        });

        test('returns LineStringZ for 3D LineString', () => {
            const geometry = {
                type: 'LineString',
                coordinates: [
                    [-122.4194, 37.7749, 50.0],
                    [-122.4184, 37.7759, 60.0]
                ]
            };

            expect(HonuaGeometry3D.getOgcTypeName(geometry)).toBe('LineStringZ');
        });
    });
});
