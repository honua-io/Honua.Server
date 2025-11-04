# OGC API Features Query Parsing Analysis - Document Index

This comprehensive analysis covers how the Honua Server implements OGC API Features query parameter parsing, validation, and error handling.

## Document Overview

### 1. OGC_QUERY_PARSING_ANALYSIS.md (Main Reference)
**Size:** ~34 KB, 1,138 lines
**Purpose:** Complete technical deep-dive into query parameter parsing

**Sections:**
- Executive Summary - Architecture overview
- Query Parameter Parsing Architecture - Main entry point and whitelist
- Limit and Offset Parsing - Pagination validation (sections 2)
- Bounding Box Parsing - Spatial filter validation (section 3)
- CRS Resolution - Coordinate system handling (section 4)
- Result Type Determination - hits vs results (section 5)
- Property Filtering - Column selection (section 6)
- Sorting/Order By - Multi-field sorting (section 7)
- Temporal Filtering - DateTime range parsing (section 8)
- Response Format Parsing - Format negotiation (section 9)
- Validation Rules and Error Responses - OGC Problem Details (section 10)
- Advanced Features - CQL filters, ID filtering (section 11)
- Integration Points - Data model and handlers (section 12)
- Code File Locations - Quick file reference (section 13)
- Key Takeaways - Summary of features (section 14)

**Best for:** 
- Understanding the complete architecture
- Deep technical analysis of validation logic
- Code snippets for each parameter type
- Error response format and exception types

---

### 2. OGC_QUERY_PARSING_QUICK_REFERENCE.md (Cheat Sheet)
**Size:** ~7 KB, 207 lines
**Purpose:** Quick lookup reference for developers

**Sections:**
- Core Entry Point - Main function location
- Query Parameters Whitelist - All allowed parameters
- Parameter Parsing Summary - Validation rules table
- Error Responses - JSON format and exception URIs
- Validation Helper Functions - Functions and their locations
- Output Data Model - FeatureQuery structure
- Integration with Handlers - How parsing fits in
- Key Implementation Details - Clamping logic, normalization
- File References - Quick path lookups

**Best for:**
- Quick parameter lookup
- Validation rules at a glance
- Error codes and formats
- Finding specific functions
- Integration patterns

---

### 3. OGC_QUERY_PARSING_CODE_EXAMPLES.md (Practical Guide)
**Size:** ~13 KB, 564 lines
**Purpose:** Real-world request/response examples and code patterns

**Sections:**
- Query Parameter Validation Examples - Valid/invalid requests
- CRS Resolution Examples - Different resolution scenarios
- Temporal Filtering Examples - DateTime formats and validation
- Result Type Examples - hits vs results responses
- Format Parameter Examples - Format negotiation
- Property Filtering Examples - Column selection
- SortBy Examples - Single and multi-field sorting
- Filter (CQL) Examples - Text and JSON-based filters
- Multi-Collection Search Example - Complex searches
- Error Handling Pattern - Complete code walkthrough

**Best for:**
- Testing query parameter combinations
- Understanding error responses
- API client implementation
- Validation test cases
- Integration examples

---

## Quick Navigation

### By Use Case

**I need to understand limit/offset parsing:**
- Quick ref: OGC_QUERY_PARSING_QUICK_REFERENCE.md section "Limit & Offset"
- Details: OGC_QUERY_PARSING_ANALYSIS.md section 2
- Examples: OGC_QUERY_PARSING_CODE_EXAMPLES.md "Example 2: Limit Parameter Validation"

**I need to implement bbox validation:**
- Quick ref: OGC_QUERY_PARSING_QUICK_REFERENCE.md section "Bounding Box"
- Details: OGC_QUERY_PARSING_ANALYSIS.md section 3
- Examples: OGC_QUERY_PARSING_CODE_EXAMPLES.md "Example 4: Bounding Box Validation"
- Code location: QueryParsingHelpers.cs lines 49-134

**I need to understand CRS handling:**
- Quick ref: OGC_QUERY_PARSING_QUICK_REFERENCE.md section "CRS Resolution"
- Details: OGC_QUERY_PARSING_ANALYSIS.md section 4
- Examples: OGC_QUERY_PARSING_CODE_EXAMPLES.md section 2
- Code locations:
  - ResolveContentCrs: OgcSharedHandlers.cs:650-669
  - ResolveAcceptCrs: OgcSharedHandlers.cs:587-649
  - ResolveCrs: QueryParsingHelpers.cs:218-258

**I need to understand error responses:**
- Quick ref: OGC_QUERY_PARSING_QUICK_REFERENCE.md section "Error Responses"
- Details: OGC_QUERY_PARSING_ANALYSIS.md section 10
- Examples: OGC_QUERY_PARSING_CODE_EXAMPLES.md section 1
- Code location: OgcProblemDetails.cs

**I need to implement multi-field sorting:**
- Quick ref: OGC_QUERY_PARSING_QUICK_REFERENCE.md section "SortBy"
- Details: OGC_QUERY_PARSING_ANALYSIS.md section 7
- Examples: OGC_QUERY_PARSING_CODE_EXAMPLES.md "Example 2: Multi-Field Sorting"
- Code location: OgcSharedHandlers.cs:379-467

**I need to understand format negotiation:**
- Quick ref: OGC_QUERY_PARSING_QUICK_REFERENCE.md section "Format (f parameter)"
- Details: OGC_QUERY_PARSING_ANALYSIS.md section 9
- Examples: OGC_QUERY_PARSING_CODE_EXAMPLES.md section 5
- Code locations:
  - ResolveResponseFormat: OgcSharedHandlers.cs:513-563
  - ParseFormat: OgcSharedHandlers.cs:469-495

**I need to implement CQL filtering:**
- Details: OGC_QUERY_PARSING_ANALYSIS.md section 11 "Filter (CQL) Parsing"
- Examples: OGC_QUERY_PARSING_CODE_EXAMPLES.md section 8
- Code location: OgcSharedHandlers.cs:174-224

---

### By File

**OgcSharedHandlers.cs**
- Main parsing: ParseItemsQuery() - lines 80-316
- Format resolution: ResolveResponseFormat() - lines 513-563
- CRS resolution: ResolveContentCrs() - lines 650-669
- Bbox parsing: ParseBoundingBox() - lines 745-764
- Temporal parsing: ParseTemporal() - lines 766-777
- ResultType parsing: ParseResultType() - lines 779-792
- Sort order parsing: ParseSortOrders() - lines 379-467
- Error creation: CreateValidationProblem() - lines 3000-3011
- More CRS: ResolveAcceptCrs() - lines 587-649

**QueryParsingHelpers.cs**
- Limit/offset: ParsePositiveInt() - lines 136-184
- Bbox: ParseBoundingBox() - lines 49-104
- Bbox with CRS: ParseBoundingBoxWithCrs() - lines 106-134
- Temporal: ParseTemporalRange() - lines 303-335
- CRS: ResolveCrs() - lines 218-258
- Boolean: ParseBoolean() - lines 260-291

**OgcProblemDetails.cs**
- Exception types: ExceptionTypes class - lines 15-101
- Validation problems: CreateValidationProblem() - lines 103-119
- Not found problems: CreateNotFoundProblem() - lines 121-132
- CRS problems: CreateInvalidCrsProblem() - lines 243-259
- Bbox problems: CreateInvalidBboxProblem() - lines 261-277

**OgcFeaturesHandlers.cs**
- Collection items: ExecuteCollectionItemsAsync() - lines 458-847
- Search: GetSearch() / PostSearch() - lines 242-424

---

## Core Concepts

### Query Parameter Validation Pipeline

```
HTTP Request
    ↓
ParseItemsQuery()
    ├─ Whitelist validation (unknown parameters rejected)
    ├─ Limit parsing (positive int, clamped)
    ├─ Offset parsing (non-negative int)
    ├─ Bbox parsing (4-6 numeric values, min < max)
    ├─ DateTime parsing (ISO 8601 ranges)
    ├─ ResultType parsing (results/hits)
    ├─ Properties parsing (comma-separated list)
    ├─ CRS resolution (Accept-Crs header + crs parameter)
    ├─ Filter parsing (CQL2 text/json)
    ├─ SortBy parsing (multi-field with directions)
    └─ Format resolution (f parameter + Accept header)
    ↓
FeatureQuery object (if all valid)
    ↓
Repository.QueryAsync()
    ↓
Response with Content-Crs header
OR
Error IResult (400/406/500 with OGC Problem Details)
```

### Validation Philosophy

1. **Whitelist Approach** - Only known parameters accepted
2. **Type Validation** - Numeric, ISO 8601, enum values
3. **Range Validation** - Min/max constraints, clamping
4. **Logical Validation** - min < max, non-empty lists
5. **Schema Validation** - Fields must exist, geometry excluded from sorts
6. **OGC Compliance** - Problem Details with proper exception URIs

---

## Key Statistics

| Metric | Value |
|--------|-------|
| Main parsing function | OgcSharedHandlers.ParseItemsQuery() |
| Lines of main logic | ~240 lines |
| Allowed query parameters | 15 parameters |
| Supported output formats | 12 formats |
| Helper validation functions | 8 functions |
| OGC exception types | 14 types |
| Default page size | 10 records |
| Supported CQL versions | 2 (cql-text, cql2-json) |

---

## Implementation Patterns

### Early Return on Error
```csharp
if (error is not null)
{
    return (default!, string.Empty, false, error);
}
```

### Clamping Values
```csharp
var effectiveLimit = limitValue.HasValue
    ? Math.Clamp(limitValue.Value, 1, maxAllowed)
    : defaultPageSize;
```

### CRS Normalization
```csharp
var normalized = CrsHelper.NormalizeIdentifier(crsToken);
```

### Filter Combination
```csharp
combinedFilter = CombineFilters(combinedFilter, idsFilter);
```

---

## Files Included in This Analysis

1. `/home/mike/projects/HonuaIO/OGC_QUERY_PARSING_ANALYSIS.md` - Main reference (34 KB)
2. `/home/mike/projects/HonuaIO/OGC_QUERY_PARSING_QUICK_REFERENCE.md` - Quick lookup (7 KB)
3. `/home/mike/projects/HonuaIO/OGC_QUERY_PARSING_CODE_EXAMPLES.md` - Examples (13 KB)
4. `/home/mike/projects/HonuaIO/OGC_ANALYSIS_INDEX.md` - This file

**Total:** ~54 KB of documentation
**Total lines:** ~1,900 lines of content

---

## Source Code Files Referenced

All analysis based on these source files:

1. `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs`
2. `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Utilities/QueryParsingHelpers.cs`
3. `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcProblemDetails.cs`
4. `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.cs`
5. `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcTypes.cs`
6. `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcHelpers.cs`

---

## How to Use These Documents

### For New Developers
1. Start with OGC_QUERY_PARSING_QUICK_REFERENCE.md for overview
2. Read OGC_QUERY_PARSING_ANALYSIS.md sections 1-5 for core concepts
3. Study OGC_QUERY_PARSING_CODE_EXAMPLES.md for practical patterns
4. Reference specific sections as needed for implementation

### For Feature Implementation
1. Find your parameter in OGC_QUERY_PARSING_QUICK_REFERENCE.md
2. Read detailed validation rules in OGC_QUERY_PARSING_ANALYSIS.md
3. Study code examples in OGC_QUERY_PARSING_CODE_EXAMPLES.md
4. Implement validation following the patterns

### For Debugging
1. Identify the parameter causing issues
2. Check validation rules in OGC_QUERY_PARSING_QUICK_REFERENCE.md
3. Look at error format in OGC_QUERY_PARSING_ANALYSIS.md section 10
4. Check code examples for similar issues

### For API Client Implementation
1. Review all query parameters in OGC_QUERY_PARSING_QUICK_REFERENCE.md
2. Study valid/invalid requests in OGC_QUERY_PARSING_CODE_EXAMPLES.md
3. Implement error handling for all error scenarios
4. Test with examples from section 10 of code examples

---

## Related Documentation

For complete understanding, also review:
- OGC API Features Part 1 specification (http://www.opengis.net/doc/IS/ogcapi-features-1)
- OGC CQL2 specification for filter expressions
- Service and Layer configuration documentation
- Repository interface documentation

---

Generated: October 23, 2025
Analysis Scope: Very Thorough
Total Analysis Coverage: All query parsing paths in OGC handler
