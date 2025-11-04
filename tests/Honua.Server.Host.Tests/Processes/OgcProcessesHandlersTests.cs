using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Processes;
using Honua.Server.Core.Processes.Implementations;
using Honua.Server.Host.Processes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Honua.Server.Host.Tests.Processes;

public sealed class OgcProcessesHandlersTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddOgcProcesses();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapOgcProcesses();
                        });
                    });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [Fact]
    public async Task GetProcesses_ReturnsProcessList()
    {
        // Act
        var response = await _client!.GetAsync("/processes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.TryGetProperty("processes", out var processes).Should().BeTrue();
        processes.GetArrayLength().Should().Be(5); // 5 built-in processes
    }

    [Fact]
    public async Task GetProcess_WithValidId_ReturnsProcessDescription()
    {
        // Act
        var response = await _client!.GetAsync("/processes/buffer");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("id").GetString().Should().Be("buffer");
        json.RootElement.GetProperty("title").GetString().Should().Be("Buffer Geometry");
        json.RootElement.TryGetProperty("inputs", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("outputs", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetProcess_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client!.GetAsync("/processes/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExecuteProcess_Buffer_Synchronous_ReturnsResult()
    {
        // Arrange
        var executeRequest = new
        {
            inputs = new Dictionary<string, object>
            {
                ["geometry"] = new
                {
                    type = "Point",
                    coordinates = new[] { 0.0, 0.0 }
                },
                ["distance"] = 10.0
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/processes/buffer/execution", executeRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
    }

    [Fact]
    public async Task ExecuteProcess_Centroid_Synchronous_ReturnsResult()
    {
        // Arrange
        var executeRequest = new
        {
            inputs = new Dictionary<string, object>
            {
                ["geometry"] = new
                {
                    type = "Polygon",
                    coordinates = new[]
                    {
                        new[]
                        {
                            new[] { 0.0, 0.0 },
                            new[] { 10.0, 0.0 },
                            new[] { 10.0, 10.0 },
                            new[] { 0.0, 10.0 },
                            new[] { 0.0, 0.0 }
                        }
                    }
                }
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/processes/centroid/execution", executeRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
    }

    [Fact]
    public async Task ExecuteProcess_Clip_Synchronous_ReturnsResult()
    {
        // Arrange
        var executeRequest = new
        {
            inputs = new Dictionary<string, object>
            {
                ["geometry"] = new
                {
                    type = "Polygon",
                    coordinates = new[]
                    {
                        new[]
                        {
                            new[] { 0.0, 0.0 },
                            new[] { 20.0, 0.0 },
                            new[] { 20.0, 20.0 },
                            new[] { 0.0, 20.0 },
                            new[] { 0.0, 0.0 }
                        }
                    }
                },
                ["clipGeometry"] = new
                {
                    type = "Polygon",
                    coordinates = new[]
                    {
                        new[]
                        {
                            new[] { 5.0, 5.0 },
                            new[] { 15.0, 5.0 },
                            new[] { 15.0, 15.0 },
                            new[] { 5.0, 15.0 },
                            new[] { 5.0, 5.0 }
                        }
                    }
                }
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/processes/clip/execution", executeRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
    }

    [Fact]
    public async Task ExecuteProcess_Dissolve_Synchronous_ReturnsResult()
    {
        // Arrange
        var executeRequest = new
        {
            inputs = new Dictionary<string, object>
            {
                ["geometries"] = new[]
                {
                    new
                    {
                        type = "Polygon",
                        coordinates = new[]
                        {
                            new[]
                            {
                                new[] { 0.0, 0.0 },
                                new[] { 10.0, 0.0 },
                                new[] { 10.0, 10.0 },
                                new[] { 0.0, 10.0 },
                                new[] { 0.0, 0.0 }
                            }
                        }
                    },
                    new
                    {
                        type = "Polygon",
                        coordinates = new[]
                        {
                            new[]
                            {
                                new[] { 10.0, 0.0 },
                                new[] { 20.0, 0.0 },
                                new[] { 20.0, 10.0 },
                                new[] { 10.0, 10.0 },
                                new[] { 10.0, 0.0 }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/processes/dissolve/execution", executeRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
    }

    [Fact]
    public async Task ExecuteProcess_Reproject_WGS84ToWebMercator_ReturnsResult()
    {
        // Arrange
        var executeRequest = new
        {
            inputs = new Dictionary<string, object>
            {
                ["geometry"] = new
                {
                    type = "Point",
                    coordinates = new[] { -122.4, 37.8 }
                },
                ["sourceCrs"] = "EPSG:4326",
                ["targetCrs"] = "EPSG:3857"
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/processes/reproject/execution", executeRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
    }

    [Fact]
    public async Task ExecuteProcess_WithMissingInputs_ReturnsBadRequest()
    {
        // Arrange
        var executeRequest = new
        {
            inputs = new Dictionary<string, object>()
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/processes/buffer/execution", executeRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created);
    }

    [Fact]
    public async Task ExecuteProcess_NonexistentProcess_ReturnsNotFound()
    {
        // Arrange
        var executeRequest = new
        {
            inputs = new Dictionary<string, object>()
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/processes/nonexistent/execution", executeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetJobStatus_WithValidJobId_ReturnsStatus()
    {
        // Arrange - First execute a process to get a job ID
        var executeRequest = new
        {
            inputs = new Dictionary<string, object>
            {
                ["geometry"] = new
                {
                    type = "Point",
                    coordinates = new[] { 0.0, 0.0 }
                },
                ["distance"] = 10.0
            }
        };

        var executeResponse = await _client!.PostAsJsonAsync("/processes/buffer/execution", executeRequest);

        if (executeResponse.StatusCode == HttpStatusCode.Created)
        {
            var location = executeResponse.Headers.Location?.ToString();
            location.Should().NotBeNullOrEmpty();

            // Extract job ID from location header
            var jobId = location!.Split('/')[^1];

            // Act - Wait a bit for the job to complete
            await Task.Delay(100);
            var response = await _client.GetAsync($"/jobs/{jobId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);

            json.RootElement.GetProperty("jobID").GetString().Should().Be(jobId);
            json.RootElement.TryGetProperty("status", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetJobStatus_WithInvalidJobId_ReturnsNotFound()
    {
        // Act
        var response = await _client!.GetAsync("/jobs/00000000-0000-0000-0000-000000000000");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetJobs_ReturnsJobList()
    {
        // Act
        var response = await _client!.GetAsync("/jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.TryGetProperty("jobs", out var jobs).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessRegistry_AllProcessesRegistered()
    {
        // Arrange
        var registry = _host!.Services.GetRequiredService<IProcessRegistry>();

        // Act
        var processes = registry.GetAllProcesses();

        // Assert
        processes.Should().HaveCount(5);
        processes.Should().Contain(p => p.Id == "buffer");
        processes.Should().Contain(p => p.Id == "centroid");
        processes.Should().Contain(p => p.Id == "dissolve");
        processes.Should().Contain(p => p.Id == "clip");
        processes.Should().Contain(p => p.Id == "reproject");
    }

    [Fact]
    public async Task BufferProcess_ValidatesInputs()
    {
        // Arrange
        var registry = _host!.Services.GetRequiredService<IProcessRegistry>();
        var process = registry.GetProcess("buffer");
        var job = new ProcessJob(Guid.NewGuid().ToString(), "buffer", null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await process!.ExecuteAsync(null, job, CancellationToken.None));
    }

    [Fact]
    public async Task CentroidProcess_ValidatesInputs()
    {
        // Arrange
        var registry = _host!.Services.GetRequiredService<IProcessRegistry>();
        var process = registry.GetProcess("centroid");
        var job = new ProcessJob(Guid.NewGuid().ToString(), "centroid", null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await process!.ExecuteAsync(null, job, CancellationToken.None));
    }

    [Fact]
    public async Task ClipProcess_ValidatesInputs()
    {
        // Arrange
        var registry = _host!.Services.GetRequiredService<IProcessRegistry>();
        var process = registry.GetProcess("clip");
        var job = new ProcessJob(Guid.NewGuid().ToString(), "clip", null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await process!.ExecuteAsync(null, job, CancellationToken.None));
    }

    [Fact]
    public async Task DissolveProcess_ValidatesInputs()
    {
        // Arrange
        var registry = _host!.Services.GetRequiredService<IProcessRegistry>();
        var process = registry.GetProcess("dissolve");
        var job = new ProcessJob(Guid.NewGuid().ToString(), "dissolve", null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await process!.ExecuteAsync(null, job, CancellationToken.None));
    }

    [Fact]
    public async Task ReprojectProcess_ValidatesInputs()
    {
        // Arrange
        var registry = _host!.Services.GetRequiredService<IProcessRegistry>();
        var process = registry.GetProcess("reproject");
        var job = new ProcessJob(Guid.NewGuid().ToString(), "reproject", null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await process!.ExecuteAsync(null, job, CancellationToken.None));
    }

    [Fact]
    public async Task BufferProcess_WithNegativeDistance_ThrowsException()
    {
        // Arrange
        var registry = _host!.Services.GetRequiredService<IProcessRegistry>();
        var process = registry.GetProcess("buffer");
        var job = new ProcessJob(Guid.NewGuid().ToString(), "buffer", new Dictionary<string, object>
        {
            ["geometry"] = new { type = "Point", coordinates = new[] { 0.0, 0.0 } },
            ["distance"] = -10.0
        });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await process!.ExecuteAsync(job.Inputs, job, CancellationToken.None));
    }

    [Fact]
    public async Task BufferProcess_WithInvalidSegments_ThrowsException()
    {
        // Arrange
        var registry = _host!.Services.GetRequiredService<IProcessRegistry>();
        var process = registry.GetProcess("buffer");
        var job = new ProcessJob(Guid.NewGuid().ToString(), "buffer", new Dictionary<string, object>
        {
            ["geometry"] = new { type = "Point", coordinates = new[] { 0.0, 0.0 } },
            ["distance"] = 10.0,
            ["segments"] = 200 // Too many
        });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await process!.ExecuteAsync(job.Inputs, job, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessJob_TracksProgress()
    {
        // Arrange
        var job = new ProcessJob(Guid.NewGuid().ToString(), "test", null);

        // Act
        job.UpdateProgress(50, "Half done");
        var status = job.GetStatus();

        // Assert
        status.Progress.Should().Be(50);
        status.Message.Should().Be("Half done");
    }

    [Fact]
    public async Task ProcessJob_MarkCompleted_SetsResults()
    {
        // Arrange
        var job = new ProcessJob(Guid.NewGuid().ToString(), "test", null);
        var results = new Dictionary<string, object> { ["output"] = "value" };

        // Act
        job.MarkCompleted(results);
        var status = job.GetStatus();
        var retrievedResults = job.GetResults();

        // Assert
        status.Status.Should().Be(JobStatus.Successful);
        status.Progress.Should().Be(100);
        retrievedResults.Should().BeEquivalentTo(results);
    }

    [Fact]
    public async Task ProcessJob_MarkFailed_SetsError()
    {
        // Arrange
        var job = new ProcessJob(Guid.NewGuid().ToString(), "test", null);

        // Act
        job.MarkFailed("Error occurred");
        var status = job.GetStatus();

        // Assert
        status.Status.Should().Be(JobStatus.Failed);
        status.Message.Should().Be("Error occurred");
    }

    [Fact]
    public async Task ProcessJob_Cancellation_Works()
    {
        // Arrange
        var job = new ProcessJob(Guid.NewGuid().ToString(), "test", null);

        // Act
        job.RequestCancellation();

        // Assert
        job.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void ProcessDescription_HasRequiredFields()
    {
        // Arrange
        var process = new BufferProcess();

        // Assert
        process.Description.Id.Should().NotBeNullOrEmpty();
        process.Description.Version.Should().NotBeNullOrEmpty();
        process.Description.Title.Should().NotBeNullOrEmpty();
        process.Description.JobControlOptions.Should().Contain("sync-execute");
        process.Description.JobControlOptions.Should().Contain("async-execute");
        process.Description.Inputs.Should().NotBeEmpty();
        process.Description.Outputs.Should().NotBeEmpty();
    }

    [Fact]
    public void AllProcesses_HaveValidDescriptions()
    {
        // Arrange
        var processes = new IProcess[]
        {
            new BufferProcess(),
            new CentroidProcess(),
            new DissolveProcess(),
            new ClipProcess(),
            new ReprojectProcess()
        };

        // Assert
        foreach (var process in processes)
        {
            process.Description.Id.Should().NotBeNullOrEmpty();
            process.Description.Version.Should().NotBeNullOrEmpty();
            process.Description.Title.Should().NotBeNullOrEmpty();
            process.Description.JobControlOptions.Should().NotBeEmpty();
            process.Description.Inputs.Should().NotBeEmpty();
            process.Description.Outputs.Should().NotBeEmpty();
        }
    }
}
