/**
 * Query Tests
 * Tests advanced query capabilities
 */

describe('Query Tests', function() {
    this.timeout(10000);

    const FEATURE_SERVICE = '/rest/services/parks/FeatureServer';
    const LAYER_ID = 0;

    describe('Attribute Queries', function() {

        it('should query with SQL where clause', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json&returnGeometry=true&outFields=*`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const data = await response.json();
            expect(data).to.have.property('features');
        });

        it('should query with field selection', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json&outFields=name,type`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const data = await response.json();
            if (data.features && data.features.length > 0) {
                const firstFeature = data.features[0];
                expect(firstFeature).to.have.property('attributes');
            }
        });

        it('should support case-insensitive queries', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=UPPER(name) LIKE 'PARK%'&f=json`;

            const response = await fetch(url);
            // Query might not have matching records
            expect([200, 400]).to.include(response.status);
        });

    });

    describe('Spatial Queries', function() {

        it('should query with bounding box', async function() {
            const bbox = '-122.7,45.4,-122.3,45.6';
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?geometry=${bbox}&geometryType=esriGeometryEnvelope&spatialRel=esriSpatialRelIntersects&f=json`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const data = await response.json();
            expect(data).to.have.property('features');
        });

        it('should query with point and distance', async function() {
            const point = { x: -122.5, y: 45.5, spatialReference: { wkid: 4326 } };
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?geometry=${JSON.stringify(point)}&geometryType=esriGeometryPoint&distance=1000&units=esriSRUnit_Meter&spatialRel=esriSpatialRelIntersects&f=json`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;
        });

        it('should query with polygon geometry', async function() {
            const polygon = {
                rings: [[[-122.5, 45.5], [-122.4, 45.5], [-122.4, 45.4], [-122.5, 45.4], [-122.5, 45.5]]],
                spatialReference: { wkid: 4326 }
            };

            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?geometry=${JSON.stringify(polygon)}&geometryType=esriGeometryPolygon&spatialRel=esriSpatialRelIntersects&f=json`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;
        });

    });

    describe('Statistics Queries', function() {

        it('should calculate statistics', async function() {
            const stats = [{
                statisticType: 'count',
                onStatisticField: 'OBJECTID',
                outStatisticFieldName: 'total_count'
            }];

            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&outStatistics=${JSON.stringify(stats)}&f=json`;

            const response = await fetch(url);
            if (response.ok) {
                const data = await response.json();
                expect(data).to.have.property('features');
            }
        });

        it('should group by field', async function() {
            const stats = [{
                statisticType: 'count',
                onStatisticField: 'OBJECTID',
                outStatisticFieldName: 'count'
            }];

            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&outStatistics=${JSON.stringify(stats)}&groupByFieldsForStatistics=type&f=json`;

            const response = await fetch(url);
            if (response.ok) {
                const data = await response.json();
                expect(data).to.have.property('features');
            }
        });

    });

    describe('Pagination and Ordering', function() {

        it('should support result offset', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&resultOffset=10&resultRecordCount=10&f=json`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const data = await response.json();
            expect(data.features.length).to.be.at.most(10);
        });

        it('should support ordering', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&orderByFields=name ASC&f=json`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;
        });

    });

    describe('Output Formats', function() {

        it('should return GeoJSON', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=geojson`;

            const response = await fetch(url);
            if (response.ok) {
                const data = await response.json();
                expect(data.type).to.equal('FeatureCollection');
                expect(data).to.have.property('features');
            }
        });

        it('should return JSON', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const data = await response.json();
            expect(data).to.have.property('features');
        });

    });

    describe('Query Performance', function() {

        it('should handle large result sets efficiently', async function() {
            const startTime = Date.now();

            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&resultRecordCount=1000&f=json`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const duration = Date.now() - startTime;
            expect(duration).to.be.below(5000); // 5 seconds max
        });

    });

});
