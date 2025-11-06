import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const wmsDuration = new Trend('wms_request_duration');

export const options = {
  stages: [
    { duration: '1m', target: 20 },   // Ramp up to 20 VUs
    { duration: '3m', target: 50 },   // Increase to 50 VUs
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'], // Less than 1% errors
    http_req_duration: ['p(95)<5000', 'p(99)<10000'], // Under 5s for 95%
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const LAYER = __ENV.LAYER || 'parcels';

// Different bounding boxes for variety
const BBOXES = [
  '-122.5,37.7,-122.3,37.8',   // SF Bay Area
  '-122.45,37.75,-122.35,37.85',
  '-122.4,37.7,-122.35,37.75',
  '-118.3,34.0,-118.2,34.1',   // LA Area
  '-74.1,40.7,-73.9,40.8',     // NYC Area
];

export default function () {
  const bbox = BBOXES[Math.floor(Math.random() * BBOXES.length)];

  const params = {
    SERVICE: 'WMS',
    VERSION: '1.3.0',
    REQUEST: 'GetMap',
    LAYERS: LAYER,
    STYLES: '',
    CRS: 'EPSG:4326',
    BBOX: bbox,
    WIDTH: '800',
    HEIGHT: '600',
    FORMAT: 'image/png',
  };

  const url = `${BASE_URL}/wms?${Object.entries(params).map(([k, v]) => `${k}=${encodeURIComponent(v)}`).join('&')}`;

  const res = http.get(url);

  wmsDuration.add(res.timings.duration);

  check(res, {
    'wms status 200': (r) => r.status === 200,
    'wms is PNG': (r) => r.headers['Content-Type'] === 'image/png',
    'wms completed under 10s': (r) => r.timings.duration < 10000,
  });

  sleep(1); // 1 second between requests
}

export function handleSummary(data) {
  return {
    'wms-results.json': JSON.stringify(data),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}

function textSummary(data, options) {
  // Simple text summary
  const summary = [];
  summary.push(`\n${options.indent}WMS Load Test Results:\n`);
  summary.push(`${options.indent}Total Requests: ${data.metrics.http_reqs.values.count}\n`);
  summary.push(`${options.indent}Error Rate: ${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%\n`);
  summary.push(`${options.indent}Avg Duration: ${data.metrics.http_req_duration.values.avg.toFixed(2)}ms\n`);
  summary.push(`${options.indent}p95 Duration: ${data.metrics.http_req_duration.values['p(95)'].toFixed(2)}ms\n`);
  summary.push(`${options.indent}p99 Duration: ${data.metrics.http_req_duration.values['p(99)'].toFixed(2)}ms\n`);
  return summary.join('');
}
