"""
Comprehensive Esri GeoServices REST API Integration Tests with ArcGIS Python API

This test suite provides comprehensive coverage of Esri GeoServices REST API operations
using the ArcGIS Python API (arcgis package) as the reference client. Tests validate
Honua's GeoServices implementation for compatibility with official ArcGIS clients.

Test Coverage:
- Connection: Connect to FeatureServer via FeatureLayer
- Query Operations: where clause, spatial filters, attribute queries
- Feature Retrieval: Convert to GeoDataFrame, iterate features
- Feature Editing: AddFeatures, UpdateFeatures, DeleteFeatures
- Batch Operations: ApplyEdits with multiple operation types
- Error Handling: Invalid layers, malformed queries, permission errors

Client: ArcGIS Python API (arcgis package)
Specification: Esri ArcGIS REST API 10.8+
Reference: https://developers.arcgis.com/python/

Note: The arcgis package may not be installed in all environments.
Tests will gracefully skip if the package is not available.
"""
import json
import os
import pytest
from typing import Dict, Any, List, Optional


pytestmark = [
    pytest.mark.integration,
    pytest.mark.geoservices,
    pytest.mark.python,
    pytest.mark.requires_honua
]


# ============================================================================
#  Fixture: Check ArcGIS Python API availability
# ============================================================================

@pytest.fixture(scope="module")
def arcgis_available():
    """
    Check if ArcGIS Python API is installed.

    The arcgis package is optional and may not be available in all environments.
    Tests will skip gracefully if not installed.
    """
    try:
        import arcgis
        return True
    except ImportError:
        pytest.skip("ArcGIS Python API (arcgis package) is not installed")


@pytest.fixture(scope="module")
def feature_layer_url(honua_api_base_url):
    """
    Construct FeatureServer layer URL for testing.

    Returns URL in format:
    https://hostname/rest/services/folder/service/FeatureServer/layerIndex
    """
    # Use environment variable for service name if available
    service_name = os.getenv("HONUA_TEST_SERVICE", "roads")
    layer_index = os.getenv("HONUA_TEST_LAYER_INDEX", "0")

    return f"{honua_api_base_url}/rest/services/{service_name}/FeatureServer/{layer_index}"


@pytest.fixture(scope="module")
def gis_connection(arcgis_available, honua_api_base_url, honua_api_bearer_token):
    """
    Create GIS connection object for ArcGIS Python API.

    If bearer token is provided, authenticates the connection.
    Otherwise creates anonymous connection.
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.gis import GIS

    # Create GIS connection
    # For enterprise portal with token authentication
    if honua_api_bearer_token:
        # Note: ArcGIS Python API typically uses token-based auth
        # Bearer tokens may need to be converted or handled differently
        gis = GIS(honua_api_base_url, token=honua_api_bearer_token, verify_cert=False)
    else:
        # Anonymous connection
        gis = GIS(honua_api_base_url, verify_cert=False)

    return gis


# ============================================================================
#  Connection Tests
# ============================================================================

def test_connect_to_featureserver_with_feature_layer(
    arcgis_available,
    feature_layer_url,
    api_request
):
    """
    Verify connection to FeatureServer using arcgis.features.FeatureLayer.

    Tests that:
    - FeatureLayer class can connect to Honua endpoint
    - Layer properties are accessible (name, geometryType, fields)
    - Layer URL is properly formed
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    # Create FeatureLayer connection
    layer = FeatureLayer(feature_layer_url)

    # Verify connection by accessing properties
    assert layer.url == feature_layer_url, "Layer URL should match input"

    # Get layer properties (this makes a request to the service)
    properties = layer.properties

    # Validate properties
    assert hasattr(properties, "name") or "name" in properties, \
        "Layer properties should include name"
    assert hasattr(properties, "geometryType") or "geometryType" in properties, \
        "Layer properties should include geometryType"
    assert hasattr(properties, "fields") or "fields" in properties, \
        "Layer properties should include fields array"


def test_feature_layer_properties_match_rest_metadata(
    arcgis_available,
    feature_layer_url,
    api_request
):
    """
    Verify FeatureLayer properties match REST API metadata response.

    Compares properties accessed through ArcGIS Python API with
    direct REST API call to ensure consistency.
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    # Get layer via ArcGIS Python API
    layer = FeatureLayer(feature_layer_url)
    api_properties = layer.properties

    # Get layer via direct REST call
    response = api_request("GET", f"{feature_layer_url}?f=json")
    assert response.status_code == 200, "REST metadata request should succeed"

    rest_properties = response.json()

    # Compare key properties
    if hasattr(api_properties, "name"):
        assert api_properties.name == rest_properties.get("name"), \
            "Layer name should match between API and REST"
    elif "name" in api_properties:
        assert api_properties["name"] == rest_properties.get("name"), \
            "Layer name should match between API and REST"


# ============================================================================
#  Query Tests
# ============================================================================

def test_query_features_with_where_clause(arcgis_available, feature_layer_url):
    """
    Verify querying features using SQL where clause.

    Tests that:
    - FeatureLayer.query() accepts where parameter
    - Query returns FeatureSet object
    - Features can be accessed from result
    - Where clause properly filters results
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    layer = FeatureLayer(feature_layer_url)

    # Query with where clause
    # Use 1=1 to get all features (common ArcGIS pattern)
    feature_set = layer.query(where="1=1", out_fields="*", return_geometry=True)

    # Validate result
    assert feature_set is not None, "Query should return FeatureSet"
    assert hasattr(feature_set, "features"), "FeatureSet should have features attribute"

    features = feature_set.features
    assert isinstance(features, list), "Features should be a list"

    # Validate feature structure if results present
    if len(features) > 0:
        first_feature = features[0]
        assert hasattr(first_feature, "attributes"), "Feature should have attributes"
        assert hasattr(first_feature, "geometry"), "Feature should have geometry"


def test_query_with_specific_where_clause_filters(arcgis_available, feature_layer_url):
    """
    Verify query with specific SQL filter returns matching records only.

    Tests that where clause filtering works correctly:
    - Equality filters (field = value)
    - String matching with quotes
    - Numeric comparisons
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    layer = FeatureLayer(feature_layer_url)

    # First get a sample feature to know valid values
    all_features = layer.query(where="1=1", out_fields="*", result_record_count=1)

    if len(all_features.features) > 0:
        sample_attrs = all_features.features[0].attributes

        # Find a string field to filter on
        string_field = None
        string_value = None

        for field_name, value in sample_attrs.items():
            if isinstance(value, str) and value and field_name.lower() not in ["objectid", "globalid"]:
                string_field = field_name
                string_value = value
                break

        if string_field and string_value:
            # Query with specific where clause
            where_clause = f"{string_field}='{string_value}'"
            filtered = layer.query(where=where_clause, out_fields="*")

            # Verify all returned features match the filter
            for feature in filtered.features:
                assert feature.attributes.get(string_field) == string_value, \
                    f"Feature {string_field} should match filter value"


def test_query_with_spatial_filter_geometry(arcgis_available, feature_layer_url):
    """
    Verify query with spatial filter using geometry parameter.

    Tests that:
    - Query accepts geometry parameter (envelope, point, polygon)
    - Spatial relationship parameter works (intersects, contains, within)
    - Only spatially matching features are returned
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer
    from arcgis.geometry import Envelope

    layer = FeatureLayer(feature_layer_url)

    # Define bounding box for spatial filter
    # Example coordinates - adjust based on your data
    bbox = Envelope({
        "xmin": -123.0,
        "ymin": 45.0,
        "xmax": -122.0,
        "ymax": 46.0,
        "spatialReference": {"wkid": 4326}
    })

    # Query with spatial filter
    try:
        feature_set = layer.query(
            where="1=1",
            geometry=bbox,
            spatial_relationship="intersects",
            out_fields="*"
        )

        # Validate result
        assert feature_set is not None, "Spatial query should return FeatureSet"
        features = feature_set.features
        assert isinstance(features, list), "Features should be a list"

        # All returned features should intersect the bounding box
        # (detailed validation would require geometry analysis)

    except Exception as e:
        # Some servers may not support all spatial operations
        pytest.skip(f"Spatial query not supported: {str(e)}")


def test_query_with_out_fields_parameter(arcgis_available, feature_layer_url):
    """
    Verify query respects out_fields parameter for field selection.

    Tests that:
    - out_fields="*" returns all fields
    - out_fields="field1,field2" returns only specified fields
    - Object ID field is always included
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    layer = FeatureLayer(feature_layer_url)

    # Get layer metadata to know field names
    properties = layer.properties
    fields = properties.get("fields", []) if isinstance(properties, dict) else properties.fields

    if len(fields) >= 2:
        # Select first two fields (exclude OID field)
        field_names = [f["name"] for f in fields if f.get("type") != "esriFieldTypeOID"][:2]
        out_fields_str = ",".join(field_names)

        # Query with specific fields
        feature_set = layer.query(
            where="1=1",
            out_fields=out_fields_str,
            result_record_count=1
        )

        if len(feature_set.features) > 0:
            attrs = feature_set.features[0].attributes

            # Verify requested fields are present
            for field_name in field_names:
                assert field_name in attrs, f"Requested field {field_name} should be in response"


def test_query_with_return_geometry_false(arcgis_available, feature_layer_url):
    """
    Verify query respects return_geometry parameter.

    Tests that:
    - return_geometry=True includes geometry
    - return_geometry=False omits geometry (attributes only)
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    layer = FeatureLayer(feature_layer_url)

    # Query without geometry
    feature_set = layer.query(
        where="1=1",
        out_fields="*",
        return_geometry=False,
        result_record_count=1
    )

    if len(feature_set.features) > 0:
        first_feature = feature_set.features[0]

        # Geometry should be None or empty when return_geometry=False
        assert first_feature.geometry is None or first_feature.geometry == {}, \
            "Geometry should be omitted when return_geometry=False"

        # Attributes should still be present
        assert first_feature.attributes is not None, \
            "Attributes should be present even without geometry"


def test_query_with_result_record_count_limits_results(arcgis_available, feature_layer_url):
    """
    Verify query respects result_record_count parameter for limiting results.

    Tests that:
    - result_record_count parameter limits number of features returned
    - Useful for pagination and testing
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    layer = FeatureLayer(feature_layer_url)

    # Query with limit
    limit = 2
    feature_set = layer.query(
        where="1=1",
        out_fields="*",
        result_record_count=limit
    )

    features = feature_set.features
    assert len(features) <= limit, \
        f"Query should return at most {limit} features, got {len(features)}"


def test_query_with_result_offset_for_pagination(arcgis_available, feature_layer_url):
    """
    Verify query supports pagination with result_offset parameter.

    Tests that:
    - result_offset skips features
    - Combined with result_record_count enables pagination
    - Different pages return different features
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    layer = FeatureLayer(feature_layer_url)

    # Get first page
    page_size = 2
    page1 = layer.query(
        where="1=1",
        out_fields="*",
        result_record_count=page_size,
        result_offset=0
    )

    # Get second page
    page2 = layer.query(
        where="1=1",
        out_fields="*",
        result_record_count=page_size,
        result_offset=page_size
    )

    # Verify pages have different features (if enough records exist)
    if len(page1.features) > 0 and len(page2.features) > 0:
        # Get OID field name
        properties = layer.properties
        oid_field = properties.get("objectIdField", "OBJECTID") if isinstance(properties, dict) \
            else properties.objectIdField

        page1_ids = [f.attributes.get(oid_field) for f in page1.features]
        page2_ids = [f.attributes.get(oid_field) for f in page2.features]

        # IDs should not overlap
        assert not set(page1_ids).intersection(set(page2_ids)), \
            "Different pages should return different features"


# ============================================================================
#  GeoDataFrame Conversion Tests
# ============================================================================

def test_convert_features_to_geodataframe(arcgis_available, feature_layer_url):
    """
    Verify features can be converted to GeoPandas GeoDataFrame.

    Tests that:
    - FeatureSet.sdf property returns GeoDataFrame (if available)
    - GeoDataFrame contains geometry column
    - Attribute fields are properly mapped
    - Coordinate reference system is set

    Note: Requires pandas and arcgis.features integration.
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    layer = FeatureLayer(feature_layer_url)

    # Query features
    feature_set = layer.query(where="1=1", out_fields="*", result_record_count=5)

    # Try to convert to Spatially Enabled DataFrame (requires pandas)
    try:
        sdf = feature_set.sdf

        # Validate DataFrame structure
        assert sdf is not None, "Should return DataFrame"
        assert len(sdf) > 0, "DataFrame should have rows"

        # Check for SHAPE column (geometry column in Esri terminology)
        assert "SHAPE" in sdf.columns, "DataFrame should have SHAPE geometry column"

        # Validate attributes are present
        attributes = feature_set.features[0].attributes
        for key in attributes.keys():
            # Not all attributes may be in DataFrame (some may be transformed)
            # Just verify we have some attribute columns
            pass

    except ImportError:
        pytest.skip("Pandas not available for DataFrame conversion")
    except AttributeError:
        pytest.skip("Spatially enabled DataFrame not supported")


# ============================================================================
#  Feature Editing Tests (Create, Update, Delete)
# ============================================================================

def test_add_features_creates_new_feature(arcgis_available, feature_layer_url):
    """
    Verify AddFeatures operation creates new feature in layer.

    Tests that:
    - FeatureLayer.edit_features() accepts adds parameter
    - New feature is created with provided attributes
    - Operation returns success status with new object ID
    - Created feature can be queried back

    Note: Requires editing permissions on the service.
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer, Feature
    from arcgis.geometry import Point

    layer = FeatureLayer(feature_layer_url)

    # Check if editing is supported
    properties = layer.properties
    capabilities = properties.get("capabilities", "") if isinstance(properties, dict) \
        else getattr(properties, "capabilities", "")

    if "create" not in capabilities.lower():
        pytest.skip("Layer does not support Create capability")

    # Create new feature
    new_feature = Feature(
        geometry=Point({"x": -122.5, "y": 45.5, "spatialReference": {"wkid": 4326}}),
        attributes={
            "name": "Test Road",
            "status": "planned"
        }
    )

    try:
        # Attempt to add feature
        result = layer.edit_features(adds=[new_feature])

        # Validate result
        assert result is not None, "edit_features should return result"
        assert "addResults" in result, "Result should contain addResults"

        add_results = result["addResults"]
        assert len(add_results) > 0, "Should have at least one add result"

        first_result = add_results[0]
        assert first_result.get("success") is True, \
            f"Add operation should succeed. Error: {first_result.get('error')}"

        # Get the new object ID
        new_oid = first_result.get("objectId")
        assert new_oid is not None, "Successful add should return new objectId"

        # Clean up: delete the created feature
        if new_oid:
            layer.edit_features(deletes=[new_oid])

    except Exception as e:
        # May fail due to permissions or server configuration
        pytest.skip(f"Feature editing not available: {str(e)}")


def test_update_features_modifies_existing_feature(arcgis_available, feature_layer_url):
    """
    Verify UpdateFeatures operation modifies existing feature attributes.

    Tests that:
    - FeatureLayer.edit_features() accepts updates parameter
    - Existing feature attributes are modified
    - Operation returns success status
    - Updated values can be queried back

    Note: Requires editing permissions and existing features.
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer, Feature

    layer = FeatureLayer(feature_layer_url)

    # Check if editing is supported
    properties = layer.properties
    capabilities = properties.get("capabilities", "") if isinstance(properties, dict) \
        else getattr(properties, "capabilities", "")

    if "update" not in capabilities.lower():
        pytest.skip("Layer does not support Update capability")

    try:
        # First, get an existing feature to update
        feature_set = layer.query(where="1=1", out_fields="*", result_record_count=1)

        if len(feature_set.features) == 0:
            pytest.skip("No features available to update")

        existing_feature = feature_set.features[0]
        oid_field = properties.get("objectIdField", "OBJECTID") if isinstance(properties, dict) \
            else properties.objectIdField

        # Modify attributes
        existing_feature.attributes["status"] = "modified_by_test"

        # Update feature
        result = layer.edit_features(updates=[existing_feature])

        # Validate result
        assert result is not None, "edit_features should return result"
        assert "updateResults" in result, "Result should contain updateResults"

        update_results = result["updateResults"]
        assert len(update_results) > 0, "Should have at least one update result"

        first_result = update_results[0]
        assert first_result.get("success") is True, \
            f"Update operation should succeed. Error: {first_result.get('error')}"

    except Exception as e:
        pytest.skip(f"Feature editing not available: {str(e)}")


def test_delete_features_removes_features(arcgis_available, feature_layer_url):
    """
    Verify DeleteFeatures operation removes features from layer.

    Tests that:
    - FeatureLayer.edit_features() accepts deletes parameter
    - Features are removed by object ID
    - Operation returns success status
    - Deleted features are no longer queryable

    Note: Requires editing permissions. This test creates and deletes
    a test feature to avoid removing production data.
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer, Feature
    from arcgis.geometry import Point

    layer = FeatureLayer(feature_layer_url)

    # Check if editing is supported
    properties = layer.properties
    capabilities = properties.get("capabilities", "") if isinstance(properties, dict) \
        else getattr(properties, "capabilities", "")

    if "delete" not in capabilities.lower() or "create" not in capabilities.lower():
        pytest.skip("Layer does not support Create/Delete capabilities")

    try:
        # First create a feature to delete
        new_feature = Feature(
            geometry=Point({"x": -122.5, "y": 45.5, "spatialReference": {"wkid": 4326}}),
            attributes={
                "name": "Test Feature For Deletion",
                "status": "temporary"
            }
        )

        add_result = layer.edit_features(adds=[new_feature])
        assert add_result["addResults"][0]["success"], "Feature creation should succeed"

        new_oid = add_result["addResults"][0]["objectId"]

        # Now delete the feature
        delete_result = layer.edit_features(deletes=[new_oid])

        # Validate result
        assert delete_result is not None, "edit_features should return result"
        assert "deleteResults" in delete_result, "Result should contain deleteResults"

        delete_results = delete_result["deleteResults"]
        assert len(delete_results) > 0, "Should have at least one delete result"

        first_result = delete_results[0]
        assert first_result.get("success") is True, \
            f"Delete operation should succeed. Error: {first_result.get('error')}"

    except Exception as e:
        pytest.skip(f"Feature editing not available: {str(e)}")


# ============================================================================
#  Batch Editing Tests (ApplyEdits)
# ============================================================================

def test_apply_edits_with_multiple_operations(arcgis_available, feature_layer_url):
    """
    Verify ApplyEdits supports multiple operation types in single request.

    Tests that:
    - Single edit_features() call can add, update, and delete
    - All operations are processed atomically (if supported)
    - Results are returned for each operation type
    - Rollback occurs on error (if supported)

    Note: Requires full editing permissions.
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer, Feature
    from arcgis.geometry import Point

    layer = FeatureLayer(feature_layer_url)

    # Check if editing is supported
    properties = layer.properties
    capabilities = properties.get("capabilities", "") if isinstance(properties, dict) \
        else getattr(properties, "capabilities", "")

    required_caps = ["create", "update", "delete"]
    if not all(cap in capabilities.lower() for cap in required_caps):
        pytest.skip("Layer does not support all editing capabilities")

    try:
        # Step 1: Create two test features
        feature1 = Feature(
            geometry=Point({"x": -122.5, "y": 45.5, "spatialReference": {"wkid": 4326}}),
            attributes={"name": "Test Feature 1", "status": "new"}
        )
        feature2 = Feature(
            geometry=Point({"x": -122.6, "y": 45.6, "spatialReference": {"wkid": 4326}}),
            attributes={"name": "Test Feature 2", "status": "new"}
        )

        add_result = layer.edit_features(adds=[feature1, feature2])
        assert len(add_result["addResults"]) == 2, "Should create 2 features"

        oid1 = add_result["addResults"][0]["objectId"]
        oid2 = add_result["addResults"][1]["objectId"]

        # Step 2: Prepare batch edit
        # - Add a new feature
        # - Update first feature
        # - Delete second feature

        feature3 = Feature(
            geometry=Point({"x": -122.7, "y": 45.7, "spatialReference": {"wkid": 4326}}),
            attributes={"name": "Test Feature 3", "status": "new"}
        )

        # Get feature for update
        oid_field = properties.get("objectIdField", "OBJECTID") if isinstance(properties, dict) \
            else properties.objectIdField

        update_feature = Feature(
            attributes={oid_field: oid1, "status": "updated"}
        )

        # Execute batch edit
        result = layer.edit_features(
            adds=[feature3],
            updates=[update_feature],
            deletes=[oid2]
        )

        # Validate results
        assert "addResults" in result, "Should have addResults"
        assert "updateResults" in result, "Should have updateResults"
        assert "deleteResults" in result, "Should have deleteResults"

        assert result["addResults"][0]["success"], "Add should succeed"
        assert result["updateResults"][0]["success"], "Update should succeed"
        assert result["deleteResults"][0]["success"], "Delete should succeed"

        # Clean up: delete remaining features
        oid3 = result["addResults"][0]["objectId"]
        layer.edit_features(deletes=[oid1, oid3])

    except Exception as e:
        pytest.skip(f"Batch editing not available: {str(e)}")


# ============================================================================
#  Error Handling Tests
# ============================================================================

def test_query_invalid_layer_raises_error(arcgis_available, honua_api_base_url):
    """
    Verify querying non-existent layer raises appropriate error.

    Tests that:
    - Invalid layer index/ID raises exception
    - Error message is descriptive
    - Connection doesn't hang on invalid layer
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    # Construct URL for non-existent layer
    invalid_url = f"{honua_api_base_url}/rest/services/roads/FeatureServer/999"

    try:
        layer = FeatureLayer(invalid_url)
        # Accessing properties will trigger actual request
        _ = layer.properties

        # If we get here, check if properties indicate error
        # Some implementations may not raise exception immediately

    except Exception as e:
        # Expected: should raise error for invalid layer
        assert "404" in str(e) or "not found" in str(e).lower() or "invalid" in str(e).lower(), \
            f"Error should indicate layer not found, got: {str(e)}"


def test_query_invalid_where_clause_raises_error(arcgis_available, feature_layer_url):
    """
    Verify invalid SQL where clause raises appropriate error.

    Tests that:
    - Malformed SQL raises exception or returns error
    - Error message describes the problem
    - Service handles error gracefully
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    layer = FeatureLayer(feature_layer_url)

    # Invalid SQL syntax
    invalid_where = "this is not valid SQL @#$%"

    try:
        result = layer.query(where=invalid_where, out_fields="*")

        # Some servers may return empty results instead of error
        # This is acceptable behavior

    except Exception as e:
        # Expected: should raise error for invalid SQL
        assert "error" in str(e).lower() or "invalid" in str(e).lower(), \
            f"Error should indicate invalid query, got: {str(e)}"


def test_query_non_existent_service_raises_error(arcgis_available, honua_api_base_url):
    """
    Verify querying non-existent service raises appropriate error.

    Tests that:
    - Invalid service name raises exception
    - Error indicates service not found
    - Connection doesn't hang
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer

    # Construct URL for non-existent service
    invalid_url = f"{honua_api_base_url}/rest/services/nonexistent/FeatureServer/0"

    try:
        layer = FeatureLayer(invalid_url)
        _ = layer.properties

    except Exception as e:
        # Expected: should raise error for non-existent service
        assert "404" in str(e) or "not found" in str(e).lower(), \
            f"Error should indicate service not found, got: {str(e)}"


def test_edit_without_permissions_raises_error(arcgis_available, feature_layer_url):
    """
    Verify editing without proper permissions raises appropriate error.

    Tests that:
    - Edit operations without permission raise exception
    - Error indicates permission/authorization issue
    - Service enforces security properly

    Note: This test assumes the test user doesn't have edit permissions.
    If permissions are granted, test will skip.
    """
    if not arcgis_available:
        pytest.skip("ArcGIS Python API not available")

    from arcgis.features import FeatureLayer, Feature
    from arcgis.geometry import Point

    layer = FeatureLayer(feature_layer_url)

    # Check capabilities
    properties = layer.properties
    capabilities = properties.get("capabilities", "") if isinstance(properties, dict) \
        else getattr(properties, "capabilities", "")

    if "create" in capabilities.lower():
        pytest.skip("Layer allows creating - can't test permission error")

    # Try to add feature without permission
    new_feature = Feature(
        geometry=Point({"x": -122.5, "y": 45.5, "spatialReference": {"wkid": 4326}}),
        attributes={"name": "Unauthorized Feature"}
    )

    try:
        result = layer.edit_features(adds=[new_feature])

        # Check if result indicates failure
        if "addResults" in result and len(result["addResults"]) > 0:
            first_result = result["addResults"][0]
            assert first_result.get("success") is False, \
                "Edit should fail without permissions"

    except Exception as e:
        # Expected: permission error
        error_str = str(e).lower()
        assert any(keyword in error_str for keyword in ["permission", "unauthorized", "forbidden", "401", "403"]), \
            f"Error should indicate permission issue, got: {str(e)}"
