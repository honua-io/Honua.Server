using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Discovery;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Discovery;

public sealed class CloudDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_Aws_ReturnsNetworksDatabasesAndZones()
    {
        var cli = new FakeAwsCli();
        var service = new CloudDiscoveryService(
            NullLogger<CloudDiscoveryService>.Instance,
            awsCli: cli);

        var snapshot = await service.DiscoverAsync(new CloudDiscoveryRequest("aws", Region: "us-west-2"), CancellationToken.None);

        cli.Calls.Should().BeEquivalentTo(new[]
        {
            new[] { "ec2", "describe-vpcs", "--output", "json" },
            new[] { "ec2", "describe-subnets", "--output", "json" },
            new[] { "rds", "describe-db-instances", "--output", "json" },
            new[] { "route53", "list-hosted-zones", "--output", "json" }
        });

        snapshot.CloudProvider.Should().Be("AWS");
        snapshot.Networks.Should().HaveCount(1);
        snapshot.Networks[0].Id.Should().Be("vpc-123");
        snapshot.Networks[0].Subnets.Should().ContainSingle()
            .Which.Id.Should().Be("subnet-1");

        snapshot.Databases.Should().ContainSingle()
            .Which.Endpoint.Should().Be("db.example.amazonaws.com");

        snapshot.DnsZones.Should().ContainSingle()
            .Which.Name.Should().Be("example.com");
    }

    [Fact]
    public async Task DiscoverAsync_Azure_ReturnsNetworksDatabasesAndZones()
    {
        var azureCli = new FakeAzureCli();
        var service = new CloudDiscoveryService(
            NullLogger<CloudDiscoveryService>.Instance,
            awsCli: new NoopAwsCli(),
            azureCli: azureCli,
            gcloudCli: new NoopGcloudCli());

        var snapshot = await service.DiscoverAsync(new CloudDiscoveryRequest("azure", Region: "eastus"), CancellationToken.None);

        azureCli.Calls.Should().BeEquivalentTo(new[]
        {
            new[] { "network", "vnet", "list", "--output", "json" },
            new[] { "sql", "server", "list", "--output", "json" },
            new[] { "network", "dns", "zone", "list", "--output", "json" }
        });

        snapshot.CloudProvider.Should().Be("Azure");
        snapshot.Networks.Should().HaveCount(1);
        snapshot.Networks[0].Name.Should().Be("vnet-hub");
        snapshot.Databases.Should().ContainSingle().Which.Endpoint.Should().Be("hub-sql.database.windows.net");
        snapshot.DnsZones.Should().ContainSingle().Which.IsPrivate.Should().BeTrue();
    }

    [Fact]
    public async Task DiscoverAsync_Gcp_ReturnsNetworksDatabasesAndZones()
    {
        var gcloudCli = new FakeGcloudCli();
        var service = new CloudDiscoveryService(
            NullLogger<CloudDiscoveryService>.Instance,
            awsCli: new NoopAwsCli(),
            azureCli: new NoopAzureCli(),
            gcloudCli: gcloudCli);

        var snapshot = await service.DiscoverAsync(new CloudDiscoveryRequest("gcp", Region: "us-central1"), CancellationToken.None);

        gcloudCli.Calls.Should().BeEquivalentTo(new[]
        {
            new[] { "compute", "networks", "list", "--format=json" },
            new[] { "compute", "networks", "subnets", "list", "--format=json" },
            new[] { "sql", "instances", "list", "--format=json" },
            new[] { "dns", "managed-zones", "list", "--format=json" }
        });

        snapshot.CloudProvider.Should().Be("GCP");
        snapshot.Networks.Should().ContainSingle();
        snapshot.Networks[0].Subnets.Should().ContainSingle();
        snapshot.Databases.Should().ContainSingle().Which.Engine.Should().Be("POSTGRES_15");
        snapshot.DnsZones.Should().ContainSingle().Which.Name.Should().Be("example.org");
    }

    private sealed class FakeAwsCli : IAwsCli
    {
        public List<string[]> Calls { get; } = new();

        public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
        {
            Calls.Add(arguments);
            var command = string.Join(" ", arguments);
            return Task.FromResult(command switch
            {
                "ec2 describe-vpcs --output json" => """
{
  "Vpcs": [
    {
      "VpcId": "vpc-123",
      "Tags": [
        { "Key": "Name", "Value": "primary-vpc" }
      ]
    }
  ]
}
""",
                "ec2 describe-subnets --output json" => """
{
  "Subnets": [
    {
      "SubnetId": "subnet-1",
      "VpcId": "vpc-123",
      "CidrBlock": "10.0.1.0/24",
      "AvailabilityZone": "us-west-2a",
      "Tags": [
        { "Key": "Name", "Value": "app-subnet" }
      ]
    }
  ]
}
""",
                "rds describe-db-instances --output json" => """
{
  "DBInstances": [
    {
      "DBInstanceIdentifier": "honua-db",
      "Engine": "postgres",
      "DBInstanceStatus": "available",
      "Endpoint": {
        "Address": "db.example.amazonaws.com",
        "Port": 5432
      }
    }
  ]
}
""",
                "route53 list-hosted-zones --output json" => """
{
  "HostedZones": [
    {
      "Id": "/hostedzone/Z123",
      "Name": "example.com.",
      "Config": {
        "PrivateZone": false
      }
    }
  ]
}
""",
                _ => "{}"
            });
        }
    }

    private sealed class FakeAzureCli : IAzureCli
    {
        public List<string[]> Calls { get; } = new();

        public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
        {
            Calls.Add(arguments);
            var command = string.Join(" ", arguments);
            return Task.FromResult(command switch
            {
                "network vnet list --output json" => """
[
  {
    "id": "/subscriptions/0000/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-hub",
    "name": "vnet-hub",
    "location": "eastus",
    "subnets": [
      {
        "id": "/subscriptions/0000/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-hub/subnets/app",
        "name": "app",
        "addressPrefix": "10.10.1.0/24"
      }
    ]
  }
]
""",
                "sql server list --output json" => """
[
  {
    "id": "/subscriptions/0000/resourceGroups/rg/providers/Microsoft.Sql/servers/hub-sql",
    "name": "hub-sql",
    "location": "eastus",
    "fullyQualifiedDomainName": "hub-sql.database.windows.net",
    "state": "Ready"
  }
]
""",
                "network dns zone list --output json" => """
[
  {
    "id": "/subscriptions/0000/resourceGroups/rg/providers/Microsoft.Network/dnsZones/internal.local",
    "name": "internal.local",
    "zoneType": "Private"
  }
]
""",
                _ => "{}"
            });
        }
    }

    private sealed class FakeGcloudCli : IGcloudCli
    {
        public List<string[]> Calls { get; } = new();

        public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
        {
            Calls.Add(arguments);
            var command = string.Join(" ", arguments);
            return Task.FromResult(command switch
            {
                "compute networks list --format=json" => """
[
  {
    "id": "12345",
    "name": "default",
    "selfLink": "https://www.googleapis.com/compute/v1/projects/project/global/networks/default"
  }
]
""",
                "compute networks subnets list --format=json" => """
[
  {
    "name": "default-us-central1",
    "network": "https://www.googleapis.com/compute/v1/projects/project/global/networks/default",
    "ipCidrRange": "10.128.0.0/20",
    "region": "https://www.googleapis.com/compute/v1/projects/project/regions/us-central1",
    "selfLink": "https://www.googleapis.com/compute/v1/projects/project/regions/us-central1/subnetworks/default"
  }
]
""",
                "sql instances list --format=json" => """
[
  {
    "name": "honua-sql",
    "state": "RUNNABLE",
    "databaseVersion": "POSTGRES_15",
    "ipAddresses": [
      { "ipAddress": "34.111.222.10" }
    ]
  }
]
""",
                "dns managed-zones list --format=json" => """
[
  {
    "id": "5555",
    "name": "example-org",
    "dnsName": "example.org.",
    "visibility": "private"
  }
]
""",
                _ => "[]"
            });
        }
    }

    private sealed class NoopAwsCli : IAwsCli
    {
        public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments) =>
            Task.FromResult(string.Empty);
    }

    private sealed class NoopAzureCli : IAzureCli
    {
        public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments) =>
            Task.FromResult(string.Empty);
    }

    private sealed class NoopGcloudCli : IGcloudCli
    {
        public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments) =>
            Task.FromResult(string.Empty);
    }
}
