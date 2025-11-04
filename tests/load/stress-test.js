import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 100 },   // Ramp to 100 VUs
    { duration: '3m', target: 200 },   // Increase to 200 VUs
    { duration: '2m', target: 300 },   // Push to 300 VUs
    { duration: '2m', target: 400 },   // Further to 400 VUs
    { duration: '1m', target: 500 },   // Max at 500 VUs
    { duration: '5m', target: 0 },     // Gradual ramp down
  ],
  thresholds: {
    http_req_duration: ['p(99)<10000'], // 99% under 10s even when stressed
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function () {
  // Mix of different endpoint types
  const endpoints = [
    '/ogc',
    '/healthz/ready',
    '/swagger/index.html',
  ];

  const endpoint = endpoints[Math.floor(Math.random() * endpoints.length)];
  const res = http.get(`${BASE_URL}${endpoint}`);

  check(res, {
    'response received': (r) => r.status > 0,
  });

  sleep(0.1);
}

export function handleSummary(data) {
  console.log('\\n=== Stress Test Results ===');
  console.log(`Max VUs reached: 500`);
  console.log(`Total requests: ${data.metrics.http_reqs.values.count}`);
  console.log(`Failed requests: ${data.metrics.http_req_failed.values.count} (${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%)`);
  console.log(`Request rate: ${data.metrics.http_reqs.values.rate.toFixed(2)} req/s`);
  console.log(`Avg duration: ${data.metrics.http_req_duration.values.avg.toFixed(2)}ms`);
  console.log(`p95 duration: ${data.metrics.http_req_duration.values['p(95)'].toFixed(2)}ms`);
  console.log(`p99 duration: ${data.metrics.http_req_duration.values['p(99)'].toFixed(2)}ms`);
  console.log('=========================\\n');

  return {
    'stress-results.json': JSON.stringify(data),
  };
}
