import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 5 },  // Ramp up to 5 VUs
    { duration: '30s', target: 5 },  // Stay at 5 VUs
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'], // Less than 1% errors
    http_req_duration: ['p(95)<2000'], // 95% of requests under 2s
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function () {
  // Test OGC landing page
  let res = http.get(`${BASE_URL}/ogc`);
  check(res, {
    'ogc landing page status 200': (r) => r.status === 200,
  });

  // Test health check
  res = http.get(`${BASE_URL}/healthz/ready`);
  check(res, {
    'health check status 200': (r) => r.status === 200,
  });

  // Test metrics endpoint (if available)
  res = http.get(`${BASE_URL}/metrics`);
  check(res, {
    'metrics endpoint accessible': (r) => r.status === 200 || r.status === 401,
  });

  // Test Swagger documentation
  res = http.get(`${BASE_URL}/swagger/index.html`);
  check(res, {
    'swagger ui accessible': (r) => r.status === 200,
  });

  sleep(1);
}
