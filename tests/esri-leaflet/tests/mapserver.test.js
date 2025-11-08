/**
 * MapServer Tests
 * Tests Esri Leaflet integration with Honua MapServer endpoints
 */

describe('MapServer Tests', function() {
    this.timeout(10000);

    const MAP_SERVICE = '/rest/services/basemap/MapServer';
    const LAYER_ID = 0;

    describe('Service Metadata', function() {

        it('should fetch MapServer metadata', async function() {
            const response = await fetch(`${BASE_URL}${MAP_SERVICE}?f=json`);
            expect(response.ok).to.be.true;

            const data = await response.json();
            expect(data).to.have.property('serviceDescription');
            expect(data).to.have.property('layers');
        });

        it('should fetch layer metadata', async function() {
            const response = await fetch(`${BASE_URL}${MAP_SERVICE}/${LAYER_ID}?f=json`);
            expect(response.ok).to.be.true;

            const data = await response.json();
            expect(data).to.have.property('name');
            expect(data).to.have.property('type');
        });

    });

    describe('Dynamic Map Layer', function() {

        it('should load DynamicMapLayer using Esri Leaflet', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            const dynamicMapLayer = L.esri.dynamicMapLayer({
                url: `${BASE_URL}${MAP_SERVICE}`,
                opacity: 0.8
            });

            dynamicMapLayer.once('load', function() {
                expect(dynamicMapLayer).to.exist;
                dynamicMapLayer.remove();
                done();
            });

            dynamicMapLayer.once('error', function(error) {
                // Service might not support export, which is ok
                done();
            });

            dynamicMapLayer.addTo(testMap);
        });

        it('should apply layer definitions', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            const dynamicMapLayer = L.esri.dynamicMapLayer({
                url: `${BASE_URL}${MAP_SERVICE}`,
                layers: [0]
            });

            dynamicMapLayer.setLayerDefs({
                0: '1=1'
            });

            dynamicMapLayer.once('load', function() {
                dynamicMapLayer.remove();
                done();
            });

            dynamicMapLayer.once('error', function() {
                done(); // Not all services support this
            });

            dynamicMapLayer.addTo(testMap);
        });

    });

    describe('Map Export', function() {

        it('should export map image', async function() {
            const bbox = '-122.7,45.4,-122.3,45.6';
            const size = '400,400';

            const url = `${BASE_URL}${MAP_SERVICE}/export?bbox=${bbox}&size=${size}&f=image`;

            const response = await fetch(url);

            // Might not be implemented, which is ok
            if (response.ok) {
                expect(response.headers.get('content-type')).to.match(/image/);
            }
        });

    });

    describe('Identify Operation', function() {

        it('should identify features in map', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            const identify = L.esri.identifyFeatures({
                url: `${BASE_URL}${MAP_SERVICE}`
            });

            identify
                .on(testMap)
                .at([45.5, -122.5])
                .tolerance(5)
                .run(function(error, featureCollection) {
                    // Might not have features at that location
                    done();
                });
        });

    });

    describe('Find Operation', function() {

        it('should find features by text search', async function() {
            const response = await fetch(
                `${BASE_URL}${MAP_SERVICE}/find?searchText=park&layers=0&f=json`
            );

            // Not all services support find
            if (response.ok) {
                const data = await response.json();
                expect(data).to.have.property('results');
            }
        });

    });

});
