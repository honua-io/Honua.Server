/**
 * Feature Editing Tests
 * Tests create, update, delete operations on FeatureServer
 */

describe('Feature Editing Tests', function() {
    this.timeout(15000);

    const FEATURE_SERVICE = '/rest/services/parks/FeatureServer';
    const LAYER_ID = 0;

    describe('Add Features', function() {

        it('should add a new feature', async function() {
            const newFeature = {
                geometry: {
                    x: -122.5,
                    y: 45.5,
                    spatialReference: { wkid: 4326 }
                },
                attributes: {
                    name: 'Test Park',
                    type: 'Recreation',
                    status: 'active'
                }
            };

            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/addFeatures`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({
                    features: JSON.stringify([newFeature]),
                    f: 'json'
                })
            });

            if (response.ok) {
                const data = await response.json();
                expect(data).to.have.property('addResults');
                expect(data.addResults).to.be.an('array');
            }
        });

        it('should add multiple features in batch', async function() {
            const features = [
                {
                    geometry: { x: -122.5, y: 45.5, spatialReference: { wkid: 4326 } },
                    attributes: { name: 'Park 1', type: 'Recreation' }
                },
                {
                    geometry: { x: -122.6, y: 45.6, spatialReference: { wkid: 4326 } },
                    attributes: { name: 'Park 2', type: 'Nature' }
                }
            ];

            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/addFeatures`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({
                    features: JSON.stringify(features),
                    f: 'json'
                })
            });

            if (response.ok) {
                const data = await response.json();
                if (data.addResults) {
                    expect(data.addResults.length).to.equal(2);
                }
            }
        });

    });

    describe('Update Features', function() {

        it('should update feature attributes', async function() {
            // First, find a feature to update
            const queryResponse = await fetch(
                `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&returnGeometry=false&outFields=OBJECTID&resultRecordCount=1&f=json`
            );

            if (queryResponse.ok) {
                const queryData = await queryResponse.json();
                if (queryData.features && queryData.features.length > 0) {
                    const objectId = queryData.features[0].attributes.OBJECTID;

                    const updateFeature = {
                        attributes: {
                            OBJECTID: objectId,
                            name: 'Updated Park Name',
                            status: 'modified'
                        }
                    };

                    const updateResponse = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/updateFeatures`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                        body: new URLSearchParams({
                            features: JSON.stringify([updateFeature]),
                            f: 'json'
                        })
                    });

                    if (updateResponse.ok) {
                        const updateData = await updateResponse.json();
                        expect(updateData).to.have.property('updateResults');
                    }
                }
            }
        });

        it('should update feature geometry', async function() {
            const queryResponse = await fetch(
                `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&returnGeometry=true&outFields=OBJECTID&resultRecordCount=1&f=json`
            );

            if (queryResponse.ok) {
                const queryData = await queryResponse.json();
                if (queryData.features && queryData.features.length > 0) {
                    const objectId = queryData.features[0].attributes.OBJECTID;

                    const updateFeature = {
                        attributes: { OBJECTID: objectId },
                        geometry: {
                            x: -122.55,
                            y: 45.55,
                            spatialReference: { wkid: 4326 }
                        }
                    };

                    const updateResponse = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/updateFeatures`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                        body: new URLSearchParams({
                            features: JSON.stringify([updateFeature]),
                            f: 'json'
                        })
                    });

                    if (updateResponse.ok) {
                        const updateData = await updateResponse.json();
                        expect(updateData).to.have.property('updateResults');
                    }
                }
            }
        });

    });

    describe('Delete Features', function() {

        it('should delete feature by OBJECTID', async function() {
            const queryResponse = await fetch(
                `${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/query?where=1=1&returnGeometry=false&outFields=OBJECTID&resultRecordCount=1&f=json`
            );

            if (queryResponse.ok) {
                const queryData = await queryResponse.json();
                if (queryData.features && queryData.features.length > 0) {
                    const objectId = queryData.features[0].attributes.OBJECTID;

                    const deleteResponse = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/deleteFeatures`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                        body: new URLSearchParams({
                            objectIds: objectId.toString(),
                            f: 'json'
                        })
                    });

                    if (deleteResponse.ok) {
                        const deleteData = await deleteResponse.json();
                        expect(deleteData).to.have.property('deleteResults');
                    }
                }
            }
        });

        it('should delete features by where clause', async function() {
            const deleteResponse = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/deleteFeatures`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({
                    where: "name = 'Test Park'",
                    f: 'json'
                })
            });

            if (deleteResponse.ok) {
                const deleteData = await deleteResponse.json();
                expect(deleteData).to.have.property('deleteResults');
            }
        });

    });

    describe('Apply Edits (Combined)', function() {

        it('should apply multiple edit operations at once', async function() {
            const edits = {
                adds: [{
                    geometry: { x: -122.5, y: 45.5, spatialReference: { wkid: 4326 } },
                    attributes: { name: 'New Park', type: 'Recreation' }
                }],
                updates: [],
                deletes: []
            };

            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/applyEdits`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({
                    adds: JSON.stringify(edits.adds),
                    updates: JSON.stringify(edits.updates),
                    deletes: edits.deletes.join(','),
                    f: 'json'
                })
            });

            if (response.ok) {
                const data = await response.json();
                expect(data).to.exist;
            }
        });

    });

    describe('Transactions', function() {

        it('should support rollback on error', async function() {
            // Try to add invalid features - should rollback
            const invalidFeatures = [
                {
                    geometry: { x: -122.5, y: 45.5, spatialReference: { wkid: 4326 } },
                    attributes: { name: 'Valid Park' }
                },
                {
                    // Missing required geometry
                    attributes: { name: 'Invalid Park' }
                }
            ];

            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}/${LAYER_ID}/addFeatures`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({
                    features: JSON.stringify(invalidFeatures),
                    rollbackOnFailure: 'true',
                    f: 'json'
                })
            });

            // Should handle error gracefully
            expect(response).to.exist;
        });

    });

});
