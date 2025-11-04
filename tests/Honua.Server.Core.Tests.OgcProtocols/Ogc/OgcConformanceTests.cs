using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

[CollectionDefinition(CollectionName)]
[Trait("Category", "Integration")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Slow")]
[Trait("Database", "Postgres")]
public sealed class OgcConformanceCollection : ICollectionFixture<OgcConformanceFixture>
{
    public const string CollectionName = "ogc-conformance";
}

[Collection(OgcConformanceCollection.CollectionName)]
public sealed class OgcConformanceTests
{
    private readonly OgcConformanceFixture _fixture;
    private readonly ITestOutputHelper _output;

    public OgcConformanceTests(OgcConformanceFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task OgcApiFeatures_PassesConformance()
    {
        if (!OgcConformanceFixture.IsEnabled)
        {
            _output.WriteLine($"OGC conformance tests disabled. Set {OgcConformanceFixture.ComplianceEnvVar}=true to enable.");
            return;
        }

        await _fixture.EnsureInitializedAsync();

        var result = await _fixture.RunOgcApiFeaturesTests(_fixture.HonuaBaseUrl!);

        _output.WriteLine($"Test results saved to: {result.ReportPath}");
        _output.WriteLine($"Passed: {result.Passed}, Failed: {result.Failed}, Skipped: {result.Skipped}");

        result.Failed.Should().Be(0, "All OGC API Features conformance tests should pass");
    }

    [Fact]
    public async Task WfsService_PassesConformance()
    {
        if (!OgcConformanceFixture.IsEnabled)
        {
            _output.WriteLine($"OGC conformance tests disabled. Set {OgcConformanceFixture.ComplianceEnvVar}=true to enable.");
            return;
        }

        await _fixture.EnsureInitializedAsync();

        var capabilitiesUrl = $"{_fixture.HonuaBaseUrl}/wfs?service=WFS&request=GetCapabilities&version=2.0.0";
        var result = await _fixture.RunWfsTests(capabilitiesUrl);

        _output.WriteLine($"Test results saved to: {result.ReportPath}");
        _output.WriteLine($"Test completed with status: {result.Success}");

        result.Success.Should().BeTrue("WFS 2.0 conformance tests should pass");
    }

    [Fact]
    public async Task WmsService_PassesConformance()
    {
        if (!OgcConformanceFixture.IsEnabled)
        {
            _output.WriteLine($"OGC conformance tests disabled. Set {OgcConformanceFixture.ComplianceEnvVar}=true to enable.");
            return;
        }

        await _fixture.EnsureInitializedAsync();

        var capabilitiesUrl = $"{_fixture.HonuaBaseUrl}/wms?service=WMS&request=GetCapabilities&version=1.3.0";
        var result = await _fixture.RunWmsTests(capabilitiesUrl);

        _output.WriteLine($"Test results saved to: {result.ReportPath}");
        _output.WriteLine($"Test completed with status: {result.Success}");

        result.Success.Should().BeTrue("WMS 1.3 conformance tests should pass");
    }

    [Fact]
    public async Task KmlExport_PassesConformance()
    {
        if (!OgcConformanceFixture.IsEnabled)
        {
            _output.WriteLine($"OGC conformance tests disabled. Set {OgcConformanceFixture.ComplianceEnvVar}=true to enable.");
            return;
        }

        await _fixture.EnsureInitializedAsync();

        // Export KML from the roads collection defined in samples/ogc/metadata.json
        var kmlUrl = $"{_fixture.HonuaBaseUrl}/ogc/collections/roads::roads-primary/items?f=kml";
        using var httpClient = new HttpClient();
        var kmlContent = await httpClient.GetStringAsync(kmlUrl);

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, kmlContent);

            var result = await _fixture.RunKmlTests(tempFile);

            _output.WriteLine($"Test results saved to: {result.ReportPath}");
            _output.WriteLine($"Test completed with status: {result.Success}");

            result.Success.Should().BeTrue("KML 2.2 conformance tests should pass");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}

public sealed class OgcConformanceFixture : IAsyncLifetime
{
    internal const string ComplianceEnvVar = "HONUA_RUN_OGC_CONFORMANCE";
    private const string ReportRootDir = "qa-report";

    private IContainer? _ogcFeaturesContainer;
    private IContainer? _wfsContainer;
    private IContainer? _wmsContainer;
    private IContainer? _kmlContainer;
    private DockerClient? _dockerClient;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public string? HonuaBaseUrl { get; private set; }
    public int? OgcFeaturesPort { get; private set; }

    public static bool IsEnabled
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(ComplianceEnvVar);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task InitializeAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        // Initialize will be done lazily in EnsureInitializedAsync
        await Task.CompletedTask;
    }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            _dockerClient = new DockerClientConfiguration().CreateClient();

            // Pre-pull images
            foreach (var image in new[]
                     {
                         "ogccite/ets-ogcapi-features10:latest",
                         "ogccite/ets-wfs20:latest",
                         "ogccite/ets-wms13:latest",
                         "ogccite/ets-kml22:latest"
                     })
            {
                await EnsureImageAsync(_dockerClient, image).ConfigureAwait(false);
            }

            // Detect Honua base URL (assume running on host)
            // In WSL, use host.docker.internal or actual host IP
            HonuaBaseUrl = DetermineHonuaBaseUrl();

            // Start OGC API Features TEAM Engine container
            _ogcFeaturesContainer = new ContainerBuilder()
                .WithImage("ogccite/ets-ogcapi-features10:latest")
                .WithPortBinding(8080, true)  // Map to random host port
                .WithExtraHost("host.docker.internal", "host-gateway")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8080))
                .Build();

            await _ogcFeaturesContainer.StartAsync().ConfigureAwait(false);
            OgcFeaturesPort = _ogcFeaturesContainer.GetMappedPublicPort(8080);

            // Wait for TEAM Engine to be ready
            await WaitForTeamEngineAsync($"http://localhost:{OgcFeaturesPort}/teamengine/").ConfigureAwait(false);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static string DetermineHonuaBaseUrl()
    {
        // Check for explicit override
        var explicitUrl = Environment.GetEnvironmentVariable("HONUA_TEST_BASE_URL");
        if (!string.IsNullOrWhiteSpace(explicitUrl))
        {
            return explicitUrl;
        }

        // Use host.docker.internal to access host from container (works on Docker Desktop/WSL2)
        return "http://host.docker.internal:5555";
    }

    public async Task<OgcApiFeaturesTestResult> RunOgcApiFeaturesTests(string honuaBaseUrl)
    {
        if (_ogcFeaturesContainer is null)
        {
            throw new InvalidOperationException("OGC Features container not initialized");
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var reportDir = Path.Combine(ReportRootDir, $"ogcfeatures-{timestamp}");
        Directory.CreateDirectory(reportDir);

        var reportPath = Path.Combine(reportDir, "testng-results.xml");
        var serviceEndpoint = $"{honuaBaseUrl}/ogc";

        // Run tests via ETS container
        var testCommand = new[]
        {
            "sh", "-c",
            $"java -jar /root/ets-ogcapi-features10-aio.jar -o /tmp/results.xml -Dets.service.endpoint={serviceEndpoint} -Dets.test.selection=confAll"
        };

        await using var testRunner = new ContainerBuilder()
            .WithImage("ogccite/ets-ogcapi-features10:latest")
            .WithCommand(testCommand)
            .WithBindMount(Path.GetFullPath(reportDir), "/tmp")
            .Build();

        await testRunner.StartAsync().ConfigureAwait(false);
        var exitCode = await testRunner.GetExitCodeAsync().ConfigureAwait(false);

        // Parse TestNG results
        var (passed, failed, skipped) = ParseTestNgResults(reportPath);

        return new OgcApiFeaturesTestResult(reportPath, passed, failed, skipped, exitCode == 0);
    }

    public async Task<WfsTestResult> RunWfsTests(string capabilitiesUrl)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var reportDir = Path.Combine(ReportRootDir, $"wfs-{timestamp}");
        Directory.CreateDirectory(reportDir);

        var reportPath = Path.Combine(reportDir, "wfs-conformance-response.xml");

        // Start ephemeral TEAM Engine container
        await using var wfsEngine = new ContainerBuilder()
            .WithImage("ogccite/ets-wfs20:latest")
            .WithPortBinding(8080, true)
            .WithExtraHost("host.docker.internal", "host-gateway")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8080))
            .Build();

        await wfsEngine.StartAsync().ConfigureAwait(false);
        var port = wfsEngine.GetMappedPublicPort(8080);
        await Task.Delay(5000); // Additional stabilization time

        // Invoke WFS test suite via REST API
        var encodedCapabilities = Uri.EscapeDataString(capabilitiesUrl);
        var runUrl = $"http://localhost:{port}/teamengine/rest/suites/wfs20/run?wfs={encodedCapabilities}&format=xml";

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("ogctest:ogctest")));

        var response = await httpClient.GetAsync(runUrl);
        var content = await response.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync(reportPath, content);

        var success = response.IsSuccessStatusCode && !content.Contains("FAIL", StringComparison.OrdinalIgnoreCase);

        return new WfsTestResult(reportPath, success);
    }

    public async Task<WmsTestResult> RunWmsTests(string capabilitiesUrl)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var reportDir = Path.Combine(ReportRootDir, $"wms-{timestamp}");
        Directory.CreateDirectory(reportDir);

        var reportPath = Path.Combine(reportDir, "wms-conformance-response.xml");

        // Start ephemeral TEAM Engine container
        await using var wmsEngine = new ContainerBuilder()
            .WithImage("ogccite/ets-wms13:latest")
            .WithPortBinding(8080, true)
            .WithExtraHost("host.docker.internal", "host-gateway")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8080))
            .Build();

        await wmsEngine.StartAsync().ConfigureAwait(false);
        var port = wmsEngine.GetMappedPublicPort(8080);
        await Task.Delay(5000); // Additional stabilization time

        // Invoke WMS test suite via REST API
        var encodedCapabilities = Uri.EscapeDataString(capabilitiesUrl);
        var runUrl = $"http://localhost:{port}/teamengine/rest/suites/wms13/run?wms={encodedCapabilities}&format=xml";

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("ogctest:ogctest")));

        var response = await httpClient.GetAsync(runUrl);
        var content = await response.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync(reportPath, content);

        var success = response.IsSuccessStatusCode && !content.Contains("FAIL", StringComparison.OrdinalIgnoreCase);

        return new WmsTestResult(reportPath, success);
    }

    public async Task<KmlTestResult> RunKmlTests(string kmlFilePath)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var reportDir = Path.Combine(ReportRootDir, $"kml-{timestamp}");
        Directory.CreateDirectory(reportDir);

        var reportPath = Path.Combine(reportDir, "earl-results.rdf");

        var hostDirectory = Path.GetDirectoryName(kmlFilePath)!;
        var fileName = Path.GetFileName(kmlFilePath);

        // Start ephemeral TEAM Engine container with volume mount
        await using var kmlEngine = new ContainerBuilder()
            .WithImage("ogccite/ets-kml22:latest")
            .WithPortBinding(8080, true)
            .WithBindMount(hostDirectory, "/data")
            .WithExtraHost("host.docker.internal", "host-gateway")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8080))
            .Build();

        await kmlEngine.StartAsync().ConfigureAwait(false);
        var port = kmlEngine.GetMappedPublicPort(8080);
        await Task.Delay(5000); // Additional stabilization time

        // Submit test run request
        var requestBody = $@"<testRunRequest xmlns='http://teamengine.sourceforge.net/ctl'>
    <entry><string>iut</string><string>file:/data/{fileName}</string></entry>
</testRunRequest>";

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("ogctest:ogctest")));

        var content = new StringContent(requestBody, Encoding.UTF8, "application/xml");
        var response = await httpClient.PostAsync($"http://localhost:{port}/teamengine/rest/suites/kml22/run", content);
        var result = await response.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync(reportPath, result);

        var success = response.IsSuccessStatusCode && !result.Contains("earl#failed", StringComparison.OrdinalIgnoreCase);

        return new KmlTestResult(reportPath, success);
    }

    public async Task DisposeAsync()
    {
        try
        {
            // Dispose test containers
            if (_ogcFeaturesContainer is not null)
            {
                await _ogcFeaturesContainer.DisposeAsync().ConfigureAwait(false);
                _ogcFeaturesContainer = null;
            }

            if (_wfsContainer is not null)
            {
                await _wfsContainer.DisposeAsync().ConfigureAwait(false);
                _wfsContainer = null;
            }

            if (_wmsContainer is not null)
            {
                await _wmsContainer.DisposeAsync().ConfigureAwait(false);
                _wmsContainer = null;
            }

            if (_kmlContainer is not null)
            {
                await _kmlContainer.DisposeAsync().ConfigureAwait(false);
                _kmlContainer = null;
            }

            // Clean up any remaining CITE containers that might be orphaned
            if (_dockerClient is not null)
            {
                try
                {
                    var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
                    {
                        All = true
                    }).ConfigureAwait(false);

                    var citeContainers = containers.Where(c =>
                        c.Image.Contains("ogccite/ets-", StringComparison.OrdinalIgnoreCase) ||
                        c.Names.Any(n => n.Contains("ogc", StringComparison.OrdinalIgnoreCase) ||
                                       n.Contains("cite", StringComparison.OrdinalIgnoreCase) ||
                                       n.Contains("teamengine", StringComparison.OrdinalIgnoreCase)));

                    foreach (var container in citeContainers)
                    {
                        try
                        {
                            await _dockerClient.Containers.StopContainerAsync(
                                container.ID,
                                new ContainerStopParameters { WaitBeforeKillSeconds = 2 }).ConfigureAwait(false);

                            await _dockerClient.Containers.RemoveContainerAsync(
                                container.ID,
                                new ContainerRemoveParameters { Force = true }).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Best effort cleanup - ignore failures
                        }
                    }
                }
                catch
                {
                    // Best effort cleanup - ignore failures
                }
            }

            _dockerClient?.Dispose();
            _dockerClient = null;

            _initLock.Dispose();
        }
        catch
        {
            // Best effort cleanup - ensure disposal completes even if cleanup fails
        }
    }

    private static async Task WaitForTeamEngineAsync(string url, int maxRetries = 30)
    {
        using var httpClient = new HttpClient();
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Ignore and retry
            }

            await Task.Delay(2000);
        }

        throw new TimeoutException($"TEAM Engine did not become ready at {url}");
    }

    private static (int Passed, int Failed, int Skipped) ParseTestNgResults(string xmlPath)
    {
        if (!File.Exists(xmlPath))
        {
            return (0, 0, 0);
        }

        try
        {
            var doc = XDocument.Load(xmlPath);
            var testSuite = doc.Root?.Element("suite");

            var passed = int.Parse(testSuite?.Attribute("passed")?.Value ?? "0");
            var failed = int.Parse(testSuite?.Attribute("failed")?.Value ?? "0");
            var skipped = int.Parse(testSuite?.Attribute("skipped")?.Value ?? "0");

            return (passed, failed, skipped);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    private static int GetRandomPort()
    {
        var random = new Random();
        for (var i = 0; i < 100; i++)
        {
            var port = random.Next(20000, 40000);
            try
            {
                using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch
            {
                continue;
            }
        }

        throw new InvalidOperationException("Unable to find available port");
    }

    private static async Task EnsureImageAsync(DockerClient client, string image, CancellationToken ct = default)
    {
        try
        {
            await client.Images.InspectImageAsync(image, ct).ConfigureAwait(false);
        }
        catch (DockerImageNotFoundException)
        {
            var (repository, tag) = ParseImage(image);
            var parameters = new ImagesCreateParameters
            {
                FromImage = repository,
                Tag = tag
            };

            var progress = new Progress<JSONMessage>(_ => { });
            await client.Images.CreateImageAsync(parameters, null, progress, ct).ConfigureAwait(false);
        }
    }

    private static (string Repository, string Tag) ParseImage(string image)
    {
        var lastSlash = image.LastIndexOf('/');
        var lastColon = image.LastIndexOf(':');

        if (lastColon > -1 && lastColon > lastSlash)
        {
            return (image[..lastColon], image[(lastColon + 1)..]);
        }

        return (image, "latest");
    }
}

public sealed record OgcApiFeaturesTestResult(string ReportPath, int Passed, int Failed, int Skipped, bool Success);
public sealed record WfsTestResult(string ReportPath, bool Success);
public sealed record WmsTestResult(string ReportPath, bool Success);
public sealed record KmlTestResult(string ReportPath, bool Success);
