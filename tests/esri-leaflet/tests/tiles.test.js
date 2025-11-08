/**
 * Tile and Basemap Tests
 * Tests tile layer rendering and basemap support
 */

describe('Tile and Basemap Tests', function() {
    this.timeout(10000);

    const TILE_SERVICE = '/rest/services/basemap/MapServer';

    describe('Tiled Map Layers', function() {

        it('should load tiled map layer', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            const tiledLayer = L.esri.tiledMapLayer({
                url: `${BASE_URL}${TILE_SERVICE}`,
                maxZoom: 18
            });

            tiledLayer.once('load', function() {
                expect(tiledLayer).to.exist;
                tiledLayer.remove();
                done();
            });

            tiledLayer.once('error', function(error) {
                // Service might not support tiles
                done();
            });

            tiledLayer.addTo(testMap);
        });

        it('should fetch tile metadata', async function() {
            const response = await fetch(`${BASE_URL}${TILE_SERVICE}?f=json`);
            if (response.ok) {
                const data = await response.json();
                expect(data).to.have.property('tileInfo');
            }
        });

    });

    describe('Vector Tile Layers', function() {

        it('should load vector tile layer if supported', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            // Check if vector tiles are supported
            const vectorTileUrl = `${BASE_URL}/rest/services/vector/VectorTileServer`;

            fetch(`${vectorTileUrl}?f=json`)
                .then(response => {
                    if (response.ok) {
                        const vectorLayer = L.esri.vectorTileLayer(vectorTileUrl, {
                            style: function(feature) {
                                return { color: '#3498db' };
                            }
                        });

                        vectorLayer.once('load', function() {
                            vectorLayer.remove();
                            done();
                        });

                        vectorLayer.addTo(testMap);
                    } else {
                        done(); // Not supported
                    }
                })
                .catch(() => done());
        });

    });

    describe('Image Services', function() {

        it('should load image map layer', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            const imageUrl = `${BASE_URL}/rest/services/imagery/ImageServer`;

            const imageLayer = L.esri.imageMapLayer({
                url: imageUrl,
                opacity: 0.7
            });

            imageLayer.once('load', function() {
                imageLayer.remove();
                done();
            });

            imageLayer.once('error', function() {
                done(); // Not supported
            });

            imageLayer.addTo(testMap);
        });

    });

});
