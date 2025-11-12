# Parallel Testing Architecture

Visual guide to the HonuaIO parallel testing infrastructure.

## System Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         HOST SYSTEM (22 cores)                           │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │         Master Test Orchestrator (run-tests-parallel.sh)           │ │
│  │                                                                    │ │
│  │  ┌──────────────────────┐  ┌──────────────────────┐              │ │
│  │  │ Build Test Cache     │  │ Start Test Server    │              │ │
│  │  │ (if needed)          │→ │ (Docker Compose)     │              │ │
│  │  └──────────────────────┘  └──────────────────────┘              │ │
│  │                                      │                            │ │
│  │                     ┌────────────────┼────────────────┐           │ │
│  │                     │                │                │           │ │
│  │                     ▼                ▼                ▼           │ │
│  │         ┌──────────────────┐ ┌──────────────┐ ┌──────────────┐  │ │
│  │         │  C# Test Runner  │ │ Python Tests │ │ QGIS Tests   │  │ │
│  │         │  (10-12 cores)   │ │ (5 cores)    │ │ (5 cores)    │  │ │
│  │         └──────────────────┘ └──────────────┘ └──────────────┘  │ │
│  │                     │                │                │           │ │
│  │                     ▼                ▼                ▼           │ │
│  │         ┌──────────────────────────────────────────────────────┐ │ │
│  │         │          Aggregate Results & Report                  │ │ │
│  │         └──────────────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────┘
```

## C# Test Runner (xUnit) - Detailed View

```
┌─────────────────────────────────────────────────────────────────────┐
│                    C# Test Runner (xUnit)                           │
│                      10-12 cores allocated                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  xunit.runner.json:                                                 │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ maxParallelThreads: 6                                        │  │
│  │ parallelizeAssembly: true                                    │  │
│  │ parallelizeTestCollections: true                             │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
│  Test Collections (run in parallel):                                │
│                                                                     │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐ │
│  │ Collection 1     │  │ Collection 2     │  │ Collection 3     │ │
│  │ [SharedPostgres] │  │ [SharedPostgres] │  │ [SharedPostgres] │ │
│  ├──────────────────┤  ├──────────────────┤  ├──────────────────┤ │
│  │ PostgreSQL:16    │  │ PostgreSQL:16    │  │ PostgreSQL:16    │ │
│  │ Container #1     │  │ Container #2     │  │ Container #3     │ │
│  ├──────────────────┤  ├──────────────────┤  ├──────────────────┤ │
│  │ Test 1 ──────┐   │  │ Test 1 ──────┐   │  │ Test 1 ──────┐   │ │
│  │   │ Txn #1   │   │  │   │ Txn #1   │   │  │   │ Txn #1   │   │ │
│  │   └──────────┘   │  │   └──────────┘   │  │   └──────────┘   │ │
│  │ Test 2 ──────┐   │  │ Test 2 ──────┐   │  │ Test 2 ──────┐   │ │
│  │   │ Txn #2   │   │  │   │ Txn #2   │   │  │   │ Txn #2   │   │ │
│  │   └──────────┘   │  │   └──────────┘   │  │   └──────────┘   │ │
│  │ Test 3 ──────┐   │  │ Test 3 ──────┐   │  │ Test 3 ──────┐   │ │
│  │   │ Txn #3   │   │  │   │ Txn #3   │   │  │   │ Txn #3   │   │ │
│  │   └──────────┘   │  │   └──────────┘   │  │   └──────────┘   │ │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘ │
│         ▲                      ▲                      ▲            │
│         │                      │                      │            │
│         └──────── Sequential ──┴───── Sequential ─────┘            │
│                 (within collection)                                │
│                                                                     │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐ │
│  │ Collection 4     │  │ Collection 5     │  │ Collection 6     │ │
│  │ [SharedPostgres] │  │ [SharedPostgres] │  │ [Integration]    │ │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘ │
│                                                                     │
│  Transaction Isolation:                                             │
│  • Each test gets its own transaction                               │
│  • Automatic rollback on completion                                 │
│  • IsolationLevel.ReadCommitted                                     │
│  • Cleanup time: microseconds (rollback is cheap!)                  │
└─────────────────────────────────────────────────────────────────────┘
```

## Python Test Runner (pytest-xdist) - Detailed View

```
┌─────────────────────────────────────────────────────────────────────┐
│                  Python Test Runner (pytest-xdist)                  │
│                        5 cores allocated                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Command: pytest -n 5 tests/python/                                 │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │              Honua Test Server (Docker)                      │  │
│  │              honua:test-cached image                         │  │
│  │                                                              │  │
│  │  ┌────────────────────────────────────────────────────────┐ │  │
│  │  │ Preloaded SQLite Databases:                            │ │  │
│  │  │  • ogc-sample.db (685 features, 9 datasets)            │ │  │
│  │  │  • stac-catalog.db                                     │ │  │
│  │  │  • test-metadata.json                                  │ │  │
│  │  └────────────────────────────────────────────────────────┘ │  │
│  │                                                              │  │
│  │  Configuration:                                              │  │
│  │  • Port: 8080                                                │  │
│  │  • Auth: None (quickstart mode)                             │  │
│  │  • Cache: Memory (no Redis)                                 │  │
│  │  • Startup: ~5 seconds                                       │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                           ▲                                         │
│                           │                                         │
│            ┌──────────────┼──────────────────┐                     │
│            │              │                  │                     │
│  ┌─────────▼────┐  ┌──────▼─────┐  ┌────────▼────┐                │
│  │ Worker 1     │  │ Worker 2   │  │ Worker 3    │                │
│  │ (Process)    │  │ (Process)  │  │ (Process)   │ ...            │
│  ├──────────────┤  ├────────────┤  ├─────────────┤                │
│  │ test_wms.py  │  │ test_wfs   │  │ test_stac   │                │
│  │ test_wmts    │  │ test_csw   │  │ test_ogc_*  │                │
│  │              │  │            │  │             │                │
│  │ HTTP GET/    │  │ HTTP GET/  │  │ HTTP GET/   │                │
│  │ POST         │  │ POST       │  │ POST        │                │
│  │ ↓            │  │ ↓          │  │ ↓           │                │
│  │ Read-only    │  │ Read-only  │  │ Read-only   │                │
│  │ operations   │  │ operations │  │ operations  │                │
│  └──────────────┘  └────────────┘  └─────────────┘                │
│                                                                     │
│  Isolation Strategy:                                                │
│  • Read-only tests (no writes to DB)                                │
│  • Shared SQLite database                                           │
│  • No cleanup needed                                                │
│  • Inherently parallel-safe                                         │
│                                                                     │
│  Test Markers:                                                      │
│  • smoke: Quick tests (<30s)                                        │
│  • integration: Full compliance tests (2-5min)                      │
│  • read_only: Safe for parallel (all Python tests)                  │
└─────────────────────────────────────────────────────────────────────┘
```

## QGIS Test Runner (pytest-xdist + PyQGIS) - Detailed View

```
┌─────────────────────────────────────────────────────────────────────┐
│              QGIS Test Runner (pytest-xdist + PyQGIS)               │
│                        5 cores allocated                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Command: pytest -n 5 tests/qgis/                                   │
│                                                                     │
│  Environment:                                                       │
│  • QT_QPA_PLATFORM=offscreen (headless mode)                        │
│  • QGIS Python bindings (PyQGIS)                                    │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │              Honua Test Server (Docker)                      │  │
│  │              honua:test-cached image                         │  │
│  │              (Same as Python tests)                          │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                           ▲                                         │
│                           │                                         │
│            ┌──────────────┼──────────────────┐                     │
│            │              │                  │                     │
│  ┌─────────▼────────┐  ┌─▼──────────────┐  ┌▼────────────────┐    │
│  │ Worker 1         │  │ Worker 2       │  │ Worker 3        │    │
│  │ (QGIS Instance)  │  │ (QGIS Instance)│  │ (QGIS Instance) │... │
│  ├──────────────────┤  ├────────────────┤  ├─────────────────┤    │
│  │ QgsApplication   │  │ QgsApplication │  │ QgsApplication  │    │
│  │ (Headless)       │  │ (Headless)     │  │ (Headless)      │    │
│  ├──────────────────┤  ├────────────────┤  ├─────────────────┤    │
│  │ Test Scenarios:  │  │ Test Scenarios:│  │ Test Scenarios: │    │
│  │                  │  │                │  │                 │    │
│  │ • Load WMS layer │  │ • Load WFS     │  │ • Load STAC     │    │
│  │ • Render tiles   │  │ • Query feats  │  │ • Load OGC Tiles│    │
│  │ • Verify CRS     │  │ • Filter data  │  │ • Verify props  │    │
│  │ • Check metadata │  │ • Export data  │  │ • Style check   │    │
│  │                  │  │                │  │                 │    │
│  │ Via QGIS Native  │  │ Via QGIS       │  │ Via QGIS        │    │
│  │ HTTP client      │  │ HTTP client    │  │ HTTP client     │    │
│  └──────────────────┘  └────────────────┘  └─────────────────┘    │
│                                                                     │
│  Test Coverage:                                                     │
│  • WMS: Layer loading, rendering, GetFeatureInfo                    │
│  • WFS: Feature queries, filtering, transactions                    │
│  • WMTS: Tile loading, zoom levels                                  │
│  • WCS: Coverage data access                                        │
│  • OGC API Features: Collection access, item retrieval              │
│  • OGC API Tiles: Vector tile rendering                             │
│  • STAC: Catalog browsing, asset loading                            │
│  • GeoServices: Geoservices REST a.k.a. Esri REST API compatibility                         │
│                                                                     │
│  Isolation Strategy:                                                │
│  • Separate QGIS instances per worker                               │
│  • Read-only operations on shared SQLite                            │
│  • No data modification                                             │
│  • Each worker has isolated QgsApplication                          │
└─────────────────────────────────────────────────────────────────────┘
```

## Database Isolation Patterns

### Pattern 1: Transaction-Based Isolation (C# Tests)

```
┌────────────────────────────────────────────────────────────────┐
│                PostgreSQL Container (postgis:16-3.4)           │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│  Database: honua_test                                          │
│  Extensions: postgis                                           │
│                                                                │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │                      Connections                         │ │
│  │                                                          │ │
│  │  ┌───────────────┐  ┌───────────────┐  ┌────────────┐  │ │
│  │  │ Test 1        │  │ Test 2        │  │ Test 3     │  │ │
│  │  │ Connection #1 │  │ Connection #2 │  │ Connection │  │ │
│  │  ├───────────────┤  ├───────────────┤  ├────────────┤  │ │
│  │  │ BEGIN TRANS   │  │ BEGIN TRANS   │  │ BEGIN TRANS│  │ │
│  │  │ ReadCommitted │  │ ReadCommitted │  │ ReadCommit │  │ │
│  │  ├───────────────┤  ├───────────────┤  ├────────────┤  │ │
│  │  │ INSERT data   │  │ INSERT data   │  │ INSERT data│  │ │
│  │  │ UPDATE data   │  │ UPDATE data   │  │ UPDATE data│  │ │
│  │  │ DELETE data   │  │ DELETE data   │  │ DELETE data│  │ │
│  │  │               │  │               │  │            │  │ │
│  │  │ (Test logic)  │  │ (Test logic)  │  │ (Test log) │  │ │
│  │  ├───────────────┤  ├───────────────┤  ├────────────┤  │ │
│  │  │ ROLLBACK      │  │ ROLLBACK      │  │ ROLLBACK   │  │ │
│  │  │ (automatic)   │  │ (automatic)   │  │ (automatic)│  │ │
│  │  └───────────────┘  └───────────────┘  └────────────┘  │ │
│  │                                                          │ │
│  │  Result: All changes are discarded                      │ │
│  │  Time: Microseconds (rollback is O(1) operation)        │ │
│  │  Isolation: Each test sees only its own changes         │ │
│  └──────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────┘
```

### Pattern 2: Read-Only Shared Database (Python/QGIS Tests)

```
┌────────────────────────────────────────────────────────────────┐
│              Docker Container: honua:test-cached               │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │  SQLite Database: /data/ogc-sample.db                    │ │
│  │                                                          │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │ │
│  │  │ cities      │  │ roads       │  │ buildings_3d    │ │ │
│  │  │ (50 pts)    │  │ (100 lines) │  │ (150 polygons)  │ │ │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘ │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │ │
│  │  │ parcels     │  │ parks       │  │ water_bodies    │ │ │
│  │  │ (75 polys)  │  │ (30 polys)  │  │ (35 polygons)   │ │ │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘ │ │
│  │                                                          │ │
│  │  Total: 685 features across 9 datasets                  │ │
│  └──────────────────────────────────────────────────────────┘ │
│                             ▲                                  │
│                             │                                  │
│                             │ (Read-only access)               │
│                             │                                  │
│        ┌────────────────────┼────────────────────┐             │
│        │                    │                    │             │
│    ┌───▼────┐          ┌────▼───┐          ┌────▼───┐         │
│    │Worker 1│          │Worker 2│          │Worker 3│         │
│    │(Python)│          │(Python)│          │ (QGIS) │         │
│    └────────┘          └────────┘          └────────┘         │
│        │                    │                    │             │
│        │ SELECT ...         │ SELECT ...         │ SELECT ...  │
│        │ (no writes)        │ (no writes)        │ (no writes) │
│                                                                │
│  Isolation: Not needed (read-only operations)                  │
│  Cleanup: Not needed (no modifications)                        │
│  Parallel-safe: Yes (SQLite supports multiple readers)         │
└────────────────────────────────────────────────────────────────┘
```

## Test Execution Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                         USER INVOKES                            │
│                 ./scripts/run-tests-parallel.sh                 │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 1: Verify Environment                                      │
│ • Check Docker, .NET, Python                                    │
│ • Verify test data exists                                       │
│ • Check system resources                                        │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 2: Build Test Cache (if --no-build not set)                │
│ • Build Dockerfile.test-cached                                  │
│ • Include prebuilt binaries                                     │
│ • Bake in SQLite databases                                      │
│ • Tag as honua:test-cached                                      │
│ Time: ~2-3 minutes (one-time cost)                              │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 3: Start Test Server                                       │
│ • docker-compose -f docker-compose.test-parallel.yml up -d      │
│ • Wait for health check (http://localhost:8080/health)          │
│ • Server ready in ~5 seconds                                    │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 4: Launch Test Runners (Concurrent)                        │
│                                                                 │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐       │
│  │ C# Tests     │   │ Python Tests │   │ QGIS Tests   │       │
│  │ (Background) │   │ (Background) │   │ (Background) │       │
│  └──────────────┘   └──────────────┘   └──────────────┘       │
│         │                   │                   │              │
│         │                   │                   │              │
│         ▼                   ▼                   ▼              │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐       │
│  │ dotnet test  │   │ pytest -n 5  │   │ pytest -n 5  │       │
│  │ (6 threads)  │   │ (5 workers)  │   │ (5 workers)  │       │
│  └──────────────┘   └──────────────┘   └──────────────┘       │
│         │                   │                   │              │
│         └───────────────────┴───────────────────┘              │
│                             │                                  │
│                             ▼                                  │
│                    Wait for all jobs                           │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 5: Aggregate Results                                       │
│ • Collect exit codes from each runner                           │
│ • Parse JUnit XML and TRX files                                 │
│ • Aggregate pass/fail counts                                    │
│ • Calculate total execution time                                │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 6: Generate Reports                                        │
│ • Console summary (colored output)                              │
│ • HTML reports (if --html)                                      │
│ • Coverage reports (if --coverage)                              │
│ • Exit code (0 = success, 1 = failures)                         │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                      USER SEES SUMMARY                          │
│                                                                 │
│  ╔════════════════════════════════════════════════════════════╗ │
│  ║  Test Execution Summary                                    ║ │
│  ╚════════════════════════════════════════════════════════════╝ │
│                                                                 │
│    C# Tests:     ✓ PASSED                                       │
│    Python Tests: ✓ PASSED                                       │
│    QGIS Tests:   ✓ PASSED                                       │
│                                                                 │
│    Total Time: 8m 32s                                           │
│                                                                 │
│    Detailed Results: TestResults/                               │
└─────────────────────────────────────────────────────────────────┘
```

## Resource Allocation (22-Core System)

```
┌────────────────────────────────────────────────────────────────┐
│                    CPU Core Allocation                         │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│  Core  0-1:  System + Docker overhead                          │
│  Core  2-7:  C# Tests (6 xUnit collections)                    │
│  Core  8-9:  C# Tests overflow (database I/O)                  │
│  Core 10-14: Python Tests (5 pytest workers)                   │
│  Core 15-19: QGIS Tests (5 pytest workers)                     │
│  Core 20-21: Spare capacity                                    │
│                                                                │
│  ┌──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┐ │
│  │██│██│▓▓│▓▓│▓▓│▓▓│▓▓│▓▓│▓▓│▓▓│░░│░░│░░│░░│░░│▒▒│▒▒│▒▒│▒▒│▒▒│ │
│  └──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┘ │
│   0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 17 18 19   │
│                                                                │
│  ██ System/Docker    ▓▓ C# Tests                              │
│  ░░ Python Tests     ▒▒ QGIS Tests                            │
└────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────┐
│                      Memory Allocation                         │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│  Total: ~8GB peak usage                                        │
│                                                                │
│  ┌───────────────────────────────────────┐                    │
│  │ C# Tests                       4-6 GB │                    │
│  │ • 6 PostgreSQL containers (500MB ea.) │                    │
│  │ • .NET test runners                   │                    │
│  └───────────────────────────────────────┘                    │
│                                                                │
│  ┌───────────────────────────────────────┐                    │
│  │ Python Tests                   1-2 GB │                    │
│  │ • 5 Python processes                  │                    │
│  │ • Shared test server                  │                    │
│  └───────────────────────────────────────┘                    │
│                                                                │
│  ┌───────────────────────────────────────┐                    │
│  │ QGIS Tests                     2-3 GB │                    │
│  │ • 5 QGIS instances (PyQGIS)           │                    │
│  │ • Shared test server                  │                    │
│  └───────────────────────────────────────┘                    │
└────────────────────────────────────────────────────────────────┘
```

## Performance Comparison

```
┌────────────────────────────────────────────────────────────────┐
│              Sequential vs. Parallel Execution                 │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│  Sequential (1 core effectively):                              │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │ C# Tests      ████████████████████ 15-20 min            │ │
│  │ Python Tests  ██████████ 8-10 min                        │ │
│  │ QGIS Tests    ████████████ 10-12 min                     │ │
│  └──────────────────────────────────────────────────────────┘ │
│  Total: 33-42 minutes                                          │
│                                                                │
│  Parallel (22 cores):                                          │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │ C# Tests      ██████ 5-7 min                             │ │
│  │ Python Tests  ██ 2-3 min                                 │ │
│  │ QGIS Tests    ███ 3-4 min                                │ │
│  └──────────────────────────────────────────────────────────┘ │
│  Total: 8-12 minutes (longest pole)                            │
│                                                                │
│  Speedup: ~4x faster                                           │
│  Efficiency: ~80% (20 of 22 cores utilized)                    │
└────────────────────────────────────────────────────────────────┘
```

## Test Data Flow

```
┌────────────────────────────────────────────────────────────────┐
│                     Test Data Architecture                     │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│  Source Files (tests/TestData/):                               │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │ seed-data/                                               │ │
│  │  ├── cities.geojson              (50 features)           │ │
│  │  ├── poi.geojson                 (200+ features)         │ │
│  │  ├── roads.geojson               (100 features)          │ │
│  │  ├── transit_routes.geojson      (25 features)           │ │
│  │  ├── parcels.geojson             (75 features)           │ │
│  │  ├── buildings_3d.geojson        (150 features)          │ │
│  │  ├── parks.geojson               (30 features)           │ │
│  │  ├── water_bodies.geojson        (35 features)           │ │
│  │  └── admin_boundaries.geojson    (40 features)           │ │
│  │                                                          │ │
│  │ Total: 685 features across 9 datasets                    │ │
│  └──────────────────────────────────────────────────────────┘ │
│                           │                                    │
│                           ▼                                    │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │ Compiled Databases:                                      │ │
│  │  ├── ogc-sample.db (SQLite)                              │ │
│  │  ├── stac-catalog.db (SQLite)                            │ │
│  │  └── test-metadata.json                                  │ │
│  └──────────────────────────────────────────────────────────┘ │
│                           │                                    │
│                           ▼                                    │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │ Docker Image: honua:test-cached                          │ │
│  │  /data/ogc-sample.db                                     │ │
│  │  /data/stac-catalog.db                                   │ │
│  │  /data/test-metadata.json                                │ │
│  │  (Baked into image layers)                               │ │
│  └──────────────────────────────────────────────────────────┘ │
│                           │                                    │
│                           ▼                                    │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │ Runtime: Test Server Container                           │ │
│  │  Read-only access to preloaded data                      │ │
│  │  No initialization time                                  │ │
│  │  Instant availability                                    │ │
│  └──────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────┘
```

This architecture enables **~4x faster** test execution while maintaining **complete database isolation** and **100% test reliability**.
