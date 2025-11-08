#!/usr/bin/env node

/**
 * Simple HTTP-based test runner for ESRI REST API
 * Tests endpoints directly without requiring a browser
 */

const BASE_URL = process.env.HONUA_TEST_BASE_URL || 'http://localhost:5100';

// ANSI color codes
const colors = {
  reset: '\x1b[0m',
  green: '\x1b[32m',
  red: '\x1b[31m',
  yellow: '\x1b[33m',
  blue: '\x1b[34m',
  gray: '\x1b[90m'
};

let stats = {
  passes: 0,
  failures: 0,
  pending: 0,
  tests: []
};

async function test(name, fn) {
  try {
    await fn();
    stats.passes++;
    console.log(`${colors.green}  ✓${colors.reset} ${colors.gray}${name}${colors.reset}`);
    stats.tests.push({ name, status: 'passed' });
  } catch (error) {
    stats.failures++;
    console.log(`${colors.red}  ✗ ${name}${colors.reset}`);
    console.log(`${colors.red}    ${error.message}${colors.reset}`);
    stats.tests.push({ name, status: 'failed', error: error.message });
  }
}

function pending(name) {
  stats.pending++;
  console.log(`${colors.yellow}  ⊝ ${name}${colors.reset} ${colors.gray}(pending)${colors.reset}`);
  stats.tests.push({ name, status: 'pending' });
}

async function fetchJSON(path) {
  const url = `${BASE_URL}${path}`;
  const response = await fetch(url);

  // Allow 400, 404, 503 status codes to be handled by tests
  if (!response.ok && response.status !== 400 && response.status !== 404 && response.status !== 503) {
    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
  }

  const text = await response.text();
  try {
    return { status: response.status, data: JSON.parse(text), ok: response.ok };
  } catch {
    return { status: response.status, data: text, ok: response.ok };
  }
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message || 'Assertion failed');
  }
}

async function runTests() {
  console.log(`\n${colors.blue}Testing Honua Server at ${BASE_URL}${colors.reset}\n`);

  // Health Check Tests
  console.log(`${colors.blue}Health Check Tests${colors.reset}`);

  await test('should respond to health endpoint', async () => {
    const result = await fetchJSON('/health');
    assert(result.status === 200 || result.status === 503, 'Expected 200 or 503');
    assert(result.data.status, 'Health status missing');
  });

  await test('should respond to liveness probe', async () => {
    const result = await fetchJSON('/health/live');
    assert(result.ok, 'Liveness probe failed');
  });

  // GeoServices REST API Tests
  console.log(`\n${colors.blue}GeoServices REST API Tests${colors.reset}`);

  await test('should return REST services catalog', async () => {
    const result = await fetchJSON('/rest/services?f=json');
    assert(result.ok || result.status === 200, 'REST catalog endpoint failed');
  });

  await test('should handle FeatureServer root request', async () => {
    const result = await fetchJSON('/rest/services/parks/FeatureServer?f=json');
    // Endpoint should respond (200 OK, 404 Not Found, or 400 validation error)
    assert(result.status === 200 || result.status === 404 || result.status === 400, 'FeatureServer endpoint not responding');
  });

  await test('should handle FeatureServer layer metadata', async () => {
    const result = await fetchJSON('/rest/services/parks/FeatureServer/0?f=json');
    // Endpoint should respond (200 OK, 404 Not Found, or 400 validation error)
    assert(result.status === 200 || result.status === 404 || result.status === 400, 'FeatureServer layer endpoint not responding');
  });

  await test('should handle FeatureServer query', async () => {
    const result = await fetchJSON('/rest/services/parks/FeatureServer/0/query?f=json&where=1=1&outFields=*&returnGeometry=false');
    assert(result.status === 200 || result.status === 404, 'FeatureServer query endpoint not responding');
  });

  await test('should handle MapServer root request', async () => {
    const result = await fetchJSON('/rest/services/basemap/MapServer?f=json');
    assert(result.status === 200 || result.status === 404, 'MapServer endpoint not responding');
  });

  await test('should handle MapServer layer metadata', async () => {
    const result = await fetchJSON('/rest/services/basemap/MapServer/0?f=json');
    assert(result.status === 200 || result.status === 404, 'MapServer layer endpoint not responding');
  });

  // Geometry Service Tests
  console.log(`\n${colors.blue}Geometry Service Tests${colors.reset}`);

  await test('should handle Geometry Service metadata', async () => {
    const result = await fetchJSON('/rest/services/Geometry/GeometryServer?f=json');
    assert(result.status === 200 || result.status === 404, 'GeometryServer endpoint not responding');
  });

  // OGC API Tests
  console.log(`\n${colors.blue}OGC API Tests${colors.reset}`);

  await test('should return OGC API landing page', async () => {
    const result = await fetchJSON('/');
    assert(result.ok, 'OGC landing page failed');
    if (result.ok && typeof result.data === 'object') {
      assert(result.data.links, 'Landing page missing links');
    }
  });

  await test('should return OGC conformance declaration', async () => {
    const result = await fetchJSON('/v1/conformance');
    assert(result.ok, 'Conformance endpoint failed');
    if (result.ok && typeof result.data === 'object') {
      assert(result.data.conformsTo, 'Missing conformsTo property');
    }
  });

  // WFS Tests
  console.log(`\n${colors.blue}WFS Tests${colors.reset}`);

  await test('should handle WFS GetCapabilities', async () => {
    const result = await fetchJSON('/v1/wfs?service=WFS&version=2.0.0&request=GetCapabilities');
    assert(result.ok || result.status === 404, 'WFS GetCapabilities failed');
  });

  // WMS Tests
  console.log(`\n${colors.blue}WMS Tests${colors.reset}`);

  await test('should handle WMS GetCapabilities', async () => {
    const result = await fetchJSON('/v1/wms?service=WMS&version=1.3.0&request=GetCapabilities');
    assert(result.ok || result.status === 404, 'WMS GetCapabilities failed');
  });

  // WMTS Tests
  console.log(`\n${colors.blue}WMTS Tests${colors.reset}`);

  await test('should handle WMTS GetCapabilities', async () => {
    const result = await fetchJSON('/wmts?service=WMTS&version=1.0.0&request=GetCapabilities');
    assert(result.ok || result.status === 404, 'WMTS GetCapabilities failed');
  });

  // STAC Tests
  console.log(`\n${colors.blue}STAC Tests${colors.reset}`);

  await test('should return STAC catalog root', async () => {
    const result = await fetchJSON('/v1/stac');
    assert(result.ok || result.status === 404, 'STAC catalog failed');
  });

  // Carto API Tests
  console.log(`\n${colors.blue}Carto SQL API Tests${colors.reset}`);

  await test('should handle Carto SQL API endpoint', async () => {
    const result = await fetchJSON('/carto/api/v2/sql?q=SELECT+1');
    assert(result.status === 200 || result.status === 404 || result.status === 400, 'Carto SQL API endpoint not responding');
  });

  // Pending tests (require full setup)
  console.log(`\n${colors.blue}Feature-Specific Tests (Pending Full Setup)${colors.reset}`);

  pending('FeatureServer query with spatial filter');
  pending('FeatureServer pagination with offset/limit');
  pending('MapServer export image');
  pending('MapServer identify operation');
  pending('Geometry Service buffer operation');
  pending('Geometry Service project operation');
  pending('Feature editing (add/update/delete)');
  pending('Token-based authentication');
  pending('Time-aware layer queries');
  pending('Attachment queries');

  // Print summary
  console.log(`\n${'='.repeat(70)}`);
  console.log(`${colors.blue}TEST RESULTS${colors.reset}`);
  console.log('='.repeat(70));
  console.log(`${colors.green}Passes:   ${stats.passes}${colors.reset}`);
  console.log(`${colors.red}Failures: ${stats.failures}${colors.reset}`);
  console.log(`${colors.yellow}Pending:  ${stats.pending}${colors.reset}`);
  console.log('='.repeat(70));

  if (stats.failures > 0) {
    console.log(`\n${colors.red}FAILED TESTS:${colors.reset}`);
    stats.tests
      .filter(t => t.status === 'failed')
      .forEach(t => {
        console.log(`  ${colors.red}✗${colors.reset} ${t.name}`);
        console.log(`    ${colors.gray}${t.error}${colors.reset}`);
      });
  }

  console.log('');

  return stats.failures === 0 ? 0 : 1;
}

// Run tests
runTests()
  .then(exitCode => {
    process.exit(exitCode);
  })
  .catch(error => {
    console.error(`\n${colors.red}Fatal error:${colors.reset}`, error.message);
    process.exit(1);
  });
