using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;
using Testcontainers.LocalStack;
using Xunit;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

[CollectionDefinition("GcpStorageEmulator")]
public sealed class GcpStorageEmulatorCollection : ICollectionFixture<GcpStorageEmulatorFixture>
{
}

public sealed class GcpStorageEmulatorFixture : IAsyncLifetime
{
    private IContainer? _container;

    public bool IsDockerAvailable { get; private set; }

    public string Endpoint { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            return;
        }

        IsDockerAvailable = await DeploymentEmulatorFixture.IsDockerRunningAsync();
        if (!IsDockerAvailable)
        {
            Console.WriteLine("Docker is unavailable. GCP storage emulator tests will be skipped.");
            return;
        }

        _container = new ContainerBuilder()
            .WithImage("fsouza/fake-gcs-server:1.52.2")
            .WithCommand("-scheme", "http", "-backend", "memory")
            .WithPortBinding(4443, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(4443))
            .Build();

        await _container.StartAsync();
        var port = _container.GetMappedPublicPort(4443);
        var host = _container.Hostname;
        Endpoint = $"http://{host}:{port}";

        await WaitForGcsAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private async Task WaitForGcsAsync()
    {
        if (string.IsNullOrEmpty(Endpoint))
        {
            return;
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri(Endpoint),
            Timeout = TimeSpan.FromSeconds(5)
        };

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("/storage/v1/b?project=health-check");
                if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound)
                {
                    return;
                }
            }
            catch (Exception ex) when (attempt < 4)
            {
                Console.WriteLine($"Fake GCS readiness probe failed (attempt {attempt + 1}): {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new InvalidOperationException("GCS emulator endpoint did not respond within the readiness window.");
    }
}
