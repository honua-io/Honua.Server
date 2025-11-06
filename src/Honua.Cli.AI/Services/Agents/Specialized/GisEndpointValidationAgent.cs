// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Validates deployed GIS services by testing OGC endpoints and Honua-specific APIs.
/// Understands WFS, WMS, WMTS, OGC API Features, STAC, and validates conformance.
/// </summary>
public sealed class GisEndpointValidationAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<GisEndpointValidationAgent> _logger;
    private readonly HttpClient _httpClient;

    public GisEndpointValidationAgent(
        Kernel kernel,
        ILlmProvider llmProvider,
        ILogger<GisEndpointValidationAgent> logger,
        IHttpClientFactory httpClientFactory)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = (httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory))).CreateClient("GisEndpointValidation");
    }

    /// <summary>
    /// Validates a deployed Honua instance by testing all GIS endpoints.
    /// </summary>
    public async Task<GisValidationResult> ValidateDeployedServicesAsync(
        GisValidationRequest request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating GIS endpoints for {BaseUrl} (smoke tests - fast execution)", request.BaseUrl);

        var checks = new List<EndpointValidation>();

        try
        {
            // Run core smoke tests in parallel for speed (critical for rollback responsiveness)
            var coreTasks = new List<Task<EndpointValidation>>
            {
                ValidateHonuaHealthAsync(request.BaseUrl, cancellationToken),
                ValidateOgcApiLandingPageAsync(request.BaseUrl, cancellationToken),
                ValidateCollectionsEndpointAsync(request.BaseUrl, cancellationToken),
                ValidateMetricsEndpointAsync(request.BaseUrl, cancellationToken)
            };

            // Add optional protocol tests (if enabled)
            if (request.TestWfs)
                coreTasks.Add(ValidateWfsAsync(request.BaseUrl, cancellationToken));
            if (request.TestWms)
                coreTasks.Add(ValidateWmsAsync(request.BaseUrl, cancellationToken));
            if (request.TestWmts)
                coreTasks.Add(ValidateWmtsAsync(request.BaseUrl, cancellationToken));
            if (request.TestEsriRest)
                coreTasks.Add(ValidateEsriRestAsync(request.BaseUrl, cancellationToken));
            if (request.TestStac)
                coreTasks.Add(ValidateStacAsync(request.BaseUrl, cancellationToken));
            if (request.TestSecurity)
                coreTasks.Add(ValidateSecurityConfigurationAsync(request.BaseUrl, cancellationToken));

            // Execute all core checks in parallel
            var coreResults = await Task.WhenAll(coreTasks).ConfigureAwait(false);
            checks.AddRange(coreResults);

            // Run service-specific tests (only if health check passed - fail fast)
            if (!checks.Any(c => c.Status == EndpointStatus.Failed && c.EndpointType == "Honua Health"))
            {
                // Service validation and feature retrieval tests
                if (request.ServicesToValidate?.Any() == true)
                {
                    // Run first service validation + feature retrieval in parallel
                    var serviceTasks = new List<Task<EndpointValidation>>();

                    var firstService = request.ServicesToValidate.First();
                    serviceTasks.Add(ValidateServiceAsync(request.BaseUrl, firstService, cancellationToken));

                    if (request.TestFeatureRetrieval)
                        serviceTasks.Add(ValidateFeatureRetrievalAsync(request.BaseUrl, firstService, cancellationToken));

                    if (request.TestOData)
                        serviceTasks.Add(ValidateODataAsync(request.BaseUrl, firstService, cancellationToken));

                    var serviceResults = await Task.WhenAll(serviceTasks).ConfigureAwait(false);
                    checks.AddRange(serviceResults);

                    // Additional services (if multiple specified) - only validate metadata
                    foreach (var service in request.ServicesToValidate.Skip(1))
                    {
                        checks.Add(await ValidateServiceAsync(request.BaseUrl, service, cancellationToken));
                    }
                }
            }
            else
            {
                _logger.LogWarning("Health check failed - skipping service-specific tests");
            }

            // Calculate overall result
            var failed = checks.Count(c => c.Status == EndpointStatus.Failed);
            var warnings = checks.Count(c => c.Status == EndpointStatus.Warning);
            var passed = checks.Count(c => c.Status == EndpointStatus.Passed);

            var overallStatus = failed > 0 ? EndpointStatus.Failed :
                               warnings > 0 ? EndpointStatus.Warning :
                               EndpointStatus.Passed;

            return new GisValidationResult
            {
                BaseUrl = request.BaseUrl,
                OverallStatus = overallStatus,
                Checks = checks,
                PassedChecks = passed,
                WarningChecks = warnings,
                FailedChecks = failed,
                Timestamp = DateTime.UtcNow,
                Summary = GenerateSummary(checks, overallStatus)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GIS endpoint validation failed");

            return new GisValidationResult
            {
                BaseUrl = request.BaseUrl,
                OverallStatus = EndpointStatus.Failed,
                Checks = checks,
                PassedChecks = 0,
                WarningChecks = 0,
                FailedChecks = checks.Count,
                Timestamp = DateTime.UtcNow,
                Summary = $"Validation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates Honua's /health endpoint.
    /// </summary>
    private async Task<EndpointValidation> ValidateHonuaHealthAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync($"{baseUrl}/health", cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var health = JsonDocument.Parse(content);

                var status = health.RootElement.GetProperty("status").GetString();

                return new EndpointValidation
                {
                    EndpointType = "Honua Health",
                    Url = $"{baseUrl}/health",
                    Status = status?.ToLowerInvariant() == "healthy" ? EndpointStatus.Passed : EndpointStatus.Warning,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = $"Health check returned: {status}",
                    Details = new Dictionary<string, string>
                    {
                        ["status"] = status ?? "unknown",
                        ["response_time"] = $"{sw.ElapsedMilliseconds}ms"
                    }
                };
            }

            return new EndpointValidation
            {
                EndpointType = "Honua Health",
                Url = $"{baseUrl}/health",
                Status = EndpointStatus.Failed,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"Health endpoint returned {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "Honua Health",
                Url = $"{baseUrl}/health",
                Status = EndpointStatus.Failed,
                Message = $"Health check failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates OGC API Features landing page conformance.
    /// </summary>
    private async Task<EndpointValidation> ValidateOgcApiLandingPageAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(baseUrl, cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var landing = JsonDocument.Parse(content);

                // Validate conformance
                var hasTitle = landing.RootElement.TryGetProperty("title", out _);
                var hasLinks = landing.RootElement.TryGetProperty("links", out var links);

                var conformanceLink = false;
                var collectionsLink = false;

                if (hasLinks)
                {
                    foreach (var link in links.EnumerateArray())
                    {
                        if (link.TryGetProperty("rel", out var rel))
                        {
                            var relValue = rel.GetString();
                            if (relValue == "conformance") conformanceLink = true;
                            if (relValue == "data" || relValue == "collections") collectionsLink = true;
                        }
                    }
                }

                var status = hasTitle && conformanceLink && collectionsLink ?
                    EndpointStatus.Passed : EndpointStatus.Warning;

                return new EndpointValidation
                {
                    EndpointType = "OGC API Landing Page",
                    Url = baseUrl,
                    Status = status,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = status == EndpointStatus.Passed ?
                        "Landing page conforms to OGC API Features Part 1" :
                        "Landing page missing required links (conformance, collections)",
                    Details = new Dictionary<string, string>
                    {
                        ["has_title"] = hasTitle.ToString(),
                        ["has_conformance_link"] = conformanceLink.ToString(),
                        ["has_collections_link"] = collectionsLink.ToString()
                    }
                };
            }

            return new EndpointValidation
            {
                EndpointType = "OGC API Landing Page",
                Url = baseUrl,
                Status = EndpointStatus.Failed,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"Landing page returned {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "OGC API Landing Page",
                Url = baseUrl,
                Status = EndpointStatus.Failed,
                Message = $"Landing page validation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates /collections endpoint returns valid JSON.
    /// </summary>
    private async Task<EndpointValidation> ValidateCollectionsEndpointAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync($"{baseUrl}/collections", cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var collections = JsonDocument.Parse(content);

                var hasCollectionsArray = collections.RootElement.TryGetProperty("collections", out var collArray);
                var collectionCount = hasCollectionsArray ? collArray.GetArrayLength() : 0;

                return new EndpointValidation
                {
                    EndpointType = "Collections Endpoint",
                    Url = $"{baseUrl}/collections",
                    Status = collectionCount > 0 ? EndpointStatus.Passed : EndpointStatus.Warning,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = $"Found {collectionCount} collections",
                    Details = new Dictionary<string, string>
                    {
                        ["collection_count"] = collectionCount.ToString()
                    }
                };
            }

            return new EndpointValidation
            {
                EndpointType = "Collections Endpoint",
                Url = $"{baseUrl}/collections",
                Status = EndpointStatus.Failed,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"Collections endpoint returned {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "Collections Endpoint",
                Url = $"{baseUrl}/collections",
                Status = EndpointStatus.Failed,
                Message = $"Collections validation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates a specific service/collection.
    /// </summary>
    private async Task<EndpointValidation> ValidateServiceAsync(
        string baseUrl,
        string serviceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync($"{baseUrl}/collections/{serviceId}", cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var collection = JsonDocument.Parse(content);

                var hasId = collection.RootElement.TryGetProperty("id", out var id);
                var hasLinks = collection.RootElement.TryGetProperty("links", out _);
                var hasExtent = collection.RootElement.TryGetProperty("extent", out _);

                var status = hasId && hasLinks && hasExtent ? EndpointStatus.Passed : EndpointStatus.Warning;

                return new EndpointValidation
                {
                    EndpointType = "Service Metadata",
                    Url = $"{baseUrl}/collections/{serviceId}",
                    Status = status,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = status == EndpointStatus.Passed ?
                        $"Service '{serviceId}' metadata valid" :
                        $"Service '{serviceId}' missing required metadata fields",
                    Details = new Dictionary<string, string>
                    {
                        ["service_id"] = serviceId,
                        ["has_extent"] = hasExtent.ToString()
                    }
                };
            }

            return new EndpointValidation
            {
                EndpointType = "Service Metadata",
                Url = $"{baseUrl}/collections/{serviceId}",
                Status = EndpointStatus.Failed,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"Service '{serviceId}' returned {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "Service Metadata",
                Url = $"{baseUrl}/collections/{serviceId}",
                Status = EndpointStatus.Failed,
                Message = $"Service validation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Tests feature retrieval from a service.
    /// </summary>
    private async Task<EndpointValidation> ValidateFeatureRetrievalAsync(
        string baseUrl,
        string serviceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(
                $"{baseUrl}/collections/{serviceId}/items?limit=1",
                cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var featureCollection = JsonDocument.Parse(content);

                var hasType = featureCollection.RootElement.TryGetProperty("type", out var type);
                var hasFeatures = featureCollection.RootElement.TryGetProperty("features", out var features);
                var featureCount = hasFeatures ? features.GetArrayLength() : 0;

                var isGeoJson = type.GetString() == "FeatureCollection";

                return new EndpointValidation
                {
                    EndpointType = "Feature Retrieval",
                    Url = $"{baseUrl}/collections/{serviceId}/items",
                    Status = isGeoJson && featureCount > 0 ? EndpointStatus.Passed : EndpointStatus.Warning,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = isGeoJson ?
                        $"Retrieved {featureCount} features from '{serviceId}'" :
                        "Response not valid GeoJSON FeatureCollection",
                    Details = new Dictionary<string, string>
                    {
                        ["feature_count"] = featureCount.ToString(),
                        ["is_geojson"] = isGeoJson.ToString()
                    }
                };
            }

            return new EndpointValidation
            {
                EndpointType = "Feature Retrieval",
                Url = $"{baseUrl}/collections/{serviceId}/items",
                Status = EndpointStatus.Failed,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"Feature retrieval returned {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "Feature Retrieval",
                Url = $"{baseUrl}/collections/{serviceId}/items",
                Status = EndpointStatus.Failed,
                Message = $"Feature retrieval failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates WFS GetCapabilities.
    /// </summary>
    private async Task<EndpointValidation> ValidateWfsAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(
                $"{baseUrl}/wfs?service=WFS&version=2.0.0&request=GetCapabilities",
                cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var isXml = content.TrimStart().StartsWith("<");

                if (isXml)
                {
                    var xml = XDocument.Parse(content);
                    var hasCapabilities = xml.Root?.Name.LocalName == "WFS_Capabilities" ||
                                         xml.Root?.Name.LocalName == "Capabilities";

                    return new EndpointValidation
                    {
                        EndpointType = "WFS GetCapabilities",
                        Url = $"{baseUrl}/wfs",
                        Status = hasCapabilities ? EndpointStatus.Passed : EndpointStatus.Warning,
                        ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                        Message = hasCapabilities ? "WFS 2.0.0 capabilities valid" : "Invalid WFS capabilities document"
                    };
                }

                return new EndpointValidation
                {
                    EndpointType = "WFS GetCapabilities",
                    Url = $"{baseUrl}/wfs",
                    Status = EndpointStatus.Warning,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = "WFS returned non-XML response"
                };
            }

            return new EndpointValidation
            {
                EndpointType = "WFS GetCapabilities",
                Url = $"{baseUrl}/wfs",
                Status = EndpointStatus.Failed,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"WFS GetCapabilities returned {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "WFS GetCapabilities",
                Url = $"{baseUrl}/wfs",
                Status = EndpointStatus.Failed,
                Message = $"WFS validation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates WMS GetCapabilities.
    /// </summary>
    private async Task<EndpointValidation> ValidateWmsAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(
                $"{baseUrl}/wms?service=WMS&version=1.3.0&request=GetCapabilities",
                cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var isXml = content.TrimStart().StartsWith("<");

                if (isXml)
                {
                    var xml = XDocument.Parse(content);
                    var hasCapabilities = xml.Root?.Name.LocalName == "WMS_Capabilities" ||
                                         xml.Root?.Name.LocalName == "Capabilities";

                    return new EndpointValidation
                    {
                        EndpointType = "WMS GetCapabilities",
                        Url = $"{baseUrl}/wms",
                        Status = hasCapabilities ? EndpointStatus.Passed : EndpointStatus.Warning,
                        ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                        Message = hasCapabilities ? "WMS 1.3.0 capabilities valid" : "Invalid WMS capabilities document"
                    };
                }
            }

            return new EndpointValidation
            {
                EndpointType = "WMS GetCapabilities",
                Url = $"{baseUrl}/wms",
                Status = EndpointStatus.Warning,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"WMS GetCapabilities returned {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "WMS GetCapabilities",
                Url = $"{baseUrl}/wms",
                Status = EndpointStatus.Failed,
                Message = $"WMS validation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates WMTS GetCapabilities.
    /// </summary>
    private async Task<EndpointValidation> ValidateWmtsAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(
                $"{baseUrl}/wmts?service=WMTS&version=1.0.0&request=GetCapabilities",
                cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var isXml = content.TrimStart().StartsWith("<");

                if (isXml)
                {
                    var xml = XDocument.Parse(content);
                    var hasCapabilities = xml.Root?.Name.LocalName == "Capabilities";

                    return new EndpointValidation
                    {
                        EndpointType = "WMTS GetCapabilities",
                        Url = $"{baseUrl}/wmts",
                        Status = hasCapabilities ? EndpointStatus.Passed : EndpointStatus.Warning,
                        ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                        Message = hasCapabilities ? "WMTS 1.0.0 capabilities valid" : "Invalid WMTS capabilities document"
                    };
                }
            }

            return new EndpointValidation
            {
                EndpointType = "WMTS GetCapabilities",
                Url = $"{baseUrl}/wmts",
                Status = EndpointStatus.Warning,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"WMTS GetCapabilities returned {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "WMTS GetCapabilities",
                Url = $"{baseUrl}/wmts",
                Status = EndpointStatus.Failed,
                Message = $"WMTS validation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates STAC catalog endpoint.
    /// </summary>
    private async Task<EndpointValidation> ValidateStacAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync($"{baseUrl}/stac", cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var catalog = JsonDocument.Parse(content);

                var hasType = catalog.RootElement.TryGetProperty("type", out var type);
                var isStacCatalog = type.GetString() == "Catalog";

                return new EndpointValidation
                {
                    EndpointType = "STAC Catalog",
                    Url = $"{baseUrl}/stac",
                    Status = isStacCatalog ? EndpointStatus.Passed : EndpointStatus.Warning,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = isStacCatalog ? "STAC catalog valid" : "Invalid STAC catalog"
                };
            }

            return new EndpointValidation
            {
                EndpointType = "STAC Catalog",
                Url = $"{baseUrl}/stac",
                Status = EndpointStatus.Warning,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"STAC catalog returned {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "STAC Catalog",
                Url = $"{baseUrl}/stac",
                Status = EndpointStatus.Failed,
                Message = $"STAC validation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API catalog endpoint.
    /// </summary>
    private async Task<EndpointValidation> ValidateEsriRestAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync($"{baseUrl}/rest/services?f=json", cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var catalog = JsonDocument.Parse(content);

                var hasFolders = catalog.RootElement.TryGetProperty("folders", out var folders);
                var hasServices = catalog.RootElement.TryGetProperty("services", out var services);

                var folderCount = hasFolders ? folders.GetArrayLength() : 0;
                var serviceCount = hasServices ? services.GetArrayLength() : 0;

                return new EndpointValidation
                {
                    EndpointType = "Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API Catalog",
                    Url = $"{baseUrl}/rest/services",
                    Status = (hasFolders || hasServices) ? EndpointStatus.Passed : EndpointStatus.Warning,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = $"Geoservices REST a.k.a. Esri REST catalog: {serviceCount} services, {folderCount} folders",
                    Details = new Dictionary<string, string>
                    {
                        ["service_count"] = serviceCount.ToString(),
                        ["folder_count"] = folderCount.ToString()
                    }
                };
            }

            return new EndpointValidation
            {
                EndpointType = "Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API Catalog",
                Url = $"{baseUrl}/rest/services",
                Status = EndpointStatus.Failed,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"Geoservices REST a.k.a. Esri REST catalog returned {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API Catalog",
                Url = $"{baseUrl}/rest/services",
                Status = EndpointStatus.Failed,
                Message = $"Geoservices REST a.k.a. Esri REST validation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates OData service endpoint with basic query.
    /// </summary>
    private async Task<EndpointValidation> ValidateODataAsync(
        string baseUrl,
        string serviceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Test OData service document
            var response = await _httpClient.GetAsync($"{baseUrl}/odata/{serviceId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new EndpointValidation
                {
                    EndpointType = "OData Service",
                    Url = $"{baseUrl}/odata/{serviceId}",
                    Status = EndpointStatus.Failed,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = $"OData service returned {response.StatusCode}"
                };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var serviceDoc = JsonDocument.Parse(content);

            var hasValue = serviceDoc.RootElement.TryGetProperty("value", out var entities);

            // Test basic query (get first entity)
            if (hasValue && entities.GetArrayLength() > 0)
            {
                var firstEntity = entities[0].GetProperty("name").GetString();
                var queryResponse = await _httpClient.GetAsync(
                    $"{baseUrl}/odata/{serviceId}/{firstEntity}?$top=1",
                    cancellationToken);
                sw.Stop();

                if (queryResponse.IsSuccessStatusCode)
                {
                    var queryContent = await queryResponse.Content.ReadAsStringAsync(cancellationToken);
                    var results = JsonDocument.Parse(queryContent);
                    var hasResults = results.RootElement.TryGetProperty("value", out var resultArray);

                    return new EndpointValidation
                    {
                        EndpointType = "OData Service",
                        Url = $"{baseUrl}/odata/{serviceId}",
                        Status = hasResults ? EndpointStatus.Passed : EndpointStatus.Warning,
                        ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                        Message = hasResults ?
                            $"OData query successful on '{serviceId}'" :
                            $"OData service responding but no results returned",
                        Details = new Dictionary<string, string>
                        {
                            ["entity_set_count"] = entities.GetArrayLength().ToString(),
                            ["query_tested"] = firstEntity ?? "unknown"
                        }
                    };
                }
            }

            sw.Stop();
            return new EndpointValidation
            {
                EndpointType = "OData Service",
                Url = $"{baseUrl}/odata/{serviceId}",
                Status = EndpointStatus.Warning,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = "OData service document valid but query test failed"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "OData Service",
                Url = $"{baseUrl}/odata/{serviceId}",
                Status = EndpointStatus.Failed,
                Message = $"OData validation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates security configuration by checking for proper authentication requirements.
    /// </summary>
    private async Task<EndpointValidation> ValidateSecurityConfigurationAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Check if protected endpoints require authentication
            // Try accessing a typically-protected admin endpoint without credentials
            var response = await _httpClient.GetAsync($"{baseUrl}/admin", cancellationToken);
            sw.Stop();

            // If we get 401/403, security is properly configured
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return new EndpointValidation
                {
                    EndpointType = "Security Configuration",
                    Url = $"{baseUrl}/admin",
                    Status = EndpointStatus.Passed,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = "Security properly configured - protected endpoints require authentication",
                    Details = new Dictionary<string, string>
                    {
                        ["admin_endpoint_status"] = response.StatusCode.ToString()
                    }
                };
            }

            // If we get 404, admin endpoint doesn't exist (acceptable)
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new EndpointValidation
                {
                    EndpointType = "Security Configuration",
                    Url = $"{baseUrl}/admin",
                    Status = EndpointStatus.Passed,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = "Admin endpoint not exposed (secure configuration)"
                };
            }

            // If we get 200, admin endpoint is accessible without auth (security issue!)
            if (response.IsSuccessStatusCode)
            {
                return new EndpointValidation
                {
                    EndpointType = "Security Configuration",
                    Url = $"{baseUrl}/admin",
                    Status = EndpointStatus.Warning,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = "⚠️  WARNING: Admin endpoint accessible without authentication",
                    Details = new Dictionary<string, string>
                    {
                        ["security_issue"] = "admin_endpoint_unprotected"
                    }
                };
            }

            return new EndpointValidation
            {
                EndpointType = "Security Configuration",
                Url = $"{baseUrl}/admin",
                Status = EndpointStatus.Warning,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"Security check returned unexpected status: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "Security Configuration",
                Url = $"{baseUrl}/admin",
                Status = EndpointStatus.Warning,
                Message = $"Security validation inconclusive: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates /metrics endpoint.
    /// </summary>
    private async Task<EndpointValidation> ValidateMetricsEndpointAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync($"{baseUrl}/metrics", cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var hasHonuaMetrics = content.Contains("honua_api_");

                return new EndpointValidation
                {
                    EndpointType = "Metrics Endpoint",
                    Url = $"{baseUrl}/metrics",
                    Status = hasHonuaMetrics ? EndpointStatus.Passed : EndpointStatus.Warning,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    Message = hasHonuaMetrics ?
                        "Metrics endpoint exposing OpenTelemetry metrics" :
                        "Metrics endpoint not exposing honua_api_* metrics"
                };
            }

            return new EndpointValidation
            {
                EndpointType = "Metrics Endpoint",
                Url = $"{baseUrl}/metrics",
                Status = EndpointStatus.Warning,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Message = $"Metrics endpoint returned {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new EndpointValidation
            {
                EndpointType = "Metrics Endpoint",
                Url = $"{baseUrl}/metrics",
                Status = EndpointStatus.Failed,
                Message = $"Metrics validation failed: {ex.Message}"
            };
        }
    }

    private string GenerateSummary(List<EndpointValidation> checks, EndpointStatus overallStatus)
    {
        var sb = new StringBuilder();

        if (overallStatus == EndpointStatus.Passed)
        {
            sb.AppendLine("✅ All GIS endpoint validations passed");
        }
        else if (overallStatus == EndpointStatus.Warning)
        {
            sb.AppendLine("⚠️  GIS endpoints validated with warnings");
        }
        else
        {
            sb.AppendLine("❌ GIS endpoint validation failed");
        }

        sb.AppendLine();
        sb.AppendLine($"Total checks: {checks.Count}");
        sb.AppendLine($"  Passed: {checks.Count(c => c.Status == EndpointStatus.Passed)}");
        sb.AppendLine($"  Warnings: {checks.Count(c => c.Status == EndpointStatus.Warning)}");
        sb.AppendLine($"  Failed: {checks.Count(c => c.Status == EndpointStatus.Failed)}");

        var failedChecks = checks.Where(c => c.Status == EndpointStatus.Failed).ToList();
        if (failedChecks.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Failed checks:");
            foreach (var check in failedChecks)
            {
                sb.AppendLine($"  • {check.EndpointType}: {check.Message}");
            }
        }

        return sb.ToString();
    }
}

// Request/Response models

public sealed class GisValidationRequest
{
    public string BaseUrl { get; init; } = string.Empty;
    public List<string>? ServicesToValidate { get; init; }
    public bool TestWfs { get; init; } = true;
    public bool TestWms { get; init; } = true;
    public bool TestWmts { get; init; } = true;
    public bool TestStac { get; init; } = false;
    public bool TestEsriRest { get; init; } = true;
    public bool TestOData { get; init; } = true;
    public bool TestSecurity { get; init; } = true;
    public bool TestFeatureRetrieval { get; init; } = true;
}

public sealed class GisValidationResult
{
    public string BaseUrl { get; init; } = string.Empty;
    public EndpointStatus OverallStatus { get; init; }
    public List<EndpointValidation> Checks { get; init; } = new();
    public int PassedChecks { get; init; }
    public int WarningChecks { get; init; }
    public int FailedChecks { get; init; }
    public DateTime Timestamp { get; init; }
    public string Summary { get; init; } = string.Empty;
}

public sealed class EndpointValidation
{
    public string EndpointType { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public EndpointStatus Status { get; init; }
    public int ResponseTimeMs { get; init; }
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, string> Details { get; init; } = new();
}

public enum EndpointStatus
{
    Passed,
    Warning,
    Failed
}
