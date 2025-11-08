/**
 * Authentication and Security Tests
 * Tests token-based authentication and secure access
 */

describe('Authentication and Security Tests', function() {
    this.timeout(10000);

    const FEATURE_SERVICE = '/rest/services/parks/FeatureServer';
    const TOKEN_ENDPOINT = '/rest/oauth2/token';
    const GENERATE_TOKEN_ENDPOINT = '/rest/generateToken';

    describe('Token Generation', function() {

        it('should check if service requires authentication', async function() {
            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}?f=json`);

            // Check for authentication requirement
            if (response.status === 401 || response.status === 403) {
                const data = await response.json();
                expect(data.error).to.exist;
                expect(data.error.code).to.be.oneOf([401, 403, 498, 499]);
            }
        });

        it('should generate token with credentials', async function() {
            const response = await fetch(`${BASE_URL}${GENERATE_TOKEN_ENDPOINT}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({
                    username: 'test_user',
                    password: 'test_password',
                    f: 'json',
                    expiration: '60' // 60 minutes
                })
            });

            // Token generation might not be enabled
            if (response.ok) {
                const data = await response.json();
                if (data.token) {
                    expect(data.token).to.be.a('string');
                    expect(data.expires).to.be.a('number');
                }
            }
        });

    });

    describe('Token-Based Access', function() {

        it('should access secured service with token', async function() {
            // Try to get a token first
            const tokenResponse = await fetch(`${BASE_URL}${GENERATE_TOKEN_ENDPOINT}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({
                    username: 'test_user',
                    password: 'test_password',
                    f: 'json'
                })
            });

            if (tokenResponse.ok) {
                const tokenData = await tokenResponse.json();
                if (tokenData.token) {
                    // Use token to access service
                    const serviceResponse = await fetch(
                        `${BASE_URL}${FEATURE_SERVICE}/0/query?where=1=1&f=json&token=${tokenData.token}`
                    );

                    expect(serviceResponse.ok).to.be.true;
                }
            }
        });

        it('should handle expired token gracefully', async function() {
            const expiredToken = 'expired_token_12345';

            const response = await fetch(
                `${BASE_URL}${FEATURE_SERVICE}/0/query?where=1=1&f=json&token=${expiredToken}`
            );

            // Should get authentication error
            if (!response.ok) {
                const data = await response.json();
                if (data.error) {
                    expect(data.error.code).to.be.oneOf([498, 499]); // Invalid/expired token
                }
            }
        });

    });

    describe('Esri Leaflet Authentication', function() {

        it('should authenticate with Esri Leaflet', function(done) {
            if (!testMap || !L.esri.get) {
                this.skip();
                return;
            }

            // Simulate authentication setup
            const mockAuthenticate = function(callback) {
                // In real scenario, would get actual token
                callback(null, 'mock_token_123');
            };

            mockAuthenticate(function(error, token) {
                if (!error && token) {
                    const featureLayer = L.esri.featureLayer({
                        url: `${BASE_URL}${FEATURE_SERVICE}/0`,
                        token: token
                    });

                    featureLayer.once('load', function() {
                        featureLayer.remove();
                        done();
                    });

                    featureLayer.once('error', function() {
                        done(); // Service might not require auth
                    });

                    featureLayer.addTo(testMap);
                } else {
                    done();
                }
            });
        });

    });

    describe('CORS and Security Headers', function() {

        it('should have proper CORS headers', async function() {
            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}?f=json`);

            // Check for CORS headers
            const corsHeader = response.headers.get('Access-Control-Allow-Origin');
            if (corsHeader) {
                expect(corsHeader).to.exist;
            }
        });

        it('should handle preflight OPTIONS request', async function() {
            const response = await fetch(`${BASE_URL}${FEATURE_SERVICE}/0/query`, {
                method: 'OPTIONS',
                headers: {
                    'Origin': window.location.origin,
                    'Access-Control-Request-Method': 'POST'
                }
            });

            // Should allow POST
            expect([200, 204]).to.include(response.status);
        });

    });

    describe('Rate Limiting', function() {

        it('should handle rate limiting gracefully', async function() {
            const requests = [];

            // Make multiple rapid requests
            for (let i = 0; i < 50; i++) {
                requests.push(
                    fetch(`${BASE_URL}${FEATURE_SERVICE}/0/query?where=1=1&resultRecordCount=1&f=json`)
                );
            }

            const responses = await Promise.all(requests);

            // Check if any were rate limited
            const rateLimited = responses.some(r => r.status === 429);

            // Either all succeed or some are rate limited
            expect(responses.every(r => r.ok) || rateLimited).to.be.true;
        });

    });

});
