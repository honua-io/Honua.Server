# Load Testing

Performance testing for Honua Server using k6.

## Prerequisites

Install k6:

```bash
# macOS
brew install k6

# Linux
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6

# Windows
choco install k6

# Docker
docker pull grafana/k6
```

## Running Tests

### Quick Smoke Test
```bash
k6 run smoke-test.js
```

### Load Test - WMS GetMap
```bash
k6 run wms-load-test.js
```

### Load Test - WFS GetFeature
```bash
k6 run wfs-load-test.js
```

### Load Test - WMTS GetTile
```bash
k6 run wmts-load-test.js
```

### Load Test - OData Queries
```bash
k6 run odata-load-test.js
```

### Stress Test - Find Breaking Point
```bash
k6 run stress-test.js
```

### Soak Test - Sustained Load
```bash
k6 run soak-test.js
```

## Test Scenarios

### Smoke Test
- **Purpose**: Verify system functions under minimal load
- **Duration**: 1 minute
- **VUs**: 1-5
- **Success Criteria**: 0% errors, <2s p95 latency

### Load Test
- **Purpose**: Test system under expected production load
- **Duration**: 5 minutes
- **VUs**: 50-100
- **Success Criteria**: <1% errors, <5s p95 latency

### Stress Test
- **Purpose**: Find system breaking point
- **Duration**: 10 minutes
- **VUs**: Ramp up to 500
- **Success Criteria**: Identify max throughput, graceful degradation

### Soak Test
- **Purpose**: Detect memory leaks and performance degradation over time
- **Duration**: 1 hour
- **VUs**: 50 (constant)
- **Success Criteria**: Stable performance, no memory growth

## Performance Baselines

### WMS GetMap (Target)
- **Throughput**: >100 req/s
- **Latency p50**: <500ms
- **Latency p95**: <2s
- **Latency p99**: <5s
- **Error Rate**: <1%

### WMTS GetTile (Cached) (Target)
- **Throughput**: >1000 req/s
- **Latency p50**: <50ms
- **Latency p95**: <200ms
- **Latency p99**: <500ms
- **Error Rate**: <0.1%

### WFS GetFeature (Target)
- **Throughput**: >50 req/s
- **Latency p50**: <1s
- **Latency p95**: <5s
- **Latency p99**: <10s
- **Error Rate**: <1%

### OData Query (Target)
- **Throughput**: >100 req/s
- **Latency p50**: <500ms
- **Latency p95**: <2s
- **Latency p99**: <5s
- **Error Rate**: <1%

## Monitoring During Tests

### Metrics to Watch

1. **Application Metrics** (Prometheus at /metrics)
   - Request rate
   - Error rate
   - Request duration histogram

2. **System Metrics**
   - CPU usage
   - Memory usage
   - Disk I/O
   - Network I/O

3. **Database Metrics**
   - Connection pool utilization
   - Query execution time
   - Active connections

4. **Cache Metrics**
   - Hit/miss ratio
   - Cache size
   - Eviction rate

### Real-time Monitoring

```bash
# Watch metrics during test
watch -n 1 'curl -s http://localhost:5000/metrics | grep -E "http_request|raster_cache"'

# Monitor system resources
htop

# Monitor Docker stats
docker stats honua-server
```

## Analyzing Results

k6 outputs detailed statistics at the end of each run:

```
     data_received..................: 1.2 GB  20 MB/s
     data_sent......................: 12 MB   200 kB/s
     http_req_blocked...............: avg=1.2ms    min=1µs     med=3µs      max=245ms   p(90)=5µs      p(95)=7µs
     http_req_connecting............: avg=453µs    min=0s      med=0s       max=88ms    p(90)=0s       p(95)=0s
     http_req_duration..............: avg=982ms    min=45ms    med=876ms    max=5.2s    p(90)=1.8s     p(95)=2.3s
       { expected_response:true }...: avg=982ms    min=45ms    med=876ms    max=5.2s    p(90)=1.8s     p(95)=2.3s
     http_req_failed................: 0.23%   ✓ 123      ✗ 52877
     http_req_receiving.............: avg=125µs    min=13µs    med=85µs     max=12ms    p(90)=198µs    p(95)=287µs
     http_req_sending...............: avg=32µs     min=5µs     med=19µs     max=8ms     p(90)=52µs     p(95)=78µs
     http_req_tls_handshaking.......: avg=0s       min=0s      med=0s       max=0s      p(90)=0s       p(95)=0s
     http_req_waiting...............: avg=982ms    min=45ms    med=876ms    max=5.2s    p(90)=1.8s     p(95)=2.3s
     http_reqs......................: 53000   883.33/s
     iteration_duration.............: avg=1.1s     min=100ms   med=1s       max=6s      p(90)=2s       p(95)=2.5s
     iterations.....................: 53000   883.33/s
     vus............................: 50      min=50     max=100
     vus_max........................: 100     min=100    max=100
```

### Key Metrics to Focus On

- **http_reqs**: Total requests per second (throughput)
- **http_req_duration**: Request latency distribution
- **http_req_failed**: Error rate (should be <1%)
- **p(95)** and **p(99)**: 95th and 99th percentile latencies

## Continuous Performance Testing

### CI/CD Integration

Add to GitHub Actions workflow:

```yaml
- name: Run performance tests
  run: |
    docker-compose up -d
    sleep 30  # Wait for server to be ready
    k6 run --summary-export=results.json tests/load/smoke-test.js
    k6 run --summary-export=results.json tests/load/wms-load-test.js
```

### Performance Regression Detection

Compare results against baseline:

```bash
# Run test and save results
k6 run --out json=results.json wms-load-test.js

# Compare against baseline
python compare-results.py results.json baseline.json
```

## Troubleshooting

### High Error Rate

1. Check server logs
2. Verify database connections
3. Check rate limiting settings
4. Monitor resource usage

### High Latency

1. Enable tracing to identify bottlenecks
2. Check database query performance
3. Verify cache hit rates
4. Profile CPU usage

### Memory Growth

1. Run soak test for longer duration
2. Monitor memory usage over time
3. Check for connection leaks
4. Profile heap allocations

## Next Steps

- [Deployment Guide](../../docs/DEPLOYMENT.md)
- [Tracing Documentation](../../docs/TRACING.md)
- [Metrics Documentation](../../docs/METRICS.md)
