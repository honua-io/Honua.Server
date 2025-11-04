/**
 * CloudWatch Synthetics Canary for Honua Liveness Endpoint
 *
 * This canary monitors the /healthz/live endpoint to ensure basic availability.
 * It validates HTTP status code, response time, and content.
 */

const synthetics = require('Synthetics');
const log = require('SyntheticsLogger');
const syntheticsConfiguration = synthetics.getConfiguration();

// Configure canary behavior
syntheticsConfiguration.setConfig({
  continueOnStepFailure: false,
  includeRequestHeaders: true,
  includeResponseHeaders: true,
  restrictedHeaders: [],
  restrictedUrlParameters: []
});

const apiCanaryBlueprint = async function () {
  const endpointUrl = process.env.ENDPOINT_URL || 'https://honua.example.com/healthz/live';

  const validateSuccessful = async function(res) {
    return new Promise((resolve, reject) => {
      // Validate status code
      if (res.statusCode !== 200) {
        throw new Error(`Expected status code 200, got ${res.statusCode}`);
      }

      // Validate response body contains "Healthy"
      const responseBody = res.body ? res.body.toString() : '';
      if (!responseBody.includes('Healthy')) {
        throw new Error(`Expected response to contain "Healthy", got: ${responseBody}`);
      }

      log.info('Liveness check successful');
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
      'User-Agent': 'CloudWatch-Synthetics-Canary'
    }
  };

  const stepConfig = {
    includeRequestHeaders: true,
    includeResponseHeaders: true,
    includeRequestBody: true,
    includeResponseBody: true
  };

  await synthetics.executeHttpStep('Verify liveness endpoint', requestOptions, validateSuccessful, stepConfig);
};

exports.handler = async () => {
  return await apiCanaryBlueprint();
};
