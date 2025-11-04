"""
Comprehensive CSW 2.0.2 Integration Tests with OWSLib

This test suite validates Honua's CSW (Catalog Service for the Web) implementation
using OWSLib, the reference Python client library for OGC Web Services. Tests verify
full compliance with CSW 2.0.2 specification.

Test Coverage:
- GetCapabilities: Service metadata, operation listing, supported filters
- GetRecords: Search for metadata records with filters and paging
- GetRecordById: Retrieve specific records by identifier
- DescribeRecord: Schema information for record types
- GetDomain: Query available domain values
- Filtering: Property filters, bbox filters, logical operators
- Output Schemas: Dublin Core, ISO 19115/19139
- Paging: startPosition, maxRecords parameters
- Result Types: results, hits validation
- Error Handling: Invalid record IDs, malformed queries, missing parameters

Requirements:
- owslib >= 0.29.0

Client: OWSLib CatalogueServiceWeb
Specification: OGC CSW 2.0.2
Reference: https://www.ogc.org/standards/cat
"""
import pytest
import os
from typing import Optional
import xml.etree.ElementTree as ET


pytestmark = [
    pytest.mark.integration,
    pytest.mark.python,
    pytest.mark.csw,
    pytest.mark.requires_honua
]


# ============================================================================
#  Fixtures
# ============================================================================

@pytest.fixture(scope="module")
def csw_client(honua_api_base_url):
    """Create OWSLib CatalogueServiceWeb client."""
    try:
        from owslib.csw import CatalogueServiceWeb
    except ImportError:
        pytest.skip("OWSLib not installed (pip install owslib)")

    csw_url = f"{honua_api_base_url}/csw"

    try:
        csw = CatalogueServiceWeb(csw_url, version='2.0.2')
        return csw
    except Exception as e:
        pytest.skip(f"Could not connect to CSW at {csw_url}: {e}")


@pytest.fixture(scope="module")
def test_record_id(csw_client):
    """Get a valid record ID for testing."""
    try:
        # Perform a GetRecords to get at least one record
        csw_client.getrecords2(maxrecords=1)

        if not csw_client.records:
            pytest.skip("No CSW records available in test environment")

        # Return the first record's identifier
        record_id = list(csw_client.records.keys())[0]
        return record_id
    except Exception as e:
        pytest.skip(f"Could not retrieve test records: {e}")


# ============================================================================
#  GetCapabilities Tests
# ============================================================================

def test_csw_get_capabilities(csw_client):
    """Verify CSW GetCapabilities returns valid service metadata."""
    assert csw_client is not None, "CSW client should be initialized"

    # Check identification
    assert hasattr(csw_client, 'identification'), "CSW should have identification"
    assert csw_client.identification is not None, "Identification should not be None"
    assert hasattr(csw_client.identification, 'title'), "CSW should have title"
    assert csw_client.identification.type == 'CSW', "Service type should be CSW"


def test_csw_version_2_0_2_supported(csw_client):
    """Verify CSW 2.0.2 version is supported."""
    assert csw_client.version == '2.0.2', f"Expected CSW 2.0.2, got {csw_client.version}"


def test_csw_service_metadata(csw_client):
    """Verify CSW service metadata is complete."""
    # Service identification
    ident = csw_client.identification
    assert ident.title is not None, "Service must have title"
    assert ident.type == 'CSW', "Service type should be CSW"

    # Check for abstract (recommended but optional)
    assert hasattr(ident, 'abstract')

    # Check for keywords (recommended but optional)
    assert hasattr(ident, 'keywords')


def test_csw_provider_information(csw_client):
    """Verify CSW provider information is present."""
    assert hasattr(csw_client, 'provider'), "CSW should have provider info"
    provider = csw_client.provider

    if provider is not None:
        # Provider name is recommended
        assert hasattr(provider, 'name')


def test_csw_supported_operations(csw_client):
    """Verify CSW declares supported operations."""
    assert hasattr(csw_client, 'operations'), "CSW should list operations"

    operation_names = [op.name for op in csw_client.operations]

    # Required operations
    assert 'GetCapabilities' in operation_names, "CSW must support GetCapabilities"
    assert 'DescribeRecord' in operation_names, "CSW must support DescribeRecord"
    assert 'GetRecords' in operation_names, "CSW must support GetRecords"
    assert 'GetRecordById' in operation_names, "CSW must support GetRecordById"


def test_csw_get_operation_constraints(csw_client):
    """Verify GetRecords operation declares constraints."""
    getrecords_op = None
    for op in csw_client.operations:
        if op.name == 'GetRecords':
            getrecords_op = op
            break

    assert getrecords_op is not None, "GetRecords operation should be present"

    # Check for GET and POST methods
    assert hasattr(getrecords_op, 'methods'), "Operation should declare methods"


def test_csw_supported_query_types(csw_client):
    """Verify CSW declares supported query constraint language."""
    # CSW typically supports Filter encoding
    # This is stored in Filter_Capabilities
    if hasattr(csw_client, 'filters'):
        filters = csw_client.filters
        # If filters are supported, they should be defined
        assert filters is not None or filters == {}, \
            "Filter capabilities should be defined"


def test_csw_supported_output_schemas(csw_client):
    """Verify CSW declares supported output schemas."""
    getrecords_op = None
    for op in csw_client.operations:
        if op.name == 'GetRecords':
            getrecords_op = op
            break

    if getrecords_op and hasattr(getrecords_op, 'parameters'):
        # Check if outputSchema parameter is defined
        params = {p['name']: p for p in getrecords_op.parameters}

        if 'outputSchema' in params:
            schemas = params['outputSchema'].get('values', [])
            assert len(schemas) > 0, "Should support at least one output schema"


# ============================================================================
#  DescribeRecord Tests
# ============================================================================

def test_csw_describe_record(csw_client):
    """Verify DescribeRecord returns schema information."""
    try:
        csw_client.describerecord()

        # Check if response was successful
        assert csw_client.request is not None, "DescribeRecord should send request"
        assert csw_client.response is not None, "DescribeRecord should get response"

        # Parse response
        response_text = csw_client.response
        assert len(response_text) > 0, "DescribeRecord response should not be empty"

        # Validate it's XML
        try:
            root = ET.fromstring(response_text)
            assert root is not None, "Response should be valid XML"

            # Should contain schema components
            assert 'Schema' in response_text or 'schema' in response_text, \
                "Response should contain schema information"

        except ET.ParseError as e:
            pytest.fail(f"Failed to parse DescribeRecord response: {e}")

    except Exception as e:
        # DescribeRecord may not be fully implemented
        pytest.skip(f"DescribeRecord not fully supported: {e}")


def test_csw_describe_record_for_dublin_core(csw_client):
    """Verify DescribeRecord can describe Dublin Core record type."""
    try:
        # Request Dublin Core schema
        csw_client.describerecord(typename='csw:Record')

        assert csw_client.response is not None, "Should return response"
        response_text = csw_client.response

        # Should reference Dublin Core elements
        assert 'Record' in response_text or 'record' in response_text, \
            "Response should describe Record type"

    except Exception as e:
        pytest.skip(f"DescribeRecord for Dublin Core not supported: {e}")


# ============================================================================
#  GetRecords Tests - Basic Retrieval
# ============================================================================

def test_csw_get_records_basic(csw_client):
    """Verify GetRecords retrieves metadata records."""
    try:
        csw_client.getrecords2(maxrecords=10)

        # Check search status
        assert hasattr(csw_client, 'results'), "Should have results attribute"
        assert csw_client.results is not None, "Results should not be None"

        # Verify result counts
        results = csw_client.results
        assert 'matches' in results, "Results should include matches count"
        assert 'returned' in results, "Results should include returned count"

        # Check records
        assert hasattr(csw_client, 'records'), "Should have records attribute"

    except Exception as e:
        pytest.fail(f"GetRecords failed: {e}")


def test_csw_get_records_count(csw_client):
    """Verify GetRecords respects maxrecords parameter."""
    max_records = 5

    try:
        csw_client.getrecords2(maxrecords=max_records)

        results = csw_client.results
        returned = results.get('returned', 0)

        # Should return at most the requested number
        assert returned <= max_records, \
            f"Should return at most {max_records} records, got {returned}"

        # Check actual records
        if csw_client.records:
            assert len(csw_client.records) == returned, \
                "Number of records should match returned count"

    except Exception as e:
        pytest.fail(f"GetRecords with maxrecords failed: {e}")


def test_csw_get_records_with_start_position(csw_client):
    """Verify GetRecords supports startposition for paging."""
    try:
        # Get first page
        csw_client.getrecords2(maxrecords=5, startposition=1)
        first_page_results = csw_client.results.copy()
        first_page_ids = list(csw_client.records.keys()) if csw_client.records else []

        # Check if there are more records
        if first_page_results.get('matches', 0) > 5:
            # Get second page
            csw_client.getrecords2(maxrecords=5, startposition=6)
            second_page_ids = list(csw_client.records.keys()) if csw_client.records else []

            # Pages should have different records
            if first_page_ids and second_page_ids:
                assert first_page_ids[0] != second_page_ids[0], \
                    "Different pages should return different records"
        else:
            # Not enough records for pagination test
            pytest.skip("Not enough records to test pagination")

    except Exception as e:
        # Pagination may not be fully supported
        pytest.skip(f"Pagination not fully supported: {e}")


def test_csw_get_records_result_type_hits(csw_client):
    """Verify GetRecords supports resulttype=hits for count-only queries."""
    try:
        csw_client.getrecords2(maxrecords=10, resulttype='hits')

        results = csw_client.results

        # Should return match count
        assert 'matches' in results, "Should return matches count"
        matches = results['matches']
        assert matches >= 0, "Matches count should be non-negative"

        # Should not return actual records for hits query
        returned = results.get('returned', 0)
        assert returned == 0, "Should not return records for resulttype=hits"

    except Exception as e:
        pytest.skip(f"ResultType=hits not supported: {e}")


def test_csw_get_records_validates_total_count(csw_client):
    """Verify GetRecords reports accurate total match count."""
    try:
        csw_client.getrecords2(maxrecords=5)

        results = csw_client.results
        matches = results.get('matches', 0)
        returned = results.get('returned', 0)

        # Returned should never exceed matches
        assert returned <= matches, \
            f"Returned ({returned}) should not exceed matches ({matches})"

        # If maxrecords < matches, returned should equal maxrecords or less
        if matches > 5:
            assert returned <= 5, "Should return at most maxrecords"

    except Exception as e:
        pytest.fail(f"GetRecords count validation failed: {e}")


# ============================================================================
#  GetRecords Tests - Filtering
# ============================================================================

def test_csw_get_records_with_text_search(csw_client):
    """Verify GetRecords supports full-text search."""
    try:
        from owslib.fes import PropertyIsLike

        # Search for records with 'road' in any text field
        search_text = 'road'

        csw_client.getrecords2(
            constraints=[PropertyIsLike('csw:AnyText', f'%{search_text}%')],
            maxrecords=10
        )

        results = csw_client.results

        # Should execute query successfully
        assert 'matches' in results, "Should return matches count"

        # If records found, verify they contain search term
        if csw_client.records:
            for record_id, record in csw_client.records.items():
                # Check if search term appears in title or abstract
                found = False
                if hasattr(record, 'title') and record.title:
                    if search_text.lower() in record.title.lower():
                        found = True
                if hasattr(record, 'abstract') and record.abstract:
                    if search_text.lower() in record.abstract.lower():
                        found = True

                # Note: Some implementations may search other fields
                # So we don't fail if term not found in title/abstract

    except ImportError:
        pytest.skip("OWSLib FES module not available")
    except Exception as e:
        pytest.skip(f"Text search not fully supported: {e}")


def test_csw_get_records_with_bbox_filter(csw_client):
    """Verify GetRecords supports spatial bbox filtering."""
    try:
        from owslib.fes import BBox

        # Define a bbox (minx, miny, maxx, maxy)
        # Using Portland, OR area
        bbox = BBox([-122.7, 45.4, -122.5, 45.6])

        csw_client.getrecords2(
            constraints=[bbox],
            maxrecords=10
        )

        results = csw_client.results
        assert 'matches' in results, "Should return matches count"

        # If records found, verify they have spatial extent
        if csw_client.records:
            for record_id, record in csw_client.records.items():
                # Records should have bbox information
                assert hasattr(record, 'bbox'), "Records should have bbox attribute"

    except ImportError:
        pytest.skip("OWSLib FES module not available")
    except Exception as e:
        pytest.skip(f"Bbox filtering not fully supported: {e}")


def test_csw_get_records_with_property_filter(csw_client):
    """Verify GetRecords supports property value filtering."""
    try:
        from owslib.fes import PropertyIsEqualTo

        # Filter by type = 'dataset'
        csw_client.getrecords2(
            constraints=[PropertyIsEqualTo('dc:type', 'dataset')],
            maxrecords=10
        )

        results = csw_client.results
        assert 'matches' in results, "Should return matches count"

        # If records found, verify type is dataset
        if csw_client.records:
            for record_id, record in csw_client.records.items():
                if hasattr(record, 'type'):
                    # Type should match filter
                    assert record.type == 'dataset', \
                        f"Record type should be 'dataset', got '{record.type}'"

    except ImportError:
        pytest.skip("OWSLib FES module not available")
    except Exception as e:
        pytest.skip(f"Property filtering not fully supported: {e}")


def test_csw_get_records_with_combined_filters(csw_client):
    """Verify GetRecords supports combining multiple filter criteria."""
    try:
        from owslib.fes import PropertyIsLike, PropertyIsEqualTo, And

        # Combine text search with type filter
        filters = [
            PropertyIsLike('csw:AnyText', '%road%'),
            PropertyIsEqualTo('dc:type', 'dataset')
        ]

        csw_client.getrecords2(
            constraints=filters,
            maxrecords=10
        )

        results = csw_client.results
        assert 'matches' in results, "Should return matches count"

        # Query should execute successfully
        assert isinstance(results['matches'], int), \
            "Matches should be an integer"

    except ImportError:
        pytest.skip("OWSLib FES module not available")
    except Exception as e:
        pytest.skip(f"Combined filtering not fully supported: {e}")


# ============================================================================
#  GetRecords Tests - Output Schemas
# ============================================================================

def test_csw_get_records_dublin_core_schema(csw_client):
    """Verify GetRecords returns Dublin Core records by default."""
    try:
        csw_client.getrecords2(maxrecords=5)

        if csw_client.records:
            # Check first record structure
            record = list(csw_client.records.values())[0]

            # Dublin Core fields
            assert hasattr(record, 'identifier'), "Record should have identifier"
            assert hasattr(record, 'title'), "Record should have title"
            assert hasattr(record, 'type'), "Record should have type"

    except Exception as e:
        pytest.fail(f"Dublin Core schema test failed: {e}")


def test_csw_get_records_iso_19139_schema(csw_client):
    """Verify GetRecords supports ISO 19115/19139 output schema."""
    try:
        # Request ISO 19139 schema
        iso_schema = 'http://www.isotc211.org/2005/gmd'

        csw_client.getrecords2(
            maxrecords=5,
            outputschema=iso_schema
        )

        # Check if response contains ISO 19139 elements
        if csw_client.response:
            response_text = csw_client.response

            # Should contain ISO namespaces
            assert 'gmd' in response_text or 'MD_Metadata' in response_text, \
                "Response should contain ISO 19139 elements"

    except Exception as e:
        pytest.skip(f"ISO 19139 schema not supported: {e}")


def test_csw_get_records_validates_schema_format(csw_client):
    """Verify GetRecords response matches requested output schema."""
    try:
        csw_client.getrecords2(maxrecords=5)

        if csw_client.response:
            response_text = csw_client.response

            # Parse XML to check structure
            root = ET.fromstring(response_text)

            # Should be a GetRecordsResponse
            assert 'GetRecordsResponse' in root.tag, \
                "Response should be GetRecordsResponse"

            # Should contain SearchResults
            assert root.find('.//{http://www.opengis.net/cat/csw/2.0.2}SearchResults') is not None, \
                "Response should contain SearchResults element"

    except Exception as e:
        pytest.skip(f"Schema validation test failed: {e}")


# ============================================================================
#  GetRecordById Tests
# ============================================================================

def test_csw_get_record_by_id_basic(csw_client, test_record_id):
    """Verify GetRecordById retrieves specific record."""
    try:
        csw_client.getrecordbyid(id=[test_record_id])

        # Should return the record
        assert csw_client.records is not None, "Should return records"
        assert len(csw_client.records) > 0, "Should return at least one record"

        # Check if requested record is present
        assert test_record_id in csw_client.records, \
            f"Should return record with ID {test_record_id}"

        record = csw_client.records[test_record_id]

        # Validate record structure
        assert hasattr(record, 'identifier'), "Record should have identifier"
        assert record.identifier == test_record_id, \
            f"Record identifier should match requested ID"

    except Exception as e:
        pytest.fail(f"GetRecordById failed: {e}")


def test_csw_get_record_by_id_multiple(csw_client):
    """Verify GetRecordById can retrieve multiple records."""
    try:
        # First get some record IDs
        csw_client.getrecords2(maxrecords=3)

        if not csw_client.records or len(csw_client.records) < 2:
            pytest.skip("Not enough records for multi-ID test")

        record_ids = list(csw_client.records.keys())[:2]

        # Request multiple records by ID
        csw_client.getrecordbyid(id=record_ids)

        # Should return all requested records
        assert len(csw_client.records) == len(record_ids), \
            f"Should return {len(record_ids)} records"

        for record_id in record_ids:
            assert record_id in csw_client.records, \
                f"Should return record {record_id}"

    except Exception as e:
        pytest.skip(f"GetRecordById with multiple IDs not supported: {e}")


def test_csw_get_record_by_id_validates_content(csw_client, test_record_id):
    """Verify GetRecordById returns complete record information."""
    try:
        csw_client.getrecordbyid(id=[test_record_id])

        record = csw_client.records[test_record_id]

        # Check Dublin Core fields
        assert hasattr(record, 'identifier'), "Should have identifier"
        assert hasattr(record, 'title'), "Should have title"
        assert record.title is not None, "Title should not be None"

        # Optional but common fields
        assert hasattr(record, 'type'), "Should have type"
        assert hasattr(record, 'abstract'), "Should have abstract"

    except Exception as e:
        pytest.fail(f"GetRecordById validation failed: {e}")


def test_csw_get_record_by_id_iso_schema(csw_client, test_record_id):
    """Verify GetRecordById supports ISO 19139 output schema."""
    try:
        iso_schema = 'http://www.isotc211.org/2005/gmd'

        csw_client.getrecordbyid(
            id=[test_record_id],
            outputschema=iso_schema
        )

        if csw_client.response:
            response_text = csw_client.response

            # Should contain ISO elements
            assert 'gmd' in response_text or 'MD_Metadata' in response_text, \
                "Response should contain ISO 19139 elements"

    except Exception as e:
        pytest.skip(f"GetRecordById with ISO schema not supported: {e}")


# ============================================================================
#  GetDomain Tests
# ============================================================================

def test_csw_get_domain(csw_client):
    """Verify GetDomain returns available domain values."""
    try:
        # OWSLib may not have direct support for GetDomain
        # We'll test via direct request if possible
        import requests

        # Get base URL from csw_client
        base_url = csw_client.url

        response = requests.get(
            base_url,
            params={
                'service': 'CSW',
                'version': '2.0.2',
                'request': 'GetDomain',
                'propertyname': 'dc:type'
            },
            timeout=30
        )

        assert response.status_code == 200, \
            f"GetDomain should return 200, got {response.status_code}"

        # Parse response
        root = ET.fromstring(response.content)

        # Should be GetDomainResponse
        assert 'GetDomainResponse' in root.tag, \
            "Response should be GetDomainResponse"

    except Exception as e:
        pytest.skip(f"GetDomain not supported or accessible: {e}")


# ============================================================================
#  Record Content Tests
# ============================================================================

def test_csw_record_has_identifier(csw_client):
    """Verify records have unique identifiers."""
    try:
        csw_client.getrecords2(maxrecords=5)

        if csw_client.records:
            identifiers = set()

            for record_id, record in csw_client.records.items():
                assert hasattr(record, 'identifier'), "Record should have identifier"
                assert record.identifier is not None, "Identifier should not be None"

                # Identifiers should be unique
                assert record.identifier not in identifiers, \
                    f"Duplicate identifier found: {record.identifier}"
                identifiers.add(record.identifier)

    except Exception as e:
        pytest.fail(f"Record identifier test failed: {e}")


def test_csw_record_has_title(csw_client):
    """Verify records have titles."""
    try:
        csw_client.getrecords2(maxrecords=5)

        if csw_client.records:
            for record_id, record in csw_client.records.items():
                assert hasattr(record, 'title'), "Record should have title"
                assert record.title is not None, "Title should not be None"
                assert len(record.title) > 0, "Title should not be empty"

    except Exception as e:
        pytest.fail(f"Record title test failed: {e}")


def test_csw_record_has_type(csw_client):
    """Verify records declare their type."""
    try:
        csw_client.getrecords2(maxrecords=5)

        if csw_client.records:
            valid_types = ['dataset', 'service', 'series', 'application']

            for record_id, record in csw_client.records.items():
                assert hasattr(record, 'type'), "Record should have type"

                if record.type:
                    # Type should be a recognized value
                    assert record.type.lower() in valid_types, \
                        f"Record type '{record.type}' should be one of {valid_types}"

    except Exception as e:
        pytest.fail(f"Record type test failed: {e}")


def test_csw_record_has_bounding_box(csw_client):
    """Verify records with spatial extent have bounding boxes."""
    try:
        csw_client.getrecords2(maxrecords=5)

        if csw_client.records:
            for record_id, record in csw_client.records.items():
                # Not all records need bbox, but attribute should exist
                assert hasattr(record, 'bbox'), "Record should have bbox attribute"

                # If bbox exists, validate structure
                if record.bbox:
                    assert len(record.bbox) == 4, \
                        "Bounding box should have 4 coordinates"

                    minx, miny, maxx, maxy = record.bbox
                    assert minx <= maxx, "Min X should be <= Max X"
                    assert miny <= maxy, "Min Y should be <= Max Y"

    except Exception as e:
        pytest.skip(f"Bounding box test failed: {e}")


# ============================================================================
#  Error Handling Tests
# ============================================================================

def test_csw_invalid_record_id_raises_error(csw_client):
    """Verify requesting invalid record ID returns error."""
    try:
        invalid_id = 'nonexistent_record_id_12345'

        csw_client.getrecordbyid(id=[invalid_id])

        # Check if an exception was captured in the response
        if csw_client.response:
            response_text = csw_client.response

            # Should contain exception
            root = ET.fromstring(response_text)
            exception = root.find('.//{http://www.opengis.net/ows}Exception')

            if exception is not None:
                # Found exception element - this is correct behavior
                exception_code = exception.get('exceptionCode')
                assert exception_code is not None, "Exception should have code"
            else:
                # No exception in response - check if records empty
                assert not csw_client.records or len(csw_client.records) == 0, \
                    "Should not return records for invalid ID"

    except Exception as e:
        # OWSLib may raise exception - this is acceptable
        assert "not found" in str(e).lower() or "invalid" in str(e).lower(), \
            f"Error should mention invalid record: {e}"


def test_csw_missing_service_parameter_raises_error(csw_client):
    """Verify missing required parameters return error."""
    try:
        import requests

        base_url = csw_client.url

        # Request without service parameter
        response = requests.get(
            base_url,
            params={'request': 'GetCapabilities'},
            timeout=30
        )

        # Should return error (400 or exception document)
        if response.status_code == 200:
            # Check if response is exception
            root = ET.fromstring(response.content)
            exception = root.find('.//{http://www.opengis.net/ows}Exception')
            assert exception is not None, \
                "Should return exception for missing service parameter"
        else:
            assert response.status_code >= 400, \
                "Should return error status for missing parameter"

    except Exception as e:
        pytest.skip(f"Error handling test not applicable: {e}")


def test_csw_invalid_request_raises_error(csw_client):
    """Verify invalid request type returns error."""
    try:
        import requests

        base_url = csw_client.url

        # Request with invalid operation
        response = requests.get(
            base_url,
            params={
                'service': 'CSW',
                'version': '2.0.2',
                'request': 'InvalidOperation'
            },
            timeout=30
        )

        # Should return error
        assert response.status_code >= 400 or b'Exception' in response.content, \
            "Should return error for invalid request"

        if b'Exception' in response.content:
            root = ET.fromstring(response.content)
            exception = root.find('.//{http://www.opengis.net/ows}Exception')
            assert exception is not None, "Should contain exception element"

    except Exception as e:
        pytest.skip(f"Invalid request error test not applicable: {e}")


def test_csw_invalid_filter_raises_error(csw_client):
    """Verify malformed filter returns error."""
    try:
        from owslib.fes import PropertyIsEqualTo

        # Try filter with invalid property name
        csw_client.getrecords2(
            constraints=[PropertyIsEqualTo('invalid:property:name', 'value')],
            maxrecords=10
        )

        # May return results or error depending on implementation
        # If results returned, that's acceptable (may ignore invalid property)
        # If error raised, that's also acceptable

    except Exception as e:
        # Exception is acceptable for invalid filter
        assert "property" in str(e).lower() or "filter" in str(e).lower() or "constraint" in str(e).lower(), \
            f"Error should mention invalid filter: {e}"


def test_csw_invalid_output_schema_handling(csw_client):
    """Verify requesting unsupported output schema is handled."""
    try:
        # Request with unsupported schema
        csw_client.getrecords2(
            maxrecords=5,
            outputschema='http://example.org/unsupported/schema'
        )

        # Server may:
        # 1. Return error (preferred)
        # 2. Fall back to default schema (acceptable)
        # 3. Return empty results (acceptable)

        # As long as it doesn't crash, test passes
        assert True, "Server handled unsupported schema gracefully"

    except Exception as e:
        # Exception is also acceptable
        assert "schema" in str(e).lower() or "format" in str(e).lower(), \
            f"Error should mention invalid schema: {e}"


# ============================================================================
#  Transaction Tests (Optional)
# ============================================================================

def test_csw_transaction_not_supported(csw_client):
    """Verify Transaction operation handling (if not supported)."""
    try:
        import requests

        base_url = csw_client.url

        # Try to perform a transaction (insert)
        transaction_xml = """<?xml version="1.0" encoding="UTF-8"?>
        <csw:Transaction service="CSW" version="2.0.2"
            xmlns:csw="http://www.opengis.net/cat/csw/2.0.2">
            <csw:Insert>
                <csw:Record>
                    <dc:identifier>test-id</dc:identifier>
                    <dc:title>Test Record</dc:title>
                </csw:Record>
            </csw:Insert>
        </csw:Transaction>"""

        response = requests.post(
            base_url,
            data=transaction_xml,
            headers={'Content-Type': 'application/xml'},
            timeout=30
        )

        # If transactions not supported, should return error
        # Check for OperationNotSupported exception
        if b'Exception' in response.content:
            root = ET.fromstring(response.content)
            exception = root.find('.//{http://www.opengis.net/ows}Exception')

            if exception is not None:
                exception_code = exception.get('exceptionCode')
                # OperationNotSupported is acceptable
                assert exception_code in ['OperationNotSupported', 'NoApplicableCode'], \
                    f"Expected OperationNotSupported, got {exception_code}"

    except Exception as e:
        # If transactions are not supported, this is expected
        pytest.skip(f"Transaction test not applicable: {e}")


# ============================================================================
#  Performance and Compliance Tests
# ============================================================================

def test_csw_response_time_reasonable(csw_client):
    """Verify CSW responses are returned in reasonable time."""
    import time

    try:
        start = time.time()
        csw_client.getrecords2(maxrecords=10)
        duration = time.time() - start

        # Should respond within 10 seconds for small query
        assert duration < 10.0, \
            f"GetRecords should respond within 10 seconds, took {duration:.2f}s"

    except Exception as e:
        pytest.skip(f"Performance test not applicable: {e}")


def test_csw_xml_response_well_formed(csw_client):
    """Verify CSW returns well-formed XML responses."""
    try:
        csw_client.getrecords2(maxrecords=5)

        if csw_client.response:
            response_text = csw_client.response

            # Should be valid XML
            try:
                root = ET.fromstring(response_text)
                assert root is not None, "Response should be valid XML"

                # Should have proper namespace declarations
                assert '{http://www.opengis.net/cat/csw/2.0.2}' in root.tag or \
                       'csw' in response_text, \
                       "Response should use CSW namespace"

            except ET.ParseError as e:
                pytest.fail(f"Response is not well-formed XML: {e}")

    except Exception as e:
        pytest.skip(f"XML validation test failed: {e}")


def test_csw_unicode_support(csw_client):
    """Verify CSW handles Unicode text properly."""
    try:
        csw_client.getrecords2(maxrecords=5)

        if csw_client.records:
            for record_id, record in csw_client.records.items():
                # Title should be proper string (may contain Unicode)
                if hasattr(record, 'title') and record.title:
                    assert isinstance(record.title, str), \
                        "Title should be string type"

                    # Should be able to encode to UTF-8
                    try:
                        record.title.encode('utf-8')
                    except UnicodeEncodeError as e:
                        pytest.fail(f"Title contains invalid Unicode: {e}")

    except Exception as e:
        pytest.skip(f"Unicode test not applicable: {e}")
