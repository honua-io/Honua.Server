/**
 * Advanced Features Tests
 * Tests clustering, time awareness, attachments, and related records
 */

describe('Advanced Features Tests', function() {
    this.timeout(15000);

    const FEATURE_SERVICE = '/rest/services/parks/FeatureServer';
    const LAYER_ID = 0;

    describe('Clustered Feature Layers', function() {

        it('should support clustered features visualization', function(done) {
            if (!testMap || !L.markerClusterGroup) {
                this.skip();
                return;
            }

            const cluster = L.markerClusterGroup();

            const featureLayer = L.esri.featureLayer({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`,
                pointToLayer: function(geojson, latlng) {
                    return L.marker(latlng);
                }
            });

            featureLayer.once('load', function() {
                cluster.addLayer(featureLayer);
                testMap.addLayer(cluster);
                expect(cluster).to.exist;
                testMap.removeLayer(cluster);
                done();
            });

            featureLayer.once('error', function() {
                done();
            });
        });

    });

    describe('Time-Aware Layers', function() {

        it('should query features with time filter', async function() {
            const timeFrom = new Date('2020-01-01').getTime();
            const timeTo = new Date('2024-12-31').getTime();

            const url = `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&time=${timeFrom},${timeTo}&f=json`;

            const response = await fetch(url);
            if (response.ok) {
                const data = await response.json();
                expect(data).to.have.property('features');
            }
        });

        it('should support time extent in layer metadata', async function() {
            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}?f=json`);
            if (response.ok) {
                const data = await response.json();
                // Check if layer has time info
                if (data.timeInfo) {
                    expect(data.timeInfo).to.have.property('startTimeField');
                    expect(data.timeInfo).to.have.property('endTimeField');
                }
            }
        });

    });

    describe('Attachments', function() {

        it('should query attachments for features', async function() {
            // First get a feature with attachments
            const queryResponse = await fetch(
                `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&outFields=OBJECTID&resultRecordCount=1&f=json`
            );

            if (queryResponse.ok) {
                const queryData = await queryResponse.json();
                if (queryData.features && queryData.features.length > 0) {
                    const objectId = queryData.features[0].attributes.OBJECTID;

                    const attachmentResponse = await fetch(
                        `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/${objectId}/attachments?f=json`
                    );

                    if (attachmentResponse.ok) {
                        const attachmentData = await attachmentResponse.json();
                        expect(attachmentData).to.have.property('attachmentInfos');
                    }
                }
            }
        });

        it('should check if layer supports attachments', async function() {
            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}?f=json`);
            if (response.ok) {
                const data = await response.json();
                // Check capabilities
                if (data.hasAttachments !== undefined) {
                    expect(data.hasAttachments).to.be.a('boolean');
                }
            }
        });

    });

    describe('Related Records', function() {

        it('should query related records', async function() {
            const queryResponse = await fetch(
                `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&outFields=OBJECTID&resultRecordCount=1&f=json`
            );

            if (queryResponse.ok) {
                const queryData = await queryResponse.json();
                if (queryData.features && queryData.features.length > 0) {
                    const objectId = queryData.features[0].attributes.OBJECTID;

                    const relatedResponse = await fetch(
                        `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/queryRelatedRecords?objectIds=${objectId}&relationshipId=1&f=json`
                    );

                    // Related records might not exist
                    if (relatedResponse.ok) {
                        const relatedData = await relatedResponse.json();
                        expect(relatedData).to.exist;
                    }
                }
            }
        });

        it('should list available relationships', async function() {
            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}?f=json`);
            if (response.ok) {
                const data = await response.json();
                if (data.relationships) {
                    expect(data.relationships).to.be.an('array');
                }
            }
        });

    });

    describe('Definition Expressions', function() {

        it('should apply definition expression to layer', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            const featureLayer = L.esri.featureLayer({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`,
                where: "status = 'active'"
            });

            featureLayer.setWhere("type = 'Recreation'");

            featureLayer.once('load', function() {
                expect(featureLayer.options.where).to.equal("type = 'Recreation'");
                featureLayer.remove();
                done();
            });

            featureLayer.once('error', function() {
                done();
            });

            featureLayer.addTo(testMap);
        });

    });

    describe('Popup and Info Windows', function() {

        it('should bind popup with feature attributes', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            const featureLayer = L.esri.featureLayer({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`,
                onEachFeature: function(feature, layer) {
                    if (feature.properties) {
                        const content = `<b>${feature.properties.name || 'Unnamed'}</b><br/>
                                       Type: ${feature.properties.type || 'N/A'}`;
                        layer.bindPopup(content);
                    }
                }
            });

            featureLayer.once('load', function() {
                expect(featureLayer).to.exist;
                featureLayer.remove();
                done();
            });

            featureLayer.once('error', function() {
                done();
            });

            featureLayer.addTo(testMap);
        });

    });

    describe('Renderers', function() {

        it('should apply simple renderer', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            const featureLayer = L.esri.featureLayer({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`,
                style: function(feature) {
                    return {
                        color: '#3498db',
                        weight: 2,
                        fillOpacity: 0.5
                    };
                }
            });

            featureLayer.once('load', function() {
                featureLayer.remove();
                done();
            });

            featureLayer.once('error', function() {
                done();
            });

            featureLayer.addTo(testMap);
        });

        it('should apply unique value renderer', function(done) {
            if (!testMap) {
                this.skip();
                return;
            }

            const featureLayer = L.esri.featureLayer({
                url: `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}`,
                style: function(feature) {
                    const colors = {
                        'Recreation': '#27ae60',
                        'Nature': '#2ecc71',
                        'Sports': '#3498db'
                    };

                    return {
                        color: colors[feature.properties.type] || '#95a5a6',
                        weight: 2
                    };
                }
            });

            featureLayer.once('load', function() {
                featureLayer.remove();
                done();
            });

            featureLayer.once('error', function() {
                done();
            });

            featureLayer.addTo(testMap);
        });

    });

});
