# Technical Debt Backlog

**Generated:** 2025-11-10
**Total TODOs:** 167 comments across 78 files
**Last Updated:** Auto-generated from codebase analysis

---

## Executive Summary

This document catalogs all TODO/FIXME/HACK comments found in the Honua.Server codebase, categorized by severity and type. The analysis identifies critical security issues, incomplete features, and technical debt requiring attention.

### Critical Security Issues (IMMEDIATE ACTION REQUIRED)

**3 critical security TODOs** identified that could impact multi-tenant security and authentication:

1. **Tenant Isolation** - Hardcoded tenant IDs in multiple controllers
2. **User Identity Extraction** - Missing authentication context integration
3. **Admin Authorization** - Incomplete authorization on admin endpoints

---

## Summary Statistics

### By Category

| Category | Count | Percentage |
|----------|-------|------------|
| Security | 15 | 9% |
| Feature Completion | 68 | 41% |
| Refactoring | 52 | 31% |
| Performance | 12 | 7% |
| Documentation | 8 | 5% |
| Integration | 12 | 7% |

### By Severity

| Severity | Count | Percentage |
|----------|-------|------------|
| Critical | 15 | 9% |
| High | 43 | 26% |
| Medium | 76 | 45% |
| Low | 33 | 20% |

### By Component

| Component | Count |
|-----------|-------|
| Admin Blazor UI | 38 |
| OGC Services | 24 |
| Data Providers (Enterprise) | 18 |
| MapSDK Components | 17 |
| ETL/Workflows | 15 |
| Authentication/Authorization | 12 |
| Field Mobile App | 6 |
| Alerts & Monitoring | 10 |
| Geoprocessing | 8 |
| Core Services | 19 |

---

## CRITICAL PRIORITY (Severity: Critical)

### 1. Tenant Isolation - Multi-Tenancy Security Gap

**Severity:** CRITICAL - SECURITY
**Category:** Security
**Impact:** Data leakage between tenants in multi-tenant deployments

**Affected Files:**
- `.github/ISSUE_TEMPLATE/todo-003-tenant-isolation.md`
- Multiple controllers with hardcoded tenant IDs

**Description:**
Multiple controllers use hardcoded `tenantId = "default"` instead of extracting from authentication claims. This creates a critical security vulnerability in multi-tenant deployments where data could be exposed across tenant boundaries.

**Locations:**
1. `GeoEventController.cs:345` - "TODO: Extract tenant ID from claims or context"
2. `AzureStreamAnalyticsController.cs:291` - "TODO: Extract tenant ID from claims or context"
3. `GeofencesController.cs:266` - "TODO: Extract tenant ID from claims or context"
4. `Admin.Blazor/GeoEtl/ScheduleEditorDialog.razor:171` - Hardcoded GUID
5. `Admin.Blazor/GeoEtl/WorkflowList.razor:174` - "TODO: Replace with actual tenant ID from authentication"
6. `Admin.Blazor/GeoEtl/WorkflowSchedules.razor:202` - "TODO: Get actual tenant ID from auth context"
7. `Admin.Blazor/GeoEtl/TemplateGallery.razor:237` - "TODO: Replace with actual tenant ID and user ID from authentication"

**Effort:** 2-3 days
**Dependencies:** Requires authentication infrastructure to be fully integrated

**Suggested Approach:**
1. Create a `ITenantContext` service that extracts tenant ID from JWT claims
2. Register as scoped service in DI container
3. Update all controllers to inject and use `ITenantContext`
4. Add integration tests to verify tenant isolation
5. Add middleware to reject requests without valid tenant claims

**Security Risk:** HIGH - Could allow unauthorized cross-tenant data access

---

### 2. User Identity Extraction from Authentication Context

**Severity:** CRITICAL - SECURITY
**Category:** Security
**Impact:** Audit trails and user tracking incomplete

**Affected Files:**
- `.github/ISSUE_TEMPLATE/todo-002-user-identity-extraction.md`
- Multiple endpoints using placeholder values

**Description:**
CreatedBy, UpdatedBy, and SessionId fields are populated with hardcoded values ("admin", Guid.Empty) instead of actual authenticated user information.

**Locations:**
1. Multiple endpoints: `CreatedBy = "admin" // TODO: Get from authentication context`
2. Session tracking: `SessionId = Guid.Empty, // TODO: Get from session`
3. `Authentication/SamlEndpoints.cs:186` - Session ID hardcoded to Guid.Empty
4. `Admin.Blazor/GeoEtl/ScheduleEditorDialog.razor:173-174` - CreatedBy/UpdatedBy hardcoded

**Effort:** 1-2 days
**Dependencies:** Authentication system integration

**Suggested Approach:**
1. Create `IUserContext` service similar to tenant context
2. Extract user ID, username, and session ID from authentication claims
3. Create base controller/page class that provides user context
4. Update all creation/update operations to use real user context
5. Add audit logging integration

**Security Risk:** MEDIUM - Impacts audit trail reliability

---

### 3. Admin Endpoints Authorization

**Severity:** CRITICAL - SECURITY
**Category:** Security
**Impact:** Admin endpoints may be accessible without proper authorization

**Affected Files:**
- `.github/ISSUE_TEMPLATE/todo-001-admin-authorization.md`

**Description:**
Admin endpoints have `.RequireAuthorization("RequireAdministrator")` policy but include TODO comments indicating auth integration is incomplete.

**Locations:**
Multiple admin endpoints with: `// TODO: Add authorization after auth integration`

**Effort:** 3-4 days
**Dependencies:** Complete authentication/authorization infrastructure

**Suggested Approach:**
1. Verify `RequireAdministrator` policy is properly configured
2. Add role-based claims to JWT tokens
3. Test all admin endpoints with non-admin users
4. Add attribute-based authorization for granular permissions
5. Implement API key authentication for service-to-service calls
6. Add rate limiting to admin endpoints

**Security Risk:** HIGH - Unauthorized admin access potential

---

### 4. Hard Delete Implementation for Enterprise Data Stores

**Severity:** CRITICAL - DATA INTEGRITY
**Category:** Feature Completion
**Impact:** GDPR/compliance requirements not met

**Affected Files:**
- `Enterprise/Data/MongoDB/MongoDbDataStoreProvider.cs:249`
- `Enterprise/Data/BigQuery/BigQueryDataStoreProvider.cs:248`
- `Enterprise/Data/Elasticsearch/ElasticsearchDataStoreProvider.Deletes.cs:90`
- `Enterprise/Data/CosmosDb/CosmosDbDataStoreProvider.cs:268`

**Description:**
Hard delete functionality not implemented for enterprise data stores (MongoDB, BigQuery, Elasticsearch, CosmosDB). Currently only soft deletes are supported, which may violate GDPR "right to be forgotten" requirements.

**Effort:** 2-3 days per provider (8-12 days total)

**Suggested Approach:**
1. Implement `HardDeleteAsync` methods for each provider
2. Add cascade delete logic for related entities
3. Create administrative API for GDPR deletion requests
4. Add audit logging for all hard deletes
5. Implement backup/archive before hard delete
6. Add confirmation workflows for accidental deletion prevention

**Compliance Risk:** HIGH - GDPR non-compliance

---

### 5. Connection String Encryption - Incomplete KMS Integration

**Severity:** CRITICAL - SECURITY
**Category:** Security
**Impact:** Connection strings may be stored in plain text

**Affected Files:**
- `Server.Host/appsettings.ConnectionEncryption.json:52` - AWS KMS
- `Server.Host/appsettings.ConnectionEncryption.json:65` - GCP KMS

**Description:**
Configuration indicates AWS and GCP KMS encryption is incomplete - "currently uses file system with TODO for full KMS integration"

**Effort:** 4-5 days
**Dependencies:** Cloud provider SDK integration

**Suggested Approach:**
1. Implement AWS KMS integration using AWS.KeyManagementService SDK
2. Implement GCP KMS integration using Google.Cloud.Kms.V1
3. Create encryption/decryption service abstraction
4. Migrate existing connection strings to encrypted format
5. Add key rotation support
6. Implement fallback mechanism for development environments

**Security Risk:** HIGH - Credentials exposure risk

---

## HIGH PRIORITY (Severity: High)

### 6. Alert Publishing Logic Implementation

**Severity:** HIGH
**Category:** Feature Completion
**Component:** Alerts & Monitoring

**Location:** `.github/ISSUE_TEMPLATE/todo-005-alert-publishing.md`

**Description:**
Alert publishing logic is not implemented - alerts are detected but not sent to notification channels.

**Effort:** 3-4 days

**Suggested Approach:**
1. Implement AlertPublisher service
2. Add support for multiple channels (email, webhook, SMS, Slack)
3. Implement retry logic with exponential backoff
4. Add alert deduplication and throttling
5. Integrate with notification channel management
6. Add delivery status tracking

---

### 7. Connection Testing for Data Sources

**Severity:** HIGH
**Category:** Feature Completion
**Component:** Data Sources

**Locations:**
- `.github/ISSUE_TEMPLATE/todo-008-connection-testing.md`
- `Server.Host/Admin/MetadataAdministrationEndpoints.DataSources.cs:266`

**Description:**
`// TODO: Implement actual connection test based on provider` - Users cannot validate data source connections before saving.

**Effort:** 2-3 days

**Suggested Approach:**
1. Add `TestConnectionAsync` method to each data store provider
2. Implement provider-specific connection validation
3. Add timeout and error handling
4. Return detailed error messages for troubleshooting
5. Add UI feedback for connection test results

---

### 8. OGC Services Phase 2 Refactoring

**Severity:** HIGH
**Category:** Refactoring
**Component:** OGC Services

**Affected Files (24 locations):**
- `Server.Host/Ogc/IWmsHandler.cs` (4 TODOs)
- `Server.Host/Ogc/IWfsHandler.cs` (5 TODOs)
- `Server.Host/Ogc/IWmtsHandler.cs` (4 TODOs)
- `Server.Host/Ogc/IWcsHandler.cs` (4 TODOs)
- `Server.Host/Ogc/WmsHandlers.cs` (4 TODOs)
- `Server.Host/Ogc/WmtsHandlers.cs` (4 TODOs)
- `Server.Host/Ogc/WcsHandlers.cs` (4 TODOs)

**Description:**
All OGC handler interfaces and classes contain "TODO Phase 2: Move implementation from OgcSharedHandlers" comments. This indicates planned architectural refactoring to separate concerns properly.

**Effort:** 2-3 weeks (significant refactoring)

**Suggested Approach:**
1. Extract WMS-specific logic from OgcSharedHandlers to WmsHandlers
2. Extract WFS-specific logic to WfsHandlers
3. Extract WMTS-specific logic to WmtsHandlers
4. Extract WCS-specific logic to WcsHandlers
5. Keep only shared/common logic in OgcSharedHandlers
6. Update all references and dependency injection
7. Add comprehensive tests for each handler
8. Update documentation

**Benefits:**
- Better separation of concerns
- Easier to maintain and extend
- Clearer code organization

---

### 9. ETL Workflow Execution Features

**Severity:** HIGH
**Category:** Feature Completion
**Component:** GeoETL

**Locations:**
- `Admin.Blazor/GeoEtl/WorkflowList.razor:197` - "TODO: Implement execution with parameter dialog"
- `Admin.Blazor/GeoEtl/WorkflowList.razor:208` - "TODO: Add confirmation dialog"
- `Admin.Blazor/GeoEtl/WorkflowDesigner.razor:339` - "TODO: Load workflow from API"
- `Enterprise/ETL/Scheduling/ScheduleExecutor.cs:328` - "TODO: Implement actual notification sending"

**Effort:** 1 week

**Suggested Approach:**
1. Implement workflow parameter input dialog
2. Add pre-execution validation
3. Add confirmation dialog with cost estimates
4. Implement workflow loading from backend API
5. Add notification integration for workflow completion

---

### 10. Service Registration Completions

**Severity:** HIGH
**Category:** Refactoring
**Component:** Core Infrastructure

**Locations:**
- `Server.Host/Extensions/ServiceCollectionExtensions.cs:237` - "TODO: Register cloud raster source providers when cloud storage is configured"
- `Server.Host/Extensions/ServiceCollectionExtensions.cs:259` - "TODO: RasterTilePreseedService has unregistered dependencies (IRasterTileCacheProvider)"
- `Server.Host/Extensions/ServiceCollectionExtensions.cs:524` - "TODO: Register remaining services as they are implemented"

**Description:**
Several services are commented out or incomplete in DI registration, potentially causing runtime errors.

**Effort:** 2-3 days

**Suggested Approach:**
1. Audit all service registrations
2. Implement missing dependencies (IRasterTileCacheProvider)
3. Register cloud raster providers conditionally based on configuration
4. Add startup validation to detect missing registrations
5. Document service registration requirements

---

### 11. Alert Administration API Completeness

**Severity:** HIGH
**Category:** Feature Completion
**Component:** Alerts

**Locations:**
- `Server.Host/Admin/AlertAdministrationEndpoints.cs:701` - "TODO: Enhance AlertHistoryStore to support full filtering"
- `Server.Host/Admin/AlertAdministrationEndpoints.cs:714` - "TODO: Check acknowledgement status"
- `Server.Host/Admin/AlertAdministrationEndpoints.cs:735` - "TODO: Add method to get alert by ID"
- `Server.Host/Admin/AlertAdministrationEndpoints.cs:760` - "TODO: Get alert by ID first"
- `Server.Host/Admin/AlertAdministrationEndpoints.cs:764` - "TODO: Get from alert" (Fingerprint)
- `Server.Host/Admin/AlertAdministrationEndpoints.cs:796` - "TODO: Get alert by ID first to extract matchers"
- `Server.Host/Admin/AlertAdministrationEndpoints.cs:800` - "TODO: Extract from alert" (Matchers)

**Effort:** 2-3 days

**Suggested Approach:**
1. Implement GetAlertById method in AlertHistoryStore
2. Add filtering support (time range, severity, status)
3. Implement acknowledgement tracking
4. Add fingerprint extraction logic
5. Implement matcher extraction from alert metadata

---

### 12. IFC Import Service Implementation

**Severity:** HIGH
**Category:** Integration
**Component:** Core Services

**Locations:**
- `Server.Core/Services/IfcImportService.cs:59` - "TODO: Implement actual Xbim.Essentials integration"
- `Server.Core/Services/IfcImportService.cs:188` - "TODO: Implement actual validation using Xbim.Essentials"
- `Server.Core/Services/IfcImportService.cs:281` - "TODO: Implement actual metadata extraction using Xbim.Essentials"

**Description:**
IFC (Industry Foundation Classes) import service is stubbed but not implemented. This prevents BIM/building data import.

**Effort:** 1-2 weeks
**Dependencies:** Xbim.Essentials NuGet package integration

**Suggested Approach:**
1. Add Xbim.Essentials NuGet package
2. Implement IFC file parsing
3. Implement geometry extraction and conversion to GeoJSON
4. Implement property/metadata extraction
5. Add validation logic for IFC schemas
6. Add support for IFC2x3 and IFC4 formats
7. Implement spatial reference system handling

---

### 13. Azure Digital Twins Integration

**Severity:** HIGH
**Category:** Integration
**Component:** IoT/Azure

**Locations:**
- `Enterprise/IoT/Azure/Events/AdtEventHandler.cs:189` - "TODO: Delete corresponding Honua feature if configured"
- `Enterprise/IoT/Azure/Events/AdtEventHandler.cs:198` - "TODO: Sync relationship to Honua if applicable"
- `Enterprise/IoT/Azure/Events/AdtEventHandler.cs:205` - "TODO: Sync relationship to Honua if applicable"
- `Enterprise/IoT/Azure/Events/AdtEventHandler.cs:212` - "TODO: Delete relationship in Honua if applicable"
- `Enterprise/IoT/Azure/Services/TwinSynchronizationService.cs:322` - "TODO: Call Honua feature update API"

**Description:**
Azure Digital Twins event handlers are not fully integrated with Honua's feature management.

**Effort:** 3-4 days

**Suggested Approach:**
1. Implement bidirectional sync between ADT twins and Honua features
2. Add relationship synchronization
3. Implement cascade delete logic
4. Add conflict resolution for concurrent updates
5. Add sync status tracking and error handling

---

### 14. Table Discovery for Data Sources

**Severity:** HIGH
**Category:** Feature Completion
**Component:** Admin/Data Sources

**Location:** `Server.Host/Admin/MetadataAdministrationEndpoints.DataSources.cs:310`

**Description:**
`// TODO: Implement actual table discovery based on provider` - Users cannot browse available tables in database connections.

**Effort:** 2-3 days

**Suggested Approach:**
1. Implement table/collection discovery for each data provider
2. Add schema introspection (columns, types, constraints)
3. Add filtering and search capabilities
4. Return metadata (row counts, size estimates)
5. Handle permissions gracefully (show only accessible tables)

---

### 15. Geoprocessing Configuration Management

**Severity:** HIGH
**Category:** Configuration
**Component:** Geoprocessing

**Locations:**
- `Enterprise/Geoprocessing/PostgresControlPlane.cs:87` - "TODO: Make configurable per tenant" (maxConcurrent)
- `Enterprise/Geoprocessing/PostgresControlPlane.cs:101` - "TODO: Make configurable per tenant" (rateLimit)
- `Enterprise/Geoprocessing/PostgresControlPlane.cs:140` - "TODO: Actual validation and normalization"
- `Enterprise/Geoprocessing/PostgresControlPlane.cs:154` - "TODO: Load from database"
- `Enterprise/Geoprocessing/PostgresControlPlane.cs:176` - "TODO: Pass from request" (ApiSurface)
- `Enterprise/Geoprocessing/PostgresControlPlane.cs:716` - "TODO: Type validation, range validation, etc."

**Effort:** 3-4 days

**Suggested Approach:**
1. Create tenant-specific configuration table
2. Implement configuration loading from database
3. Add parameter validation framework
4. Add runtime configuration updates without restart
5. Add default configurations with override capability

---

## MEDIUM PRIORITY (Severity: Medium)

### 16. SQL View Filter Translation

**Severity:** MEDIUM
**Category:** Feature Completion
**Component:** Core Data

**Locations:**
- `Server.Core/Data/SqlViewQueryBuilder.cs:67` - "TODO: Implement filter translation for SQL views"
- `Server.Core/Data/SqlViewQueryBuilder.cs:126` - "TODO: Implement filter translation for SQL views"

**Effort:** 2-3 days

**Suggested Approach:**
1. Implement WHERE clause generation from filter objects
2. Add parameter sanitization to prevent SQL injection
3. Support common operators (=, !=, <, >, LIKE, IN, etc.)
4. Add spatial filter support
5. Test with all supported database backends

---

### 17. Graph Database Optimizations

**Severity:** MEDIUM
**Category:** Performance
**Component:** Graph Database

**Locations:**
- `Server.Core/Services/GraphDatabaseService.cs:470` - "TODO: Add parameter binding when ApacheAGE library supports it"
- `Server.Core/Services/GraphDatabaseService.cs:631` - "TODO: Optimize with batch operations when supported by ApacheAGE library"
- `Server.Core/Services/GraphDatabaseService.cs:652` - "TODO: Optimize with batch operations when supported by ApacheAGE library"

**Effort:** 1 week
**Dependencies:** ApacheAGE library updates

**Suggested Approach:**
1. Monitor ApacheAGE library releases for parameter binding support
2. Implement batch insert/update operations when available
3. Add query plan analysis and optimization
4. Implement connection pooling optimization
5. Add performance benchmarks

---

### 18. MapSDK Component Features

**Severity:** MEDIUM
**Category:** Feature Completion
**Component:** MapSDK

**Locations:**
- `MapSDK/Components/FilterPanel/HonuaFilterPanel.razor:550` - "TODO: Implement polygon drawing - would require additional JS interop"
- `MapSDK/Components/Chart/HonuaChart.razor:324` - "TODO: Implement spatial filtering when geometry is available"
- `MapSDK/Components/DataGrid/HonuaDataGrid.razor.cs:486` - "TODO: Implement WFS loading"
- `MapSDK/Components/DataGrid/HonuaDataGrid.razor.cs:493` - "TODO: Implement gRPC loading"
- `MapSDK/Components/DataGrid/HonuaDataGrid.razor.cs:606` - "TODO: Implement proper spatial filtering"
- `MapSDK/Components/DataGrid/HonuaDataGrid.razor.cs:614` - "TODO: Implement attribute filtering based on filter expression"
- `MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:668` - "TODO: Show advanced filter builder dialog"
- `MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:674` - "TODO: Show filter presets dialog"
- `MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:680` - "TODO: Save current filter as preset"
- `MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:710` - "TODO: Implement proper filter application"
- `MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:847` - "TODO: Show bulk edit dialog"
- `MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:853` - "TODO: Show calculate field dialog"
- `MapSDK/Components/Bookmarks/HonuaBookmarks.razor:553` - "TODO: Show confirmation dialog"
- `MapSDK/Components/Bookmarks/HonuaBookmarks.razor:583` - "TODO: Show success message"
- `MapSDK/Components/Bookmarks/HonuaBookmarks.razor:623` - "TODO: Show folder options menu"
- `MapSDK/Components/Bookmarks/HonuaBookmarks.razor:664` - "TODO: Show file picker and import"

**Effort:** 3-4 weeks for all features

**Suggested Approach:**
Prioritize based on user demand:
1. Filter builder and presets (high user value)
2. WFS/gRPC loading (integration capability)
3. Bulk edit operations (productivity)
4. Polygon drawing (advanced features)

---

### 19. Field Mobile App Features

**Severity:** MEDIUM
**Category:** Feature Completion
**Component:** Field Mobile

**Locations:**
- `Field/Honua.Field/ViewModels/MapViewModel.cs:454` - "TODO: Store geometry type in Collection model"
- `Field/Honua.Field/ViewModels/FeatureDetailViewModel.cs:286` - "TODO: Implement actual sharing when Share API is available"
- `Field/Honua.Field/ViewModels/FeatureDetailViewModel.cs:331` - "TODO: Implement attachment viewer"
- `Field/Honua.Field/ViewModels/LoginViewModel.cs:192` - "TODO: Navigate to server settings page"
- `Field/Honua.Field/Services/SymbologyService.cs:227` - "TODO: Handle different point symbol types (circle, square, triangle, etc.)"

**Effort:** 1-2 weeks

**Suggested Approach:**
1. Add geometry type field to Collection model
2. Implement share functionality using platform-specific APIs
3. Add attachment viewer with support for images, PDFs, documents
4. Add server settings navigation
5. Expand symbology support for point markers

---

### 20. Admin UI Dialog Implementations

**Severity:** MEDIUM
**Category:** Feature Completion
**Component:** Admin Blazor

**Locations:**
- `Admin.Blazor/Components/Pages/DataSourceList.razor:300` - "TODO: Implement table browser dialog"
- `Admin.Blazor/Components/Pages/ServiceDetail.razor:707` - "TODO: Implement save to backend"
- `Admin.Blazor/Components/Pages/EsriServiceImportWizard.razor:412` - "TODO: Call actual Esri REST API to get service metadata"
- `Admin.Blazor/Components/Pages/EsriServiceImportWizard.razor:497` - "TODO: Call migration API"
- `Admin.Blazor/Components/Pages/LayerCreationWizard.razor:589` - "TODO: Call backend endpoint to fetch preview data"
- `Admin.Blazor/Components/Pages/LayerCreationWizard.razor:608` - "TODO: Replace with actual backend API call"
- `Admin.Blazor/Components/Pages/LayerCreationWizard.razor:654` - "TODO: Call backend endpoint to get filtered count"
- `Admin.Blazor/Components/Pages/LayerCreationWizard.razor:695` - "TODO: Create layer with all configurations"
- `Admin.Blazor/Components/Pages/LayerEditor.razor:368` - "TODO: Show dialog for custom CRS input"
- `Admin.Blazor/Components/Shared/WhereClauseBuilder.razor:300` - "TODO: Call backend endpoint to validate query against actual database"
- `Admin.Blazor/Components/Shared/DataSourceDialog.razor:376` - "TODO: Parse connection string back to parameters"
- `Admin.Blazor/Components/Shared/MapLibreMapPreview.razor:104` - "TODO: Implement actual layer data fetching"

**Effort:** 2-3 weeks for all dialogs

**Suggested Approach:**
Implement in priority order based on user workflow impact:
1. Layer creation wizard (core functionality)
2. Table browser (data exploration)
3. Esri service import (integration)
4. Connection string parsing (usability)
5. Query validation (user experience)

---

### 21. Alert Configuration UI Features

**Severity:** MEDIUM
**Category:** Feature Completion
**Component:** Alerts

**Locations:**
- `Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:366` - "TODO: Replace with actual API calls when backend is implemented"
- `Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:404` - "TODO: Open dialog to create new alert rule"
- `Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:410` - "TODO: Open dialog to edit alert rule"
- `Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:418` - "TODO: Call API to test alert rule"
- `Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:441` - "TODO: Call API to delete rule"
- `Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:456` - "TODO: Open dialog to create notification channel"
- `Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:462` - "TODO: Open dialog to edit notification channel"
- `Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:470` - "TODO: Call API to test notification channel"
- `Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:493` - "TODO: Call API to delete channel"
- `Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:508` - "TODO: Open dialog to show full alert details"
- `Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor:300-379` - Multiple API integration TODOs
- `Admin.Blazor/Components/Pages/Alerts/AlertHistory.razor:206` - "TODO: Replace with actual API call"
- `Admin.Blazor/Components/Pages/Alerts/AlertHistory.razor:281` - "TODO: Open dialog with full alert details"
- `Admin.Blazor/Components/Shared/AlertRuleDialog.razor:114` - "TODO: Replace with actual API call"

**Effort:** 1-2 weeks

**Suggested Approach:**
1. Implement backend API endpoints first (dependencies)
2. Create rule editor dialog with validation
3. Add notification channel management
4. Implement alert testing functionality
5. Add alert history viewer with filtering

---

### 22. ETL Data Source Extensions

**Severity:** MEDIUM
**Category:** Feature Completion
**Component:** ETL

**Locations:**
- `Enterprise/ETL/Nodes/DataSourceNodes.cs:260` - "TODO: Add support for URL download and other formats (Shapefile, GeoPackage)"
- `Enterprise/ETL/Nodes/GdalDataSourceNodes.cs:322` - "TODO: Add SharpKml NuGet package for comprehensive KML support"
- `Enterprise/ETL/Nodes/DataSinkNodes.cs:120` - "TODO: Properly parse and insert geometry using ST_GeomFromGeoJSON or ST_GeomFromText"

**Effort:** 1 week

**Suggested Approach:**
1. Add HTTP/HTTPS URL download support with authentication
2. Add Shapefile reader support via NetTopologySuite
3. Add GeoPackage support via GDAL bindings
4. Integrate SharpKml for better KML parsing
5. Improve geometry parsing with proper spatial reference handling

---

### 23. Raster Processing Features

**Severity:** MEDIUM
**Category:** Feature Completion
**Component:** Raster

**Locations:**
- `Server.Core.Raster/Kerchunk/GdalKerchunkGenerator.cs:262` - "TODO: Generate chunk byte offset references"
- `Server.Host/Wms/WmsGetMapHandlers.cs:148` - "TODO: Apply opacity from overlayContext.Opacity during rendering"

**Effort:** 3-4 days

**Suggested Approach:**
1. Implement Kerchunk metadata generation for cloud-optimized rasters
2. Add opacity support in WMS rendering pipeline
3. Test with various raster formats (GeoTIFF, COG, Zarr)

---

### 24. Metadata and Service Management

**Severity:** MEDIUM
**Category:** Data Quality
**Component:** Metadata

**Locations:**
- `Server.Host/Admin/MetadataAdministrationEndpoints.Services.cs:80` - "TODO: Get from metadata when available"
- `Server.Host/Admin/Models/ServiceDtos.cs:50` - "TODO: Add to metadata model" (CreatedAt)
- `Server.Host/Admin/Models/ServiceDtos.cs:51` - "TODO: Add to metadata model" (ModifiedAt)
- `Server.Core/Metadata/MetadataSchemaParser.cs:435` - "TODO: Implement full RBAC parsing when RbacDocument structure is defined"

**Effort:** 2-3 days

**Suggested Approach:**
1. Add CreatedAt/ModifiedAt fields to metadata schema
2. Implement automatic timestamp tracking
3. Define RBAC document structure
4. Implement RBAC metadata parsing
5. Add metadata versioning support

---

### 25. Geoprocessing Features

**Severity:** MEDIUM
**Category:** Feature Completion
**Component:** Geoprocessing

**Locations:**
- `Enterprise/Geoprocessing/GeoprocessingWorkerService.cs:161` - "TODO: Update progress in database"
- `Enterprise/Geoprocessing/TierExecutorCoordinator.cs:125` - "TODO: Implement actual health checks"
- `Enterprise/Geoprocessing/Operations/BufferOperation.cs:176` - "TODO: Implement loading from collections, URLs, etc."
- `Server.Host/Admin/GeoEtlExecutionEndpoints.cs:163` - "TODO: Add cost calculation when available"

**Effort:** 1 week

**Suggested Approach:**
1. Implement progress tracking in database with WebSocket updates
2. Add health check endpoints for executor monitoring
3. Add input source loading (collections, URLs, files)
4. Implement cost calculation based on operation complexity and data size
5. Add resource usage metrics

---

## LOW PRIORITY (Severity: Low)

### 26. Code Quality and Refactoring

**Severity:** LOW
**Category:** Code Quality

**Locations:**
- `Directory.Build.props:5` - "TODO: Re-enable after addressing pre-existing analyzer warnings in separate PR"
- `Server.Host/Extensions/HealthCheckExtensions.cs:30` - "TODO: Fix CacheConsistencyHealthCheck implementation"

**Effort:** 1-2 days

---

### 27. 3D Rendering Support

**Severity:** LOW
**Category:** Feature Enhancement
**Component:** MapSDK

**Locations:**
- `MapSDK/wwwroot/js/maplibre-interop.js:537` - "TODO: Render mesh using appropriate 3D rendering library"
- `MapSDK/wwwroot/js/maplibre-interop.js:569` - "TODO: Render point cloud"

**Effort:** 2-3 weeks
**Dependencies:** 3D rendering library selection (Three.js, Babylon.js, etc.)

---

### 28. Cloud Provider Integrations

**Severity:** LOW
**Category:** Integration
**Component:** Infrastructure

**Locations:**
- `Server.Intake/Services/RegistryProvisioner.cs:439` - "TODO: Update to use the latest Azure.ResourceManager.ContainerRegistry API"
- `Server.Intake/Services/RegistryProvisioner.cs:523` - "TODO: Install Google.Apis.Iam.v1 NuGet package or use Google.Cloud.Iam.Admin.V1"
- `Server.Host/Hosting/HonuaHostConfigurationExtensions.cs:3` - "TODO: GitOps feature not yet implemented"
- `Server.Host/Hosting/HonuaHostConfigurationExtensions.cs:123` - "GitOps (conditional) - TODO: Not yet implemented"

**Effort:** 1 week

---

### 29. AI/ML Features

**Severity:** LOW
**Category:** Feature Enhancement
**Component:** AI/CLI

**Locations:**
- `Cli/Commands/DeployPlanCommand.cs:97` - "TODO: Integrate ArchitectureConsultingAgent (requires Kernel, not ILlmProvider)"
- `Cli.AI/Services/Agents/HonuaMagenticCoordinator.cs:752` - "TODO: Implement HonuaGroupChatManager that uses LLM to dynamically select agents"
- `Cli.AI/Services/AI/StructuredLlmOutput.cs:231` - "TODO: Integrate with JSON schema validation library (e.g., JsonSchema.Net)"
- `Cli.AI/EncryptedFileSecretsManager.cs:476` - "TODO: In production, prompt user for actual passphrase via secure input"

**Effort:** 2-3 weeks

---

### 30. Documentation and Examples

**Severity:** LOW
**Category:** Documentation
**Component:** Various

**Locations:**
- `docs/integrations/power-bi-embedding.md:79` - "TODO: Add your authorization logic here"
- `Enterprise/ENTERPRISE_FEATURES.md:428` - "Audit Logging - Comprehensive audit trail (TODO)"
- `Enterprise/MULTITENANT_SAAS_README.md:267` - "Email notifications (TODO: add email service)"
- `Server.Core/Import/Validation/FeatureSchemaValidator.cs:622` - "TODO: Could add more detailed GeoJSON validation here"
- `Admin.Blazor/Components/Pages/Home.razor:79` - "TODO: Add recent activity feed after audit log integration"

**Effort:** Ongoing

---

## Implementation Roadmap

### Sprint 1 (2 weeks) - CRITICAL Security Issues
- [ ] Implement tenant isolation (TODO #1)
- [ ] Implement user identity extraction (TODO #2)
- [ ] Complete admin authorization (TODO #3)

### Sprint 2 (2 weeks) - CRITICAL Data & Security
- [ ] Implement hard delete for all data stores (TODO #4)
- [ ] Complete KMS encryption integration (TODO #5)
- [ ] Implement alert publishing (TODO #6)

### Sprint 3 (2 weeks) - HIGH Priority Features
- [ ] Implement connection testing (TODO #7)
- [ ] Complete alert administration API (TODO #11)
- [ ] Implement IFC import service (TODO #12)

### Sprint 4 (3 weeks) - OGC Refactoring
- [ ] Complete OGC Phase 2 refactoring (TODO #8)
- [ ] Update all references and tests

### Sprint 5+ (Ongoing) - MEDIUM/LOW Priority
- Address remaining medium and low priority items based on user feedback and business priorities

---

## Top 10 Critical/High Priority Items - Detailed Tasks

### 1. CRITICAL: Tenant Isolation Implementation

**File:** Multiple controllers and components
**Lines:** Various (see section above)
**Priority:** P0 - CRITICAL SECURITY ISSUE

**Current State:**
```csharp
var tenantId = "default"; // TODO: Extract tenant ID from claims or context
```

**Estimated Effort:** 2-3 days (16-24 hours)

**Detailed Implementation Steps:**

1. **Create ITenantContext Service (2 hours)**
   ```csharp
   public interface ITenantContext
   {
       Guid TenantId { get; }
       string TenantName { get; }
       bool IsMultiTenant { get; }
   }

   public class TenantContext : ITenantContext
   {
       public TenantContext(IHttpContextAccessor httpContextAccessor)
       {
           // Extract from claims: "tenant_id"
           var tenantClaim = httpContextAccessor.HttpContext?.User
               ?.FindFirst("tenant_id");
           TenantId = tenantClaim != null
               ? Guid.Parse(tenantClaim.Value)
               : throw new UnauthorizedAccessException("Tenant ID not found in claims");
       }
   }
   ```

2. **Register in DI Container (1 hour)**
   - Add scoped registration
   - Add middleware to validate tenant claims
   - Configure for development vs. production

3. **Update All Controllers (8-12 hours)**
   - Inject ITenantContext in 20+ affected controllers
   - Replace all hardcoded tenant IDs
   - Update method signatures as needed
   - Add validation

4. **Update Blazor Components (4-6 hours)**
   - Create Blazor service wrapper
   - Update 7 affected Razor components
   - Test rendering with tenant context

5. **Add Integration Tests (2-4 hours)**
   - Test tenant isolation in queries
   - Test cross-tenant access denial
   - Test JWT claim extraction

6. **Documentation (1 hour)**
   - Update authentication docs
   - Add multi-tenancy configuration guide

**Testing Checklist:**
- [ ] Verify tenant extracted from JWT claims correctly
- [ ] Verify queries filtered by tenant ID
- [ ] Verify cross-tenant access blocked
- [ ] Verify works with single-tenant mode
- [ ] Load test with multiple concurrent tenants

**Risk Assessment:**
- **High Risk:** Breaking change for existing deployments
- **Mitigation:** Add feature flag for gradual rollout

---

### 2. CRITICAL: User Identity Extraction

**File:** Multiple endpoints
**Priority:** P0 - CRITICAL for audit compliance

**Estimated Effort:** 1-2 days (8-16 hours)

**Detailed Implementation Steps:**

1. **Create IUserContext Service (2 hours)**
   ```csharp
   public interface IUserContext
   {
       Guid UserId { get; }
       string UserName { get; }
       string Email { get; }
       Guid SessionId { get; }
   }
   ```

2. **Implement Extraction from Claims (3 hours)**
   - Extract from "sub", "email", "name" claims
   - Generate or extract session ID
   - Handle missing claims gracefully

3. **Update All Creation/Update Operations (4-8 hours)**
   - Replace `CreatedBy = "admin"` with actual user
   - Replace `SessionId = Guid.Empty` with session tracking
   - Add LastModifiedBy tracking

4. **Add Audit Trail Integration (2-3 hours)**
   - Log all user actions
   - Store session information
   - Add correlation IDs

**Testing Checklist:**
- [ ] Verify user ID extracted correctly
- [ ] Verify audit logs contain real user info
- [ ] Verify session tracking works
- [ ] Verify anonymous access handled

---

### 3. CRITICAL: Admin Authorization Completion

**File:** Multiple admin endpoints
**Priority:** P0 - CRITICAL SECURITY

**Estimated Effort:** 3-4 days (24-32 hours)

**Detailed Implementation Steps:**

1. **Audit All Admin Endpoints (4 hours)**
   - List all endpoints with RequireAdministrator
   - Identify missing authorization
   - Document authorization requirements

2. **Implement Role-Based Authorization (8 hours)**
   - Define admin roles (SuperAdmin, Admin, Operator)
   - Add role claims to JWT
   - Implement policy-based authorization
   - Add permission system for granular control

3. **Add API Key Authentication (6 hours)**
   - For service-to-service calls
   - Implement API key validation
   - Add rate limiting per API key

4. **Security Testing (6-8 hours)**
   - Penetration testing
   - Authorization bypass testing
   - Rate limiting testing
   - Token validation testing

5. **Add Security Monitoring (4 hours)**
   - Log all admin operations
   - Alert on suspicious activity
   - Add audit trail

**Testing Checklist:**
- [ ] Non-admin users blocked from admin endpoints
- [ ] Role-based access works correctly
- [ ] API key authentication works
- [ ] Rate limiting prevents abuse
- [ ] Audit logs capture all admin actions

---

### 4. CRITICAL: Hard Delete Implementation

**Files:** 4 enterprise data store providers
**Priority:** P0 - GDPR compliance

**Estimated Effort:** 8-12 days total (2-3 days per provider)

**MongoDB Implementation (2-3 days):**

1. **Add HardDeleteAsync Method (4 hours)**
   ```csharp
   public async Task<bool> HardDeleteAsync(string collectionName, Guid featureId, Guid tenantId)
   {
       var collection = GetCollection(collectionName, tenantId);
       var filter = Builders<BsonDocument>.Filter.Eq("_id", featureId.ToString());

       // Delete related documents first (cascade)
       await DeleteRelatedDocumentsAsync(collectionName, featureId, tenantId);

       var result = await collection.DeleteOneAsync(filter);
       return result.DeletedCount > 0;
   }
   ```

2. **Implement Cascade Delete (6 hours)**
   - Identify all related collections
   - Delete in correct order to maintain integrity
   - Handle circular references

3. **Add Audit Logging (2 hours)**
   - Log all hard deletes with reason
   - Store deleted data in archive collection
   - Add restoration capability

4. **Add Administrative API (4 hours)**
   - Create GDPR deletion request endpoint
   - Add approval workflow
   - Add batch deletion support

5. **Testing (4-6 hours)**
   - Unit tests for delete logic
   - Integration tests for cascade
   - Performance tests for large deletions

**Repeat for BigQuery, Elasticsearch, CosmosDB**

**Testing Checklist:**
- [ ] Hard delete removes data completely
- [ ] Cascade deletes work correctly
- [ ] Audit trail captured
- [ ] Backup/archive created
- [ ] Restoration possible if needed
- [ ] GDPR compliance verified

---

### 5. CRITICAL: KMS Encryption Integration

**Files:** Connection encryption configuration
**Priority:** P0 - SECURITY

**Estimated Effort:** 4-5 days (32-40 hours)

**Detailed Implementation Steps:**

1. **AWS KMS Integration (12-16 hours)**
   - Add AWS.KeyManagementService NuGet package
   - Implement encryption service
   - Implement decryption service
   - Add key rotation support
   - Migrate existing connection strings

2. **GCP KMS Integration (12-16 hours)**
   - Add Google.Cloud.Kms.V1 NuGet package
   - Implement encryption service
   - Implement decryption service
   - Add key rotation support
   - Migrate existing connection strings

3. **Create Abstraction Layer (4-6 hours)**
   ```csharp
   public interface IConnectionStringEncryptionService
   {
       Task<string> EncryptAsync(string plainText);
       Task<string> DecryptAsync(string cipherText);
       Task RotateKeyAsync();
   }
   ```

4. **Add Development Fallback (2 hours)**
   - Use local encryption for dev environments
   - Add configuration for KMS vs. local

5. **Migration Tool (4-6 hours)**
   - Tool to encrypt existing connection strings
   - Backup before migration
   - Rollback capability

**Testing Checklist:**
- [ ] AWS KMS encryption/decryption works
- [ ] GCP KMS encryption/decryption works
- [ ] Key rotation works without downtime
- [ ] Development mode works without cloud KMS
- [ ] Migration tool tested

---

### 6. HIGH: Alert Publishing Implementation

**Files:** Alert administration endpoints
**Priority:** P1 - HIGH

**Estimated Effort:** 3-4 days (24-32 hours)

**Implementation Plan:**

1. **Create AlertPublisher Service (8 hours)**
   ```csharp
   public interface IAlertPublisher
   {
       Task PublishAsync(Alert alert, IEnumerable<NotificationChannel> channels);
   }
   ```

2. **Implement Channel Providers (12-16 hours)**
   - Email (SendGrid/SMTP)
   - Webhook (HTTP POST)
   - SMS (Twilio)
   - Slack
   - Microsoft Teams
   - PagerDuty

3. **Add Retry Logic (4 hours)**
   - Exponential backoff
   - Dead letter queue
   - Max retry configuration

4. **Add Deduplication (3 hours)**
   - Fingerprint-based deduplication
   - Time window configuration
   - Alert grouping

5. **Add Delivery Tracking (3 hours)**
   - Store delivery status
   - Track failures and retries
   - Delivery confirmation

**Testing Checklist:**
- [ ] Alerts sent to all channel types
- [ ] Retry logic works correctly
- [ ] Deduplication prevents spam
- [ ] Delivery status tracked
- [ ] Failed deliveries retried

---

### 7. HIGH: Connection Testing

**Files:** Data source administration
**Priority:** P1 - HIGH

**Estimated Effort:** 2-3 days (16-24 hours)

**Implementation Plan:**

1. **Add TestConnectionAsync to Providers (12 hours)**
   - PostgreSQL connection test
   - MongoDB connection test
   - SQL Server connection test
   - MySQL connection test
   - Elasticsearch connection test
   - And all other providers

2. **Implement Timeout Handling (2 hours)**
   - 30-second default timeout
   - Configurable timeout
   - Graceful timeout handling

3. **Add Detailed Error Messages (4 hours)**
   - Network errors
   - Authentication errors
   - Permission errors
   - Provider-specific errors

4. **Create Admin API Endpoint (2 hours)**
   ```csharp
   [HttpPost("test-connection")]
   public async Task<IActionResult> TestConnection([FromBody] DataSourceDto dataSource)
   {
       var result = await _dataSourceService.TestConnectionAsync(dataSource);
       return Ok(result);
   }
   ```

5. **Add UI Integration (4-6 hours)**
   - Test button in UI
   - Loading indicator
   - Success/failure feedback
   - Error message display

**Testing Checklist:**
- [ ] All providers implement test connection
- [ ] Timeout works correctly
- [ ] Error messages helpful
- [ ] UI feedback clear
- [ ] Test doesn't modify data

---

### 8. HIGH: OGC Phase 2 Refactoring

**Files:** All OGC handler interfaces and implementations
**Priority:** P1 - Architectural improvement

**Estimated Effort:** 2-3 weeks (80-120 hours)

**Implementation Plan:**

1. **Extract WMS Logic (20-30 hours)**
   - Move GetCapabilities to WmsHandlers
   - Move GetMap to WmsHandlers
   - Move GetFeatureInfo to WmsHandlers
   - Remove from OgcSharedHandlers

2. **Extract WFS Logic (20-30 hours)**
   - Move GetCapabilities to WfsHandlers
   - Move GetFeature to WfsHandlers
   - Move DescribeFeatureType to WfsHandlers
   - Move Transaction to WfsHandlers

3. **Extract WMTS Logic (15-20 hours)**
   - Move GetCapabilities to WmtsHandlers
   - Move GetTile to WmtsHandlers
   - Move GetFeatureInfo to WmtsHandlers

4. **Extract WCS Logic (15-20 hours)**
   - Move GetCapabilities to WcsHandlers
   - Move DescribeCoverage to WcsHandlers
   - Move GetCoverage to WcsHandlers

5. **Update Shared Logic (5-8 hours)**
   - Keep only truly shared code
   - Create shared utility methods
   - Document shared components

6. **Update DI Registration (2 hours)**
   - Update service registration
   - Verify dependencies

7. **Comprehensive Testing (10-15 hours)**
   - Unit tests for each handler
   - Integration tests for OGC compliance
   - Performance regression testing

**Testing Checklist:**
- [ ] All OGC services still work
- [ ] OGC compliance tests pass
- [ ] Performance not degraded
- [ ] Code better organized
- [ ] Documentation updated

---

### 9. HIGH: ETL Workflow Features

**Files:** GeoETL Blazor components and backend
**Priority:** P1 - Feature completion

**Estimated Effort:** 1 week (40 hours)

**Implementation Plan:**

1. **Workflow Parameter Dialog (8 hours)**
   - Dynamic form based on workflow parameters
   - Validation
   - Default values
   - Help text

2. **Workflow Loading from API (6 hours)**
   - API endpoint for workflow retrieval
   - Workflow version management
   - Caching

3. **Confirmation Dialog (4 hours)**
   - Show cost estimate
   - Show execution time estimate
   - Show affected resources
   - Require explicit confirmation

4. **Notification Integration (8 hours)**
   - Send notification on completion
   - Send notification on failure
   - Include execution summary
   - Include error details

5. **Pre-execution Validation (6 hours)**
   - Validate parameters
   - Check resource availability
   - Estimate cost
   - Check permissions

6. **Progress Tracking (8 hours)**
   - Real-time progress updates
   - WebSocket integration
   - Progress bar UI
   - Cancellation support

**Testing Checklist:**
- [ ] Parameters collected correctly
- [ ] Validation works
- [ ] Confirmation shown
- [ ] Notifications sent
- [ ] Progress tracked
- [ ] Cancellation works

---

### 10. HIGH: Service Registration Completions

**Files:** ServiceCollectionExtensions
**Priority:** P1 - Infrastructure stability

**Estimated Effort:** 2-3 days (16-24 hours)

**Implementation Plan:**

1. **Implement IRasterTileCacheProvider (6-8 hours)**
   - Define interface
   - Implement in-memory provider
   - Implement Redis provider
   - Implement file system provider

2. **Register RasterTilePreseedService (2 hours)**
   - Add dependency on IRasterTileCacheProvider
   - Configure preseeding options
   - Add background service registration

3. **Cloud Raster Providers (6-8 hours)**
   - S3 raster source provider
   - Azure Blob raster source provider
   - GCS raster source provider
   - Conditional registration based on config

4. **Complete Remaining Services (4-6 hours)**
   - Identify all TODOs in service registration
   - Implement missing services
   - Add to DI container

5. **Add Startup Validation (2 hours)**
   - Validate all required services registered
   - Fail fast on missing dependencies
   - Clear error messages

6. **Documentation (2 hours)**
   - Document service registration requirements
   - Document configuration options
   - Add troubleshooting guide

**Testing Checklist:**
- [ ] All services resolve correctly
- [ ] No missing dependencies at startup
- [ ] Configuration validated
- [ ] Cloud providers work
- [ ] Cache providers work

---

## Maintenance and Prevention

### Preventing New Technical Debt

1. **Pre-commit Hooks**
   - Warn on new TODO comments
   - Require issue link for TODOs

2. **Code Review Guidelines**
   - TODOs require justification
   - Security TODOs require immediate issue creation
   - Feature TODOs require backlog item

3. **Automated Monitoring**
   - Track TODO count in CI/CD
   - Alert on critical TODO additions
   - Generate monthly reports

4. **Regular Grooming**
   - Monthly technical debt review
   - Quarterly cleanup sprints
   - Annual architecture review

### TODO Comment Standards

```csharp
// GOOD: Links to issue and provides context
// TODO(#1234): Extract tenant from claims after auth integration is complete

// BAD: Vague, no tracking
// TODO: Fix this later
```

---

## Metrics and KPIs

Track these metrics monthly:

- **Total TODO Count:** Current: 167, Target: <50
- **Critical TODOs:** Current: 15, Target: 0
- **Average TODO Age:** Track creation date
- **TODO Resolution Rate:** TODOs fixed per sprint
- **New TODO Rate:** TODOs added per sprint

---

## References

- GitHub Issue Templates: `.github/ISSUE_TEMPLATE/todo-*.md`
- Security Guidelines: `docs/security/guidelines.md`
- Architecture Decision Records: `docs/adr/`
- Contributing Guidelines: `CONTRIBUTING.md`

---

**Document Owner:** Engineering Team
**Review Cadence:** Monthly
**Next Review:** 2025-12-10
