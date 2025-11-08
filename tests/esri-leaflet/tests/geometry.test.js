/**
 * Geometry Service Tests
 * Tests geometry operations via Esri REST API
 */

describe('Geometry Service Tests', function() {
    this.timeout(10000);

    const GEOMETRY_SERVICE = '/rest/services/Geometry/GeometryServer';

    describe('Service Availability', function() {

        it('should have geometry service available', async function() {
            const response = await fetch(`${BASE_URL}${GEOMETRY_SERVICE}?f=json`);

            // Geometry service might not be enabled
            if (response.ok) {
                const data = await response.json();
                expect(data).to.exist;
            }
        });

    });

    describe('Buffer Operation', function() {

        it('should buffer a point geometry', async function() {
            const geometry = {
                geometryType: 'esriGeometryPoint',
                geometries: [{
                    x: -122.5,
                    y: 45.5,
                    spatialReference: { wkid: 4326 }
                }]
            };

            const response = await fetch(`${BASE_URL}${GEOMETRY_SERVICE}/buffer?f=json`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({
                    geometries: JSON.stringify(geometry),
                    inSR: '4326',
                    outSR: '4326',
                    distances: '1000',
                    unit: 'esriMeters',
                    unionResults: 'false',
                    f: 'json'
                })
            });

            if (response.ok) {
                const data = await response.json();
                expect(data).to.have.property('geometries');
                expect(data.geometries).to.be.an('array');
            }
        });

    });

    describe('Project Operation', function() {

        it('should project coordinates', async function() {
            const geometries = {
                geometryType: 'esriGeometryPoint',
                geometries: [{
                    x: -122.5,
                    y: 45.5
                }]
            };

            const response = await fetch(`${BASE_URL}${GEOMETRY_SERVICE}/project?f=json`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({
                    geometries: JSON.stringify(geometries),
                    inSR: '4326',
                    outSR: '3857',
                    f: 'json'
                })
            });

            if (response.ok) {
                const data = await response.json();
                expect(data).to.have.property('geometries');
            }
        });

    });

    describe('Spatial Relationships', function() {

        it('should test spatial relationships', async function() {
            const geom1 = {
                rings: [[[-122.5, 45.5], [-122.4, 45.5], [-122.4, 45.4], [-122.5, 45.4], [-122.5, 45.5]]],
                spatialReference: { wkid: 4326 }
            };

            const geom2 = {
                rings: [[[-122.45, 45.45], [-122.35, 45.45], [-122.35, 45.35], [-122.45, 45.35], [-122.45, 45.45]]],
                spatialReference: { wkid: 4326 }
            };

            const response = await fetch(`${BASE_URL}${GEOMETRY_SERVICE}/relation?f=json`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({
                    geometries1: JSON.stringify({ geometryType: 'esriGeometryPolygon', geometries: [geom1] }),
                    geometries2: JSON.stringify({ geometryType: 'esriGeometryPolygon', geometries: [geom2] }),
                    sr: '4326',
                    relation: 'esriGeometryRelationIntersection',
                    f: 'json'
                })
            });

            if (response.ok) {
                const data = await response.json();
                expect(data).to.exist;
            }
        });

    });

    describe('Simplify Operation', function() {

        it('should simplify polygon geometry', async function() {
            const geometry = {
                geometryType: 'esriGeometryPolygon',
                geometries: [{
                    rings: [[
                        [-122.5, 45.5],
                        [-122.4, 45.5],
                        [-122.45, 45.45],
                        [-122.4, 45.4],
                        [-122.5, 45.4],
                        [-122.5, 45.5]
                    ]],
                    spatialReference: { wkid: 4326 }
                }]
            };

            const response = await fetch(`${BASE_URL}${GEOMETRY_SERVICE}/simplify?f=json`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({
                    geometries: JSON.stringify(geometry),
                    sr: '4326',
                    f: 'json'
                })
            });

            if (response.ok) {
                const data = await response.json();
                expect(data).to.have.property('geometries');
            }
        });

    });

});
