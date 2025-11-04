/**
 * CloudWatch Synthetics Canary for Honua OGC Conformance Endpoint
 *
 * This canary monitors the /ogc/conformance endpoint.
 * It validates HTTP status code, JSON structure, and conformance classes.
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
  const endpointUrl = process.env.ENDPOINT_URL || 'https://honua.example.com/ogc/conformance';

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

      // Validate conformance structure
      const responseBody = res.body ? res.body.toString() : '';
      try {
        const data = JSON.parse(responseBody);

        // OGC conformance must have conformsTo array
        if (!data.conformsTo || !Array.isArray(data.conformsTo)) {
          throw new Error('OGC conformance endpoint missing "conformsTo" array');
        }

        // Check for core conformance class
        const coreConformance = 'http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core';
        if (!data.conformsTo.includes(coreConformance)) {
          log.warn(`OGC API missing core conformance class: ${coreConformance}`);
        }

        log.info(`OGC API conforms to ${data.conformsTo.length} classes`);
      } catch (e) {
        throw new Error(`Invalid OGC conformance response: ${e.message}`);
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

  await synthetics.executeHttpStep('Verify OGC conformance endpoint', requestOptions, validateSuccessful, stepConfig);
};

exports.handler = async () => {
  return await apiCanaryBlueprint();
};
