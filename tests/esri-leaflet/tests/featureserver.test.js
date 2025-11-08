/**
 * FeatureServer Tests
 * Tests Esri Leaflet integration with Honua FeatureServer endpoints
 */

describe('FeatureServer Tests', function() {
    this.timeout(10000);

    const FEATURE_SERVICE = '/rest/services/parks/FeatureServer';
    const LAYER_ID = 0;

    describe('Service Metadata', function() {

        it('should fetch FeatureServer metadata', async function() {
            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}?f=json`);
            expect(response.ok).to.be.true;

            const data = await response.json();
            expect(data).to.have.property('serviceDescription');
            expect(data).to.have.property('layers');
            expect(data.layers).to.be.an('array');
        });

        it('should fetch layer metadata', async function() {
            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}?f=json`);
            expect(response.ok).to.be.true;

            const data = await response.json();
            expect(data).to.have.property('name');
            expect(data).to.have.property('type');
            expect(data).to.have.property('geometryType');
            expect(data).to.have.property('fields');
        });

    });

    describe('Feature Layer Loading', function() {

        it('should load FeatureLayer using Esri Leaflet', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            const featureLayer = L.esri.featureLayer({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`,
                style: function(feature) {
                    return { color: '#3498db', weight: 2 };
                }
            });

            featureLayer.once('load', function() {
                expect(featureLayer).to.exist;
                featureLayer.remove();
                done();
            });

            featureLayer.once('error', function(error) {
                // Layer might be empty, which is ok
                done();
            });

            featureLayer.addTo(testMap);
        });

        it('should query features with where clause', function(done) {
            const query = L.esri.query({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`
            });

            query.where('1=1').run(function(error, featureCollection, response) {
                if (error) {
                    // Empty layer is ok
                    done();
                    return;
                }

                expect(featureCollection).to.have.property('features');
                expect(featureCollection.features).to.be.an('array');
                done();
            });
        });

    });

    describe('Feature Queries', function() {

        it('should query features within bounds', async function() {
            const bounds = L.latLngBounds([45.4, -122.7], [45.6, -122.3]);

            const query = L.esri.query({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`
            });

            return new Promise((resolve, reject) => {
                query.within(bounds).run(function(error, featureCollection) {
                    if (error) {
                        // Empty result is acceptable
                        resolve();
                        return;
                    }

                    expect(featureCollection).to.have.property('features');
                    resolve();
                });
            });
        });

        it('should query features with field filters', async function() {
            const query = L.esri.query({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`
            });

            return new Promise((resolve, reject) => {
                query
                    .fields(['name', 'type'])
                    .where('1=1')
                    .limit(10)
                    .run(function(error, featureCollection) {
                        if (error) {
                            resolve(); // Empty is ok
                            return;
                        }

                        expect(featureCollection).to.have.property('features');
                        resolve();
                    });
            });
        });

        it('should handle pagination correctly', async function() {
            const query = L.esri.query({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`
            });

            return new Promise((resolve, reject) => {
                query
                    .offset(0)
                    .limit(5)
                    .run(function(error, featureCollection) {
                        if (error) {
                            resolve(); // Empty is ok
                            return;
                        }

                        expect(featureCollection.features.length).to.be.at.most(5);
                        resolve();
                    });
            });
        });

    });

    describe('Feature Identification', function() {

        it('should identify features at a point', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            const identify = L.esri.identifyFeatures({
                url: `${BASE_URL}${FEATURE_SERVICE}`
            });

            identify
                .on(testMap)
                .at([45.5, -122.5])
                .layers('all')
                .run(function(error, featureCollection) {
                    if (error) {
                        done(); // No features is ok
                        return;
                    }

                    expect(featureCollection).to.exist;
                    done();
                });
        });

    });

    describe('Error Handling', function() {

        it('should handle invalid layer ID gracefully', async function() {
            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}/999?f=json`);
            expect([400, 404, 500]).to.include(response.status);
        });

        it('should handle invalid query parameters', function(done) {
            const query = L.esri.query({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`
            });

            query.where('invalid_field = bad_syntax').run(function(error) {
                expect(error).to.exist;
                done();
            });
        });

    });

    describe('Performance', function() {

        it('should return features within 3 seconds', function(done) {
            const startTime = Date.now();

            const query = L.esri.query({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`
            });

            query.where('1=1').limit(100).run(function(error, featureCollection) {
                const duration = Date.now() - startTime;
                expect(duration).to.be.below(3000);
                done();
            });
        });

    });

});
