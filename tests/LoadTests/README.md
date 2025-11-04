# Load Testing with NBomber

This directory contains load testing scenarios for the Honua Server using NBomber.

## Prerequisites

```bash
dotnet add package NBomber --version 5.0.0
dotnet add package NBomber.Http --version 5.0.0
```

## Running Load Tests

```bash
# Run OGC API load test
dotnet run --project LoadTests.csproj -- ogc-collections

# Run with custom parameters
dotnet run --project LoadTests.csproj -- ogc-items --duration 60 --rps 100
```

## Test Scenarios

### 1. OGC Collections Endpoint
- Tests: `/collections` endpoint
- Duration: 30 seconds
- RPS: 50 requests/second
- Expected: < 200ms p99 latency

### 2. OGC Items/Features Query
- Tests: `/collections/{id}/items` with spatial queries
- Duration: 60 seconds  
- RPS: 100 requests/second
- Expected: < 500ms p99 latency

### 3. Vector Tiles
- Tests: `/collections/{id}/tiles/{z}/{x}/{y}.mvt`
- Duration: 30 seconds
- RPS: 200 requests/second
- Expected: < 100ms p99 latency (cached)

## Metrics Collected

- Requests per second (RPS)
- Latency (min, mean, max, p50, p75, p95, p99)
- Error rate
- Data transfer rate
- Status code distribution

## Reporting

Reports are generated in `./load-test-results/` directory:
- HTML report
- JSON report
- CSV export

## CI/CD Integration

Load tests can be run in CI/CD with:

```bash
dotnet test --filter "Category=LoadTest" --logger:"console;verbosity=detailed"
```
