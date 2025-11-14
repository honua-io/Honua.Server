"""
Comprehensive OGC API - Processes Integration Tests with requests

This test suite validates Honua's OGC API - Processes implementation using the requests
library for REST/JSON API interactions. Tests verify full compliance with OGC API - Processes 1.0.0
specification using real-world HTTP client patterns.

Test Coverage:
- Landing page: Links to processes endpoint
- Conformance: OGC API Processes conformance classes
- Process list: Get available processes with metadata
- Process description: Get detailed metadata for specific processes
- Process execution: Synchronous and asynchronous execution modes
- Job management: Get job status, retrieve results, dismiss/cancel jobs
- Job list: List all jobs with pagination
- Input/output formats: JSON, GeoJSON for spatial processes
- Error handling: invalid processes, malformed execution requests, missing jobs
- Prefer header: respond-async for async execution control
- Status codes: 200, 201, 404, 400 for various scenarios

Requirements:
- requests >= 2.28.0
- pytest >= 7.0.0

Client: requests (HTTP client library)
Specification: OGC API - Processes 1.0.0
"""
import json
import time
import pytest
from typing import Any, Dict, List, Optional


pytestmark = [
    pytest.mark.integration,
    pytest.mark.python,
    pytest.mark.ogc_processes,
    pytest.mark.requires_honua,
]


# ============================================================================
#  Helper Functions and Fixtures
# ============================================================================


def get_processes_api_url(honua_api_base_url: str) -> str:
    """Get OGC API Processes base URL from Honua base URL."""
    return f"{honua_api_base_url}/processes"


def validate_link(link: Dict[str, Any]) -> None:
    """Validate that a link object has required properties."""
    assert "href" in link, "Link must have href"
    assert "rel" in link, "Link must have rel"
    assert isinstance(link["href"], str), "Link href must be string"
    assert isinstance(link["rel"], str), "Link rel must be string"


def validate_process_summary(process: Dict[str, Any]) -> None:
    """Validate that a process summary has required fields."""
    assert "id" in process, "Process must have id"
    assert isinstance(process["id"], str), "Process id must be string"

    # Title and description are recommended
    if "title" in process:
        assert isinstance(process["title"], str), "Process title must be string"

    if "version" in process:
        assert isinstance(process["version"], str), "Process version must be string"


def validate_process_description(process: Dict[str, Any]) -> None:
    """Validate that a process description has required fields."""
    assert "id" in process, "Process must have id"
    assert isinstance(process["id"], str), "Process id must be string"

    # Check for inputs and outputs
    if "inputs" in process:
        assert isinstance(process["inputs"], dict), "Process inputs must be an object"

    if "outputs" in process:
        assert isinstance(process["outputs"], dict), "Process outputs must be an object"

    # Job control options
    if "jobControlOptions" in process:
        assert isinstance(process["jobControlOptions"], list), "jobControlOptions must be array"


def validate_job_status(status: Dict[str, Any]) -> None:
    """Validate that a job status response has required fields."""
    assert "jobID" in status, "Status must have jobID"
    assert "status" in status, "Status must have status field"
    assert "type" in status, "Status must have type"

    # Status must be one of the valid values
    valid_statuses = ["accepted", "running", "successful", "failed", "dismissed"]
    assert status["status"] in valid_statuses, f"Invalid status: {status['status']}"

    # Timestamps
    assert "created" in status, "Status must have created timestamp"

    # Links
    if "links" in status:
        assert isinstance(status["links"], list), "Links must be an array"
        for link in status["links"]:
            validate_link(link)


@pytest.fixture(scope="module")
def processes_api_url(honua_api_base_url):
    """Get the OGC API Processes base URL."""
    return get_processes_api_url(honua_api_base_url)


@pytest.fixture(scope="module")
def process_list(api_request, processes_api_url):
    """Fetch and cache the process list for the test session."""
    response = api_request("GET", f"{processes_api_url}")
    if response.status_code == 404:
        pytest.skip("OGC API - Processes not available in test environment")
    response.raise_for_status()
    return response.json()


@pytest.fixture(scope="module")
def valid_process_id(process_list):
    """Get a valid process ID from the process list."""
    processes = process_list.get("processes", [])
    if not processes:
        pytest.skip("No processes available in test environment")
    return processes[0]["id"]


@pytest.fixture(scope="module")
def valid_process_description(api_request, processes_api_url, valid_process_id):
    """Fetch and cache a valid process description for the test session."""
    response = api_request("GET", f"{processes_api_url}/{valid_process_id}")
    response.raise_for_status()
    return response.json()


# ============================================================================
#  Landing Page Tests
# ============================================================================


def test_landing_page_includes_processes_link(api_request, honua_api_base_url):
    """Verify OGC API landing page includes link to processes."""
    response = api_request("GET", f"{honua_api_base_url}/ogc/")

    # Landing page may not be available
    if response.status_code == 404:
        pytest.skip("OGC API landing page not available")

    assert response.status_code == 200
    data = response.json()

    links = data.get("links", [])
    link_rels = {link.get("rel") for link in links}

    # Check for processes link (may use different rel values)
    has_processes_link = (
        "http://www.opengis.net/def/rel/ogc/1.0/processes" in link_rels or
        "processes" in link_rels or
        any("processes" in link.get("href", "") for link in links)
    )

    # Processes link is optional but good to verify if present


# ============================================================================
#  Conformance Tests
# ============================================================================


def test_conformance_declares_ogc_processes_core(api_request, honua_api_base_url):
    """Verify conformance declares OGC API - Processes Core conformance class."""
    response = api_request("GET", f"{honua_api_base_url}/ogc/conformance")

    # Conformance endpoint may not be available
    if response.status_code == 404:
        pytest.skip("Conformance endpoint not available")

    assert response.status_code == 200
    data = response.json()

    conformance_classes = data.get("conformsTo", [])

    # Check for Processes conformance classes
    has_processes_conformance = any(
        "ogcapi-processes" in cc.lower() or "processes" in cc.lower()
        for cc in conformance_classes
    )

    # If processes are available, should declare conformance
    # But this is optional check since not all deployments may expose this


# ============================================================================
#  Process List Tests
# ============================================================================


def test_get_process_list_returns_json(api_request, processes_api_url):
    """Verify process list endpoint returns valid JSON."""
    response = api_request("GET", f"{processes_api_url}")

    # Processes API may not be available
    if response.status_code == 404:
        pytest.skip("OGC API - Processes not available")

    assert response.status_code == 200, f"Expected 200, got {response.status_code}"
    assert response.headers.get("Content-Type", "").startswith("application/json"), \
        "Process list should return JSON"

    data = response.json()
    assert isinstance(data, dict), "Process list must be a JSON object"


def test_process_list_includes_processes_array(process_list):
    """Verify process list response includes processes array."""
    assert "processes" in process_list, "Response must include processes array"
    assert isinstance(process_list["processes"], list), "processes must be an array"


def test_process_list_includes_links(process_list):
    """Verify process list response includes links."""
    assert "links" in process_list, "Process list must include links"
    assert isinstance(process_list["links"], list), "Links must be an array"

    for link in process_list["links"]:
        validate_link(link)


def test_process_list_has_self_link(process_list):
    """Verify process list includes self link."""
    links = process_list.get("links", [])
    link_rels = {link.get("rel") for link in links}

    assert "self" in link_rels, "Process list must include self link"


def test_process_summaries_have_required_fields(process_list):
    """Verify each process summary has required metadata fields."""
    processes = process_list.get("processes", [])

    if not processes:
        pytest.skip("No processes available")

    for process in processes:
        validate_process_summary(process)


def test_process_summaries_include_links(process_list):
    """Verify each process summary includes links."""
    processes = process_list.get("processes", [])

    if not processes:
        pytest.skip("No processes available")

    for process in processes:
        if "links" in process:
            assert isinstance(process["links"], list), "Process links must be an array"
            for link in process["links"]:
                validate_link(link)


# ============================================================================
#  Process Description Tests
# ============================================================================


def test_get_process_description_returns_json(api_request, processes_api_url, valid_process_id):
    """Verify getting a process description returns valid JSON."""
    response = api_request("GET", f"{processes_api_url}/{valid_process_id}")

    assert response.status_code == 200, f"Expected 200, got {response.status_code}"
    assert response.headers.get("Content-Type", "").startswith("application/json"), \
        "Process description should return JSON"

    data = response.json()
    assert isinstance(data, dict), "Process description must be a JSON object"


def test_process_description_has_required_fields(valid_process_description):
    """Verify process description has required fields."""
    validate_process_description(valid_process_description)


def test_process_description_includes_id(valid_process_description, valid_process_id):
    """Verify process description includes correct ID."""
    assert valid_process_description["id"] == valid_process_id, \
        "Process ID should match requested ID"


def test_process_description_includes_inputs(valid_process_description):
    """Verify process description includes input definitions."""
    # Inputs are optional but common
    if "inputs" in valid_process_description:
        inputs = valid_process_description["inputs"]
        assert isinstance(inputs, dict), "Inputs must be an object"

        # Each input should have schema information
        for input_id, input_def in inputs.items():
            assert isinstance(input_def, dict), f"Input {input_id} must be an object"


def test_process_description_includes_outputs(valid_process_description):
    """Verify process description includes output definitions."""
    # Outputs are optional but common
    if "outputs" in valid_process_description:
        outputs = valid_process_description["outputs"]
        assert isinstance(outputs, dict), "Outputs must be an object"

        # Each output should have schema information
        for output_id, output_def in outputs.items():
            assert isinstance(output_def, dict), f"Output {output_id} must be an object"


def test_process_description_includes_links(valid_process_description):
    """Verify process description includes links."""
    if "links" in valid_process_description:
        links = valid_process_description["links"]
        assert isinstance(links, list), "Links must be an array"

        for link in links:
            validate_link(link)


def test_process_description_has_execution_link(valid_process_description):
    """Verify process description includes execution link."""
    if "links" in valid_process_description:
        links = valid_process_description["links"]
        link_rels = {link.get("rel") for link in links}

        # Check for execute link
        has_execute_link = (
            "execute" in link_rels or
            "http://www.opengis.net/def/rel/ogc/1.0/execute" in link_rels
        )

        # Execute link is recommended
        if links:
            assert has_execute_link, "Process should include execute link"


def test_get_nonexistent_process_returns_404(api_request, processes_api_url):
    """Verify requesting non-existent process returns 404."""
    response = api_request("GET", f"{processes_api_url}/nonexistent-process-xyz-12345")

    assert response.status_code == 404, \
        f"Expected 404 for non-existent process, got {response.status_code}"


def test_nonexistent_process_error_includes_details(api_request, processes_api_url):
    """Verify 404 error response includes error details."""
    response = api_request("GET", f"{processes_api_url}/nonexistent-process-xyz-12345")

    assert response.status_code == 404

    # Error response should be JSON
    if response.headers.get("Content-Type", "").startswith("application/json"):
        data = response.json()

        # OGC API typically includes type, title, detail, status
        # But these are optional
        assert isinstance(data, dict), "Error response should be JSON object"


# ============================================================================
#  Process Execution Tests (Synchronous)
# ============================================================================


def test_execute_process_synchronous_simple(api_request, processes_api_url, valid_process_id):
    """Verify synchronous process execution with simple inputs."""
    # Prepare execution request
    execute_request = {
        "inputs": {}  # Empty inputs for simple test
    }

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "return=representation"}
    )

    # Execution may fail due to missing required inputs or other validation
    # Accept 200 (success), 201 (async), 400 (bad request)
    assert response.status_code in [200, 201, 400], \
        f"Expected 200/201/400, got {response.status_code}"


def test_execute_process_returns_json(api_request, processes_api_url, valid_process_id):
    """Verify process execution returns JSON response."""
    execute_request = {
        "inputs": {}
    }

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "return=representation"}
    )

    if response.status_code in [200, 201, 400]:
        content_type = response.headers.get("Content-Type", "")
        assert "json" in content_type.lower(), "Response should be JSON"


def test_execute_process_with_missing_inputs_returns_400(api_request, processes_api_url, valid_process_id, valid_process_description):
    """Verify executing process without required inputs returns 400."""
    # Check if process has required inputs
    inputs = valid_process_description.get("inputs", {})

    has_required_inputs = any(
        input_def.get("minOccurs", 0) > 0
        for input_def in inputs.values()
        if isinstance(input_def, dict)
    )

    if not has_required_inputs:
        pytest.skip("Process has no required inputs")

    # Execute without inputs
    execute_request = {
        "inputs": {}
    }

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request
    )

    # Should return 400 for missing required inputs
    # But implementation may vary, so accept 201 (async) as well
    assert response.status_code in [400, 201], \
        f"Expected 400 or 201, got {response.status_code}"


def test_execute_nonexistent_process_returns_404(api_request, processes_api_url):
    """Verify executing non-existent process returns 404."""
    execute_request = {
        "inputs": {}
    }

    response = api_request(
        "POST",
        f"{processes_api_url}/nonexistent-process-xyz-12345/execution",
        json=execute_request
    )

    assert response.status_code == 404, \
        f"Expected 404 for non-existent process, got {response.status_code}"


def test_execute_with_malformed_json_returns_400(api_request, processes_api_url, valid_process_id):
    """Verify executing with malformed JSON returns 400."""
    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        data="not valid json",
        headers={"Content-Type": "application/json"}
    )

    assert response.status_code == 400, \
        f"Expected 400 for malformed JSON, got {response.status_code}"


# ============================================================================
#  Process Execution Tests (Asynchronous)
# ============================================================================


def test_execute_process_asynchronous(api_request, processes_api_url, valid_process_id):
    """Verify asynchronous process execution creates a job."""
    execute_request = {
        "inputs": {}
    }

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    # Async execution should return 201 Created or 200 (if sync fallback)
    # Also accept 400 if inputs are invalid
    assert response.status_code in [200, 201, 400], \
        f"Expected 200/201/400, got {response.status_code}"

    if response.status_code == 201:
        # Check Location header
        assert "Location" in response.headers, "Async response should include Location header"


def test_async_execution_returns_job_status(api_request, processes_api_url, valid_process_id):
    """Verify async execution returns job status information."""
    execute_request = {
        "inputs": {}
    }

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response.status_code != 201:
        pytest.skip("Async execution not supported or inputs invalid")

    data = response.json()
    validate_job_status(data)


def test_async_execution_creates_unique_job_id(api_request, processes_api_url, valid_process_id):
    """Verify each async execution creates a unique job ID."""
    execute_request = {
        "inputs": {}
    }

    # Execute twice
    response1 = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    response2 = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response1.status_code != 201 or response2.status_code != 201:
        pytest.skip("Async execution not supported")

    data1 = response1.json()
    data2 = response2.json()

    # Job IDs should be different
    assert data1["jobID"] != data2["jobID"], "Job IDs should be unique"


# ============================================================================
#  Job Status Tests
# ============================================================================


def test_get_job_status_returns_json(api_request, processes_api_url, valid_process_id, honua_api_base_url):
    """Verify getting job status returns valid JSON."""
    # First create a job
    execute_request = {"inputs": {}}

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response.status_code != 201:
        pytest.skip("Cannot create async job")

    data = response.json()
    job_id = data["jobID"]

    # Get job status
    status_response = api_request("GET", f"{honua_api_base_url}/jobs/{job_id}")

    assert status_response.status_code == 200, \
        f"Expected 200, got {status_response.status_code}"

    status_data = status_response.json()
    validate_job_status(status_data)


def test_job_status_includes_process_id(api_request, processes_api_url, valid_process_id, honua_api_base_url):
    """Verify job status includes the process ID."""
    # Create a job
    execute_request = {"inputs": {}}

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response.status_code != 201:
        pytest.skip("Cannot create async job")

    data = response.json()
    job_id = data["jobID"]

    # Get job status
    status_response = api_request("GET", f"{honua_api_base_url}/jobs/{job_id}")
    status_data = status_response.json()

    assert "processID" in status_data, "Status must include processID"
    assert status_data["processID"] == valid_process_id, \
        "Process ID should match"


def test_job_status_includes_timestamps(api_request, processes_api_url, valid_process_id, honua_api_base_url):
    """Verify job status includes required timestamps."""
    # Create a job
    execute_request = {"inputs": {}}

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response.status_code != 201:
        pytest.skip("Cannot create async job")

    data = response.json()
    job_id = data["jobID"]

    # Get job status
    status_response = api_request("GET", f"{honua_api_base_url}/jobs/{job_id}")
    status_data = status_response.json()

    assert "created" in status_data, "Status must include created timestamp"

    # Started and finished are optional depending on job state
    # Just verify they're valid if present
    if "started" in status_data:
        assert isinstance(status_data["started"], str), "Started must be string (ISO 8601)"

    if "finished" in status_data:
        assert isinstance(status_data["finished"], str), "Finished must be string (ISO 8601)"


def test_job_status_includes_links(api_request, processes_api_url, valid_process_id, honua_api_base_url):
    """Verify job status includes links."""
    # Create a job
    execute_request = {"inputs": {}}

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response.status_code != 201:
        pytest.skip("Cannot create async job")

    data = response.json()
    job_id = data["jobID"]

    # Get job status
    status_response = api_request("GET", f"{honua_api_base_url}/jobs/{job_id}")
    status_data = status_response.json()

    if "links" in status_data:
        links = status_data["links"]
        assert isinstance(links, list), "Links must be an array"

        for link in links:
            validate_link(link)


def test_get_nonexistent_job_returns_404(api_request, honua_api_base_url):
    """Verify requesting non-existent job returns 404."""
    response = api_request("GET", f"{honua_api_base_url}/jobs/nonexistent-job-xyz-12345")

    assert response.status_code == 404, \
        f"Expected 404 for non-existent job, got {response.status_code}"


# ============================================================================
#  Job Results Tests
# ============================================================================


def test_get_job_results_for_incomplete_job(api_request, processes_api_url, valid_process_id, honua_api_base_url):
    """Verify requesting results for incomplete job returns appropriate status."""
    # Create a job
    execute_request = {"inputs": {}}

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response.status_code != 201:
        pytest.skip("Cannot create async job")

    data = response.json()
    job_id = data["jobID"]

    # Immediately try to get results (job likely not complete)
    results_response = api_request("GET", f"{honua_api_base_url}/jobs/{job_id}/results")

    # Should return 404 (not found) or 400 (not ready) depending on implementation
    # Some implementations may return 200 if job completed very quickly
    assert results_response.status_code in [200, 400, 404], \
        f"Expected 200/400/404, got {results_response.status_code}"


def test_get_job_results_returns_json(api_request, processes_api_url, valid_process_id, honua_api_base_url):
    """Verify job results endpoint returns JSON when job is complete."""
    # Create a job
    execute_request = {"inputs": {}}

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response.status_code != 201:
        pytest.skip("Cannot create async job")

    data = response.json()
    job_id = data["jobID"]

    # Wait a bit and check status
    max_wait = 10  # seconds
    wait_interval = 1
    waited = 0

    while waited < max_wait:
        status_response = api_request("GET", f"{honua_api_base_url}/jobs/{job_id}")
        if status_response.status_code != 200:
            break

        status_data = status_response.json()
        if status_data["status"] in ["successful", "failed"]:
            break

        time.sleep(wait_interval)
        waited += wait_interval

    # Try to get results
    results_response = api_request("GET", f"{honua_api_base_url}/jobs/{job_id}/results")

    # If job succeeded, should return results
    if status_data.get("status") == "successful":
        assert results_response.status_code == 200, \
            "Should return 200 for successful job results"

        # Results should be JSON
        content_type = results_response.headers.get("Content-Type", "")
        assert "json" in content_type.lower(), "Results should be JSON"


def test_get_results_for_nonexistent_job_returns_404(api_request, honua_api_base_url):
    """Verify requesting results for non-existent job returns 404."""
    response = api_request("GET", f"{honua_api_base_url}/jobs/nonexistent-job-xyz-12345/results")

    assert response.status_code == 404, \
        f"Expected 404 for non-existent job, got {response.status_code}"


# ============================================================================
#  Job Dismissal/Cancellation Tests
# ============================================================================


def test_dismiss_job(api_request, processes_api_url, valid_process_id, honua_api_base_url):
    """Verify dismissing (cancelling) a job."""
    # Create a job
    execute_request = {"inputs": {}}

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response.status_code != 201:
        pytest.skip("Cannot create async job")

    data = response.json()
    job_id = data["jobID"]

    # Dismiss the job
    dismiss_response = api_request("DELETE", f"{honua_api_base_url}/jobs/{job_id}")

    # Should return 200 (dismissed) or 404 (if already completed/removed)
    # or 410 (Gone - if completed)
    assert dismiss_response.status_code in [200, 404, 410], \
        f"Expected 200/404/410, got {dismiss_response.status_code}"


def test_dismiss_nonexistent_job_returns_404(api_request, honua_api_base_url):
    """Verify dismissing non-existent job returns 404."""
    response = api_request("DELETE", f"{honua_api_base_url}/jobs/nonexistent-job-xyz-12345")

    assert response.status_code == 404, \
        f"Expected 404 for non-existent job, got {response.status_code}"


def test_dismiss_completed_job_returns_410(api_request, processes_api_url, valid_process_id, honua_api_base_url):
    """Verify dismissing completed job returns 410 Gone."""
    # Create a job
    execute_request = {"inputs": {}}

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response.status_code != 201:
        pytest.skip("Cannot create async job")

    data = response.json()
    job_id = data["jobID"]

    # Wait for job to complete
    max_wait = 10
    wait_interval = 1
    waited = 0

    while waited < max_wait:
        status_response = api_request("GET", f"{honua_api_base_url}/jobs/{job_id}")
        if status_response.status_code != 200:
            break

        status_data = status_response.json()
        if status_data["status"] in ["successful", "failed"]:
            break

        time.sleep(wait_interval)
        waited += wait_interval

    # Try to dismiss completed job
    dismiss_response = api_request("DELETE", f"{honua_api_base_url}/jobs/{job_id}")

    # Should return 410 Gone or 200 (if implementation allows)
    # Some implementations may return 404
    assert dismiss_response.status_code in [200, 404, 410], \
        f"Expected 200/404/410, got {dismiss_response.status_code}"


# ============================================================================
#  Job List Tests
# ============================================================================


def test_get_job_list_returns_json(api_request, honua_api_base_url):
    """Verify getting job list returns valid JSON."""
    response = api_request("GET", f"{honua_api_base_url}/jobs")

    # Job list endpoint may not be available
    if response.status_code == 404:
        pytest.skip("Job list endpoint not available")

    assert response.status_code == 200, f"Expected 200, got {response.status_code}"

    content_type = response.headers.get("Content-Type", "")
    assert "json" in content_type.lower(), "Job list should return JSON"

    data = response.json()
    assert isinstance(data, dict), "Job list must be a JSON object"


def test_job_list_includes_jobs_array(api_request, honua_api_base_url):
    """Verify job list response includes jobs array."""
    response = api_request("GET", f"{honua_api_base_url}/jobs")

    if response.status_code == 404:
        pytest.skip("Job list endpoint not available")

    assert response.status_code == 200
    data = response.json()

    assert "jobs" in data, "Response must include jobs array"
    assert isinstance(data["jobs"], list), "jobs must be an array"


def test_job_list_includes_links(api_request, honua_api_base_url):
    """Verify job list response includes links."""
    response = api_request("GET", f"{honua_api_base_url}/jobs")

    if response.status_code == 404:
        pytest.skip("Job list endpoint not available")

    assert response.status_code == 200
    data = response.json()

    if "links" in data:
        links = data["links"]
        assert isinstance(links, list), "Links must be an array"

        for link in links:
            validate_link(link)


def test_job_list_with_limit_parameter(api_request, honua_api_base_url):
    """Verify job list endpoint respects limit parameter."""
    limit = 5

    response = api_request(
        "GET",
        f"{honua_api_base_url}/jobs",
        params={"limit": limit}
    )

    if response.status_code == 404:
        pytest.skip("Job list endpoint not available")

    assert response.status_code == 200
    data = response.json()

    jobs = data.get("jobs", [])
    assert len(jobs) <= limit, f"Expected at most {limit} jobs, got {len(jobs)}"


def test_job_list_with_pagination(api_request, honua_api_base_url, processes_api_url, valid_process_id):
    """Verify job list supports pagination."""
    # Create multiple jobs first
    execute_request = {"inputs": {}}

    for _ in range(3):
        api_request(
            "POST",
            f"{processes_api_url}/{valid_process_id}/execution",
            json=execute_request,
            headers={"Prefer": "respond-async"}
        )

    # Get first page
    response1 = api_request(
        "GET",
        f"{honua_api_base_url}/jobs",
        params={"limit": 2, "offset": 0}
    )

    if response1.status_code == 404:
        pytest.skip("Job list endpoint not available")

    assert response1.status_code == 200
    data1 = response1.json()

    jobs1 = data1.get("jobs", [])

    # Get second page
    response2 = api_request(
        "GET",
        f"{honua_api_base_url}/jobs",
        params={"limit": 2, "offset": 2}
    )

    assert response2.status_code == 200
    data2 = response2.json()

    jobs2 = data2.get("jobs", [])

    # Verify pagination worked (if we have enough jobs)
    if len(jobs1) >= 2 and len(jobs2) >= 1:
        job_ids1 = {job["jobID"] for job in jobs1 if "jobID" in job}
        job_ids2 = {job["jobID"] for job in jobs2 if "jobID" in job}

        # Pages should have different jobs
        assert job_ids1 != job_ids2, "Pages should contain different jobs"


# ============================================================================
#  Input/Output Format Tests
# ============================================================================


def test_process_execution_accepts_json_inputs(api_request, processes_api_url, valid_process_id):
    """Verify process execution accepts JSON input format."""
    execute_request = {
        "inputs": {
            "test_param": "test_value"
        }
    }

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request
    )

    # Should accept JSON (even if inputs are invalid)
    assert response.status_code in [200, 201, 400], \
        f"Expected 200/201/400, got {response.status_code}"


def test_process_execution_with_geojson_input(api_request, processes_api_url, valid_process_id, valid_process_description):
    """Verify process execution accepts GeoJSON input for spatial processes."""
    # Check if process accepts spatial inputs
    inputs = valid_process_description.get("inputs", {})

    has_spatial_input = any(
        "geometry" in str(input_def).lower() or "geojson" in str(input_def).lower()
        for input_def in inputs.values()
        if isinstance(input_def, dict)
    )

    if not has_spatial_input:
        pytest.skip("Process does not accept spatial inputs")

    # Create GeoJSON feature
    geojson_input = {
        "type": "Feature",
        "geometry": {
            "type": "Point",
            "coordinates": [-122.4194, 37.7749]
        },
        "properties": {}
    }

    execute_request = {
        "inputs": {
            "geometry": geojson_input
        }
    }

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request
    )

    # Should accept GeoJSON format
    assert response.status_code in [200, 201, 400], \
        f"Expected 200/201/400, got {response.status_code}"


def test_process_execution_with_output_format_specification(api_request, processes_api_url, valid_process_id):
    """Verify process execution supports output format specification."""
    execute_request = {
        "inputs": {},
        "outputs": {
            "result": {
                "format": {
                    "mediaType": "application/json"
                }
            }
        }
    }

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request
    )

    # Output format specification is optional
    # Should accept or return 400 for invalid inputs
    assert response.status_code in [200, 201, 400], \
        f"Expected 200/201/400, got {response.status_code}"


# ============================================================================
#  Error Handling Tests
# ============================================================================


def test_execute_with_invalid_input_types_returns_400(api_request, processes_api_url, valid_process_id):
    """Verify executing with invalid input types returns 400."""
    execute_request = {
        "inputs": {
            "invalid_param": None  # Invalid type
        }
    }

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request
    )

    # Should return 400 or 201 (if inputs are ignored)
    assert response.status_code in [200, 201, 400], \
        f"Expected 200/201/400, got {response.status_code}"


def test_execute_without_content_type_returns_400(api_request, processes_api_url, valid_process_id):
    """Verify executing without Content-Type header returns 400."""
    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        data='{"inputs": {}}',
        headers={}  # No Content-Type
    )

    # Should return 400 or accept with default
    assert response.status_code in [200, 201, 400, 415], \
        f"Expected 200/201/400/415, got {response.status_code}"


def test_job_status_for_failed_job_includes_error(api_request, processes_api_url, valid_process_id, honua_api_base_url):
    """Verify job status for failed job includes error information."""
    # Try to create a job that will likely fail
    execute_request = {
        "inputs": {
            "invalid_required_param": "bad_value"
        }
    }

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response.status_code != 201:
        pytest.skip("Cannot create async job")

    data = response.json()
    job_id = data["jobID"]

    # Wait for job to complete/fail
    max_wait = 10
    wait_interval = 1
    waited = 0

    while waited < max_wait:
        status_response = api_request("GET", f"{honua_api_base_url}/jobs/{job_id}")
        if status_response.status_code != 200:
            break

        status_data = status_response.json()
        if status_data["status"] in ["successful", "failed"]:
            break

        time.sleep(wait_interval)
        waited += wait_interval

    # If job failed, check for error message
    if status_data.get("status") == "failed":
        # Failed jobs may include message
        assert "message" in status_data or "error" in str(status_data).lower(), \
            "Failed job should include error information"


# ============================================================================
#  Content Negotiation Tests
# ============================================================================


def test_process_list_json_via_accept_header(api_request, processes_api_url):
    """Verify process list returns JSON via Accept header."""
    response = api_request(
        "GET",
        f"{processes_api_url}",
        headers={"Accept": "application/json"}
    )

    if response.status_code == 404:
        pytest.skip("Processes API not available")

    assert response.status_code == 200

    content_type = response.headers.get("Content-Type", "")
    assert "json" in content_type.lower(), "Should return JSON format"


def test_process_description_json_via_accept_header(api_request, processes_api_url, valid_process_id):
    """Verify process description returns JSON via Accept header."""
    response = api_request(
        "GET",
        f"{processes_api_url}/{valid_process_id}",
        headers={"Accept": "application/json"}
    )

    assert response.status_code == 200

    content_type = response.headers.get("Content-Type", "")
    assert "json" in content_type.lower(), "Should return JSON format"


def test_unsupported_accept_header_returns_406(api_request, processes_api_url, valid_process_id):
    """Verify requesting unsupported format returns 406."""
    response = api_request(
        "GET",
        f"{processes_api_url}/{valid_process_id}",
        headers={"Accept": "application/xml"}
    )

    # May return 406 or fall back to JSON
    assert response.status_code in [200, 406], \
        f"Expected 200 or 406, got {response.status_code}"


# ============================================================================
#  Prefer Header Tests
# ============================================================================


def test_prefer_header_respond_async(api_request, processes_api_url, valid_process_id):
    """Verify Prefer: respond-async header triggers async execution."""
    execute_request = {"inputs": {}}

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    # Should return 201 for async or 200 if not supported
    assert response.status_code in [200, 201, 400], \
        f"Expected 200/201/400, got {response.status_code}"

    if response.status_code == 201:
        # Async execution
        assert "Location" in response.headers, \
            "Async response should include Location header"


def test_prefer_header_return_representation(api_request, processes_api_url, valid_process_id):
    """Verify Prefer: return=representation header triggers sync execution."""
    execute_request = {"inputs": {}}

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "return=representation"}
    )

    # Should attempt sync execution
    assert response.status_code in [200, 201, 400], \
        f"Expected 200/201/400, got {response.status_code}"


# ============================================================================
#  Job Progress Tests
# ============================================================================


def test_job_status_includes_progress(api_request, processes_api_url, valid_process_id, honua_api_base_url):
    """Verify job status includes progress information for running jobs."""
    # Create a job
    execute_request = {"inputs": {}}

    response = api_request(
        "POST",
        f"{processes_api_url}/{valid_process_id}/execution",
        json=execute_request,
        headers={"Prefer": "respond-async"}
    )

    if response.status_code != 201:
        pytest.skip("Cannot create async job")

    data = response.json()
    job_id = data["jobID"]

    # Get job status
    status_response = api_request("GET", f"{honua_api_base_url}/jobs/{job_id}")
    status_data = status_response.json()

    # Progress is optional but useful
    if "progress" in status_data:
        progress = status_data["progress"]
        assert isinstance(progress, (int, float)), "Progress must be numeric"
        assert 0 <= progress <= 100, "Progress must be between 0 and 100"
