/**
 * Export Tests
 * Tests various export formats and operations
 */

describe('Export Tests', function() {
    this.timeout(15000);

    const FEATURE_SERVICE = '/rest/services/parks/FeatureServer';
    const LAYER_ID = 0;

    describe('Feature Export Formats', function() {

        it('should export as GeoJSON', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=geojson&outFields=*`;

            const response = await fetch(url);
            if (response.ok) {
                const data = await response.json();
                expect(data.type).to.equal('FeatureCollection');
                expect(data).to.have.property('features');
            }
        });

        it('should export as JSON (Esri Format)', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json&outFields=*`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const data = await response.json();
            expect(data).to.have.property('features');
            expect(data).to.have.property('spatialReference');
        });

        it('should export as CSV (if supported)', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=csv&outFields=*`;

            const response = await fetch(url);
            if (response.ok) {
                const contentType = response.headers.get('content-type');
                expect(contentType).to.match(/csv|text/);
            }
        });

        it('should export as KML (if supported)', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=kmz`;

            const response = await fetch(url);
            // KML export might not be supported
            if (response.ok) {
                expect(response.headers.get('content-type')).to.exist;
            }
        });

    });

    describe('Coordinate Reference Systems', function() {

        it('should export in WGS84 (EPSG:4326)', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json&outSR=4326`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const data = await response.json();
            if (data.spatialReference) {
                expect(data.spatialReference.wkid).to.equal(4326);
            }
        });

        it('should export in Web Mercator (EPSG:3857)', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json&outSR=3857`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const data = await response.json();
            if (data.spatialReference) {
                expect(data.spatialReference.wkid).to.equal(3857);
            }
        });

    });

    describe('Geometry Types', function() {

        it('should export with full geometry', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json&returnGeometry=true`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const data = await response.json();
            if (data.features && data.features.length > 0) {
                expect(data.features[0]).to.have.property('geometry');
            }
        });

        it('should export without geometry (attributes only)', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json&returnGeometry=false`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const data = await response.json();
            if (data.features && data.features.length > 0) {
                expect(data.features[0]).to.not.have.property('geometry');
                expect(data.features[0]).to.have.property('attributes');
            }
        });

        it('should export with envelope only', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json&returnGeometry=true&geometryPrecision=6`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;
        });

    });

    describe('Large Dataset Export', function() {

        it('should handle export of large datasets with pagination', async function() {
            const pageSize = 100;
            let allFeatures = [];
            let offset = 0;
            let hasMore = true;

            while (hasMore && offset < 500) { // Limit to 500 total for test
                const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json&resultOffset=${offset}&resultRecordCount=${pageSize}`;

                const response = await fetch(url);
                expect(response.ok).to.be.true;

                const data = await response.json();

                if (data.features && data.features.length > 0) {
                    allFeatures = allFeatures.concat(data.features);
                    offset += data.features.length;

                    if (data.features.length < pageSize) {
                        hasMore = false;
                    }
                } else {
                    hasMore = false;
                }
            }

            expect(allFeatures).to.be.an('array');
        });

    });

    describe('Field Customization', function() {

        it('should export specific fields only', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json&outFields=name,type&returnGeometry=false`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const data = await response.json();
            if (data.features && data.features.length > 0) {
                const attrs = data.features[0].attributes;
                // Should only have requested fields
                expect(Object.keys(attrs).length).to.be.at.most(3); // name, type, maybe OBJECTID
            }
        });

    });

    describe('Export Performance', function() {

        it('should complete export within reasonable time', async function() {
            const startTime = Date.now();

            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=geojson&resultRecordCount=500`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            const duration = Date.now() - startTime;
            expect(duration).to.be.below(10000); // 10 seconds max
        });

        it('should stream large exports efficiently', async function() {
            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&f=json&resultRecordCount=1000`;

            const response = await fetch(url);
            expect(response.ok).to.be.true;

            // Check if streaming is supported
            const reader = response.body.getReader();
            expect(reader).to.exist;
        });

    });

});
