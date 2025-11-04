/**
 * CloudWatch Synthetics Canary for Honua Readiness Endpoint
 *
 * This canary monitors the /healthz/ready endpoint to ensure full stack health.
 * It validates HTTP status code, response time, and content.
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
  const endpointUrl = process.env.ENDPOINT_URL || 'https://honua.example.com/healthz/ready';

  const validateSuccessful = async function(res) {
    return new Promise((resolve, reject) => {
      // Validate status code
      if (res.statusCode !== 200) {
        throw new Error(`Expected status code 200, got ${res.statusCode}`);
      }

      // Validate response body
      const responseBody = res.body ? res.body.toString() : '';
      if (!responseBody.includes('Healthy')) {
        throw new Error(`Expected response to contain "Healthy", got: ${responseBody}`);
      }

      // Parse JSON to validate structure
      try {
        const data = JSON.parse(responseBody);
        if (data.status && data.status !== 'Healthy') {
          throw new Error(`Health check status is not Healthy: ${data.status}`);
        }
      } catch (e) {
        // If not JSON, just check for "Healthy" text
        if (!responseBody.includes('Healthy')) {
          throw new Error('Response does not indicate healthy status');
        }
      }

      log.info('Readiness check successful');
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

  await synthetics.executeHttpStep('Verify readiness endpoint', requestOptions, validateSuccessful, stepConfig);
};

exports.handler = async () => {
  return await apiCanaryBlueprint();
};
