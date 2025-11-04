# CSW (Catalog Service for the Web) Integration Tests

## Overview

This directory contains comprehensive integration tests for Honua's CSW 2.0.2 implementation using OWSLib, the reference Python client library for OGC Web Services.

## Test File

**File:** `test_csw_owslib.py`
- **Lines of code:** 1009
- **Test functions:** 40
- **Client library:** OWSLib CatalogueServiceWeb

## Test Coverage

### 1. GetCapabilities Tests (6 tests)
Tests the CSW GetCapabilities operation to ensure proper service metadata, version support, and operation declarations.

- `test_csw_get_capabilities` - Basic capabilities retrieval
- `test_csw_version_2_0_2_supported` - Version compliance
- `test_csw_service_metadata` - Service identification
- `test_csw_provider_information` - Provider details
- `test_csw_supported_operations` - Operation listing
- `test_csw_get_operation_constraints` - Operation parameters
- `test_csw_supported_query_types` - Filter capabilities
- `test_csw_supported_output_schemas` - Schema support

### 2. DescribeRecord Tests (2 tests)
Tests schema information retrieval for record types.

- `test_csw_describe_record` - Schema retrieval
- `test_csw_describe_record_for_dublin_core` - Dublin Core schema

### 3. GetRecords Tests (12 tests)
Comprehensive tests for searching and retrieving metadata records.

- `test_csw_get_records_basic` - Basic record retrieval
- `test_csw_get_records_count` - maxrecords parameter
- `test_csw_get_records_with_start_position` - Pagination
- `test_csw_get_records_result_type_hits` - Count-only queries
- `test_csw_get_records_validates_total_count` - Result counting
- `test_csw_get_records_with_text_search` - Full-text search
- `test_csw_get_records_with_bbox_filter` - Spatial filtering
- `test_csw_get_records_with_property_filter` - Property filters
- `test_csw_get_records_with_combined_filters` - Multiple filters
- `test_csw_get_records_dublin_core_schema` - Dublin Core output
- `test_csw_get_records_iso_19139_schema` - ISO 19139 output
- `test_csw_get_records_validates_schema_format` - Response validation

### 4. GetRecordById Tests (4 tests)
Tests retrieval of specific records by identifier.

- `test_csw_get_record_by_id_basic` - Single record retrieval
- `test_csw_get_record_by_id_multiple` - Multiple records
- `test_csw_get_record_by_id_validates_content` - Content validation
- `test_csw_get_record_by_id_iso_schema` - ISO 19139 output

### 5. GetDomain Tests (1 test)
Tests querying available domain values.

- `test_csw_get_domain` - Domain value queries

### 6. Record Content Validation (4 tests)
Validates the structure and content of returned records.

- `test_csw_record_has_identifier` - Identifier validation
- `test_csw_record_has_title` - Title validation
- `test_csw_record_has_type` - Type validation
- `test_csw_record_has_bounding_box` - Spatial extent validation

### 7. Error Handling Tests (5 tests)
Tests proper error handling for invalid requests.

- `test_csw_invalid_record_id_raises_error` - Invalid IDs
- `test_csw_missing_service_parameter_raises_error` - Missing parameters
- `test_csw_invalid_request_raises_error` - Invalid operations
- `test_csw_invalid_filter_raises_error` - Malformed filters
- `test_csw_invalid_output_schema_handling` - Unsupported schemas

### 8. Performance & Compliance (4 tests)
Performance and standards compliance testing.

- `test_csw_response_time_reasonable` - Response time validation
- `test_csw_transaction_not_supported` - Transaction handling
- `test_csw_xml_response_well_formed` - XML validation
- `test_csw_unicode_support` - Unicode handling

## Requirements

```bash
pip install owslib>=0.29.0 pytest>=7.0.0 requests>=2.28.0
```

Or install from requirements.txt:
```bash
pip install -r requirements.txt
```

## Running the Tests

### Run all CSW tests
```bash
pytest tests/python/test_csw_owslib.py
```

### Run with verbose output
```bash
pytest tests/python/test_csw_owslib.py -v
```

### Run specific test
```bash
pytest tests/python/test_csw_owslib.py::test_csw_get_capabilities
```

### Run with CSW marker
```bash
pytest -m csw
```

### Run with multiple markers
```bash
pytest -m "csw and integration"
```

### Skip tests that require Honua
```bash
pytest -m "not requires_honua"
```

## Environment Variables

The tests require the following environment variables:

- `HONUA_API_BASE_URL` - Base URL of the Honua API instance
  - Example: `http://localhost:5000`
  - Alternative: `HONUA_QGIS_BASE_URL`

- `HONUA_API_BEARER` (optional) - Bearer token for authentication
  - Alternative: `HONUA_QGIS_BEARER`

### Example Setup
```bash
export HONUA_API_BASE_URL=http://localhost:5000
pytest tests/python/test_csw_owslib.py
```

## Test Fixtures

### csw_client
- **Scope:** module
- **Type:** owslib.csw.CatalogueServiceWeb
- **Description:** CSW client connected to the Honua API

### test_record_id
- **Scope:** module
- **Type:** str
- **Description:** Valid record ID for testing GetRecordById operations

## Pytest Markers

All tests are marked with:
- `@pytest.mark.integration` - Integration test
- `@pytest.mark.python` - Python client test
- `@pytest.mark.csw` - CSW-specific test
- `@pytest.mark.requires_honua` - Requires running Honua instance

## Test Pattern

The CSW tests follow the same comprehensive pattern as the WMS and WFS tests:

1. **Fixtures** - Reusable test components
2. **Success Cases** - Verify correct behavior
3. **Edge Cases** - Test boundaries and special conditions
4. **Error Cases** - Verify proper error handling
5. **Graceful Skipping** - Skip tests when features not available

## Key Features

- **Comprehensive Coverage:** 40 tests covering all major CSW operations
- **Standards Compliant:** Tests against OGC CSW 2.0.2 specification
- **Multiple Schemas:** Tests both Dublin Core and ISO 19139 outputs
- **Filtering:** Tests property, spatial, and combined filters
- **Pagination:** Tests startPosition and maxRecords
- **Error Handling:** Validates proper error responses
- **Performance:** Includes response time validation
- **Unicode Support:** Tests proper text encoding

## Related Tests

- `test_wms_owslib.py` - WMS integration tests (29 tests)
- `test_wfs_owslib.py` - WFS integration tests (16 tests)
- `test_wcs_rasterio.py` - WCS integration tests
- `test_stac_pystac.py` - STAC integration tests

## Contributing

When adding new CSW tests:

1. Follow the existing naming convention: `test_csw_<operation>_<scenario>`
2. Add appropriate docstrings describing what the test validates
3. Use proper pytest markers
4. Handle exceptions gracefully with pytest.skip() when appropriate
5. Include both success and error scenarios
6. Validate response structure and content

## References

- [OGC CSW 2.0.2 Specification](https://www.ogc.org/standards/cat)
- [OWSLib Documentation](https://owslib.readthedocs.io/)
- [Pytest Documentation](https://docs.pytest.org/)
- [Dublin Core Metadata](https://www.dublincore.org/)
- [ISO 19115/19139 Metadata](https://www.iso.org/standard/53798.html)
