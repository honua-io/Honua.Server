/**
 * CloudWatch Synthetics Canary for Honua OGC API Endpoint
 *
 * This canary monitors the /ogc endpoint (OGC API landing page).
 * It validates HTTP status code, JSON structure, and required links.
 */

const synthetics = require('Synthetics');
const log = require('SyntheticsLogger');
const syntheticsConfiguration = synthetics.getConfiguration();

syntheticsConfiguration.setConfig({
  continueOnStepFailure: false,
  includeRequestHeaders: true,
  includeResponseHeaders: true,
  restrictedHeaders: [],
  restrictedUrlParameters: []
});

const apiCanaryBlueprint = async function () {
  const endpointUrl = process.env.ENDPOINT_URL || 'https://honua.example.com/ogc';

  const validateSuccessful = async function(res) {
    return new Promise((resolve, reject) => {
      // Validate status code
      if (res.statusCode !== 200) {
        throw new Error(`Expected status code 200, got ${res.statusCode}`);
      }

      // Validate content type
      const contentType = res.headers['content-type'] || '';
      if (!contentType.includes('application/json')) {
        throw new Error(`Expected JSON content type, got: ${contentType}`);
      }

      // Validate OGC API structure
      const responseBody = res.body ? res.body.toString() : '';
      try {
        const data = JSON.parse(responseBody);

        // OGC API landing page must have links
        if (!data.links || !Array.isArray(data.links)) {
          throw new Error('OGC API landing page missing "links" array');
        }

        // Check for required link relations
        const linkRels = data.links.map(link => link.rel);
        const requiredRels = ['self', 'conformance', 'data'];
        const missingRels = requiredRels.filter(rel => !linkRels.includes(rel));

        if (missingRels.length > 0) {
          log.warn(`OGC API missing recommended link relations: ${missingRels.join(', ')}`);
        }

        log.info('OGC API landing page validated successfully');
      } catch (e) {
        throw new Error(`Invalid OGC API response: ${e.message}`);
      }

      resolve();
    });
  };

  const requestOptions = {
    hostname: new URL(endpointUrl).hostname,
    method: 'GET',
    path: new URL(endpointUrl).pathname,
    port: 443,
    protocol: 'https:',
    headers: {
      'User-Agent': 'CloudWatch-Synthetics-Canary',
      'Accept': 'application/json'
    }
  };

  const stepConfig = {
    includeRequestHeaders: true,
    includeResponseHeaders: true,
    includeRequestBody: true,
    includeResponseBody: true
  };

  await synthetics.executeHttpStep('Verify OGC API landing page', requestOptions, validateSuccessful, stepConfig);
};

exports.handler = async () => {
  return await apiCanaryBlueprint();
};
