using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Discovery;

namespace Honua.Cli.AI.Tests.TestInfrastructure;

internal sealed class TestDiscoveryService : ICloudDiscoveryService
{
    public Task<CloudDiscoverySnapshot> DiscoverAsync(CloudDiscoveryRequest request, CancellationToken cancellationToken)
    {
        var snapshot = new CloudDiscoverySnapshot(
            CloudProvider: request.CloudProvider,
            Networks: Array.Empty<DiscoveredNetwork>(),
            Databases: Array.Empty<DiscoveredDatabase>(),
            DnsZones: Array.Empty<DiscoveredDnsZone>());

        return Task.FromResult(snapshot);
    }
}
