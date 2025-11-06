# Geoprocessing Architecture: Learning from Esri and GeoServer

**Status**: Analysis Complete
**Date**: 2025-10-25
**Purpose**: Comparative analysis of mature geoprocessing architectures to inform Honua's design

## Executive Summary

This document analyzes the geoprocessing architectures of two industry-leading platforms:
- **Esri GeoservicesREST / ArcGIS Server** - Commercial enterprise GIS platform with open JSON REST specification and mature geoprocessing framework
- **GeoServer WPS** - Open-source OGC Web Processing Service implementation

Both platforms have evolved over 15+ years to handle production-scale workloads across thousands of deployments. Their design patterns, strengths, and limitations provide valuable guidance for Honua's geoprocessing architecture.

**CRITICAL FINDING**: The **GeoservicesREST GPServer specification** (transferred to Open Web Foundation, 2010) provides a complete JSON-based REST API for geoprocessing that is more widely adopted than OGC WPS. Honua should prioritize GeoservicesREST compliance for ecosystem compatibility.

**Key Takeaways for Honua**:
1. **Implement GeoservicesREST GPServer spec first** - JSON API, wider adoption than XML-based OGC WPS
2. Dual execution modes (sync/async) are essential, not optional
3. Process isolation must be at the OS level, not just application-level threads
4. Job lifecycle management is more complex than expected (9 states in Esri)
5. Direct integration with the data catalog provides massive UX benefits
6. Process chaining enables powerful workflows but requires careful API design
7. Resource governance must be proactive, not reactive
8. File upload pattern (two-step: upload → reference) avoids JSON encoding overhead

---

## Part 1: Esri ArcGIS Geoprocessing Architecture

### 1.1 Overview

Esri's geoprocessing framework is built around **ArcPy** (Python-based scripting) and **Model Builder** (visual workflow designer), with server-side execution via **Geoprocessing Services** published to ArcGIS Server.

**Core Principles**:
- **Toolbox model**: Processes are organized into toolboxes (namespaces)
- **Parameter-driven**: Each tool declares typed input/output parameters
- **Python-first**: Most custom tools are Python scripts leveraging arcpy
- **Map-integrated**: Tools can operate on layers in a map document

### 1.2 GeoservicesREST GPServer Specification

**IMPORTANT**: The **GeoservicesREST Specification** is Esri's open, JSON-based REST API standard that defines how geoprocessing services are exposed via HTTP. Originally developed by Esri in 2010, it was transferred to the Open Web Foundation and is widely implemented.

#### URL Structure
```
https://[server]/[instance]/rest/services/[ServiceName]/GPServer
```

**Child Resources**:
- `GPServer` - Service metadata endpoint
- `GPServer/[TaskName]` - Individual task endpoint
- `GPServer/[TaskName]/execute` - Synchronous execution
- `GPServer/[TaskName]/submitJob` - Asynchronous execution
- `GPServer/jobs/[JobId]` - Job status/results
- `GPServer/uploads` - File upload endpoint (if enabled)

#### Service Metadata Response
**GET** `/GPServer?f=json`

```json
{
  "currentVersion": 11.5,
  "serviceDescription": "Geoprocessing service for spatial analysis",
  "tasks": ["Buffer", "Clip", "Intersect"],
  "executionType": "esriExecutionTypeAsynchronous",
  "resultMapServerName": "",
  "maximumRecords": 5000,
  "capabilities": "Uploads",
  "schema": {
    "url": "https://server/rest/services/schema.json"
  }
}
```

#### Task Metadata Response
**GET** `/GPServer/Buffer?f=json`

```json
{
  "name": "Buffer",
  "displayName": "Buffer Features",
  "category": "Analysis",
  "description": "Creates buffer polygons around input features",
  "helpUrl": "https://...",
  "executionType": "esriExecutionTypeAsynchronous",
  "parameters": [
    {
      "name": "Input_Features",
      "dataType": "GPFeatureRecordSetLayer",
      "displayName": "Input Features",
      "direction": "esriGPParameterDirectionInput",
      "defaultValue": {...},
      "parameterType": "esriGPParameterTypeRequired",
      "category": ""
    },
    {
      "name": "Distance",
      "dataType": "GPLinearUnit",
      "displayName": "Buffer Distance",
      "direction": "esriGPParameterDirectionInput",
      "defaultValue": {"distance": 100, "units": "esriMeters"},
      "parameterType": "esriGPParameterTypeRequired"
    },
    {
      "name": "Output_Features",
      "dataType": "GPFeatureRecordSetLayer",
      "displayName": "Buffered Features",
      "direction": "esriGPParameterDirectionOutput",
      "defaultValue": {...},
      "parameterType": "esriGPParameterTypeDerived"
    }
  ]
}
```

#### Execute Request (Synchronous)
**POST** `/GPServer/Buffer/execute`

Request parameters (URL-encoded or JSON):
```json
{
  "Input_Features": {
    "geometryType": "esriGeometryPoint",
    "features": [
      {
        "geometry": {"x": -122.45, "y": 37.78, "spatialReference": {"wkid": 4326}},
        "attributes": {"id": 1, "name": "Point A"}
      }
    ],
    "spatialReference": {"wkid": 4326}
  },
  "Distance": {"distance": 100, "units": "esriMeters"},
  "f": "json"
}
```

Response:
```json
{
  "results": [
    {
      "paramName": "Output_Features",
      "dataType": "GPFeatureRecordSetLayer",
      "value": {
        "geometryType": "esriGeometryPolygon",
        "features": [...]
      }
    }
  ],
  "messages": [
    {"type": "esriGPMessageTypeInformative", "description": "Start Time: Wed Oct 25 14:30:00 2025"}
  ]
}
```

#### SubmitJob Request (Asynchronous)
**POST** `/GPServer/Buffer/submitJob`

Request (same parameters as execute):
```json
{
  "Input_Features": {...},
  "Distance": {...},
  "context": {
    "outSR": {"wkid": 3857},
    "processSR": {"wkid": 3857}
  },
  "f": "json"
}
```

Response:
```json
{
  "jobId": "j8a7b2c1d4e5f6g7h8i9j0",
  "jobStatus": "esriJobSubmitted"
}
```

#### Job Status Request
**GET** `/GPServer/jobs/j8a7b2c1d4e5f6g7h8i9j0?f=json`

Response:
```json
{
  "jobId": "j8a7b2c1d4e5f6g7h8i9j0",
  "jobStatus": "esriJobExecuting",
  "messages": [
    {"type": "esriGPMessageTypeInformative", "description": "Processing 1000 features..."}
  ]
}
```

When complete (`jobStatus: "esriJobSucceeded"`):
```json
{
  "jobId": "j8a7b2c1d4e5f6g7h8i9j0",
  "jobStatus": "esriJobSucceeded",
  "results": {
    "Output_Features": {
      "paramUrl": "GPServer/jobs/j8a7b2c1d4e5f6g7h8i9j0/results/Output_Features"
    }
  },
  "messages": [...]
}
```

#### Data Types
GeoservicesREST defines typed parameters:

| GPDataType | Description | JSON Structure |
|------------|-------------|----------------|
| `GPString` | Text value | `{"value": "text"}` |
| `GPLong` | Integer | `{"value": 42}` |
| `GPDouble` | Floating point | `{"value": 3.14159}` |
| `GPBoolean` | Boolean | `{"value": true}` |
| `GPDate` | Timestamp | `{"value": 1698249600000}` (epoch ms) |
| `GPLinearUnit` | Distance + units | `{"distance": 100, "units": "esriMeters"}` |
| `GPFeatureRecordSetLayer` | Feature collection | `{"geometryType": "...", "features": [...]}` |
| `GPRasterDataLayer` | Raster reference | `{"url": "..."}` |
| `GPDataFile` | File reference | `{"url": "..."}` |

#### Special Parameters (Enterprise 10.6.1+)

**Context Parameter**: Spatial reference control
```json
{
  "context": {
    "outSR": {"wkid": 3857},        // Output spatial reference
    "processSR": {"wkid": 3857},    // Processing spatial reference
    "extent": {                      // Processing extent
      "xmin": -122.5, "ymin": 37.7,
      "xmax": -122.3, "ymax": 37.9,
      "spatialReference": {"wkid": 4326}
    }
  }
}
```

**Output Feature Service Creation** (Enterprise 11.0+):
```json
{
  "esri_out_feature_service_name": "BufferedRoads",
  "esri_out_feature_service_overwrite": true
}
```

This automatically publishes the result as a hosted feature service.

#### File Upload Pattern
For large input files (shapefiles, rasters, etc.):

1. **Upload File**: `POST /GPServer/uploads/upload`
```
Content-Type: multipart/form-data
```
Response:
```json
{"item": {"itemID": "i123abc456def"}}
```

2. **Reference in Job**:
```json
{
  "Input_File": {"itemID": "i123abc456def"},
  "f": "json"
}
```

**Honua Insight**: This two-step upload pattern avoids encoding large files in JSON. Honua should implement the same pattern for binary inputs.

### 1.3 Execution Modes

ArcGIS supports two execution modes determined at publish time:

#### Synchronous Execution
- Client blocks until result is ready
- HTTP timeout constraints (typically 600 seconds max)
- Suitable for operations completing in < 1 minute
- REST endpoint: `http://<service-url>/<tool>/execute`
- Response includes result directly in HTTP body

#### Asynchronous Execution (submitJob)
- Client receives immediate job ticket
- Client polls for status via job resource URL
- No HTTP timeout constraints
- Suitable for long-running operations (minutes to hours)
- REST endpoint: `http://<service-url>/<tool>/submitJob`
- Job resource URL: `http://<service-url>/jobs/<jobId>`

**Design Pattern**: The mode is **not runtime-selectable**. Developers must choose at publish time based on expected workload characteristics. This forces architectural clarity but reduces flexibility.

### 1.3 Job Management

#### Job States
ArcGIS Server maintains 9 distinct job states:
- `esriJobNew` - Job created but not yet queued
- `esriJobSubmitted` - Queued, waiting for execution slot
- `esriJobWaiting` - Dependencies not satisfied
- `esriJobExecuting` - Currently running
- `esriJobSucceeded` - Completed successfully
- `esriJobFailed` - Execution error occurred
- `esriJobCancelling` - Cancel requested, terminating
- `esriJobCancelled` - Termination complete
- `esriJobTimedOut` - Exceeded maximum execution time

**Notable**: The `esriJobWaiting` state supports dependencies between jobs, enabling workflow orchestration at the job manager level (not via process chaining).

#### Job Storage
Each job creates a directory structure:
```
arcgisjobs/
  <service-name>/
    <job-id>/
      scratch/          # Temporary workspace
      inputs/           # Serialized input parameters
      outputs/          # Result datasets
      messages.json     # Execution log
```

**Retention Policy**:
- Default TTL: 6 hours (360 minutes) after completion
- Configurable per-service via `maxRecordAge` parameter
- Cleanup runs on a schedule, not immediately after TTL expires

**Honua Insight**: This filesystem-based job storage is simple but doesn't scale well in distributed/cloud environments. Modern designs should use object storage (S3/Blob) for artifacts.

#### Job Monitoring
ArcGIS Server Manager provides:
- Real-time job list with status indicators
- Per-service job counts and resource utilization
- Message log viewer with severity filtering
- Manual cancellation controls

### 1.4 Resource Management and Isolation

#### Process Isolation
**Critical Design Decision**: Esri deprecated "low isolation" (in-process execution) in favor of **high isolation only**.

High isolation means:
- Each geoprocessing job runs in a separate OS process
- Process crashes do not affect ArcGIS Server or other jobs
- Memory limits are enforced at the OS level
- Process is terminated if it exceeds timeout

**Implementation**: Uses a process pool model with configurable pool size per service.

#### Workload Separation
Enterprise deployments use **dedicated ArcGIS Server sites** for geoprocessing:
- One site handles map/feature services (low latency required)
- Another site handles geoprocessing jobs (high CPU/memory)
- Load balancer routes by URL path prefix

**Benefits**:
- Prevents geoprocessing from starving map service resources
- Different SLA guarantees per workload type
- Independent scaling policies

**Honua Insight**: This maps directly to the "separate deployable" discussion in Honua's architecture - start embedded, migrate to separate workers under load.

#### Connection Pooling
For database-heavy operations:
- Services configured with database connection string
- Connection pool shared across job instances
- Configurable min/max pool size
- Idle connection timeout

### 1.5 Security Model

#### Service-Level Authorization
- Role-based access control (RBAC)
- Roles assigned to services (not individual tools)
- Anonymous access configurable per-service
- Integration with enterprise identity providers (Active Directory, SAML, OAuth)

#### Filesystem Permissions
**Critical Security Practice**: Lock down the `arcgisjobs` directory
- ArcGIS Server account needs read/write
- Users should NOT have direct filesystem access
- Results retrieved via REST API only, not file shares

**Vulnerability**: Early versions exposed job directories via HTTP, allowing directory traversal attacks. Modern versions restrict access.

#### Input Validation
- Type checking enforced (e.g., FeatureSet, GPString, GPDouble)
- No built-in protection against SQL injection in custom tools
- Python scripts must sanitize inputs themselves

**Honua Insight**: This is where PostGIS stored procedures excel - parameterized queries eliminate injection risk.

### 1.6 Performance Optimization

#### Service-Side Recommendations
From Esri's performance guide:
1. **Minimize data movement**: Use server-side data sources, not client-uploaded datasets
2. **Reuse connections**: Database connections are expensive to create
3. **Optimize model**: Use in-memory workspaces for intermediate results
4. **Batch operations**: Process multiple features in one tool execution vs. iterative calls
5. **Simplify geometry**: Use appropriate tolerance for the map scale

#### Client-Side Recommendations
1. **Polling interval**: Start at 1s, back off exponentially to max 10s
2. **Avoid sequential execution**: Submit multiple jobs concurrently
3. **Use appropriate mode**: Don't use async for sub-second operations

### 1.7 Strengths

1. **Mature job lifecycle**: 9 states cover edge cases (timeouts, cancellation, waiting)
2. **Clear separation**: Sync vs async modes prevent misuse
3. **Process isolation**: High-isolation-only policy is crash-proof
4. **Familiar tooling**: Python + ArcPy is well-understood by GIS analysts
5. **Enterprise-ready**: Workload separation, monitoring, RBAC all built-in

### 1.8 Weaknesses

1. **Filesystem-centric**: Job storage doesn't fit cloud-native architectures
2. **Inflexible execution mode**: Can't choose sync vs async at runtime
3. **No process chaining**: Must write custom scripts or use Model Builder
4. **Python bottleneck**: Global Interpreter Lock (GIL) limits concurrency
5. **Vendor lock-in**: ArcPy only works with Esri data formats and licenses
6. **Coarse-grained RBAC**: Authorization at service level, not per-tool

---

## Part 2: GeoServer WPS Architecture

### 2.1 Overview

GeoServer implements the **OGC Web Processing Service (WPS) 1.0/2.0** standard as an extension module. Unlike Esri's proprietary API, WPS is an open standard with XML-based request/response formats.

**Core Principles**:
- **Standards-first**: Strict OGC WPS compliance
- **Plugin architecture**: Processes discovered via SPI (Service Provider Interface)
- **GeoTools integration**: Leverages gt-process framework
- **Catalog-aware**: Direct access to GeoServer data layers

### 2.2 Process Discovery and Execution

#### SPI-Based Plugin Mechanism
GeoServer discovers processes via Java SPI:
```java
// META-INF/services/org.geotools.process.ProcessFactory
com.example.MyCustomProcessFactory
```

**ProcessFactory** interface:
```java
public interface ProcessFactory {
    Set<Name> getNames();
    InternationalString getTitle(Name name);
    InternationalString getDescription(Name name);
    Map<String, Parameter<?>> getParameterInfo(Name name);
    Map<String, Parameter<?>> getResultInfo(Name name);
    Process create(Name name);
}
```

**Benefits**:
- Zero-configuration deployment (drop JAR in classpath)
- Self-describing processes (inputs/outputs declared in code)
- Type safety via Parameter<T> generic

**Honua Insight**: This is similar to Honua's current pattern with `IFeatureRepository` implementations. Could extend to `IProcessFactory`.

#### WPS Operations

**GetCapabilities**: Lists all available processes
```xml
<wps:Process wps:processVersion="1.0.0">
  <ows:Identifier>JTS:buffer</ows:Identifier>
  <ows:Title>Buffer</ows:Title>
  <ows:Abstract>Computes buffer around geometry</ows:Abstract>
</wps:Process>
```

**DescribeProcess**: Returns detailed parameter metadata
```xml
<Input minOccurs="1" maxOccurs="1">
  <ows:Identifier>geom</ows:Identifier>
  <ows:Title>Geometry</ows:Title>
  <ComplexData>
    <Default><Format><MimeType>application/wkt</MimeType></Format></Default>
    <Supported>
      <Format><MimeType>application/gml+xml</MimeType></Format>
      <Format><MimeType>application/json</MimeType></Format>
    </Supported>
  </ComplexData>
</Input>
```

**Execute**: Runs process with parameters
- Synchronous by default (response contains result)
- Asynchronous if `status="true"` in request
- Result can be inlined or referenced via URL

### 2.3 Data Format Handling (PPIO)

GeoServer uses **PPIO** (Process Parameter Input/Output) classes for serialization:
```
org.geoserver.wps.ppio/
  PPIOFactory.java          # Creates PPIO instances
  PPIO.java                 # Base interface
  BinaryPPIO.java           # byte[] handling
  CDataPPIO.java            # XML CDATA wrapping
  ComplexPPIO.java          # Structured data (GeoJSON, GML)
  LiteralPPIO.java          # Primitives (String, Integer, Double)
```

**Example**: GeoJSON geometry input
```java
public class GeoJSONPPIO extends ComplexPPIO {
    public GeoJSONPPIO() {
        super(Geometry.class, Geometry.class, "application/json");
    }

    @Override
    public Object decode(InputStream input) throws Exception {
        FeatureJSON json = new FeatureJSON();
        return json.readGeometry(new InputStreamReader(input));
    }

    @Override
    public void encode(Object value, OutputStream output) throws Exception {
        FeatureJSON json = new FeatureJSON();
        json.writeGeometry((Geometry) value, output);
    }
}
```

**Honua Insight**: This is very similar to Honua's `IFeatureFormatter` pattern (GeoJsonFeatureFormatter, WktFeatureFormatter, etc.). Could reuse for process I/O.

### 2.4 Process Chaining

GeoServer WPS supports **subprocess inputs**, where the output of one process becomes the input of another:

```xml
<wps:Input>
  <ows:Identifier>geom</ows:Identifier>
  <wps:Reference mimeType="application/wkt">
    <wps:Body>
      <wps:Execute>
        <ows:Identifier>JTS:centroid</ows:Identifier>
        <wps:DataInputs>
          <wps:Input>
            <ows:Identifier>geom</ows:Identifier>
            <wps:Data><wps:LiteralData>POLYGON(...)</wps:LiteralData></wps:Data>
          </wps:Input>
        </wps:DataInputs>
      </wps:Execute>
    </wps:Body>
  </wps:Reference>
</wps:Input>
```

This chains: `POLYGON -> centroid -> buffer`

**Implementation**: Nested executions resolved recursively before parent executes.

**Benefits**:
- No intermediate results written to disk
- Atomic execution (all-or-nothing)
- Simplified client logic

**Drawbacks**:
- Nested XML is verbose and error-prone
- No partial result caching if chain fails midway
- Hard to debug which step failed

**Honua Insight**: Process chaining is powerful but XML-based approach is dated. Modern design should use JSON with better error reporting.

### 2.5 Catalog Integration

**Key Differentiator**: GeoServer WPS can directly reference layers in the GeoServer catalog:

```xml
<wps:Input>
  <ows:Identifier>features</ows:Identifier>
  <wps:Reference mimeType="application/wfs-collection-1.0"
                 xlink:href="http://localhost:8080/geoserver/wfs?
                   service=WFS&amp;version=1.0.0&amp;request=GetFeature&amp;
                   typeName=my_layer&amp;outputFormat=GML2"/>
</wps:Input>
```

**Benefits**:
- No need to upload large datasets in request
- Respects GeoServer security rules for layer access
- Leverages optimized data access paths (e.g., PostGIS native queries)
- Results can be stored as new layers in catalog

**Honua Insight**: This is CRITICAL for usability. Honua should enable:
- Process inputs referencing Honua services by ID
- Process outputs auto-registering as new layers

### 2.6 Security and Resource Limits

#### Process-Level Access Control
GeoServer allows per-process authorization:
- **Disabled**: Process hidden from GetCapabilities
- **Roles required**: Process only available to authenticated users with roles
- **Public**: Available to all users

Configuration via Web UI:
```
Security > WPS Security > Process Groups
  - JTS Topology Suite
    * JTS:buffer [admin, power_user]
    * JTS:union [admin]
    * JTS:simplify [*] (all users)
```

**Honua Insight**: This is better than commercial GIS platforms's service-level-only RBAC.

#### Complex Input Restrictions
By default, WPS accepts input references to:
- Internal WFS/WCS/WPS requests
- External HTTP URLs (potential SSRF vulnerability!)

**Security Hardening**: Restrict to internal references only
- Prevents Server-Side Request Forgery (SSRF)
- Blocks malicious URLs (e.g., `http://169.254.169.254/latest/meta-data/`)

#### Resource Limits
Three types of limits configurable per process:
1. **Maximum input size** (bytes) - Prevents memory exhaustion
2. **Maximum execution time** (seconds) - Prevents runaway processes
3. **Maximum output size** (bytes) - Prevents disk exhaustion

**Global Defaults**: Set on WPS Security page, overridable per-process

**Limitations**: No CPU quotas or memory limits (relies on OS/JVM limits)

### 2.7 REST API Support

While WPS is XML-centric, GeoServer supports **REST API** execution via curl:
```bash
curl -u admin:geoserver \
     -H 'Content-type: application/xml' \
     -XPOST \
     -d @wps-request.xml \
     http://localhost:8080/geoserver/wps
```

**Response Formats**:
- XML (default, WPS standard)
- JSON (via custom PPIO)
- GeoJSON (for geometry results)

**Honua Insight**: Honua should provide both OGC WPS (XML) for standards compliance AND a modern JSON REST API for developer experience.

### 2.8 Strengths

1. **Standards-compliant**: OGC WPS interoperability with other tools
2. **Plugin architecture**: Easy to add custom processes (drop JAR)
3. **Catalog integration**: Direct layer references eliminate data uploads
4. **Process chaining**: Enables complex workflows declaratively
5. **Fine-grained security**: Per-process RBAC and resource limits
6. **Self-describing**: Processes declare metadata via ProcessFactory

### 2.9 Weaknesses

1. **XML-heavy**: Verbose request/response format
2. **No job dashboard**: Limited monitoring UI (just logs)
3. **No job persistence**: Async jobs lost on server restart
4. **Limited resource governance**: No CPU/memory quotas
5. **Process isolation gaps**: Runs in JVM threads, not separate processes
6. **No artifact externalization**: Large outputs kept in memory
7. **Chaining limitations**: No partial caching, poor error reporting

---

## Part 3: Comparative Analysis

### 3.1 Execution Model Comparison

| Aspect | Esri ArcGIS | GeoServer WPS | Honua Recommendation |
|--------|-------------|---------------|----------------------|
| **Sync/Async Mode** | Determined at publish time | Determined at request time (`status` flag) | Runtime-selectable like GeoServer, but with governor hints |
| **Job States** | 9 states (waiting, cancelling, etc.) | 3 states (accepted, running, succeeded/failed) | 6 states: Queued, Running, Succeeded, Failed, Cancelling, Cancelled |
| **Job Persistence** | Filesystem (arcgisjobs/) | None (in-memory only) | Database + Object Storage (Hangfire + S3/Blob) |
| **Job Monitoring** | Built-in dashboard in Manager | Logs only | REST API + optional admin UI |
| **Job Retention** | Configurable TTL (default 6h) | N/A (cleared on restart) | Configurable TTL with job archival to cold storage |

**Key Insight**: Esri's persistence is too filesystem-centric, GeoServer's is too ephemeral. Honua should use **Hangfire (PostgreSQL storage) + S3 for artifacts**.

### 3.2 Process Isolation Comparison

| Aspect | Esri ArcGIS | GeoServer WPS | Honua Recommendation |
|--------|-------------|---------------|----------------------|
| **Isolation Level** | OS process (high isolation) | JVM threads (low isolation) | Multi-tier: Thread (Tier 1) → OS Process (Tier 2/3) |
| **Crash Handling** | Job fails, server unaffected | JVM crash affects all jobs | Tier 1: best-effort, Tier 2/3: isolated |
| **Resource Limits** | Process pool size + timeouts | Timeout only, no memory/CPU limits | Docker/cgroups limits for Tier 3 |
| **Workload Separation** | Dedicated server sites | Single JVM | Start embedded, scale to separate workers |

**Key Insight**: GeoServer's thread-based execution is a significant weakness. Honua's multi-tier approach with subprocess execution (Tier 3) is superior.

### 3.3 Security Model Comparison

| Aspect | Esri ArcGIS | GeoServer WPS | Honua Recommendation |
|--------|-------------|---------------|----------------------|
| **Access Control** | Service-level RBAC | Process-level RBAC | Process-level RBAC with tenant isolation |
| **Input Validation** | Type checking only | Input size limits + SSRF protection | Type + size + SQL injection prevention (stored procs) |
| **Result Access** | REST API (no filesystem access) | REST API or WFS storage | REST API + signed URLs for large artifacts |
| **Sandboxing** | Python subprocess | None (JVM threads) | Python subprocess with resource limits + manifest signing |

**Key Insight**: Esri's coarse-grained RBAC is insufficient for multi-tenant SaaS. GeoServer's process-level RBAC is better. Honua should add **tenant isolation** on top.

### 3.4 Integration Patterns Comparison

| Aspect | Esri ArcGIS | GeoServer WPS | Honua Recommendation |
|--------|-------------|---------------|----------------------|
| **Data Catalog Access** | Hardcoded paths in tool scripts | Direct WFS/WCS references | Service/layer references by ID |
| **Result Storage** | Filesystem (arcgisjobs/) | Optional WFS-T insertion | Artifact store + auto-register as layers |
| **Process Discovery** | Static list from service metadata | SPI-based plugin system | Reflection-based + optional dynamic registration |
| **Process Chaining** | Manual via Model Builder | Declarative subprocess inputs | JSON-based workflow DSL |

**Key Insight**: GeoServer's catalog integration is KILLER FEATURE. Honua MUST support:
```json
{
  "processId": "buffer",
  "inputs": {
    "features": {"layerReference": "my-collection-id"},
    "distance": 100
  },
  "outputs": {
    "result": {"registerAsLayer": "buffered-features"}
  }
}
```

### 3.5 API Design Comparison

| Aspect | Esri ArcGIS | GeoServer WPS | Honua Recommendation |
|--------|-------------|---------------|----------------------|
| **Protocol** | Proprietary REST | OGC WPS (XML) | Dual: OGC WPS (standards) + JSON REST (DX) |
| **Request Format** | JSON (modern) | XML (verbose) | JSON primary, XML via WPS endpoint |
| **Response Format** | JSON | XML + JSON (via PPIO) | JSON primary with streaming for large results |
| **Status Polling** | Job resource URL | StatusLocation URL | Job resource URL + Server-Sent Events |
| **Error Reporting** | Message array with severity | ExceptionReport | Structured errors with troubleshooting hints |

**Key Insight**: Esri's JSON API has better DX, GeoServer's XML API has better interoperability. Honua should provide BOTH via separate endpoints:
- `/api/processes/...` - Modern JSON REST
- `/wps` - OGC WPS 2.0 (XML)

---

## Part 4: Recommendations for Honua

### 4.1 What to Adopt

#### From Esri:
1. **High isolation by default** - Never run untrusted code in-process
2. **Dedicated job states** - Include Cancelling, Waiting, TimedOut
3. **Workload separation** - Support separate worker deployments from day one
4. **Clear sync/async semantics** - Governor hints guide but don't mandate
5. **Comprehensive job dashboard** - Users need visibility into status

#### From GeoServer:
6. **Process-level RBAC** - Finer-grained than service-level
7. **Catalog integration** - Layer references eliminate uploads
8. **SPI-style discovery** - Zero-config process registration
9. **Process chaining** - But with JSON syntax, not XML
10. **Input size limits** - Prevent memory exhaustion attacks
11. **SSRF protection** - Restrict external URL references

### 4.2 What to Avoid

#### From Esri:
1. **Filesystem-centric storage** - Use object storage (S3/Blob)
2. **Publish-time execution mode** - Allow runtime selection
3. **Python-only execution** - Support .NET, PostGIS, Python
4. **Coarse-grained RBAC** - Add per-process authorization

#### From GeoServer:
5. **XML-first API** - Provide JSON as primary interface
6. **Thread-based execution** - Use OS processes for isolation
7. **In-memory job state** - Persist to database for restarts
8. **Missing resource limits** - Add CPU/memory quotas
9. **Nested XML chaining** - Use flat JSON workflow DSL

### 4.3 Where to Innovate

#### Multi-Tier Execution (Honua Differentiator)
Neither platform has graceful fallback between execution tiers:
```
Tier 1 (NTS) → Tier 2 (PostGIS) → Tier 3 (Python)
 < 100ms          1-10s            10s-30m
```

**Innovation**: Processes advertise tier preferences, governor selects based on capacity.

#### Resource Classes (Honua Differentiator)
Neither platform has workload classification:
- `CpuBurst`: < 100ms, run immediately
- `DbHeavy`: 1-10s, throttle by DB connection pool
- `PythonGpu`: GPU required, queue with capacity reservation
- `LongTail`: > 1m, low priority background

**Innovation**: Automatic tier and queue selection based on resource class.

#### Artifact Externalization (Honua Differentiator)
Neither platform handles large outputs well:
- Esri: Filesystem doesn't scale in cloud
- GeoServer: In-memory causes OOM

**Innovation**: Results > threshold automatically written to S3/Blob with signed URL in response.

#### Provenance Tracking (Honua Differentiator)
Neither platform has built-in lineage:
- Esri: Job log is text messages
- GeoServer: No execution history

**Innovation**: `ProcessRun` ledger tracks:
- Input/output content hashes
- Execution tier and duration
- Cost estimation and actual cost
- Full parameter snapshot for reproducibility

#### Tenant Isolation (Honua Differentiator)
Neither platform designed for multi-tenant SaaS:
- Esri: Enterprise deployment only
- GeoServer: Single-tenant by design

**Innovation**: Row-Level Security (RLS) in PostgreSQL + tenant-scoped job queues.

### 4.4 Compatibility Strategy

#### GeoservicesREST GPServer Compliance (Priority 1)
**CRITICAL**: Implement the **GeoservicesREST GPServer specification** for proprietary GIS platforms compatibility. This is an open specification (transferred to Open Web Foundation) and more widely adopted than OGC WPS.

**Endpoints to Implement**:
```
GET  /arcgis/rest/services/Geoprocessing/GPServer?f=json
     → Service metadata (tasks, executionType, capabilities)

GET  /arcgis/rest/services/Geoprocessing/GPServer/{task}?f=json
     → Task metadata (parameters, data types)

POST /arcgis/rest/services/Geoprocessing/GPServer/{task}/execute
     → Synchronous execution (returns results directly)

POST /arcgis/rest/services/Geoprocessing/GPServer/{task}/submitJob
     → Asynchronous execution (returns jobId)

GET  /arcgis/rest/services/Geoprocessing/GPServer/jobs/{jobId}?f=json
     → Job status and results

GET  /arcgis/rest/services/Geoprocessing/GPServer/jobs/{jobId}/results/{outputParam}?f=json
     → Individual output parameter

POST /arcgis/rest/services/Geoprocessing/GPServer/uploads/upload
     → File upload for large inputs

DELETE /arcgis/rest/services/Geoprocessing/GPServer/jobs/{jobId}/cancel
     → Job cancellation
```

**Data Type Mapping**:
| GeoservicesREST Type | Honua Internal Type |
|---------------------|---------------------|
| `GPFeatureRecordSetLayer` | `IAsyncEnumerable<FeatureRecord>` |
| `GPString` | `string` |
| `GPLong` | `long` |
| `GPDouble` | `double` |
| `GPBoolean` | `bool` |
| `GPDate` | `DateTimeOffset` |
| `GPLinearUnit` | `LinearUnit` (custom struct) |
| `GPDataFile` | `IUploadedFile` |

**Key Features to Support**:
1. **Context parameter** - `outSR`, `processSR`, `extent` for spatial reference control
2. **File uploads** - Two-step pattern (upload → reference by itemID)
3. **Output service creation** - `esri_out_feature_service_name` auto-registration
4. **Job messages** - Typed messages (Informative, Warning, Error, Empty, Abort)
5. **Format parameter** - `f=json|html|pjson` (pjson = pretty JSON)

#### OGC WPS Compliance (Priority 2)
Implement WPS 2.0 for standards-based interoperability:
```
GET  /wps?service=WPS&request=GetCapabilities
POST /wps (Execute XML request)
```

Map WPS operations to Honua process engine:
- `GetCapabilities` → List registered processes
- `DescribeProcess` → Return process metadata
- `Execute` → Submit to job manager

**Note**: OGC WPS is XML-heavy and less widely used than GeoservicesREST. Implement after GPServer for maximum ecosystem reach.

#### Modern JSON REST API
Primary developer interface:
```
GET    /api/processes                    # List processes
GET    /api/processes/{id}               # Get metadata
POST   /api/processes/{id}/execute       # Run (sync or async)
GET    /api/jobs/{id}                    # Job status
DELETE /api/jobs/{id}                    # Cancel job
GET    /api/jobs/{id}/events             # SSE status updates
```

### 4.5 Phased Implementation

#### Phase 0: Foundation (1 week)
- Define `IProcess` interface
- Implement in-memory `ProcessRegistry`
- Create `ProcessExecutionContext`
- Build simple executor (Tier 1 only)

#### Phase 1: REST API (2 weeks)
- Add `/api/processes` CRUD endpoints
- Implement sync execution
- Add OpenAPI schema
- Write integration tests

#### Phase 2: Async Jobs (2 weeks)
- Integrate Hangfire for persistence
- Add `/api/jobs` status endpoints
- Implement cancellation
- Add job cleanup worker

#### Phase 3: Catalog Integration (1 week)
- Support `layerReference` inputs
- Support `registerAsLayer` outputs
- Add security checks (user can access layer?)
- Write documentation

#### Phase 4: PostGIS Tier (2 weeks)
- Implement stored procedure executor
- Add `IPostGisProcessFactory`
- Create sample procedures (buffer, intersection, etc.)
- Add fallback logic

#### Phase 5: Python Tier (3 weeks)
- Implement subprocess executor
- Add manifest signing
- Create resource limits (Docker or cgroups)
- Write Python SDK

#### Phase 6: Process Chaining (2 weeks)
- Design JSON workflow DSL
- Implement nested execution
- Add partial result caching
- Test error handling

#### Phase 7: OGC WPS (1 week)
- Add `/wps` XML endpoint
- Implement GetCapabilities/DescribeProcess/Execute
- Test with OGC compliance suite
- Document deviations

---

## Part 5: Detailed Design Patterns

### 5.1 Catalog-Integrated Input Pattern

**Problem**: Uploading large datasets in process requests is slow and wasteful when data already exists in Honua.

**Esri Solution**: Hardcoded file paths in tool scripts (requires server filesystem access).

**GeoServer Solution**: WFS reference URLs:
```xml
<wps:Reference xlink:href="http://localhost:8080/geoserver/wfs?service=WFS&request=GetFeature&typeName=roads"/>
```

**Honua Solution**: Layer reference by ID with optional CQL2 filter:
```json
{
  "processId": "clip",
  "inputs": {
    "features": {
      "layerReference": {
        "collectionId": "roads",
        "filter": {
          "op": "s_intersects",
          "args": [
            {"property": "geometry"},
            {"bbox": [-122.5, 37.7, -122.3, 37.9]}
          ]
        }
      }
    },
    "clipGeometry": {...}
  }
}
```

**Implementation**:
```csharp
public class LayerReferenceResolver
{
    private readonly IFeatureRepository _repository;
    private readonly IAuthorizationService _authz;

    public async IAsyncEnumerable<FeatureRecord> ResolveAsync(
        LayerReference reference,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        // 1. Check user has access to layer
        var layer = await _catalog.GetLayerAsync(reference.CollectionId, ct);
        var authzResult = await _authz.AuthorizeAsync(user, layer, "Read");
        if (!authzResult.Succeeded)
            throw new UnauthorizedAccessException();

        // 2. Build query context
        var context = new QueryContext
        {
            LayerId = layer.Id,
            Filter = reference.Filter,
            TargetSrs = reference.Srs ?? layer.DefaultSrs
        };

        // 3. Stream features
        await foreach (var feature in _repository.QueryAsync(context, ct))
            yield return feature;
    }
}
```

**Benefits**:
- No data upload latency
- Respects Honua security rules
- Supports server-side filtering (push down to PostGIS)
- Works with any Honua data source

### 5.2 Auto-Register Output Pattern

**Problem**: Process results are often datasets that users want to visualize/share via Honua services.

**Esri Solution**: Manual - user must publish result as new service.

**GeoServer Solution**: `storeExecuteResponse="true"` writes result to WFS-T, but awkward.

**Honua Solution**: Declarative output registration:
```json
{
  "processId": "buffer",
  "inputs": {...},
  "outputs": {
    "result": {
      "registerAs": {
        "collectionId": "buffered-roads-100m",
        "title": "Roads with 100m Buffer",
        "ttl": "7d"
      }
    }
  }
}
```

**Implementation**:
```csharp
public class ProcessOutputHandler
{
    public async Task HandleAsync(ProcessResult result, ProcessRequest request, CancellationToken ct)
    {
        foreach (var (outputName, output) in result.Outputs)
        {
            if (request.OutputSpecs.TryGetValue(outputName, out var spec) &&
                spec.RegisterAs is not null)
            {
                if (output is FeatureCollection features)
                {
                    // Write to temporary table
                    var tempTable = await _repository.CreateTemporaryTableAsync(features, ct);

                    // Register as new layer
                    var layer = new LayerDefinition
                    {
                        Id = spec.RegisterAs.CollectionId,
                        Title = spec.RegisterAs.Title,
                        SourceTable = tempTable,
                        Ttl = spec.RegisterAs.Ttl
                    };
                    await _catalog.RegisterLayerAsync(layer, ct);

                    // Schedule cleanup if TTL specified
                    if (spec.RegisterAs.Ttl is not null)
                        _scheduler.ScheduleCleanup(layer.Id, spec.RegisterAs.Ttl.Value);
                }
            }
        }
    }
}
```

**Benefits**:
- Seamless workflow: process → visualize → share
- Automatic cleanup via TTL
- No manual configuration required

### 5.3 Graceful Tier Fallback Pattern

**Problem**: Neither Esri nor GeoServer handle execution tier failures gracefully.

**Honua Solution**: Processes advertise ordered tier preferences:
```csharp
public class VoronoiProcess : IProcess
{
    public ProcessMetadata Metadata => new()
    {
        Id = "voronoi",
        TierPreferences = [Tier.PostGIS, Tier.Python, Tier.NetTopologySuite],
        ResourceClass = ResourceClass.CpuBurst
    };

    public async Task<ProcessResult> ExecuteAsync(ProcessExecutionContext ctx, CancellationToken ct)
    {
        return ctx.Tier switch
        {
            Tier.PostGIS => await ExecutePostGISAsync(ctx, ct),
            Tier.Python => await ExecutePythonAsync(ctx, ct),
            Tier.NetTopologySuite => await ExecuteNTSAsync(ctx, ct),
            _ => throw new NotSupportedException()
        };
    }
}
```

**Coordinator Logic**:
```csharp
public async Task<ProcessResult> ExecuteAsync(string processId, ProcessParameters parameters, CancellationToken ct)
{
    var process = _registry.GetProcess(processId);

    foreach (var tier in process.Metadata.TierPreferences)
    {
        if (!_governor.TryReserve(process, tier, out var reservation))
            continue; // Capacity exhausted, try next tier

        try
        {
            var context = new ProcessExecutionContext(parameters, tier, _services);
            return await process.ExecuteAsync(context, ct);
        }
        catch (TierUnavailableException)
        {
            // PostGIS extension not installed, Python not available, etc.
            _logger.LogWarning("Tier {Tier} unavailable for {Process}, falling back", tier, processId);
        }
        finally
        {
            reservation.Dispose();
        }
    }

    throw new ProcessExecutionException($"No execution tier available for {processId}");
}
```

**Benefits**:
- Resilient to missing dependencies (PostGIS extension, Python runtime)
- Load balancing: if PostGIS is overloaded, use Python
- Observability: Tier selection logged for performance analysis

### 5.4 JSON Workflow DSL Pattern

**Problem**: GeoServer's nested XML is verbose and hard to debug.

**Honua Solution**: Flat JSON with explicit dependency graph:
```json
{
  "workflow": {
    "steps": [
      {
        "id": "step1",
        "processId": "clip",
        "inputs": {
          "features": {"layerReference": "buildings"},
          "clipGeometry": {"$ref": "#/inputs/extent"}
        }
      },
      {
        "id": "step2",
        "processId": "buffer",
        "inputs": {
          "features": {"$ref": "#/steps/step1/outputs/result"},
          "distance": 10
        }
      },
      {
        "id": "step3",
        "processId": "intersection",
        "inputs": {
          "features1": {"$ref": "#/steps/step2/outputs/result"},
          "features2": {"layerReference": "parcels"}
        }
        "dependsOn": ["step1", "step2"]
      }
    ],
    "outputs": {
      "final": {"$ref": "#/steps/step3/outputs/result"}
    }
  },
  "inputs": {
    "extent": {"bbox": [-122.5, 37.7, -122.3, 37.9]}
  }
}
```

**Benefits**:
- Explicit dependency graph (easier to validate)
- JSON pointers for step references
- Flat structure (easier to debug failures)
- Can execute independent steps in parallel

---

## Part 6: Critical Lessons Learned

### 6.1 Process Isolation is Non-Negotiable

**Esri's Evolution**: Started with "low isolation" (in-process) for performance, deprecated it after realizing crash instability outweighed speed gains.

**Lesson**: ALWAYS isolate untrusted code at the OS level. Thread-level isolation (GeoServer) is insufficient.

**Honua Implication**: Tier 1 (NTS) can use threads because it's trusted code. Tier 2 (PostGIS) is isolated by database. Tier 3 (Python) MUST use subprocess or containers.

### 6.2 Job Persistence is Critical

**GeoServer's Gap**: Async jobs lost on server restart. This is unacceptable for long-running operations.

**Lesson**: Job state must survive restarts. Database persistence is mandatory.

**Honua Implication**: Hangfire with PostgreSQL storage ensures jobs resume after crashes.

### 6.3 Catalog Integration Makes or Breaks UX

**GeoServer's Advantage**: Direct WFS references eliminate 90% of data upload overhead.

**Lesson**: Users expect to operate on data that's already in the system, not re-upload it.

**Honua Implication**: `layerReference` inputs and `registerAs` outputs are KILLER FEATURES.

### 6.4 Workload Separation is Essential at Scale

**Esri's Recommendation**: Dedicated geoprocessing sites in production.

**Lesson**: Mixing low-latency services (map tiles) with high-CPU services (geoprocessing) causes resource starvation.

**Honua Implication**: Start with embedded execution, but design for separate worker deployment from day one.

### 6.5 Resource Governance Must Be Proactive

**Both Platforms' Gap**: Reactive limits (timeout after 600s) vs. proactive (reject if queue full).

**Lesson**: Users prefer fast rejection ("service busy, retry in 5m") over slow timeout after wasted resources.

**Honua Implication**: Queue governor with capacity reservations prevents cascading failures.

### 6.6 Standards Compliance Has Hidden Costs

**GeoServer's Burden**: XML marshalling overhead, verbose request/response.

**Lesson**: Standards compliance is important for interoperability, but modern APIs need JSON alternatives.

**Honua Implication**: Dual endpoints - `/wps` (XML, standards) and `/api/processes` (JSON, DX).

---

## Part 7: Security Deep Dive

### 7.1 Input Validation Attacks

#### SQL Injection (Both Platforms Vulnerable)
**Esri**: Custom Python scripts can construct dynamic SQL:
```python
# VULNERABLE CODE
query = f"SELECT * FROM parcels WHERE owner = '{owner_param}'"
arcpy.MakeQueryLayer_management(connection, "layer", query)
```

**GeoServer**: Processes can execute dynamic queries:
```java
// VULNERABLE CODE
String sql = "SELECT * FROM roads WHERE type = '" + roadType + "'";
dataStore.getFeatureSource().getFeatures(new Query(sql));
```

**Honua Protection**: PostGIS stored procedures eliminate dynamic SQL:
```sql
CREATE FUNCTION honua_gp.filter_by_owner(owner_name TEXT)
RETURNS TABLE(geom geometry, attrs jsonb) AS $$
BEGIN
  RETURN QUERY SELECT geom, attrs FROM parcels WHERE attrs->>'owner' = owner_name;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;
```

#### SSRF (Server-Side Request Forgery)
**GeoServer Vulnerability**: External URL references can hit internal services:
```xml
<wps:Reference xlink:href="http://169.254.169.254/latest/meta-data/iam/security-credentials/"/>
```

**Honua Protection**: Restrict input references to internal catalog only:
```csharp
public class InputReferenceValidator
{
    private static readonly string[] BlockedHosts =
    {
        "169.254.169.254",  // AWS metadata
        "metadata.google.internal",  // GCP metadata
        "10.0.0.0/8",       // Private networks
        "172.16.0.0/12",
        "192.168.0.0/16",
        "localhost",
        "127.0.0.1"
    };

    public void Validate(InputReference reference)
    {
        if (reference.Type == InputReferenceType.Url)
        {
            var uri = new Uri(reference.Value);
            if (BlockedHosts.Any(blocked => IsBlocked(uri.Host, blocked)))
                throw new SecurityException($"External URL references are restricted");
        }
    }
}
```

### 7.2 Resource Exhaustion Attacks

#### Memory Bombs
**Attack**: Submit process with huge input (e.g., 10GB GeoJSON).

**GeoServer Protection**: Maximum input size per process.

**Honua Protection**: Multi-layered:
1. HTTP request body size limit (100MB default)
2. Per-process input size limits
3. Streaming processing (never load entire dataset in memory)
4. Artifact externalization (results > 100MB written to S3)

#### Fork Bombs (Python Tier)
**Attack**: Python script spawns unlimited subprocesses.

**Protection**: Resource limits via systemd or Docker:
```yaml
# docker-compose.yml
services:
  geoprocessing-worker:
    image: honua-worker:latest
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 4G
          pids: 100  # Max 100 processes/threads
```

### 7.3 Tenant Isolation (Multi-Tenancy)

**Neither Platform Supports Multi-Tenant SaaS**

**Honua Requirements**:
1. Tenant A cannot access Tenant B's processes
2. Tenant A cannot access Tenant B's layers (even via `layerReference`)
3. Tenant A's jobs cannot starve Tenant B's jobs

**Solution**: Row-Level Security + Tenant-Scoped Queues

#### Database-Level Isolation
```sql
-- Enable RLS on process_runs table
ALTER TABLE process_runs ENABLE ROW LEVEL SECURITY;

-- Policy: Users only see jobs from their tenant
CREATE POLICY tenant_isolation ON process_runs
  USING (tenant_id = current_setting('app.current_tenant')::uuid);
```

#### Queue-Level Isolation
```csharp
public class TenantAwareJobManager
{
    public async Task<JobTicket> SubmitAsync(ProcessRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var tenantId = user.GetTenantId();

        // Check tenant quota
        var currentJobs = await _repository.GetActiveJobCountAsync(tenantId, ct);
        if (currentJobs >= _tenantQuotas[tenantId].MaxConcurrentJobs)
            throw new QuotaExceededException($"Tenant {tenantId} has {currentJobs} active jobs (limit {_tenantQuotas[tenantId].MaxConcurrentJobs})");

        // Enqueue with tenant context
        var jobId = BackgroundJob.Enqueue<ProcessExecutor>(
            x => x.ExecuteAsync(request, tenantId, CancellationToken.None));

        return new JobTicket(jobId);
    }
}
```

---

## Part 8: Performance Optimization Patterns

### 8.1 Connection Pooling (Esri Best Practice)

**Problem**: Database connections are expensive (50-100ms to establish).

**Esri Guidance**: Configure connection pool per service:
```python
# Tool script
arcpy.env.workspace = "Database Connections\\gis.sde"  # Pooled connection
```

**Honua Implementation**: Npgsql connection pooling (automatic):
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=gis;Username=app;Password=secret;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=100"
  }
}
```

**Tuning**:
- `Minimum Pool Size`: Warm connections (avoid cold start latency)
- `Maximum Pool Size`: Prevent connection exhaustion
- `Connection Idle Lifetime`: Rotate connections (balances load across replicas)

### 8.2 Intermediate Data Management (Esri Best Practice)

**Problem**: Multi-step processes create temporary datasets.

**Esri Guidance**: Use in-memory workspace:
```python
arcpy.env.scratchWorkspace = "in_memory"  # Faster than disk
intermediate_fc = arcpy.Buffer_analysis("input", "in_memory/buffered", "100 METERS")
```

**Honua Implementation**: Pass data between steps in memory, not via database:
```csharp
public async Task<ProcessResult> ExecuteWorkflowAsync(Workflow workflow, CancellationToken ct)
{
    var stepResults = new Dictionary<string, object>();

    foreach (var step in workflow.Steps.TopologicalSort())
    {
        var inputs = ResolveInputs(step, stepResults);  // In-memory objects
        var result = await _coordinator.ExecuteAsync(step.ProcessId, inputs, ct);
        stepResults[step.Id] = result.Outputs["result"];  // Keep in memory
    }

    // Only final result persisted to database/S3
    return new ProcessResult(stepResults[workflow.FinalStep]);
}
```

### 8.3 Geometry Simplification (Esri Best Practice)

**Problem**: Operating on complex geometries (1M+ vertices) is slow.

**Esri Guidance**: Simplify before processing:
```python
simplified = arcpy.Simplify_management("complex_input", "simplified", "10 METERS")
result = arcpy.Buffer_analysis("simplified", "output", "100 METERS")
```

**Honua Implementation**: Automatic simplification based on map scale:
```csharp
public class GeometrySimplifier
{
    public Geometry Simplify(Geometry geom, double mapScale)
    {
        // Tolerance = 1 pixel at map scale
        var pixelSize = mapScale / 96.0 * 0.0254;  // meters per pixel
        var tolerance = pixelSize * 2.0;  // 2-pixel tolerance

        return NetTopologySuite.Simplify.DouglasPeuckerSimplifier.Simplify(geom, tolerance);
    }
}
```

**Benefits**: 10-100x speedup for complex geometries.

### 8.4 Batch Processing (Esri Best Practice)

**Problem**: Processing 1 million features via 1 million API calls is slow.

**Esri Guidance**: Use batch operations:
```python
# SLOW: 1 call per feature
for feature in features:
    buffered = arcpy.Buffer_analysis(feature, "output", "100 METERS")

# FAST: 1 call for all features
arcpy.Buffer_analysis("all_features", "output", "100 METERS")
```

**Honua Implementation**: Process supports feature collections:
```json
{
  "processId": "buffer",
  "inputs": {
    "features": {
      "layerReference": "roads"  // All features processed in one job
    },
    "distance": 100
  }
}
```

**PostGIS Executor**: Single SQL query processes all features:
```sql
SELECT id, ST_Buffer(geom, 100) AS geom
FROM roads
WHERE ...
```

---

## Part 9: Cloud-Native Execution Options for Tier 3

### 9.1 Overview

For **Tier 3 (Python/long-running)** execution, you have several infrastructure choices ranging from simple subprocess execution to fully managed cloud services. The right choice depends on scale, operational maturity, and multi-cloud requirements.

### 9.2 Execution Option Comparison

| Option | Complexity | Cost | Isolation | Scalability | Multi-Cloud | Best For |
|--------|------------|------|-----------|-------------|-------------|----------|
| **Subprocess** | Low | Free | Moderate | Limited (vertical) | ✅ Yes | MVP, single-server, < 10 concurrent jobs |
| **Docker (local)** | Medium | Free | High | Limited (vertical) | ✅ Yes | Development, testing, single-server |
| **Kubernetes Jobs** | High | Variable | High | Excellent (horizontal) | ✅ Yes | Production, multi-cloud, > 100 concurrent jobs |
| **AWS Batch** | Medium | Pay-per-use | High | Excellent (horizontal) | ❌ AWS only | Production, AWS-committed, GPU workloads |
| **Azure Batch** | Medium | Pay-per-use | High | Excellent (horizontal) | ❌ Azure only | Production, Azure-committed |
| **GCP Cloud Run Jobs** | Low | Pay-per-use | High | Good (horizontal) | ❌ GCP only | Production, GCP-committed, serverless preference |

### 9.3 Option 1: Subprocess Execution (Simplest)

**Architecture**:
```csharp
public class SubprocessPythonExecutor : IPythonExecutor
{
    public async Task<ProcessResult> ExecuteAsync(string processId, ProcessParameters parameters, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"/app/processes/{processId}.py",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)!;

        // Write inputs as JSON to stdin
        await JsonSerializer.SerializeAsync(process.StandardInput.BaseStream, parameters, ct);
        await process.StandardInput.BaseStream.FlushAsync(ct);
        process.StandardInput.Close();

        // Read outputs from stdout
        var output = await process.StandardOutput.ReadToEndAsync();
        var errors = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new ProcessExecutionException($"Python process failed: {errors}");

        return JsonSerializer.Deserialize<ProcessResult>(output)!;
    }
}
```

**Pros**:
- ✅ Zero infrastructure - just install Python
- ✅ Fast startup (< 100ms)
- ✅ Simple debugging (can run scripts manually)
- ✅ No cloud vendor lock-in

**Cons**:
- ❌ Limited isolation (same machine as web server)
- ❌ Vertical scaling only (add more RAM/CPU to one machine)
- ❌ No resource limits (Python can consume all memory)
- ❌ Process crashes affect server stability

**When to Use**: MVP, development, single-server deployments, < 10 concurrent jobs

---

### 9.4 Option 2: Docker Containers (Local)

**Architecture**:
```csharp
public class DockerPythonExecutor : IPythonExecutor
{
    public async Task<ProcessResult> ExecuteAsync(string processId, ProcessParameters parameters, CancellationToken ct)
    {
        var inputJson = JsonSerializer.Serialize(parameters);

        var result = await Cli.Wrap("docker")
            .WithArguments(args => args
                .Add("run")
                .Add("--rm")
                .Add("--memory").Add("2g")
                .Add("--cpus").Add("1.0")
                .Add("--network").Add("none")  // No network access
                .Add("--read-only")
                .Add("--tmpfs").Add("/tmp")
                .Add("-e").Add($"PROCESS_ID={processId}")
                .Add("honua-python:latest")
                .Add("python3")
                .Add($"/app/processes/{processId}.py"))
            .WithStandardInputPipe(PipeSource.FromString(inputJson))
            .WithStandardOutputPipe(PipeTarget.ToStream(outputStream))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(ct);

        if (result.ExitCode != 0)
            throw new ProcessExecutionException($"Docker process failed with exit code {result.ExitCode}");

        return JsonSerializer.Deserialize<ProcessResult>(outputStream)!;
    }
}
```

**Pros**:
- ✅ Better isolation (containerized)
- ✅ Resource limits (memory, CPU via `--memory`, `--cpus`)
- ✅ Network isolation (`--network none`)
- ✅ Read-only filesystem security
- ✅ Consistent environment (Docker image)

**Cons**:
- ❌ Still vertical scaling only
- ❌ Docker overhead (~50-200ms startup)
- ❌ Requires Docker daemon running

**When to Use**: Development, testing, single-server with better isolation, < 50 concurrent jobs

---

### 9.5 Option 3: Kubernetes Jobs (Cloud-Native)

**Architecture**:
```csharp
public class KubernetesJobExecutor : IPythonExecutor
{
    private readonly IKubernetes _k8s;

    public async Task<ProcessResult> ExecuteAsync(string processId, ProcessParameters parameters, CancellationToken ct)
    {
        var jobId = Guid.NewGuid().ToString();

        // 1. Create ConfigMap with input parameters
        var configMap = new V1ConfigMap
        {
            Metadata = new V1ObjectMeta { Name = $"job-{jobId}-input" },
            Data = new Dictionary<string, string>
            {
                ["parameters.json"] = JsonSerializer.Serialize(parameters)
            }
        };
        await _k8s.CoreV1.CreateNamespacedConfigMapAsync(configMap, "honua-gp", cancellationToken: ct);

        // 2. Create Job
        var job = new V1Job
        {
            Metadata = new V1ObjectMeta
            {
                Name = $"gp-{processId}-{jobId}",
                Labels = new Dictionary<string, string>
                {
                    ["app"] = "honua-geoprocessing",
                    ["process"] = processId,
                    ["job-id"] = jobId
                }
            },
            Spec = new V1JobSpec
            {
                BackoffLimit = 0,  // Don't retry
                TtlSecondsAfterFinished = 300,  // Clean up after 5 minutes
                Template = new V1PodTemplateSpec
                {
                    Spec = new V1PodSpec
                    {
                        RestartPolicy = "Never",
                        Containers = new List<V1Container>
                        {
                            new()
                            {
                                Name = "python-worker",
                                Image = "honua-python:latest",
                                Command = new[] { "python3", $"/app/processes/{processId}.py" },
                                Resources = new V1ResourceRequirements
                                {
                                    Limits = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["memory"] = new ResourceQuantity("4Gi"),
                                        ["cpu"] = new ResourceQuantity("2")
                                    },
                                    Requests = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["memory"] = new ResourceQuantity("2Gi"),
                                        ["cpu"] = new ResourceQuantity("1")
                                    }
                                },
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new()
                                    {
                                        Name = "input",
                                        MountPath = "/input"
                                    }
                                }
                            }
                        },
                        Volumes = new List<V1Volume>
                        {
                            new()
                            {
                                Name = "input",
                                ConfigMap = new V1ConfigMapVolumeSource
                                {
                                    Name = $"job-{jobId}-input"
                                }
                            }
                        }
                    }
                }
            }
        };

        await _k8s.BatchV1.CreateNamespacedJobAsync(job, "honua-gp", cancellationToken: ct);

        // 3. Poll for completion
        return await PollJobCompletionAsync(jobId, ct);
    }
}
```

**Pros**:
- ✅ Horizontal scaling (run 1000s of jobs across cluster)
- ✅ Automatic resource management (CPU, memory, GPU)
- ✅ Node affinity (GPU jobs on GPU nodes)
- ✅ Multi-cloud (EKS, AKS, GKE)
- ✅ Job cleanup (TTL after completion)
- ✅ Monitoring & observability (Prometheus, Grafana)
- ✅ Spot instances (cost savings)

**Cons**:
- ❌ High complexity (requires K8s expertise)
- ❌ Slower startup (pod scheduling ~2-10s)
- ❌ Operational overhead (cluster management)
- ❌ Cost (cluster infrastructure)

**When to Use**: Production, > 100 concurrent jobs, multi-cloud requirement, GPU workloads, elastic scaling

**Cost Example** (AWS EKS):
- 3-node cluster (m5.xlarge): ~$350/month baseline
- Spot instances: 50-70% savings
- Autoscaling: Only pay for nodes when jobs running

---

### 9.6 Option 4: AWS Batch (Managed Service)

**Architecture**:
```csharp
public class AwsBatchExecutor : IPythonExecutor
{
    private readonly IAmazonBatch _batch;
    private readonly IAmazonS3 _s3;

    public async Task<ProcessResult> ExecuteAsync(string processId, ProcessParameters parameters, CancellationToken ct)
    {
        var jobId = Guid.NewGuid().ToString();

        // 1. Upload input to S3
        var inputKey = $"gp/inputs/{jobId}/parameters.json";
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = "honua-geoprocessing",
            Key = inputKey,
            ContentBody = JsonSerializer.Serialize(parameters)
        }, ct);

        // 2. Submit job to AWS Batch
        var response = await _batch.SubmitJobAsync(new SubmitJobRequest
        {
            JobName = $"gp-{processId}-{jobId}",
            JobQueue = "honua-geoprocessing-queue",
            JobDefinition = $"honua-{processId}:1",
            ContainerOverrides = new ContainerOverrides
            {
                Environment = new List<KeyValuePair>
                {
                    new("INPUT_S3_KEY", inputKey),
                    new("OUTPUT_S3_KEY", $"gp/outputs/{jobId}/result.json")
                },
                ResourceRequirements = new List<ResourceRequirement>
                {
                    new() { Type = ResourceType.VCPU, Value = "2" },
                    new() { Type = ResourceType.MEMORY, Value = "4096" }
                }
            },
            Timeout = new JobTimeout { AttemptDurationSeconds = 1800 }  // 30 min max
        }, ct);

        // 3. Poll for completion and read from S3
        return await PollBatchJobAndRetrieveResultAsync(response.JobId, ct);
    }
}
```

**Pros**:
- ✅ Fully managed (no cluster to maintain)
- ✅ Automatic scaling (0 to 1000s of jobs)
- ✅ Cost-effective (pay per compute second)
- ✅ Spot instance support (up to 90% savings)
- ✅ GPU support (P3, G4 instances)
- ✅ Array jobs (run same process on 10,000 items)
- ✅ Job prioritization and scheduling
- ✅ CloudWatch integration

**Cons**:
- ❌ AWS lock-in
- ❌ Slower startup (EC2 instance provisioning ~2-5 min cold start)
- ❌ Less granular control than K8s
- ❌ Complex IAM permissions

**When to Use**: Production on AWS, GPU workloads, variable load (0-1000 jobs), cost-sensitive

**Cost Example**:
- No baseline cost (pay only when jobs run)
- m5.xlarge (4 vCPU, 16 GB): $0.192/hour
- 100 jobs/day @ 10 min each = $3.20/day
- Spot instances: ~$1.00/day

---

### 9.7 Option 5: Azure Batch

Similar to AWS Batch but for Azure:

**Pros**:
- ✅ Fully managed
- ✅ Azure integration (Blob Storage, Key Vault)
- ✅ Low-priority VMs (up to 80% savings)
- ✅ Task dependencies (DAG workflows)

**Cons**:
- ❌ Azure lock-in
- ❌ Slower startup than K8s

**When to Use**: Production on Azure, existing Azure infrastructure

---

### 9.8 Option 6: Google Cloud Batch (Fully Managed)

**Architecture**:
```csharp
public class GoogleCloudBatchExecutor : IPythonExecutor
{
    private readonly BatchServiceClient _batchClient;

    public async Task<ProcessResult> ExecuteAsync(string processId, ProcessParameters parameters, CancellationToken ct)
    {
        var jobId = Guid.NewGuid().ToString();

        // 1. Upload input to Cloud Storage
        var inputKey = $"gp/inputs/{jobId}/parameters.json";
        await _storage.UploadObjectAsync("honua-geoprocessing", inputKey,
            "application/json", JsonSerializer.Serialize(parameters), ct);

        // 2. Create Batch job
        var job = new Job
        {
            TaskGroups =
            {
                new TaskGroup
                {
                    TaskSpec = new TaskSpec
                    {
                        Runnables =
                        {
                            new Runnable
                            {
                                Container = new Runnable.Types.Container
                                {
                                    ImageUri = "gcr.io/honua/geoprocessing-python:latest",
                                    Commands = { "python3", $"/app/processes/{processId}.py" },
                                    Entrypoint = "/bin/bash"
                                },
                                Environment = new Environment
                                {
                                    Variables =
                                    {
                                        ["INPUT_GCS_KEY"] = inputKey,
                                        ["OUTPUT_GCS_KEY"] = $"gp/outputs/{jobId}/result.json"
                                    }
                                }
                            }
                        },
                        ComputeResource = new ComputeResource
                        {
                            CpuMilli = 2000,      // 2 CPUs
                            MemoryMib = 4096,     // 4 GB
                            BootDiskMib = 10240   // 10 GB
                        },
                        MaxRetryCount = 0,
                        MaxRunDuration = Duration.FromTimeSpan(TimeSpan.FromMinutes(30))
                    },
                    TaskCount = 1
                }
            },
            AllocationPolicy = new AllocationPolicy
            {
                Instances =
                {
                    new AllocationPolicy.Types.InstancePolicyOrTemplate
                    {
                        Policy = new AllocationPolicy.Types.InstancePolicy
                        {
                            MachineType = "e2-standard-2",
                            ProvisioningModel = AllocationPolicy.Types.ProvisioningModel.Spot  // 91% savings
                        }
                    }
                }
            },
            LogsPolicy = new LogsPolicy
            {
                Destination = LogsPolicy.Types.Destination.CloudLogging
            }
        };

        var createJobRequest = new CreateJobRequest
        {
            ParentAsLocationName = LocationName.FromProjectLocation("honua", "us-central1"),
            JobId = $"gp-{processId}-{jobId}",
            Job = job
        };

        var createdJob = await _batchClient.CreateJobAsync(createJobRequest, ct);

        // 3. Poll for completion and read from Cloud Storage
        return await PollBatchJobAndRetrieveResultAsync(createdJob.Name, ct);
    }
}
```

**Pros**:
- ✅ Fully managed (no cluster, no VMs)
- ✅ Automatic scaling (0 to 10,000s of tasks)
- ✅ Spot VM support (up to 91% savings)
- ✅ Array jobs (parallel task execution)
- ✅ Cloud Logging integration
- ✅ Custom machine types
- ✅ No cold start penalty

**Cons**:
- ❌ GCP lock-in
- ❌ Limited GPU support vs AWS
- ❌ Newer service (less mature than AWS Batch)

**When to Use**: Production on GCP, HPC workloads, cost-sensitive, need managed service

**Cost Example**:
- No baseline cost (pay only when jobs run)
- e2-standard-2 (2 vCPU, 8 GB): $0.067/hour
- 100 jobs/day @ 10 min each = $1.12/day
- Spot VMs: ~$0.10/day (91% savings)

---

### 9.9 Option 7: GCP Cloud Run Jobs

**Architecture**:
```csharp
public class CloudRunJobsExecutor : IPythonExecutor
{
    private readonly RunServiceClient _runService;

    public async Task<ProcessResult> ExecuteAsync(string processId, ProcessParameters parameters, CancellationToken ct)
    {
        var jobId = Guid.NewGuid().ToString();

        var execution = new Execution
        {
            JobAsExecutionName = JobName.FromProjectLocationJob("honua", "us-central1", $"gp-{processId}"),
            LaunchStage = LaunchStage.Beta,
            Template = new TaskTemplate
            {
                Containers =
                {
                    new Container
                    {
                        Image = $"gcr.io/honua/geoprocessing-python:latest",
                        Args = { processId },
                        Env =
                        {
                            new EnvVar { Name = "PARAMETERS_JSON", Value = JsonSerializer.Serialize(parameters) }
                        },
                        Resources = new ResourceRequirements
                        {
                            Limits =
                            {
                                ["cpu"] = "2",
                                ["memory"] = "4Gi"
                            }
                        }
                    }
                },
                MaxRetries = 0,
                Timeout = Duration.FromTimeSpan(TimeSpan.FromMinutes(30))
            }
        };

        var response = await _runService.RunJobAsync(execution.JobAsExecutionName.JobName);
        return await PollExecutionAsync(response.Name, ct);
    }
}
```

**Pros**:
- ✅ Serverless (no cluster management)
- ✅ Fast cold start (~1-3s)
- ✅ Auto-scaling (0 to 1000 tasks)
- ✅ Simple pricing (pay per request)
- ✅ GCP integration

**Cons**:
- ❌ GCP lock-in
- ❌ Limited GPU support
- ❌ 60-minute max execution time

**When to Use**: Production on GCP, serverless preference, < 1 hour jobs

---

### 9.9 **SERVERLESS COORDINATION** (Fully Event-Driven Architecture)

**CRITICAL INSIGHT**: Instead of running persistent Honua servers with Hangfire workers, use a **fully serverless architecture** where API calls trigger cloud functions that coordinate batch jobs. This eliminates infrastructure management entirely.

#### Architecture Pattern

```
User Request
    ↓
API Gateway / Cloud Endpoints
    ↓
Serverless Function (Lambda/Functions/Cloud Functions)
    ↓
Batch Service Submission (Batch/Azure Batch/Cloud Batch)
    ↓
Event-Driven Status Updates (EventBridge/Event Grid/Pub/Sub)
    ↓
Serverless Function → Update job status in database
    ↓
User polls job status via API Gateway
```

**Benefits**:
- ✅ Zero infrastructure to manage
- ✅ Pay only for actual execution time
- ✅ Automatic scaling (0 to millions of requests)
- ✅ High availability built-in
- ✅ No persistent workers to maintain

---

#### 9.9.1 AWS Serverless Coordination

**Stack**: API Gateway + Lambda + Step Functions + AWS Batch + DynamoDB

```
┌──────────────────────────────────────────────────────┐
│  API Gateway                                          │
│  POST /api/processes/{id}/execute                    │
└─────────────────┬────────────────────────────────────┘
                  │
┌─────────────────▼────────────────────────────────────┐
│  Lambda Function: SubmitGeoprocessingJob             │
│  - Validate request                                   │
│  - Upload inputs to S3                                │
│  - Submit to AWS Batch                                │
│  - Store job metadata in DynamoDB                     │
│  - Return jobId                                       │
└─────────────────┬────────────────────────────────────┘
                  │
┌─────────────────▼────────────────────────────────────┐
│  AWS Batch                                            │
│  - Job runs in container                              │
│  - Writes results to S3                               │
│  - Triggers EventBridge event on completion           │
└─────────────────┬────────────────────────────────────┘
                  │
┌─────────────────▼────────────────────────────────────┐
│  EventBridge Rule: Batch Job State Change             │
│  → Lambda Function: UpdateJobStatus                   │
│     - Update DynamoDB with status/results             │
│     - Send SNS notification (optional)                │
└───────────────────────────────────────────────────────┘

User polls:
GET /api/jobs/{jobId} → Lambda → DynamoDB → Return status
```

**Implementation**:

```csharp
// Lambda function handler for job submission
public class SubmitGeoprocessingJobFunction
{
    [LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var processRequest = JsonSerializer.Deserialize<ProcessExecutionRequest>(request.Body);
        var jobId = Guid.NewGuid().ToString();

        // 1. Upload inputs to S3
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = "honua-gp-inputs",
            Key = $"{jobId}/parameters.json",
            ContentBody = JsonSerializer.Serialize(processRequest.Inputs)
        });

        // 2. Submit to AWS Batch
        var batchResponse = await _batch.SubmitJobAsync(new SubmitJobRequest
        {
            JobName = $"gp-{processRequest.ProcessId}-{jobId}",
            JobQueue = "honua-geoprocessing",
            JobDefinition = $"honua-{processRequest.ProcessId}",
            ContainerOverrides = new ContainerOverrides
            {
                Environment = new List<KeyValuePair>
                {
                    new("JOB_ID", jobId),
                    new("INPUT_S3_KEY", $"{jobId}/parameters.json")
                }
            }
        });

        // 3. Store job metadata in DynamoDB
        await _dynamoDB.PutItemAsync(new PutItemRequest
        {
            TableName = "GeoprocessingJobs",
            Item = new Dictionary<string, AttributeValue>
            {
                ["JobId"] = new AttributeValue { S = jobId },
                ["ProcessId"] = new AttributeValue { S = processRequest.ProcessId },
                ["Status"] = new AttributeValue { S = "SUBMITTED" },
                ["BatchJobId"] = new AttributeValue { S = batchResponse.JobId },
                ["CreatedAt"] = new AttributeValue { N = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
            }
        });

        // 4. Return job ticket
        return new APIGatewayProxyResponse
        {
            StatusCode = 202,
            Body = JsonSerializer.Serialize(new { jobId, status = "SUBMITTED" })
        };
    }
}

// Lambda function triggered by EventBridge on Batch job state change
public class UpdateJobStatusFunction
{
    public async Task FunctionHandler(CloudWatchEvent<BatchJobStateChange> @event, ILambdaContext context)
    {
        var jobId = @event.Detail.JobName.Split('-').Last();
        var status = @event.Detail.Status; // SUCCEEDED, FAILED

        await _dynamoDB.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = "GeoprocessingJobs",
            Key = new Dictionary<string, AttributeValue> { ["JobId"] = new AttributeValue { S = jobId } },
            UpdateExpression = "SET #status = :status, #completedAt = :completedAt",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#status"] = "Status",
                ["#completedAt"] = "CompletedAt"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":status"] = new AttributeValue { S = status },
                [":completedAt"] = new AttributeValue { N = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
            }
        });
    }
}
```

**Cost** (1000 jobs/day):
- API Gateway: $3.50/month (1M requests free tier)
- Lambda: $0.20/month (1M requests free tier)
- AWS Batch: $96/month (with Spot)
- DynamoDB: $1.25/month (25 GB free tier)
- **Total: ~$101/month**

---

#### 9.9.2 Azure Serverless Coordination

**Stack**: API Management + Azure Functions + Durable Functions + Azure Batch + Cosmos DB

```
┌──────────────────────────────────────────────────────┐
│  API Management                                       │
│  POST /api/processes/{id}/execute                    │
└─────────────────┬────────────────────────────────────┘
                  │
┌─────────────────▼────────────────────────────────────┐
│  Azure Function: HTTP Trigger                         │
│  → Start Durable Orchestration                        │
└─────────────────┬────────────────────────────────────┘
                  │
┌─────────────────▼────────────────────────────────────┐
│  Durable Function Orchestrator                        │
│  1. Upload inputs to Blob Storage                     │
│  2. Submit to Azure Batch                             │
│  3. Wait for completion (activity function polling)   │
│  4. Retrieve results from Blob                        │
│  5. Update Cosmos DB                                  │
└───────────────────────────────────────────────────────┘
```

**Implementation**:

```csharp
[FunctionName("SubmitGeoprocessingJob")]
public async Task<IActionResult> HttpStart(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
    [DurableClient] IDurableOrchestrationClient starter,
    ILogger log)
{
    var processRequest = await JsonSerializer.DeserializeAsync<ProcessExecutionRequest>(req.Body);
    var instanceId = await starter.StartNewAsync("GeoprocessingOrchestrator", processRequest);

    return starter.CreateCheckStatusResponse(req, instanceId);
}

[FunctionName("GeoprocessingOrchestrator")]
public async Task<ProcessResult> RunOrchestrator(
    [OrchestrationTrigger] IDurableOrchestrationContext context)
{
    var request = context.GetInput<ProcessExecutionRequest>();
    var jobId = context.InstanceId;

    // 1. Upload inputs
    await context.CallActivityAsync("UploadInputsActivity", (jobId, request.Inputs));

    // 2. Submit to Azure Batch
    var batchJobId = await context.CallActivityAsync<string>("SubmitBatchJobActivity", (jobId, request.ProcessId));

    // 3. Wait for completion (polling with exponential backoff)
    var status = await context.CallActivityAsync<string>("PollBatchJobActivity", batchJobId);
    while (status != "Completed" && status != "Failed")
    {
        await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(5), CancellationToken.None);
        status = await context.CallActivityAsync<string>("PollBatchJobActivity", batchJobId);
    }

    // 4. Retrieve results
    var result = await context.CallActivityAsync<ProcessResult>("RetrieveResultsActivity", jobId);

    return result;
}

[FunctionName("SubmitBatchJobActivity")]
public async Task<string> SubmitBatchJob([ActivityTrigger] (string jobId, string processId) input, ILogger log)
{
    using var batchClient = BatchClient.Open(new BatchSharedKeyCredentials(...));

    var job = new CloudJob($"gp-{input.jobId}", new PoolInformation { PoolId = "honua-gp-pool" });
    await batchClient.JobOperations.AddJobAsync(job);

    var task = new CloudTask($"task-{input.jobId}", $"python /app/processes/{input.processId}.py");
    task.EnvironmentSettings = new List<EnvironmentSetting>
    {
        new("JOB_ID", input.jobId)
    };

    await batchClient.JobOperations.AddTaskAsync(job.Id, task);

    return job.Id;
}
```

**Cost** (1000 jobs/day):
- API Management: $13/month (Consumption tier)
- Azure Functions: $0.00/month (1M executions free)
- Durable Functions: $5/month (storage)
- Azure Batch: $0/month (pay for VMs only)
- Cosmos DB: $24/month (serverless, 1M RU)
- **Total: ~$42/month + VM costs**

---

#### 9.9.3 GCP Serverless Coordination

**Stack**: Cloud Functions + Workflows + Cloud Batch + Firestore

```
┌──────────────────────────────────────────────────────┐
│  Cloud Functions (HTTP)                               │
│  POST /api/processes/{id}/execute                    │
│  → Start Workflow execution                           │
└─────────────────┬────────────────────────────────────┘
                  │
┌─────────────────▼────────────────────────────────────┐
│  Cloud Workflows (YAML-based orchestration)           │
│  1. uploadInputs (call Cloud Function)                │
│  2. submitBatchJob (call Batch API)                   │
│  3. waitForCompletion (sleep + poll)                  │
│  4. retrieveResults (read from Cloud Storage)         │
│  5. updateFirestore                                   │
└───────────────────────────────────────────────────────┘
```

**Workflow Definition** (YAML):

```yaml
main:
  params: [input]
  steps:
    - assignJobId:
        assign:
          - jobId: ${sys.get_env("GOOGLE_CLOUD_WORKFLOW_EXECUTION_ID")}
          - processId: ${input.processId}

    - uploadInputs:
        call: googleapis.storage.v1.objects.insert
        args:
          bucket: honua-gp-inputs
          name: ${jobId + "/parameters.json"}
          body:
            data: ${json.encode(input.parameters)}

    - submitBatchJob:
        call: googleapis.batch.v1.projects.locations.jobs.create
        args:
          parent: ${"projects/" + sys.get_env("GOOGLE_CLOUD_PROJECT_ID") + "/locations/us-central1"}
          jobId: ${"gp-" + processId + "-" + jobId}
          body:
            taskGroups:
              - taskSpec:
                  runnables:
                    - container:
                        imageUri: gcr.io/honua/geoprocessing-python:latest
                        commands: ["python3", ${"/app/processes/" + processId + ".py"}]
                  computeResource:
                    cpuMilli: 2000
                    memoryMib: 4096
        result: batchJob

    - waitForCompletion:
        steps:
          - checkStatus:
              call: googleapis.batch.v1.projects.locations.jobs.get
              args:
                name: ${batchJob.name}
              result: jobStatus

          - checkIfDone:
              switch:
                - condition: ${jobStatus.status.state == "SUCCEEDED"}
                  next: retrieveResults
                - condition: ${jobStatus.status.state == "FAILED"}
                  raise: "Batch job failed"

          - waitAndRetry:
              call: sys.sleep
              args:
                seconds: 5
              next: checkStatus

    - retrieveResults:
        call: googleapis.storage.v1.objects.get
        args:
          bucket: honua-gp-outputs
          object: ${jobId + "/result.json"}
        result: resultData

    - updateFirestore:
        call: googleapis.firestore.v1.projects.databases.documents.patch
        args:
          name: ${"projects/" + sys.get_env("GOOGLE_CLOUD_PROJECT_ID") + "/databases/(default)/documents/jobs/" + jobId}
          body:
            fields:
              status: { stringValue: "COMPLETED" }
              result: { stringValue: ${json.encode(resultData)} }

    - returnResult:
        return: ${resultData}
```

**Cloud Function** (HTTP trigger):

```csharp
[FunctionsStartup(typeof(Startup))]
public class SubmitGeoprocessingJob : IHttpFunction
{
    private readonly WorkflowsServiceClient _workflows;

    public async Task HandleAsync(HttpContext context)
    {
        var request = await JsonSerializer.DeserializeAsync<ProcessExecutionRequest>(context.Request.Body);

        var execution = await _workflows.CreateExecutionAsync(new CreateExecutionRequest
        {
            ParentAsWorkflowName = WorkflowName.FromProjectLocationWorkflow(
                "honua", "us-central1", "geoprocessing-workflow"),
            Execution = new Execution
            {
                Argument = JsonSerializer.Serialize(request)
            }
        });

        context.Response.StatusCode = 202;
        await JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            jobId = execution.Name,
            status = "SUBMITTED"
        });
    }
}
```

**Cost** (1000 jobs/day):
- Cloud Functions: $0.40/month (2M invocations free)
- Workflows: $0.01/month (5000 steps free)
- Cloud Batch: ~$3/month (Spot VMs)
- Firestore: $0.06/month (serverless)
- **Total: ~$3.50/month**

**Winner for serverless**: **GCP ($3.50/month)** 🏆

---

#### 9.9.4 Serverless Coordination Comparison

| Aspect | AWS | Azure | GCP |
|--------|-----|-------|-----|
| **API Layer** | API Gateway | API Management | Cloud Functions (HTTP) |
| **Orchestrator** | Step Functions | Durable Functions | Workflows |
| **Batch Service** | AWS Batch | Azure Batch | Cloud Batch |
| **Database** | DynamoDB | Cosmos DB | Firestore |
| **Event Bus** | EventBridge | Event Grid | Pub/Sub |
| **Monthly Cost** | $101 | $42 + VMs | $3.50 |
| **Complexity** | Medium | High (Durable Functions) | Low (YAML workflows) |
| **Cold Start** | 50-200ms | 200-500ms | 100-300ms |

**Key Insight**: GCP's Workflows are YAML-based declarative orchestration vs AWS Step Functions (JSON state machines) vs Azure Durable Functions (code-based). GCP has the simplest model for serverless coordination.

---

#### 9.9.5 **Job State Management in Serverless Architecture**

**CRITICAL QUESTION**: "Don't we need a coordinator managing the batch jobs? How is state persisted?"

**Answer**: The **batch service itself IS the coordinator** - AWS Batch, Azure Batch, and Cloud Batch all have built-in job management with persistent state. Your serverless functions are just thin glue code.

### State Persistence Layers

```
┌─────────────────────────────────────────────────────────────┐
│  Layer 1: Batch Service Internal State (Fully Managed)      │
│  - AWS Batch, Azure Batch, Cloud Batch                      │
│  - Job queue, execution state, retries, timeouts            │
│  - Survives service restarts                                │
│  - NOT directly queryable by your API                       │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  Layer 2: Your Application Database                         │
│  - DynamoDB, Cosmos DB, Firestore                           │
│  - Job metadata: processId, userId, parameters              │
│  - Status cache: last known state from batch service        │
│  - Results: output data or S3/Blob reference                │
│  - This is what your API queries                            │
└─────────────────────────────────────────────────────────────┘
```

### How It Works: Job Lifecycle

#### 1. **Job Submission** (Your code runs)
```csharp
// Lambda/Function receives request
POST /api/processes/buffer/execute

// Function writes to YOUR database
await db.PutItem(new {
    JobId = "abc123",
    ProcessId = "buffer",
    Status = "SUBMITTED",
    CreatedAt = DateTimeOffset.UtcNow
});

// Function submits to BATCH SERVICE
await batch.SubmitJob(new {
    JobName = "gp-buffer-abc123",
    JobQueue = "honua-gp"
});

// Batch service stores job in ITS database (you don't manage this)
// Returns immediately - your function exits
```

At this point:
- **Batch service** has the job queued in its persistent store
- **Your database** has the job record with status "SUBMITTED"
- **No persistent workers** - your function has exited

#### 2. **Job Execution** (Batch service manages)
```
Batch Service Internal State Machine:
SUBMITTED → PENDING → RUNNABLE → STARTING → RUNNING → SUCCEEDED/FAILED

Your serverless functions are NOT running during this time!
The batch service handles:
- Provisioning VMs/containers
- Executing the job
- Retries on failure
- Resource cleanup
```

**Key Point**: The batch service's internal job queue IS the coordinator. It's like Hangfire but fully managed by AWS/Azure/GCP.

#### 3. **Status Updates** (Event-driven)

**AWS Pattern** (EventBridge):
```
Batch job changes state (RUNNING → SUCCEEDED)
    ↓
EventBridge fires event
    ↓
Lambda function is triggered (only for a few seconds)
    ↓
Lambda updates YOUR database:
    await db.UpdateItem(new {
        JobId = "abc123",
        Status = "SUCCEEDED",
        CompletedAt = DateTimeOffset.UtcNow
    });
    ↓
Lambda exits
```

**Azure Pattern** (Durable Functions):
```
Durable orchestrator polls batch service
    ↓
while (status != "Completed") {
    await context.CreateTimer(5 seconds);  // Durable Functions persists state
    status = await PollBatchService();
}
    ↓
Update Cosmos DB
```

**GCP Pattern** (Workflows):
```yaml
# Workflow waits (Workflows service persists state)
- waitForCompletion:
    steps:
      - checkStatus:
          call: googleapis.batch.v1.jobs.get
      - waitAndRetry:
          call: sys.sleep
          args:
            seconds: 5
      # Repeat until done
```

### State Persistence Details

#### AWS: DynamoDB + Batch Service State
```
DynamoDB Table: GeoprocessingJobs
- Partition Key: JobId
- Attributes:
  * ProcessId (string)
  * Status (string) - "SUBMITTED", "RUNNING", "SUCCEEDED", "FAILED"
  * BatchJobId (string) - Reference to AWS Batch job
  * CreatedAt (number)
  * CompletedAt (number)
  * ResultS3Key (string) - Where to find the output
```

**What if Lambda crashes during status update?**
- EventBridge retries the event delivery (at-least-once semantics)
- Lambda is idempotent (updates same record)
- Batch service state is authoritative - you can always query it

**What if AWS Batch service restarts?**
- Job state is persisted to S3 internally by AWS Batch
- Jobs resume automatically
- Your DynamoDB might be briefly stale until next EventBridge event

#### Azure: Cosmos DB + Batch Service State + Durable Functions Storage
```
Cosmos DB Collection: GeoprocessingJobs
- id: JobId
- Attributes: (same as AWS)

Durable Functions State (Azure Storage):
- Orchestration instances
- Activity logs
- Timer states
```

**What if Azure Function crashes during orchestration?**
- Durable Functions automatically replays from last checkpoint
- Orchestration state stored in Azure Storage (Table + Queue + Blob)
- At-least-once execution guarantee

**What if Azure Batch service restarts?**
- Job state persisted internally by Azure Batch
- Durable orchestrator continues polling when it resumes

#### GCP: Firestore + Batch Service State + Workflows State
```
Firestore Collection: GeoprocessingJobs
- Document ID: JobId
- Fields: (same as AWS)

Workflows State (Google-managed):
- Execution history
- Current step
- Variable values
```

**What if Cloud Function crashes?**
- Workflows service manages retry (not your function)
- Workflow state is durable in Google's infrastructure
- Can resume from any step

**What if Cloud Batch service restarts?**
- Job state persisted internally
- Workflow polls will eventually succeed

### State Synchronization Pattern

Your application database is a **cache** of the batch service state:

```csharp
// API endpoint to get job status
GET /api/jobs/{jobId}

public async Task<JobStatus> GetJobStatus(string jobId)
{
    // 1. Read from YOUR database (fast)
    var cachedStatus = await _db.GetItem(jobId);

    // 2. If job is not complete, optionally refresh from batch service
    if (cachedStatus.Status is "SUBMITTED" or "RUNNING")
    {
        var liveStatus = await _batch.DescribeJobs(cachedStatus.BatchJobId);

        // 3. Update cache if changed
        if (liveStatus.Status != cachedStatus.Status)
        {
            await _db.UpdateItem(jobId, new { Status = liveStatus.Status });
            cachedStatus.Status = liveStatus.Status;
        }
    }

    return cachedStatus;
}
```

### Comparison: Serverless vs Hangfire

| Aspect | Hangfire (Traditional) | Serverless Batch |
|--------|------------------------|------------------|
| **Job Queue** | PostgreSQL table managed by Hangfire | AWS Batch / Azure Batch / Cloud Batch internal queue |
| **State Persistence** | PostgreSQL `hangfire.job` table | Batch service internal + your DynamoDB/Cosmos/Firestore |
| **Worker Process** | Persistent .NET process polling DB | Ephemeral containers launched by batch service |
| **Coordinator** | Hangfire server process | Batch service control plane |
| **High Availability** | You manage (multiple Hangfire servers) | Built-in (managed by cloud provider) |
| **Failure Recovery** | Hangfire retries from DB | Batch service retries + event redelivery |
| **Infrastructure Cost** | Always-on server ($50-200/month) | Pay per execution ($3-100/month) |
| **Operational Complexity** | Medium (deploy, monitor, scale) | Low (cloud provider handles it) |

### The Key Insight

**You don't need a persistent coordinator because the batch service IS the coordinator.**

```
Traditional Architecture:
  Your Server → Hangfire (coordinator) → Background Worker → Database

Serverless Architecture:
  API Gateway → Lambda (thin glue) → Batch Service (coordinator) → Container
                    ↓                          ↓
               DynamoDB (cache)      Internal persistent queue
```

In the serverless model:
- **AWS Batch** replaces Hangfire - it manages job queue, retries, state persistence
- **Lambda** replaces your API server - it just translates requests and events
- **DynamoDB** caches job metadata for fast API queries
- **EventBridge** notifies you of state changes (replaces Hangfire's polling)

### What Persists and Where

| Data | Storage Location | Managed By | Queryable Via |
|------|------------------|------------|---------------|
| **Job definition** | Batch job definition | Cloud provider | Batch API |
| **Job queue state** | Batch service internal | Cloud provider | Batch API |
| **Execution history** | Batch service internal | Cloud provider | Batch API |
| **Job metadata** | Your database | You | Your API |
| **Input parameters** | S3/Blob/GCS | You | Storage API |
| **Output results** | S3/Blob/GCS | You | Storage API |

### Recovery Scenarios

#### Scenario 1: Serverless function crashes during job submission
```
1. User calls POST /api/processes/buffer/execute
2. Lambda writes to DynamoDB ✅
3. Lambda calls batch.SubmitJob() ✅
4. Lambda crashes before returning response ❌

Result:
- Job is submitted to batch service ✅
- Job will execute successfully ✅
- User gets 500 error but can query GET /api/jobs/{jobId} to see it's running
```

**Fix**: Make submission idempotent using jobId as idempotency key.

#### Scenario 2: Event delivery fails
```
1. Batch job completes
2. EventBridge fires event
3. Lambda crashes before updating DynamoDB ❌

Result:
- EventBridge retries (up to 24 hours)
- Eventually Lambda succeeds
- DynamoDB updated ✅
```

#### Scenario 3: Batch service restarts
```
1. 100 jobs are running
2. AWS Batch control plane restarts
3. Jobs continue running on compute instances
4. Batch service recovers from persistent state
5. All jobs complete normally ✅
```

**The batch service guarantees job completion** - it's their responsibility, not yours.

---

#### 9.9.6 **Honua.Server.Host Integration Pattern**

**IMPORTANT CLARIFICATION**: You don't need separate Lambda/Functions for the API layer. **Honua.Server.Host can submit directly to batch services** regardless of how Honua itself is deployed.

### Architecture Options

#### Option A: Traditional Honua + Cloud Batch (Recommended for Most)

```
┌────────────────────────────────────────────────────┐
│  Honua.Server.Host (ASP.NET Core)                  │
│  - Running on VM, Container, or K8s                │
│  - Handles OGC API, GeoservicesREST, etc.          │
│  - POST /api/processes/buffer/execute              │
└─────────────────┬──────────────────────────────────┘
                  │
                  │ (submits job)
                  ↓
┌────────────────────────────────────────────────────┐
│  AWS Batch / Azure Batch / Cloud Batch             │
│  - Manages job queue and execution                 │
│  - Runs Python/long-running processes              │
└─────────────────┬──────────────────────────────────┘
                  │
                  ↓ (writes results)
┌────────────────────────────────────────────────────┐
│  S3 / Blob Storage / Cloud Storage                 │
└────────────────────────────────────────────────────┘
```

**Implementation in Honua.Server.Host**:

```csharp
// Honua.Server.Host/GeoservicesREST/GeoprocessingServerController.cs
public class GeoprocessingServerController : ControllerBase
{
    private readonly IAmazonBatch _batch;  // Or Azure/GCP equivalent
    private readonly IProcessRunRepository _processRuns;

    [HttpPost("arcgis/rest/services/GP/GPServer/{task}/submitJob")]
    public async Task<IActionResult> SubmitJob(string task, [FromBody] JsonElement parameters)
    {
        var jobId = Guid.NewGuid().ToString();

        // 1. Store job metadata in Honua's database (PostgreSQL/SQL Server/etc)
        await _processRuns.CreateAsync(new ProcessRun
        {
            JobId = jobId,
            ProcessId = task,
            Status = ProcessStatus.Submitted,
            Parameters = parameters.ToString(),
            SubmittedBy = User.Identity?.Name,
            SubmittedAt = DateTimeOffset.UtcNow
        });

        // 2. Submit to batch service
        var response = await _batch.SubmitJobAsync(new SubmitJobRequest
        {
            JobName = $"gp-{task}-{jobId}",
            JobQueue = _config["Geoprocessing:BatchQueue"],
            JobDefinition = $"honua-{task}",
            ContainerOverrides = new ContainerOverrides
            {
                Environment = new List<KeyValuePair>
                {
                    new("JOB_ID", jobId),
                    new("HONUA_API_URL", _config["SelfUrl"]),  // For callbacks
                    new("PARAMETERS", parameters.ToString())
                }
            }
        });

        // 3. Store batch job ID reference
        await _processRuns.UpdateBatchJobIdAsync(jobId, response.JobId);

        // 4. Return job ticket (GeoservicesREST format)
        return Ok(new
        {
            jobId,
            jobStatus = "esriJobSubmitted"
        });
    }

    [HttpGet("arcgis/rest/services/GP/GPServer/jobs/{jobId}")]
    public async Task<IActionResult> GetJobStatus(string jobId)
    {
        // Query Honua's database (fast)
        var job = await _processRuns.GetByIdAsync(jobId);

        // If still running, optionally refresh from batch service
        if (job.Status == ProcessStatus.Running)
        {
            var batchStatus = await _batch.DescribeJobsAsync(new DescribeJobsRequest
            {
                Jobs = new[] { job.BatchJobId }
            });

            // Update cache if changed
            if (MapBatchStatus(batchStatus.Jobs[0].Status) != job.Status)
            {
                job.Status = MapBatchStatus(batchStatus.Jobs[0].Status);
                await _processRuns.UpdateStatusAsync(jobId, job.Status);
            }
        }

        return Ok(new
        {
            jobId,
            jobStatus = MapToEsriStatus(job.Status),
            results = job.ResultS3Key != null ? new { outputParam = new { paramUrl = $"/jobs/{jobId}/results/output" } } : null
        });
    }
}
```

**Key Points**:
- ✅ Honua is the API endpoint (no separate Lambda)
- ✅ Honua submits to batch service
- ✅ Honua's database stores job metadata
- ✅ Batch service manages execution
- ✅ Works whether Honua runs on VM, container, or even serverless

**Event Handling** (batch job completion - **Event-Driven, No Polling**):

### AWS: EventBridge + SNS/SQS

**Setup** (one-time infrastructure):

```yaml
# CloudFormation / Terraform
EventBridgeRule:
  Type: AWS::Events::Rule
  Properties:
    EventPattern:
      source: [aws.batch]
      detail-type: [Batch Job State Change]
      detail:
        status: [SUCCEEDED, FAILED]
    Targets:
      - Arn: !GetAtt BatchJobCompletionQueue.Arn  # SQS queue
        Id: BatchCompletionTarget

BatchJobCompletionQueue:
  Type: AWS::SQS::Queue
  Properties:
    VisibilityTimeout: 300
```

**Honua Background Service** (consumes SQS messages):

```csharp
public class AwsBatchCompletionService : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly IProcessRunRepository _processRuns;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var queueUrl = _config["Geoprocessing:CompletionQueueUrl"];

        while (!ct.IsCancellationRequested)
        {
            // Long-poll SQS (20 second wait, no tight loop)
            var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 20,  // Long polling
                MessageAttributeNames = new List<string> { "All" }
            }, ct);

            foreach (var message in response.Messages)
            {
                var eventData = JsonSerializer.Deserialize<EventBridgeEvent>(message.Body);
                var jobName = eventData.Detail.JobName;  // "gp-buffer-abc123"
                var jobId = jobName.Split('-').Last();  // "abc123"
                var status = eventData.Detail.Status;  // "SUCCEEDED" or "FAILED"

                // Update Honua's database
                await _processRuns.UpdateStatusAsync(jobId, MapStatus(status));

                // Delete message from queue
                await _sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, ct);

                _logger.LogInformation("Batch job {JobId} completed with status {Status}", jobId, status);
            }
        }
    }
}

public record EventBridgeEvent(
    string DetailType,
    BatchJobDetail Detail
);

public record BatchJobDetail(
    string JobName,
    string Status,
    Dictionary<string, string> Container
);
```

**Benefits**:
- ✅ No polling - push-based notifications
- ✅ SQS long-polling (efficient, low cost)
- ✅ Guaranteed delivery (SQS persists messages)
- ✅ Automatic retries if processing fails
- ✅ Works with multiple Honua instances (queue-based)

---

### Azure: Event Grid + Service Bus

**Setup** (one-time):

```bash
# Create Event Grid subscription for Azure Batch events
az eventgrid event-subscription create \
  --name honua-batch-completion \
  --source-resource-id /subscriptions/.../resourceGroups/.../providers/Microsoft.Batch/batchAccounts/honua-gp \
  --endpoint-type servicebusqueue \
  --endpoint /subscriptions/.../resourceGroups/.../providers/Microsoft.ServiceBus/namespaces/honua/queues/batch-completion \
  --included-event-types Microsoft.Batch.TaskCompleted
```

**Honua Background Service**:

```csharp
public class AzureBatchCompletionService : BackgroundService
{
    private readonly ServiceBusClient _serviceBus;
    private readonly IProcessRunRepository _processRuns;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var receiver = _serviceBus.CreateReceiver("batch-completion");

        await foreach (var message in receiver.ReceiveMessagesAsync(ct))
        {
            var eventData = JsonSerializer.Deserialize<EventGridEvent>(message.Body);

            if (eventData.EventType == "Microsoft.Batch.TaskCompleted")
            {
                var data = eventData.Data.ToObject<BatchTaskCompletedData>();
                var jobId = data.TaskId.Split('-').Last();

                await _processRuns.UpdateStatusAsync(jobId,
                    data.ExitCode == 0 ? ProcessStatus.Succeeded : ProcessStatus.Failed);

                await receiver.CompleteMessageAsync(message, ct);
            }
        }
    }
}
```

---

### GCP: Pub/Sub Notifications

**Setup** (one-time):

```bash
# Create Pub/Sub topic
gcloud pubsub topics create honua-batch-completion

# Create subscription
gcloud pubsub subscriptions create honua-batch-completion-sub \
  --topic honua-batch-completion \
  --ack-deadline 300

# Configure Cloud Batch to publish to Pub/Sub (via Cloud Logging sink)
gcloud logging sinks create batch-completion-sink \
  pubsub.googleapis.com/projects/honua/topics/honua-batch-completion \
  --log-filter='resource.type="cloud_batch_job" AND severity="INFO" AND jsonPayload.message="Job completed"'
```

**Honua Background Service**:

```csharp
public class GcpBatchCompletionService : BackgroundService
{
    private readonly SubscriberClient _subscriber;
    private readonly IProcessRunRepository _processRuns;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var subscriptionName = SubscriptionName.FromProjectSubscription("honua", "honua-batch-completion-sub");

        await _subscriber.StartAsync((message, cancellationToken) =>
        {
            var logEntry = JsonSerializer.Deserialize<LogEntry>(message.Data.ToStringUtf8());
            var jobId = logEntry.Labels["job_id"];
            var status = logEntry.JsonPayload.Status;

            await _processRuns.UpdateStatusAsync(jobId, MapStatus(status));

            return Task.FromResult(SubscriberClient.Reply.Ack);
        });

        await Task.Delay(Timeout.Infinite, ct);
    }
}
```

---

### Alternative: Webhook Callback (Cloud-Agnostic)

If you want a simpler, cloud-agnostic approach without EventBridge/Event Grid/Pub/Sub:

**Honua Webhook Endpoint**:

```csharp
[HttpPost("api/internal/batch-webhook")]
[AllowAnonymous]
public async Task<IActionResult> BatchWebhook([FromBody] BatchWebhookRequest request)
{
    // Verify HMAC signature to prevent spoofing
    var expectedSignature = ComputeHmac(_config["Geoprocessing:WebhookSecret"], request.JobId + request.Status);
    if (request.Signature != expectedSignature)
        return Unauthorized();

    await _processRuns.UpdateStatusAsync(request.JobId, request.Status, request.ResultS3Key);

    return Ok();
}

private string ComputeHmac(string secret, string message)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
    return Convert.ToBase64String(hash);
}
```

**Batch Job Container** (calls webhook on completion):

```python
# Python container running in AWS Batch / Azure Batch / Cloud Batch
import os
import hmac
import hashlib
import requests
import json

def compute_signature(secret, message):
    return hmac.new(
        secret.encode(),
        message.encode(),
        hashlib.sha256
    ).hexdigest()

# Load config
job_id = os.environ['JOB_ID']
honua_webhook_url = os.environ['HONUA_WEBHOOK_URL']
webhook_secret = os.environ['WEBHOOK_SECRET']

try:
    # Run geoprocessing
    result = run_buffer_process(json.loads(os.environ['PARAMETERS']))

    # Upload results
    s3_key = f"gp/outputs/{job_id}/result.json"
    s3.put_object(Bucket='honua-gp', Key=s3_key, Body=json.dumps(result))

    status = "SUCCEEDED"
except Exception as e:
    status = "FAILED"
    s3_key = None

# Notify Honua
signature = compute_signature(webhook_secret, f"{job_id}{status}")
requests.post(honua_webhook_url, json={
    "jobId": job_id,
    "status": status,
    "resultS3Key": s3_key,
    "signature": signature
}, timeout=30)
```

**Benefits**:
- ✅ Works with any cloud provider
- ✅ No additional infrastructure (no queues)
- ✅ Direct notification (lowest latency)
- ✅ HMAC signature prevents spoofing

**Drawbacks**:
- ❌ Not resilient if Honua is down (batch job can't retry webhook)
- ❌ Requires Honua to be publicly accessible

**Solution**: Combine webhook with SQS/Service Bus fallback:

```python
# Try webhook first (fast path)
try:
    response = requests.post(honua_webhook_url, json=payload, timeout=10)
    if response.status_code == 200:
        exit(0)  # Success
except:
    pass  # Fall through to queue

# Fallback: Publish to SQS/Service Bus (reliable path)
sqs.send_message(QueueUrl=completion_queue_url, MessageBody=json.dumps(payload))
```

---

### Recommended Pattern

**AWS**: EventBridge → SQS → Honua background service
**Azure**: Event Grid → Service Bus → Honua background service
**GCP**: Pub/Sub → Honua background service
**Cloud-agnostic**: Webhook with queue fallback

**All patterns are push-based - zero polling!**

---

### 🔒 Networking Security: Private vs Public Honua Deployment

#### **Queue/Pub-Sub Pattern: ✅ Honua Can Be PRIVATE**

With the recommended event-driven patterns (SQS, Service Bus, Pub/Sub), **Honua does NOT need a public IP address** because it **pulls** messages from the queue:

**AWS (EventBridge → SQS)**:
- ✅ Honua pulls messages from SQS via long-polling (outbound HTTPS)
- ✅ Honua can be in a private subnet with no public IP
- ✅ Use VPC Endpoint for SQS (no internet gateway required)
- ✅ EventBridge publishes to SQS (no callback to Honua)

**Azure (Event Grid → Service Bus)**:
- ✅ Honua pulls messages from Service Bus via ReceiveMessagesAsync (outbound HTTPS)
- ✅ Honua can be in a private VNet with no public IP
- ✅ Use Private Link for Service Bus (no internet required)
- ✅ Event Grid publishes to Service Bus (no callback to Honua)

**GCP (Pub/Sub)**:
- ✅ Honua subscribes and pulls messages from Pub/Sub (outbound HTTPS)
- ✅ Honua can be in a private VPC with no public IP
- ✅ Use Private Service Connect for Pub/Sub (no internet required)
- ✅ Cloud Logging publishes to Pub/Sub (no callback to Honua)

**Architecture Diagram** (Honua remains private):

```
┌─────────────────────────────────────────┐
│  Private VNet/VPC                       │
│  ┌────────────────────────────────────┐ │
│  │  Honua.Server.Host                 │ │
│  │  - No public IP                    │ │
│  │  - Pulls from queue (outbound)  ───┼─┼──→ Service Bus/SQS/Pub/Sub
│  └────────────────────────────────────┘ │      (via Private Link)
└─────────────────────────────────────────┘
```

#### **Webhook Pattern: ⚠️ Honua Must Be PUBLICLY ACCESSIBLE**

With the webhook callback pattern, batch job containers need to POST back to Honua:

- ⚠️ Honua needs a public endpoint (or VPN/private link from batch service to Honua VNet)
- ⚠️ Requires HMAC signature verification to prevent spoofing
- ⚠️ Exposes attack surface (DDoS risk)
- ⚠️ Not recommended for production

**Security Comparison**:

| Pattern | Honua Accessibility | Attack Surface | Recommended |
|---------|---------------------|----------------|-------------|
| **EventBridge → SQS** | Private | Minimal (outbound only) | ✅ Yes |
| **Event Grid → Service Bus** | Private | Minimal (outbound only) | ✅ Yes |
| **Pub/Sub** | Private | Minimal (outbound only) | ✅ Yes |
| **Webhook Callback** | Public or VPN | High (exposed endpoint) | ⚠️ No |

**Recommendation**: Use the queue/pub-sub pattern (SQS/Service Bus/Pub/Sub) to keep Honua private and minimize attack surface.

---

#### Option B: Serverless Honua + Cloud Batch

If you want to run **Honua.Server.Host itself as serverless** (e.g., AWS Lambda + API Gateway), you can! The same code works:

```
┌────────────────────────────────────────────────────┐
│  Honua.Server.Host deployed as Lambda Function     │
│  - AWS Lambda .NET runtime                         │
│  - API Gateway in front                            │
│  - Same ASP.NET Core code                          │
└─────────────────┬──────────────────────────────────┘
                  │
                  ↓ (submits job)
┌────────────────────────────────────────────────────┐
│  AWS Batch                                         │
└────────────────────────────────────────────────────┘
```

**Benefits**:
- Honua scales to zero when idle
- Pay only for API requests
- Still submits to batch service for long-running jobs

**Limitations**:
- Lambda timeout (15 min max) - not ideal for long-running Tier 2 PostGIS queries
- Cold starts (500ms-2s)
- Stateful connections harder (e.g., persistent PostGIS connection pools)

**Recommendation**: Run **Honua traditionally** (VM/container/K8s) for the main API, use **cloud batch services** for Tier 3 Python jobs.

---

#### Option C: Hybrid (Traditional Tier 1/2, Serverless Tier 3)

The **recommended architecture** for most deployments:

```
┌────────────────────────────────────────────────────┐
│  Honua.Server.Host (Traditional/Container)         │
│  - Handles all API requests                        │
│  - Tier 1 (NTS): In-process                        │
│  - Tier 2 (PostGIS): Via connection pool           │
│  - Tier 3 (Python): Submits to batch service       │
└─────────────────┬──────────────────────────────────┘
                  │
                  │ (only Tier 3 jobs)
                  ↓
┌────────────────────────────────────────────────────┐
│  AWS Batch / Azure Batch / Cloud Batch             │
│  - Handles long-running Python jobs                │
│  - Scales independently of Honua                   │
└────────────────────────────────────────────────────┘
```

**Why this is optimal**:
- ✅ Honua handles fast operations (Tier 1/2) with low latency
- ✅ Batch service handles slow operations (Tier 3) with isolation
- ✅ No cold start penalty for common operations
- ✅ PostGIS connection pooling works normally
- ✅ Scale Tier 3 independently (0 to 1000s of containers)

**Implementation**:

```csharp
public class ProcessExecutionCoordinator : IProcessExecutionCoordinator
{
    private readonly INtsExecutor _nts;
    private readonly IPostGisExecutor _postGis;
    private readonly IBatchServiceExecutor _batch;  // AWS/Azure/GCP

    public async Task<ProcessResult> ExecuteAsync(ProcessExecutionRequest request, CancellationToken ct)
    {
        var process = _registry.GetProcess(request.ProcessId);

        // Tier 1: NetTopologySuite (in-process, < 100ms)
        if (process.TierPreferences.Contains(Tier.NetTopologySuite) &&
            process.ResourceClass == ResourceClass.CpuBurst)
        {
            return await _nts.ExecuteAsync(process, request, ct);
        }

        // Tier 2: PostGIS (connection pool, 1-10s)
        if (process.TierPreferences.Contains(Tier.PostGIS) &&
            process.ResourceClass == ResourceClass.DbHeavy)
        {
            return await _postGis.ExecuteAsync(process, request, ct);
        }

        // Tier 3: Batch service (long-running, 10s-30min)
        if (process.TierPreferences.Contains(Tier.Python) &&
            process.ResourceClass is ResourceClass.LongTail or ResourceClass.PythonGpu)
        {
            return await _batch.SubmitAsync(process, request, ct);  // Returns job ticket
        }

        throw new NotSupportedException($"No executor available for {request.ProcessId}");
    }
}
```

---

### Deployment Comparison

| Aspect | Honua Traditional + Batch | Honua Serverless + Batch | Separate Lambda + Batch |
|--------|---------------------------|--------------------------|-------------------------|
| **Honua Deployment** | VM/Container/K8s | Lambda Functions | Not applicable |
| **Tier 1/2 Latency** | Low (in-process) | Medium (cold starts) | N/A (no Honua) |
| **Tier 3 Execution** | Batch service | Batch service | Batch service |
| **Operational Complexity** | Medium | Low | Lowest |
| **Cost (idle)** | $50-200/month | $0/month | $0/month |
| **Cost (active)** | $50-200/month + batch | Batch only | Batch only |
| **Best For** | Production, low latency | Variable traffic | Geoprocessing-only service |

**Recommendation**: **Honua Traditional + Cloud Batch** (Option C) for most deployments.

---

**The batch service guarantees job completion** - it's their responsibility, not yours.

---

### 9.10 Recommendation Matrix

#### Phase 0-1: MVP (Months 0-3)
**Recommendation**: **Subprocess** or **Docker (local)**
- Simplest to implement
- Get to market fast
- Validate product-market fit before infrastructure investment

```csharp
services.AddSingleton<IPythonExecutor, SubprocessPythonExecutor>();
```

#### Phase 2: Growth (Months 3-12)
**Recommendation**: **Kubernetes Jobs** or **AWS Batch** (pick based on cloud commitment)

**Choose Kubernetes if**:
- Multi-cloud requirement
- Already have K8s expertise
- Need fine-grained control
- GPU workloads with diverse requirements

**Choose AWS Batch if**:
- Committed to AWS
- Want fully managed solution
- Variable/spiky load
- Cost-sensitive

```csharp
// Abstract behind interface
services.AddSingleton<IPythonExecutor>(sp =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    return env.IsProduction()
        ? new KubernetesJobExecutor(sp.GetRequiredService<IKubernetes>())
        : new DockerPythonExecutor();
});
```

#### Phase 3: Scale (12+ months, 1000s of jobs/day)
**Recommendation**: **Kubernetes Jobs** with multi-cloud support

- Deploy to multiple cloud providers (EKS, AKS, GKE)
- Use Terraform for cluster provisioning
- Autoscaling based on Hangfire queue depth
- Cost optimization via spot/preemptible instances

---

### 9.10 Hybrid Approach (Recommended)

**Best practice**: Support multiple executors with configuration-based selection:

```csharp
public interface IPythonExecutor
{
    Task<ProcessResult> ExecuteAsync(string processId, ProcessParameters parameters, CancellationToken ct);
}

public class PythonExecutorFactory
{
    public IPythonExecutor Create(ProcessExecutionRequest request)
    {
        // GPU required → AWS Batch (P3 instances)
        if (request.Process.RequiresGpu)
            return new AwsBatchExecutor(_batchClient, "gpu-queue");

        // Long-running (> 30 min) → Kubernetes (no time limit)
        if (request.Process.EstimatedDuration > TimeSpan.FromMinutes(30))
            return new KubernetesJobExecutor(_k8s);

        // Quick jobs → Docker (faster startup)
        if (request.Process.EstimatedDuration < TimeSpan.FromMinutes(5))
            return new DockerPythonExecutor();

        // Default → Kubernetes
        return new KubernetesJobExecutor(_k8s);
    }
}
```

### 9.11 Cost Comparison (1000 jobs/day, 10 min avg duration)

| Option | Monthly Cost | Notes |
|--------|--------------|-------|
| **Subprocess** | $0 (existing server) | Limited by server capacity |
| **Docker (local)** | $0 (existing server) | Limited by server capacity |
| **Kubernetes (EKS)** | $350 (cluster) + $192 (compute) = **$542** | 3-node cluster + autoscaling |
| **AWS Batch** | $0 (baseline) + $320 (compute) = **$320** | Pay only for job execution |
| **AWS Batch (Spot)** | $0 (baseline) + $96 (compute) = **$96** | 70% savings with Spot |
| **GCP Cloud Run Jobs** | $280 (compute) | Serverless pricing |

**Winner for scale**: **AWS Batch with Spot Instances** ($96/month for 1000 jobs/day)

---

### 9.12 Implementation Phases

**Phase 1 (Weeks 1-2)**: Subprocess executor
- Implement `SubprocessPythonExecutor`
- Add resource limits via cgroups (Linux) or Job Objects (Windows)
- Test with sample Python processes

**Phase 2 (Weeks 3-4)**: Docker executor
- Create `honua-python` Docker image
- Implement `DockerPythonExecutor`
- Add network isolation and read-only filesystem

**Phase 3 (Weeks 5-8)**: Cloud executor (choose one)
- **Option A**: Kubernetes
  - Set up EKS/AKS/GKE cluster
  - Implement `KubernetesJobExecutor`
  - Add autoscaling policies
- **Option B**: AWS Batch
  - Create Batch compute environment
  - Define job definitions
  - Implement `AwsBatchExecutor`

**Phase 4 (Weeks 9-10)**: Production hardening
- Add retry logic for transient failures
- Implement job timeout handling
- Cost monitoring and alerting
- Load testing (1000+ concurrent jobs)

---

## Conclusion

Both Esri and GeoServer have valuable lessons for Honua:

**From Esri, adopt**:
- High isolation (OS processes)
- Dedicated job states
- Workload separation
- Job persistence

**From GeoServer, adopt**:
- Process-level RBAC
- Catalog integration
- Plugin architecture
- Process chaining (with JSON, not XML)

**Honua innovations**:
- Multi-tier execution with fallback
- Resource classes and queue governor
- Artifact externalization
- Provenance tracking
- Multi-tenant isolation
- Dual API (OGC WPS + JSON REST)

**Recommended Architecture**:
```
┌─────────────────────────────────────────────────────────────────────────┐
│  Honua.Server.Host - API Adapters (Protocol Translation Only)          │
├─────────────────────────┬───────────────────────┬───────────────────────┤
│  /wps                   │  /arcgis/.../GPServer │  /api/processes       │
│  (OGC WPS 2.0)          │  (GeoservicesREST)    │  (Modern JSON)        │
│                         │                       │                       │
│  XML Request            │  JSON Request         │  JSON Request         │
│    ↓                    │    ↓                  │    ↓                  │
│  WpsRequestAdapter      │  GpServerAdapter      │  ApiProcessAdapter    │
│    ↓                    │    ↓                  │    ↓                  │
│  Internal ProcessModel  │  Internal ProcessModel│  Internal ProcessModel│
│    ↓                    │    ↓                  │    ↓                  │
└─────┬───────────────────┴───────┬───────────────┴───────┬───────────────┘
      │                           │                       │
      └───────────────────────────┼───────────────────────┘
                                  │
┌─────────────────────────────────▼───────────────────────────────────────┐
│  Geoprocessing Core (Protocol-Agnostic)                                 │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  IProcess Interface                                             │   │
│  │    - Metadata (name, description, parameters)                   │   │
│  │    - ExecuteAsync(ProcessExecutionContext, CancellationToken)   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  ProcessRegistry (discovers and manages processes)                      │
│  ProcessExecutionCoordinator (tier selection & fallback)                │
│  GeoprocessingJobManager (Hangfire integration)                         │
│  QueueGovernor (capacity reservation)                                   │
│  LayerReferenceResolver (catalog integration)                           │
│  ArtifactStore (S3/Blob storage)                                        │
│  ProcessRunLedger (provenance tracking)                                 │
└─────────────────────────────┬────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
      ┌───────▼──────┐ ┌──────▼──────┐ ┌─────▼─────────┐
      │   Tier 1     │ │   Tier 2    │ │    Tier 3     │
      │  NTS (.NET)  │ │  PostGIS    │ │  Python       │
      │  Thread      │ │  Stored     │ │  Subprocess / │
      │  < 100ms     │ │  Procedure  │ │  K8s Job /    │
      │              │ │  1-10s      │ │  AWS Batch    │
      │              │ │             │ │  10s-30m      │
      └──────────────┘ └─────────────┘ └───────────────┘
```

### Shared Backend Pattern

**Key Insight**: All three API surfaces (WPS, GPServer, modern JSON) are **thin adapters** that translate between protocol-specific formats and a common internal process model. The actual execution logic is implemented once in the core.

#### API Adapter Responsibilities

**WpsRequestAdapter** (`/wps` endpoint):
- Parse XML Execute request → `ProcessExecutionRequest`
- Invoke core engine
- Translate `ProcessResult` → WPS ExecuteResponse XML
- Map WPS data types to/from internal types
- Handle WPS job status polling format

**GpServerAdapter** (`/arcgis/rest/services/.../GPServer` endpoint):
- Parse JSON parameters → `ProcessExecutionRequest`
- Invoke core engine
- Translate `ProcessResult` → GPServer JSON response
- Map GPServer data types to/from internal types
- Handle GPServer job status format (jobId, jobStatus, results)

**ApiProcessAdapter** (`/api/processes` endpoint):
- Parse modern JSON → `ProcessExecutionRequest`
- Invoke core engine
- Translate `ProcessResult` → Clean JSON response
- Support Server-Sent Events for job updates
- Enhanced error messages with troubleshooting hints

#### Internal Process Model

All adapters translate to/from this protocol-agnostic model:

```csharp
public sealed record ProcessExecutionRequest(
    string ProcessId,
    IReadOnlyDictionary<string, object?> Inputs,
    ProcessExecutionOptions Options,
    ClaimsPrincipal User
);

public sealed record ProcessExecutionOptions(
    int? TargetSrs,
    int? ProcessSrs,
    BoundingBox? Extent,
    bool Async,
    ProcessOutputOptions? OutputOptions
);

public sealed record ProcessResult(
    IReadOnlyDictionary<string, object?> Outputs,
    IReadOnlyList<ProcessMessage> Messages,
    ProcessExecutionTier ActualTier,
    TimeSpan Duration
);
```

#### Benefits of Shared Backend

1. **Write Once, Run Anywhere**: Process implementations work with all three APIs automatically
2. **Test Once**: Core execution logic tested independently of protocol formats
3. **Add New Protocols Easily**: e.g., OGC API - Processes (Part 1) is just another adapter
4. **Consistent Behavior**: Job lifecycle, security, resource limits work identically
5. **Performance**: No duplicate code paths or logic
6. **Maintainability**: Bug fixes apply to all APIs simultaneously

#### Example: Buffer Process Implementation

```csharp
public class BufferProcess : IProcess
{
    public ProcessMetadata Metadata => new()
    {
        Id = "buffer",
        DisplayName = "Buffer Features",
        Description = "Creates buffer polygons around input features",
        Parameters = new[]
        {
            new ProcessParameter("features", ProcessParameterType.FeatureCollection, required: true),
            new ProcessParameter("distance", ProcessParameterType.LinearUnit, required: true),
            new ProcessParameter("output", ProcessParameterType.FeatureCollection, direction: ParameterDirection.Output)
        },
        TierPreferences = [Tier.PostGIS, Tier.NetTopologySuite],
        ResourceClass = ResourceClass.DbHeavy
    };

    public async Task<ProcessResult> ExecuteAsync(ProcessExecutionContext ctx, CancellationToken ct)
    {
        var features = ctx.GetInput<IAsyncEnumerable<FeatureRecord>>("features");
        var distance = ctx.GetInput<LinearUnit>("distance");

        var buffered = ctx.Tier switch
        {
            Tier.PostGIS => await ExecutePostGISAsync(features, distance, ct),
            Tier.NetTopologySuite => await ExecuteNTSAsync(features, distance, ct),
            _ => throw new NotSupportedException()
        };

        return new ProcessResult(
            Outputs: new Dictionary<string, object?> { ["output"] = buffered },
            Messages: [],
            ActualTier: ctx.Tier,
            Duration: ctx.Elapsed
        );
    }
}
```

This single implementation automatically works with:

**OGC WPS Request**:
```xml
<wps:Execute service="WPS" version="2.0.0">
  <ows:Identifier>buffer</ows:Identifier>
  <wps:Input id="features">
    <wps:Reference href="http://localhost/collections/roads/items"/>
  </wps:Input>
  <wps:Input id="distance">
    <wps:Data><wps:LiteralValue>100</wps:LiteralValue></wps:Data>
  </wps:Input>
</wps:Execute>
```

**GeoservicesREST GPServer Request**:
```json
POST /arcgis/rest/services/GP/GPServer/buffer/submitJob
{
  "features": {"layerReference": "roads"},
  "distance": {"distance": 100, "units": "esriMeters"},
  "f": "json"
}
```

**Modern JSON API Request**:
```json
POST /api/processes/buffer/execute
{
  "inputs": {
    "features": {"layerReference": "roads"},
    "distance": {"value": 100, "unit": "meters"}
  }
}
```

All three formats execute the same `BufferProcess.ExecuteAsync` method.

#### Adapter Implementation Example

```csharp
// GeoservicesREST adapter
public class GeoprocessingServerController : ControllerBase
{
    private readonly IProcessExecutionCoordinator _coordinator;
    private readonly IGpServerAdapter _adapter;

    [HttpPost("arcgis/rest/services/GP/GPServer/{task}/submitJob")]
    public async Task<IActionResult> SubmitJob(string task, [FromBody] JsonElement body, CancellationToken ct)
    {
        // 1. Translate GPServer JSON to internal model
        var request = _adapter.TranslateRequest(task, body, User);

        // 2. Execute via core engine (protocol-agnostic)
        var ticket = await _coordinator.SubmitAsync(request, ct);

        // 3. Translate internal model to GPServer JSON
        var response = _adapter.TranslateJobTicket(ticket);
        return Ok(response);
    }
}

// OGC WPS adapter
public class WpsController : ControllerBase
{
    private readonly IProcessExecutionCoordinator _coordinator;
    private readonly IWpsAdapter _adapter;

    [HttpPost("wps")]
    public async Task<IActionResult> Execute([FromBody] XElement executeRequest, CancellationToken ct)
    {
        // 1. Translate WPS XML to internal model
        var request = _adapter.TranslateExecuteRequest(executeRequest, User);

        // 2. Execute via core engine (same as above!)
        var ticket = await _coordinator.SubmitAsync(request, ct);

        // 3. Translate internal model to WPS XML
        var response = _adapter.TranslateExecuteResponse(ticket);
        return Content(response.ToString(), "application/xml");
    }
}
```

This design balances:
- **Standards compliance** (OGC WPS)
- **Ecosystem compatibility** (GeoservicesREST)
- **Developer experience** (Modern JSON REST)
- **Performance** (multi-tier execution)
- **Security** (process isolation, tenant boundaries)
- **Scalability** (separate workers, artifact externalization)
- **Usability** (catalog integration, auto-registration)
- **Maintainability** (single implementation, multiple API surfaces)

Estimated effort: **12-15 weeks** for full implementation across all phases.
