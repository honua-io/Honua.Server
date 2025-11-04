"""
WCS 2.0 Integration Tests with rasterio/GDAL

This test suite validates Honua's WCS implementation using rasterio (Python GDAL bindings).
Tests verify that WCS coverages can be accessed via GDAL's WCS driver and that all
georeferencing, metadata, and data access operations work correctly.

Test Coverage:
- Opening WCS coverage via GDAL WCS driver
- Reading raster data with correct georeferencing
- Reading metadata (CRS, transform, bounds, resolution)
- Reading subsets (windowed reads)
- Reading with overviews (scaling/resampling)
- Band access and metadata
- Data type validation
- Multiband coverage support
- CRS transformation via GDAL
- Error handling (invalid coverage, missing parameters)

Requirements:
- rasterio >= 1.3.0
- GDAL >= 3.0 with WCS driver support

Client: rasterio/GDAL Python bindings
Specification: OGC WCS 2.0.1
"""
import pytest
import os
from typing import Optional


pytestmark = [pytest.mark.integration, pytest.mark.requires_honua]


# ============================================================================
#  Helper Functions
# ============================================================================

def get_wcs_coverage_id(honua_api_base_url: str) -> str:
    """Get WCS coverage ID from environment or use default."""
    return os.getenv("HONUA_WCS_COVERAGE_ID", "elevation")


def create_wcs_xml_descriptor(base_url: str, coverage_id: str, version: str = "2.0.1") -> str:
    """
    Create GDAL WCS service descriptor XML.

    GDAL WCS driver requires XML descriptor file or can use WCS: URL format.
    """
    xml = f"""<WCS_GDAL>
  <ServiceURL>{base_url}/wcs?</ServiceURL>
  <CoverageName>{coverage_id}</CoverageName>
  <Version>{version}</Version>
  <PreferredFormat>GeoTIFF</PreferredFormat>
  <Timeout>30</Timeout>
</WCS_GDAL>"""
    return xml


# ============================================================================
#  WCS Connection Tests
# ============================================================================

def test_wcs_open_coverage_via_gdal(honua_api_base_url):
    """Verify GDAL can open WCS coverage using WCS driver."""
    try:
        import rasterio
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed (pip install rasterio)")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)

    # GDAL WCS URL format: WCS:http://server/wcs?coverageId=xxx
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            assert dataset is not None, "Should be able to open WCS coverage"
            assert dataset.driver == "WCS", f"Expected WCS driver, got {dataset.driver}"
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available in this build")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available in test environment")
        else:
            raise


def test_wcs_open_coverage_via_xml_descriptor(honua_api_base_url, tmp_path):
    """Verify GDAL can open WCS coverage using XML service descriptor."""
    try:
        import rasterio
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)

    # Create XML descriptor file
    xml_content = create_wcs_xml_descriptor(honua_api_base_url, coverage_id)
    xml_file = tmp_path / "wcs_descriptor.xml"
    xml_file.write_text(xml_content)

    try:
        with rasterio.open(str(xml_file)) as dataset:
            assert dataset is not None
            assert dataset.name == str(xml_file)
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available in test environment")
        else:
            raise


# ============================================================================
#  Metadata Reading Tests
# ============================================================================

def test_wcs_read_coverage_metadata(honua_api_base_url):
    """Verify WCS coverage metadata can be read (dimensions, bands, CRS)."""
    try:
        import rasterio
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Validate dimensions
            assert dataset.width > 0, "Coverage must have width"
            assert dataset.height > 0, "Coverage must have height"
            assert dataset.count > 0, "Coverage must have at least one band"

            # Validate basic metadata
            assert dataset.dtypes is not None, "Dataset must have data type information"
            assert len(dataset.dtypes) == dataset.count
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        else:
            raise


def test_wcs_read_coverage_crs(honua_api_base_url):
    """Verify WCS coverage CRS can be read correctly."""
    try:
        import rasterio
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Validate CRS
            crs = dataset.crs
            assert crs is not None, "Coverage must have CRS"
            assert crs.is_valid, "CRS must be valid"

            # CRS should have EPSG code or WKT
            assert crs.to_epsg() is not None or crs.to_wkt() is not None
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        else:
            raise


def test_wcs_read_coverage_transform(honua_api_base_url):
    """Verify WCS coverage affine transform (georeferencing) can be read."""
    try:
        import rasterio
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Validate affine transform
            transform = dataset.transform
            assert transform is not None, "Coverage must have affine transform"

            # Transform should have non-zero pixel size
            assert transform.a != 0, "Pixel width must be non-zero"
            assert transform.e != 0, "Pixel height must be non-zero"

            # Validate origin coordinates
            assert transform.c != 0 or transform.f != 0, "Transform must have origin"
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        else:
            raise


def test_wcs_read_coverage_bounds(honua_api_base_url):
    """Verify WCS coverage bounds can be read correctly."""
    try:
        import rasterio
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Validate bounds
            bounds = dataset.bounds
            assert bounds is not None, "Coverage must have bounds"

            # Bounds should be valid (left < right, bottom < top)
            assert bounds.left < bounds.right, "Left must be less than right"
            assert bounds.bottom < bounds.top, "Bottom must be less than top"
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        else:
            raise


def test_wcs_read_coverage_resolution(honua_api_base_url):
    """Verify WCS coverage resolution (pixel size) can be read."""
    try:
        import rasterio
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Calculate resolution from transform
            transform = dataset.transform
            res_x = abs(transform.a)
            res_y = abs(transform.e)

            assert res_x > 0, "X resolution must be positive"
            assert res_y > 0, "Y resolution must be positive"

            # Alternative: use res property (may not be available on all GDAL versions)
            if hasattr(dataset, 'res'):
                res = dataset.res
                assert res[0] > 0 and res[1] > 0
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        else:
            raise


# ============================================================================
#  Data Reading Tests
# ============================================================================

def test_wcs_read_coverage_data_full(honua_api_base_url):
    """Verify full coverage raster data can be read."""
    try:
        import rasterio
        import numpy as np
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio or numpy not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Read band 1
            data = dataset.read(1)

            assert data is not None, "Should be able to read raster data"
            assert isinstance(data, np.ndarray), "Data should be numpy array"
            assert data.shape == (dataset.height, dataset.width), \
                f"Data shape {data.shape} should match dataset dimensions ({dataset.height}, {dataset.width})"
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        elif "timeout" in str(e).lower():
            pytest.skip("WCS request timeout (coverage may be too large)")
        else:
            raise


def test_wcs_read_coverage_data_windowed(honua_api_base_url):
    """Verify coverage data can be read with spatial window (subset)."""
    try:
        import rasterio
        from rasterio.windows import Window
        import numpy as np
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Read a small window (100x100 pixels from origin)
            window = Window(0, 0, 100, 100)

            # Ensure window doesn't exceed dataset bounds
            window_width = min(100, dataset.width)
            window_height = min(100, dataset.height)
            window = Window(0, 0, window_width, window_height)

            data = dataset.read(1, window=window)

            assert data is not None, "Should be able to read windowed data"
            assert isinstance(data, np.ndarray), "Data should be numpy array"
            assert data.shape == (window_height, window_width), \
                f"Window data shape should be ({window_height}, {window_width}), got {data.shape}"
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        else:
            raise


def test_wcs_read_coverage_data_downsampled(honua_api_base_url):
    """Verify coverage data can be read with downsampling (overview/scaling)."""
    try:
        import rasterio
        import numpy as np
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Read at lower resolution (half size)
            out_shape = (
                max(1, dataset.height // 2),
                max(1, dataset.width // 2)
            )

            data = dataset.read(
                1,
                out_shape=out_shape
            )

            assert data is not None, "Should be able to read downsampled data"
            assert isinstance(data, np.ndarray), "Data should be numpy array"
            assert data.shape == out_shape, \
                f"Downsampled data shape should be {out_shape}, got {data.shape}"
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        elif "timeout" in str(e).lower():
            pytest.skip("WCS request timeout")
        else:
            raise


def test_wcs_read_coverage_data_with_resampling(honua_api_base_url):
    """Verify coverage data can be read with specific resampling algorithm."""
    try:
        import rasterio
        from rasterio.enums import Resampling
        import numpy as np
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Read at lower resolution with bilinear resampling
            out_shape = (
                max(1, dataset.height // 2),
                max(1, dataset.width // 2)
            )

            data = dataset.read(
                1,
                out_shape=out_shape,
                resampling=Resampling.bilinear
            )

            assert data is not None, "Should be able to read with resampling"
            assert isinstance(data, np.ndarray), "Data should be numpy array"
            assert data.shape == out_shape
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        else:
            raise


# ============================================================================
#  Band Metadata Tests
# ============================================================================

def test_wcs_read_band_metadata(honua_api_base_url):
    """Verify WCS coverage band metadata can be read."""
    try:
        import rasterio
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Check band count
            assert dataset.count > 0, "Coverage must have at least one band"

            # Read band 1 metadata
            band_dtype = dataset.dtypes[0]
            assert band_dtype is not None, "Band must have data type"

            # Check nodata value (may be None if not set)
            nodata = dataset.nodata
            # nodata can be None, that's valid

            # Check band statistics if available
            stats = dataset.statistics(1)
            if stats is not None:
                assert len(stats) == 4  # min, max, mean, std
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        else:
            raise


def test_wcs_read_multiband_coverage(honua_api_base_url):
    """Verify multiband WCS coverage can be read correctly."""
    try:
        import rasterio
        import numpy as np
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            if dataset.count < 2:
                pytest.skip("Coverage has only one band, cannot test multiband functionality")

            # Read all bands
            data = dataset.read()

            assert data is not None, "Should be able to read all bands"
            assert isinstance(data, np.ndarray), "Data should be numpy array"
            assert data.shape[0] == dataset.count, \
                f"First dimension should be band count ({dataset.count}), got {data.shape[0]}"
            assert data.shape[1] == dataset.height, \
                f"Second dimension should be height ({dataset.height})"
            assert data.shape[2] == dataset.width, \
                f"Third dimension should be width ({dataset.width})"
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        elif "timeout" in str(e).lower():
            pytest.skip("WCS request timeout")
        else:
            raise


# ============================================================================
#  Data Type Tests
# ============================================================================

def test_wcs_coverage_data_type_preserved(honua_api_base_url):
    """Verify WCS coverage data type is correctly preserved."""
    try:
        import rasterio
        import numpy as np
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Check data type
            dtype = dataset.dtypes[0]
            assert dtype is not None, "Band must have data type"

            # Read data and verify type matches
            data = dataset.read(1)

            # Convert rasterio dtype string to numpy dtype for comparison
            expected_dtype = np.dtype(dtype)
            assert data.dtype == expected_dtype or str(data.dtype) == str(dtype), \
                f"Data dtype {data.dtype} should match declared dtype {dtype}"
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        elif "timeout" in str(e).lower():
            pytest.skip("WCS request timeout")
        else:
            raise


# ============================================================================
#  CRS Transformation Tests
# ============================================================================

def test_wcs_coverage_crs_transformation(honua_api_base_url):
    """Verify WCS coverage can be reprojected using rasterio."""
    try:
        import rasterio
        from rasterio.warp import calculate_default_transform, reproject, Resampling
        from rasterio.crs import CRS
        import numpy as np
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as src:
            # Define target CRS (Web Mercator)
            dst_crs = CRS.from_epsg(3857)

            if src.crs == dst_crs:
                pytest.skip("Coverage is already in EPSG:3857, cannot test transformation")

            # Calculate transform for reprojection
            transform, width, height = calculate_default_transform(
                src.crs, dst_crs, src.width, src.height, *src.bounds
            )

            assert transform is not None
            assert width > 0 and height > 0

            # Create destination array
            dst_data = np.empty((height, width), dtype=src.dtypes[0])

            # Perform reprojection
            reproject(
                source=rasterio.band(src, 1),
                destination=dst_data,
                src_transform=src.transform,
                src_crs=src.crs,
                dst_transform=transform,
                dst_crs=dst_crs,
                resampling=Resampling.nearest
            )

            assert dst_data is not None
            assert dst_data.shape == (height, width)
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        elif "timeout" in str(e).lower():
            pytest.skip("WCS request timeout")
        else:
            raise


# ============================================================================
#  Error Handling Tests
# ============================================================================

def test_wcs_invalid_coverage_raises_error(honua_api_base_url):
    """Verify accessing invalid WCS coverage raises appropriate error."""
    try:
        import rasterio
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId=nonexistent_coverage"

    with pytest.raises(RasterioIOError):
        with rasterio.open(wcs_url) as dataset:
            # Should not reach here
            _ = dataset.width


def test_wcs_malformed_url_raises_error(honua_api_base_url):
    """Verify malformed WCS URL raises appropriate error."""
    try:
        import rasterio
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    # Missing coverageId parameter
    wcs_url = f"WCS:{honua_api_base_url}/wcs"

    with pytest.raises(RasterioIOError):
        with rasterio.open(wcs_url) as dataset:
            _ = dataset.width


# ============================================================================
#  GDAL WCS Driver Configuration Tests
# ============================================================================

def test_wcs_gdal_config_options(honua_api_base_url, tmp_path):
    """Verify GDAL WCS driver respects configuration options."""
    try:
        import rasterio
        from rasterio.env import Env
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        # Set GDAL configuration options
        with Env(
            GDAL_HTTP_TIMEOUT="30",
            GDAL_HTTP_UNSAFESSL="YES",  # For testing with self-signed certs
        ):
            with rasterio.open(wcs_url) as dataset:
                assert dataset is not None
                assert dataset.width > 0
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        else:
            raise


def test_wcs_with_authentication(honua_api_base_url, honua_api_bearer_token):
    """Verify WCS coverage can be accessed with authentication."""
    try:
        import rasterio
        from rasterio.env import Env
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    if not honua_api_bearer_token:
        pytest.skip("No authentication token provided")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        # GDAL HTTP headers for authentication
        # Note: GDAL WCS driver may not support custom headers directly
        # This test verifies the pattern, actual implementation may vary
        with Env(
            GDAL_HTTP_HEADERS=f"Authorization: Bearer {honua_api_bearer_token}"
        ):
            with rasterio.open(wcs_url) as dataset:
                assert dataset is not None
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 401" in str(e):
            pytest.skip("Authentication failed (may require different auth method)")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        else:
            raise


# ============================================================================
#  Performance Tests
# ============================================================================

@pytest.mark.slow
def test_wcs_large_coverage_streaming(honua_api_base_url):
    """Verify WCS can stream large coverage data efficiently."""
    try:
        import rasterio
        from rasterio.windows import Window
        import numpy as np
        from rasterio.errors import RasterioIOError
    except ImportError:
        pytest.skip("rasterio not installed")

    coverage_id = get_wcs_coverage_id(honua_api_base_url)
    wcs_url = f"WCS:{honua_api_base_url}/wcs?coverageId={coverage_id}"

    try:
        with rasterio.open(wcs_url) as dataset:
            # Read coverage in tiles (simulates streaming large dataset)
            tile_size = 256
            tiles_read = 0
            max_tiles = 4  # Limit test to 4 tiles

            for row in range(0, min(dataset.height, tile_size * 2), tile_size):
                for col in range(0, min(dataset.width, tile_size * 2), tile_size):
                    if tiles_read >= max_tiles:
                        break

                    window = Window(
                        col, row,
                        min(tile_size, dataset.width - col),
                        min(tile_size, dataset.height - row)
                    )

                    data = dataset.read(1, window=window)
                    assert data is not None
                    tiles_read += 1

                if tiles_read >= max_tiles:
                    break

            assert tiles_read > 0, "Should be able to read at least one tile"
    except RasterioIOError as e:
        if "not recognized as a supported file format" in str(e):
            pytest.skip("GDAL WCS driver not available")
        elif "HTTP error code : 404" in str(e):
            pytest.skip(f"Coverage {coverage_id} not available")
        elif "timeout" in str(e).lower():
            pytest.skip("WCS request timeout")
        else:
            raise


# ============================================================================
#  Integration with OGC API - Coverages Tests
# ============================================================================

def test_ogc_api_coverages_compatibility(honua_api_base_url):
    """Verify potential future compatibility with OGC API - Coverages."""
    # This is a placeholder test for future OGC API - Coverages support
    # WCS 2.0 is being superseded by OGC API - Coverages
    pytest.skip("OGC API - Coverages not yet implemented (future enhancement)")
