import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';

const cacheHits = new Counter('cache_hits');
const cacheMisses = new Counter('cache_misses');
const tileDuration = new Trend('tile_request_duration');

export const options = {
  stages: [
    { duration: '1m', target: 50 },   // Ramp up to 50 VUs
    { duration: '3m', target: 100 },  // Increase to 100 VUs
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_failed: ['rate<0.001'], // Less than 0.1% errors
    http_req_duration: ['p(95)<200', 'p(99)<500'], // Fast tile serving
    'cache_hits': ['count>10000'], // Expect high cache hit ratio
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const LAYER = __ENV.LAYER || 'parcels'; // Default layer to test

// Common tile coordinates (Web Mercator zoom levels)
const TILES = [
  { z: 10, x: 163, y: 395 },  // San Francisco area
  { z: 10, x: 164, y: 395 },
  { z: 10, x: 163, y: 396 },
  { z: 11, x: 327, y: 791 },
  { z: 11, x: 328, y: 791 },
  { z: 12, x: 655, y: 1582 },
  { z: 12, x: 656, y: 1582 },
];

export default function () {
  // Pick a random tile
  const tile = TILES[Math.floor(Math.random() * TILES.length)];

  const url = `${BASE_URL}/wmts/${LAYER}/WebMercatorQuad/${tile.z}/${tile.y}/${tile.x}.png`;

  const res = http.get(url);

  tileDuration.add(res.timings.duration);

  check(res, {
    'tile status 200': (r) => r.status === 200,
    'tile is PNG': (r) => r.headers['Content-Type'] === 'image/png',
    'tile served quickly': (r) => r.timings.duration < 500,
  });

  // Track cache performance via headers (if server provides them)
  if (res.headers['X-Cache-Hit']) {
    cacheHits.add(1);
  } else {
    cacheMisses.add(1);
  }

  sleep(0.1); // Small sleep between requests
}
