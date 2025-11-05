# GeoEvent Performance Testing Guide

This guide provides instructions for performance testing the Honua GeoEvent geofencing service to validate MVP targets.

## Performance Targets (Month 1-2 MVP)

According to `docs/GEOEVENT_MVP_REALISTIC.md`:

- **Latency**: P95 < 100ms for location evaluation
- **Geofence Capacity**: 1,000 active geofences
- **Throughput**: 100 events/second sustained
- **Uptime**: 99% availability target

## Test Environment Setup

### Prerequisites

1. **PostgreSQL with PostGIS**
   ```bash
   docker run -d \
     --name postgis \
     -e POSTGRES_PASSWORD=postgres \
     -p 5432:5432 \
     postgis/postgis:16-3.4
   ```

2. **Load Test Data**
   ```sql
   -- Create 1,000 test geofences covering San Francisco Bay Area
   -- See scripts/load_test_geofences.sql
   ```

3. **Load Testing Tool**: Apache Bench, k6, or Locust

## Test Scenarios

### Scenario 1: Latency Test (P95 < 100ms)

**Objective**: Verify location evaluation latency with 1,000 geofences

**Setup**:
- Load 1,000 geofences into database
- Use single evaluation endpoint
- Measure response time distribution

**Test with Apache Bench**:
```bash
# 1,000 requests, 10 concurrent
ab -n 1000 -c 10 \
   -H "Authorization: Bearer $TOKEN" \
   -H "Content-Type: application/json" \
   -p evaluate_request.json \
   https://your-server/api/v1/geoevent/evaluate

# evaluate_request.json
{
  "entity_id": "test-vehicle-1",
  "location": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  }
}
```

**Expected Results**:
```
Percentage of requests served within certain time (ms)
  50%    45
  66%    55
  75%    65
  80%    72
  90%    85
  95%    95    ← Target: < 100ms
  98%   105
  99%   120
 100%   250 (longest request)
```

**Pass Criteria**: P95 < 100ms

### Scenario 2: Throughput Test (100 events/sec)

**Objective**: Sustain 100 location evaluations per second for 10 minutes

**Test with k6**:
```javascript
// geoevent-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 50 },   // Ramp up to 50 RPS
    { duration: '2m', target: 100 },  // Ramp up to 100 RPS
    { duration: '10m', target: 100 }, // Sustain 100 RPS for 10 min
    { duration: '2m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<100'], // P95 < 100ms
    http_req_failed: ['rate<0.01'],   // Error rate < 1%
  },
};

const locations = [
  [-122.4194, 37.7749], // San Francisco
  [-122.2711, 37.8044], // Oakland
  [-122.0838, 37.3861], // San Jose
  // Add more test locations
];

export default function () {
  const location = locations[Math.floor(Math.random() * locations.length)];

  const payload = JSON.stringify({
    entity_id: `vehicle-${__VU}-${__ITER}`,
    entity_type: 'test_vehicle',
    location: {
      type: 'Point',
      coordinates: location,
    },
    properties: {
      speed: Math.random() * 60 + 10,
      heading: Math.random() * 360,
    },
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${__ENV.API_TOKEN}`,
    },
  };

  const res = http.post(
    'https://your-server/api/v1/geoevent/evaluate',
    payload,
    params
  );

  check(res, {
    'status is 200': (r) => r.status === 200,
    'has events_generated': (r) => JSON.parse(r.body).events_generated !== undefined,
    'processing_time < 100ms': (r) => JSON.parse(r.body).processing_time_ms < 100,
  });

  sleep(1); // 1 second between requests per VU
}
```

**Run Test**:
```bash
k6 run --env API_TOKEN=$TOKEN geoevent-load-test.js
```

**Expected Output**:
```
     ✓ status is 200
     ✓ has events_generated
     ✓ processing_time < 100ms

     checks.........................: 100.00% ✓ 60000    ✗ 0
     data_received..................: 24 MB   41 kB/s
     data_sent......................: 18 MB   31 kB/s
     http_req_blocked...............: avg=1.2ms   p(95)=3ms
     http_req_connecting............: avg=0.8ms   p(95)=2ms
     http_req_duration..............: avg=45ms    p(95)=85ms   ← Target met!
     http_req_failed................: 0.00%   ✓ 0        ✗ 60000
     http_req_receiving.............: avg=0.5ms   p(95)=1ms
     http_req_sending...............: avg=0.3ms   p(95)=0.8ms
     http_req_tls_handshaking.......: avg=0ms     p(95)=0ms
     http_req_waiting...............: avg=44.2ms  p(95)=84ms
     http_reqs......................: 60000   100/s
     iteration_duration.............: avg=1.05s   p(95)=1.1s
```

**Pass Criteria**:
- 100 requests/second sustained for 10 minutes
- P95 latency < 100ms
- Error rate < 1%

### Scenario 3: Batch Processing Test

**Objective**: Verify batch endpoint can handle 1,000 locations per request

**Test with Python**:
```python
import requests
import time
import statistics

def test_batch_processing():
    """Test batch evaluation with 1,000 locations."""

    # Generate 1,000 test locations
    locations = []
    for i in range(1000):
        lon = -122.5 + (i % 100) * 0.001
        lat = 37.7 + (i // 100) * 0.001

        locations.append({
            'entity_id': f'vehicle-{i}',
            'location': {
                'type': 'Point',
                'coordinates': [lon, lat]
            }
        })

    # Send batch request
    start = time.time()
    response = requests.post(
        'https://your-server/api/v1/geoevent/evaluate/batch',
        json=locations,
        headers={'Authorization': f'Bearer {api_token}'}
    )
    elapsed = (time.time() - start) * 1000  # Convert to ms

    result = response.json()

    print(f"Batch Size: {result['total_processed']}")
    print(f"Success Rate: {result['success_count']} / {result['total_processed']}")
    print(f"Total Processing Time: {result['total_processing_time_ms']:.2f}ms")
    print(f"Request Duration: {elapsed:.2f}ms")
    print(f"Avg per location: {result['total_processing_time_ms'] / result['total_processed']:.2f}ms")

    assert result['total_processed'] == 1000
    assert result['success_count'] == 1000
    assert result['total_processing_time_ms'] < 10000  # < 10 seconds for 1000 locations

# Run test
test_batch_processing()
```

**Expected Output**:
```
Batch Size: 1000
Success Rate: 1000 / 1000
Total Processing Time: 4523.45ms
Request Duration: 4650.23ms
Avg per location: 4.52ms
```

**Pass Criteria**:
- Process 1,000 locations in single batch
- 100% success rate
- Total processing time < 10 seconds

### Scenario 4: Concurrent Users Test

**Objective**: Test with multiple concurrent users simulating real-world usage

**Test with Locust**:
```python
# locustfile.py
from locust import HttpUser, task, between
import random

class GeoEventUser(HttpUser):
    wait_time = between(1, 3)  # Wait 1-3 seconds between requests

    def on_start(self):
        """Called when a user starts."""
        self.entity_id = f"vehicle-{random.randint(1000, 9999)}"

    @task(10)  # 10x weight
    def evaluate_location(self):
        """Evaluate single location (most common operation)."""
        location = self.get_random_location()

        self.client.post(
            "/api/v1/geoevent/evaluate",
            json={
                "entity_id": self.entity_id,
                "location": {
                    "type": "Point",
                    "coordinates": location
                },
                "properties": {
                    "speed": random.uniform(10, 70),
                    "heading": random.randint(0, 359)
                }
            },
            headers={"Authorization": f"Bearer {self.api_token}"}
        )

    @task(1)  # 1x weight (less common)
    def list_geofences(self):
        """List geofences."""
        self.client.get(
            "/api/v1/geofences?limit=50",
            headers={"Authorization": f"Bearer {self.api_token}"}
        )

    def get_random_location(self):
        """Generate random location in SF Bay Area."""
        return [
            random.uniform(-122.5, -122.0),  # Longitude
            random.uniform(37.3, 38.0)        # Latitude
        ]
```

**Run Test**:
```bash
# Ramp up to 100 concurrent users
locust -f locustfile.py --headless \
  --users 100 \
  --spawn-rate 10 \
  --run-time 10m \
  --host https://your-server
```

**Expected Results**:
- 100 concurrent users
- 90-95 requests/second aggregate
- P95 < 100ms maintained
- 0% error rate

## Database Performance Monitoring

### Key Metrics to Track

```sql
-- Query performance for spatial lookups
EXPLAIN ANALYZE
SELECT id, name, ST_AsBinary(geometry) as geometry_wkb
FROM geofences
WHERE is_active = true
AND ST_Contains(geometry, ST_SetSRID(ST_MakePoint(-122.4194, 37.7749), 4326));

-- Check index usage
SELECT schemaname, tablename, indexname, idx_scan, idx_tup_read, idx_tup_fetch
FROM pg_stat_user_indexes
WHERE tablename IN ('geofences', 'entity_geofence_state', 'geofence_events');

-- Monitor query times
SELECT query, mean_exec_time, stddev_exec_time, calls
FROM pg_stat_statements
WHERE query LIKE '%geofences%'
ORDER BY mean_exec_time DESC
LIMIT 10;
```

### Expected Query Performance

- Spatial lookup (FindGeofencesAtPointAsync): 5-20ms
- State lookup (GetEntityStatesAsync): 1-5ms
- Event insert (CreateAsync): 2-10ms

## Resource Monitoring

### Application Metrics

Monitor these via OpenTelemetry/Prometheus:

- `geoevent_evaluation_duration_ms` - P50, P95, P99 latencies
- `geoevent_evaluation_total` - Request count
- `geoevent_evaluation_errors` - Error count
- `geofence_count` - Active geofence count
- `entity_state_count` - Tracked entity count

### System Metrics

- **CPU Usage**: Should stay < 70% under load
- **Memory**: Monitor for leaks, should stabilize
- **Database Connections**: Monitor connection pool usage
- **Network I/O**: Track bandwidth usage

**Prometheus Queries**:
```promql
# P95 latency
histogram_quantile(0.95, rate(geoevent_evaluation_duration_ms_bucket[5m]))

# Throughput (requests/sec)
rate(geoevent_evaluation_total[1m])

# Error rate
rate(geoevent_evaluation_errors[5m]) / rate(geoevent_evaluation_total[5m])
```

## Performance Optimization Tips

### 1. Database Tuning

```sql
-- Tune PostgreSQL for geospatial queries
ALTER SYSTEM SET shared_buffers = '2GB';
ALTER SYSTEM SET effective_cache_size = '6GB';
ALTER SYSTEM SET maintenance_work_mem = '512MB';
ALTER SYSTEM SET random_page_cost = 1.1;  -- For SSD

-- Ensure GIST index exists
CREATE INDEX IF NOT EXISTS idx_geofences_geometry
ON geofences USING GIST(geometry);

-- Vacuum and analyze regularly
VACUUM ANALYZE geofences;
VACUUM ANALYZE entity_geofence_state;
VACUUM ANALYZE geofence_events;
```

### 2. Application Tuning

- Enable database connection pooling (min 10, max 100)
- Use async/await throughout
- Consider caching frequently accessed geofences
- Batch database operations when possible

### 3. Horizontal Scaling

For > 100 events/sec:
- Add read replicas for evaluation queries
- Use write-through cache (Redis) for entity state
- Consider sharding by tenant_id

## Performance Test Checklist

Before marking Month 1-2 MVP complete:

- [ ] P95 latency < 100ms with 1,000 geofences
- [ ] Sustain 100 events/second for 10 minutes
- [ ] Batch processing: 1,000 locations < 10 seconds
- [ ] 100 concurrent users with no degradation
- [ ] Database query times within targets
- [ ] No memory leaks over 1-hour load test
- [ ] Error rate < 1% under load
- [ ] Monitoring and alerting configured

## Continuous Performance Testing

### Week 6 Load Testing (from MVP roadmap)

Run comprehensive load test in production-like environment:

```bash
# Week 6 Test Suite
./scripts/run-performance-tests.sh

# Includes:
# - 1k geofences, 100 RPS, 1 hour
# - Batch processing tests
# - Concurrent user simulation
# - Failover testing
# - Database performance validation
```

### Monitoring in Production

Set up alerts for:
- P95 latency > 150ms (warning), > 200ms (critical)
- Error rate > 1% (warning), > 5% (critical)
- Throughput < 80 events/sec (warning)
- Database connection pool > 80% (warning)

## Reporting Results

Document performance test results in format:

```markdown
## Performance Test Results - YYYY-MM-DD

**Environment**: Production-like (4 CPU, 16GB RAM, PostgreSQL 16)

**Test Configuration**:
- Geofence Count: 1,000
- Test Duration: 10 minutes
- Target RPS: 100

**Results**:
- P50 Latency: 45ms ✓
- P95 Latency: 85ms ✓
- P99 Latency: 120ms ✓
- Throughput: 100.2 RPS ✓
- Error Rate: 0.02% ✓
- Uptime: 100% ✓

**Pass/Fail**: PASS

**Notes**: All targets met. System stable throughout test.
```

## Next Steps

After achieving Month 1-2 performance targets, plan for:

**Month 3-4 Targets**:
- 100 events/second → 500 events/second
- Azure Stream Analytics integration
- Geographic load balancing

**Month 5-6 Targets**:
- 500 events/second → 1,000 events/second
- Multi-region deployment
- Advanced caching strategies
