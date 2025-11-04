# WFS Query Parameter Parsing Analysis - Document Index

This directory contains a comprehensive analysis of how the HonuaIO WFS (Web Feature Service) implementation handles query parameter parsing.

## Documents

### 1. WFS_ANALYSIS_SUMMARY.txt
**Quick reference guide (169 lines)**
- Executive summary of findings
- 10 key findings overview
- File reference guide
- Design strengths assessment
- Statistics and compliance notes

**Best for:** Quick lookup, high-level overview

### 2. WFS_QUERY_PARAMETER_ANALYSIS.md
**Comprehensive technical report (672 lines, 23 KB)**
- Complete architecture overview
- Detailed parameter documentation for each parameter:
  - count/maxFeatures (pagination)
  - startIndex (offset)
  - bbox (spatial filtering)
  - srsName (CRS resolution)
  - resultType (hits vs results)
  - outputFormat (response format)
  - typeNames (layer selection)
  - filter/cql_filter (attribute filtering)
  - valueReference (property selection)
  - storedQuery_Id (stored queries)
  - expiry (lock duration)
- Supported query parameters by operation
- Validation rules pipeline
- Error response format (OGC ExceptionReport)
- Advanced query features
- Data flow diagram
- Security considerations
- File locations reference
- Testing examples
- Implementation details summary

**Best for:** In-depth understanding of parameter parsing, validation, and error handling

### 3. WFS_CODE_SNIPPETS.md
**Key code examples (710 lines, 22 KB)**
Contains actual code snippets from:
1. Core parsing functions (QueryParsingHelpers.cs)
   - GetQueryValue
   - ParseBoundingBox with CRS
   - ParsePositiveInt

2. WFS-specific parsing (WfsHelpers.cs)
   - ParseInt wrapper
   - EnforceCountLimit (3-level enforcement)
   - ParseBoundingBox (WFS variant)
   - ParseResultType
   - ParseLockDuration
   - TryNormalizeOutputFormat
   - BuildFilterAsync
   - ResolveLayerContextAsync
   - ToUrn (CRS conversion)

3. GetFeature query building (WfsGetFeatureHandlers.cs)
   - BuildFeatureQueryExecutionAsync
   - ExecuteFeatureQueryAsync

4. Exception handling (OgcExceptionHelper.cs)
   - CreateWfsException
   - MapExecutionError

5. XML filter parsing (XmlFilterParser.cs)
   - Parse with XXE protection

6. Data structures (IDataStoreProvider.cs, WfsSharedTypes.cs)
   - FeatureQuery record
   - BoundingBox record
   - Constants

**Best for:** Copy-paste reference, understanding implementation details

## How to Use This Analysis

### For Understanding Query Parameter Parsing:
1. Start with WFS_ANALYSIS_SUMMARY.txt (2-3 min read)
2. For specific parameter, check WFS_QUERY_PARAMETER_ANALYSIS.md section 2.x
3. See code example in WFS_CODE_SNIPPETS.md

### For Implementation/Maintenance:
1. Use WFS_CODE_SNIPPETS.md as direct code reference
2. Cross-reference to files listed in each snippet
3. Understand validation rules from WFS_QUERY_PARAMETER_ANALYSIS.md section 4

### For Security Review:
- See WFS_QUERY_PARAMETER_ANALYSIS.md section 8 (Security Considerations)
- Check XXE protection in WFS_CODE_SNIPPETS.md section 5
- Review validation pipeline in section 4.1

### For Adding New Features:
- Understand data flow from WFS_QUERY_PARAMETER_ANALYSIS.md section 7
- Follow pattern in WFS_CODE_SNIPPETS.md section 3 (BuildFeatureQueryExecutionAsync)
- Update validation in section 4 of comprehensive report

## Key Findings at a Glance

### Parameters Parsed (10+)
- count (pagination, max 5000)
- startIndex (offset)
- bbox (spatial, 2D/3D with CRS)
- srsName (coordinate system)
- resultType (hits or results)
- outputFormat (GeoJSON, GML, CSV, Shapefile)
- typeNames (required layer)
- filter/cql_filter (attribute filtering)
- valueReference (property selection)
- expiry (lock duration)

### Validation Highlights
- 3-level count enforcement (requested < layer < service < 5000)
- Bounding box: 4 or 6 values, min < max validation
- CRS: normalized to URN format
- Filter: XXE-protected XML or CQL parsing
- Format: whitelist validation, case-insensitive

### Error Response
- OGC-compliant OWS ExceptionReport XML
- HTTP 400 Bad Request
- Exception codes: InvalidParameterValue, MissingParameterValue, etc.
- Locator attribute indicates problem parameter

### Security
- XXE prevention in XML filters
- DoS prevention (5000 record limit)
- SQL injection prevention (parameterized queries)
- Authorization checks (DataPublisher role)
- Type-safe parameter representation

## File Locations Quick Reference

| Component | Path |
|-----------|------|
| Main Router | /src/Honua.Server.Host/Wfs/WfsHandlers.cs |
| Query Handler | /src/Honua.Server.Host/Wfs/WfsGetFeatureHandlers.cs |
| Parameter Parsing | /src/Honua.Server.Host/Wfs/WfsHelpers.cs |
| Generic Parsing | /src/Honua.Server.Host/Utilities/QueryParsingHelpers.cs |
| Error Handling | /src/Honua.Server.Host/Ogc/OgcExceptionHelper.cs |
| XML Filtering | /src/Honua.Server.Host/Wfs/Filters/XmlFilterParser.cs |
| CQL Filtering | /src/Honua.Server.Core/Query/Filter/CqlFilterParser.cs |
| Constants | /src/Honua.Server.Host/Wfs/WfsSharedTypes.cs |
| Data Types | /src/Honua.Server.Core/Data/IDataStoreProvider.cs |
| Tests | /tests/Honua.Server.Core.Tests/Hosting/WfsEndpointTests.cs |

## Standards Compliance

- WFS 2.0 (OGC 09-025r2)
- OWS Common 1.1 (exception format)
- GML 3.2 (geometry encoding)
- OGC Filter Encoding 2.0 (XML filters)
- Common Query Language (CQL/ECQL text filters)

## Document Statistics

| Metric | Value |
|--------|-------|
| Total lines analyzed | 1,551 |
| Files analyzed | 14+ |
| Parameters documented | 10+ |
| Parsing functions | 20+ |
| Code snippets | 30+ |
| Error codes | 5+ |
| Supported formats | 4 |

---

**Generated:** October 2024
**Analysis Scope:** HonuaIO WFS Query Parameter Parsing
**Coverage:** Complete GetFeature parameter handling

For questions about specific parameters or validation rules, refer to the comprehensive analysis document.
