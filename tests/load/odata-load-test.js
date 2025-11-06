import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const queryDuration = new Trend('odata_query_duration');

export const options = {
  stages: [
    { duration: '1m', target: 30 },   // Ramp up to 30 VUs
    { duration: '3m', target: 100 },  // Increase to 100 VUs
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'], // Less than 1% errors
    http_req_duration: ['p(95)<2000', 'p(99)<5000'], // Fast queries
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const COLLECTION = __ENV.COLLECTION || 'parcels';

// Different query patterns
const QUERIES = [
  // Simple queries
  '$top=10',
  '$top=50&$skip=100',
  '$orderby=id desc&$top=20',
  '$select=id,name,area',

  // Filter queries
  '$filter=area gt 1000',
  '$filter=area lt 5000 and area gt 1000',
  '$filter=contains(name, \'park\')',

  // Spatial queries
  '$filter=geo.intersects(geometry, geography\'POLYGON((-122.5 37.7,-122.3 37.7,-122.3 37.8,-122.5 37.8,-122.5 37.7))\')',
  '$filter=geo.distance(geometry, geography\'POINT(-122.4 37.75)\') lt 1000',

  // Complex queries
  '$filter=area gt 1000&$orderby=area desc&$top=10&$select=id,name,area',
];

export default function () {
  const query = QUERIES[Math.floor(Math.random() * QUERIES.length)];
  const url = `${BASE_URL}/odata/${COLLECTION}?${query}`;

  const res = http.get(url, {
    headers: {
      'Accept': 'application/json',
    },
  });

  queryDuration.add(res.timings.duration);

  check(res, {
    'odata status 200': (r) => r.status === 200,
    'odata is JSON': (r) => r.headers['Content-Type']?.includes('application/json'),
    'odata has value array': (r) => {
      try {
        const body = JSON.parse(r.body);
        return Array.isArray(body.value);
      } catch {
        return false;
      }
    },
    'odata completed quickly': (r) => r.timings.duration < 5000,
  });

  sleep(0.5); // 500ms between requests
}

export function handleSummary(data) {
  return {
    'odata-results.json': JSON.stringify(data),
  };
}
