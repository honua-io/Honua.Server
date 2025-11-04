// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Npgsql;
using DeploymentState = Honua.Cli.AI.Services.Processes.State.DeploymentState;
using Amazon.Route53;
using Amazon.Route53.Model;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;
using Azure.Identity;
using DnsClient;
using DnsClient.Protocol;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Configures PostGIS extensions, S3 buckets, and DNS.
/// </summary>
public class ConfigureServicesStep : KernelProcessStep<DeploymentState>
{
    private readonly ILogger<ConfigureServicesStep> _logger;
    private readonly IAwsCli _awsCli;
    private readonly IAzureCli _azureCli;
    private readonly IGcloudCli _gcloudCli;
    private DeploymentState _state = new();

    public ConfigureServicesStep(
        ILogger<ConfigureServicesStep> logger,
        IAwsCli? awsCli = null,
        IAzureCli? azureCli = null,
        IGcloudCli? gcloudCli = null)
    {
        _logger = logger;
        _awsCli = awsCli ?? DefaultAwsCli.Shared;
        _azureCli = azureCli ?? DefaultAzureCli.Shared;
        _gcloudCli = gcloudCli ?? DefaultGcloudCli.Shared;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
    {
        _state = state.State ?? new DeploymentState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ConfigureServices")]
    public async Task ConfigureServicesAsync(KernelProcessStepContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Configuring services for deployment {DeploymentId}", _state.DeploymentId);

        _state.Status = "ConfiguringServices";

        try
        {
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            // Configure PostGIS
            await ConfigurePostGIS(cancellationToken);

            // Configure storage
            await ConfigureStorage(cancellationToken);

            // Configure DNS
            await ConfigureDNS(cancellationToken);

            _logger.LogInformation("Services configured successfully for {DeploymentId}", _state.DeploymentId);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ServicesConfigured",
                Data = _state
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Service configuration cancelled for {DeploymentId}", _state.DeploymentId);
            _state.Status = "Cancelled";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ConfigurationCancelled",
                Data = new { _state.DeploymentId }
            });
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure services for {DeploymentId}", _state.DeploymentId);
            _state.Status = "ConfigurationFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ConfigurationFailed",
                Data = new { _state.DeploymentId, Error = ex.Message }
            });
        }
    }

    private async Task ConfigurePostGIS(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Installing PostGIS extensions");

        try
        {
            var dbEndpoint = _state.InfrastructureOutputs?["database_endpoint"];
            if (string.IsNullOrEmpty(dbEndpoint))
            {
                _logger.LogWarning("No database endpoint found, skipping PostGIS configuration");
                return;
            }

            // Build connection string
            var connectionString = BuildConnectionString(dbEndpoint);

            // Connect and install extensions
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            _logger.LogInformation("Creating PostGIS extension");
            await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS postgis;", connection))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            _logger.LogInformation("Creating PostGIS topology extension");
            await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS postgis_topology;", connection))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            _logger.LogInformation("Creating PostGIS raster extension");
            await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS postgis_raster;", connection))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Verify installation
            await using (var cmd = new NpgsqlCommand("SELECT PostGIS_version();", connection))
            {
                var version = await cmd.ExecuteScalarAsync(cancellationToken);
                _logger.LogInformation("PostGIS version: {Version}", version);
            }

            _logger.LogInformation("PostGIS extensions installed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure PostGIS extensions");
            throw;
        }
    }

    private async Task ConfigureStorage(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Configuring storage bucket policies");

        try
        {
            var bucketName = _state.InfrastructureOutputs?["storage_bucket"];
            if (string.IsNullOrEmpty(bucketName))
            {
                _logger.LogWarning("No storage bucket found, skipping storage configuration");
                return;
            }

            var provider = _state.CloudProvider.ToLower();
            await (provider switch
            {
                "aws" => ConfigureS3Bucket(bucketName, cancellationToken),
                "azure" => ConfigureAzureStorage(bucketName, cancellationToken),
                "gcp" => ConfigureGCSBucket(bucketName, cancellationToken),
                _ => Task.CompletedTask
            });

            _logger.LogInformation("Storage bucket configured successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure storage bucket");
            throw;
        }
    }

    private async Task ConfigureDNS(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Configuring DNS records");

        try
        {
            var provider = _state.CloudProvider.ToLower();

            // Use custom domain if specified, otherwise default to {DeploymentName}.honua.io
            var dnsName = _state.CustomDomain ?? $"{_state.DeploymentName}.honua.io";

            // Get the load balancer endpoint from infrastructure outputs
            var endpoint = _state.InfrastructureOutputs?.GetValueOrDefault("load_balancer_endpoint", "");

            if (string.IsNullOrEmpty(endpoint))
            {
                _logger.LogWarning("No load balancer endpoint found, skipping DNS configuration");
                return;
            }

            await (provider switch
            {
                "aws" => ConfigureRoute53(dnsName, endpoint, cancellationToken),
                "azure" => ConfigureAzureDNS(dnsName, endpoint, cancellationToken),
                "gcp" => ConfigureCloudDNS(dnsName, endpoint, cancellationToken),
                _ => Task.CompletedTask
            });

            _logger.LogInformation("DNS records configured successfully for {DnsName}", dnsName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure DNS records");
            throw;
        }
    }

    private string BuildConnectionString(string dbEndpoint)
    {
        // Extract host from endpoint (might be in format "host:port" or just "host")
        var host = dbEndpoint.Split(':')[0];
        var port = dbEndpoint.Contains(':') ? dbEndpoint.Split(':')[1] : "5432";

        var outputs = _state.InfrastructureOutputs ?? new Dictionary<string, string>();

        // Gather password with fallbacks for older Terraform output keys
        var password = outputs.GetValueOrDefault("database_password", null)
            ?? outputs.GetValueOrDefault("db_password", null)
            ?? outputs.GetValueOrDefault("rds_password", null)
            ?? outputs.GetValueOrDefault("database_master_password", null)
            ?? outputs.GetValueOrDefault("postgres_password", null)
            ?? string.Empty;

        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                "Database password not found in infrastructure outputs. " +
                "Ensure Terraform outputs include 'database_password' or 'db_password'.");
        }

        var database = outputs.GetValueOrDefault("database_name", "honua") ?? "honua";
        var username = outputs.GetValueOrDefault("database_username", "honua_admin") ?? "honua_admin";
        var sslMode = outputs.GetValueOrDefault("database_ssl_mode", "Require") ?? "Require";
        var trustServerCert = outputs.GetValueOrDefault("database_trust_server_certificate", "false") ?? "false";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode={sslMode};Trust Server Certificate={trustServerCert}";
    }

    private string[] GetAllowedCorsOrigins()
    {
        // In production, these should come from configuration/environment variables
        // Use deployment-specific origins based on tier
        var tier = _state.Tier?.ToLower() ?? "development";

        return tier switch
        {
            "production" => new[]
            {
                "https://app.honua.io",
                "https://honua.io",
                "https://grafana.honua.io"
            },
            "staging" => new[]
            {
                "https://staging.honua.io",
                "https://app-staging.honua.io",
                $"https://grafana.{_state.DeploymentName}.honua.io"
            },
            _ => new[]
            {
                // Development tier - allow localhost and deployment-specific origins
                "http://localhost:3000",
                "http://localhost:5173",
                "http://localhost:8080",
                $"https://{_state.DeploymentName}.honua.io",
                $"https://grafana.{_state.DeploymentName}.honua.io"
            }
        };
    }

    private async Task ConfigureS3Bucket(string bucketName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Configuring S3 bucket: {BucketName}", bucketName);

        // Enable versioning
        await ExecuteAwsCommandAsync(cancellationToken, "s3api", "put-bucket-versioning",
            "--bucket", bucketName,
            "--versioning-configuration", "Status=Enabled");

        // Configure CORS with restrictive policy
        // In production, retrieve allowed origins from configuration/environment
        var allowedOrigins = GetAllowedCorsOrigins();
        var allowedHeaders = new[] { "Authorization", "Content-Type", "Content-Range", "Range" };

        var corsConfig = new
        {
            CORSRules = new[]
            {
                new
                {
                    AllowedOrigins = allowedOrigins,
                    AllowedMethods = new[] { "GET", "HEAD" },
                    AllowedHeaders = allowedHeaders,
                    MaxAgeSeconds = 3000
                }
            }
        };

        var corsJson = System.Text.Json.JsonSerializer.Serialize(corsConfig);
        var corsFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cors-{_state.DeploymentId}.json");
        await System.IO.File.WriteAllTextAsync(corsFile, corsJson, cancellationToken);

        await ExecuteAwsCommandAsync(cancellationToken, "s3api", "put-bucket-cors",
            "--bucket", bucketName,
            "--cors-configuration", $"file://{corsFile}");

        _logger.LogInformation("S3 bucket configured with CORS origins: {Origins}", string.Join(", ", allowedOrigins));
    }

    private async Task ConfigureAzureStorage(string accountName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Configuring Azure Storage: {AccountName}", accountName);

        var resourceGroup = $"{_state.DeploymentName}-rg";

        // Enable blob versioning
        await ExecuteAzCommandAsync(cancellationToken, "storage", "account", "blob-service-properties", "update",
            "--resource-group", resourceGroup,
            "--account-name", accountName,
            "--enable-versioning", "true");

        // Configure CORS with restrictive policy
        // In production, retrieve allowed origins from configuration/environment
        var allowedOrigins = GetAllowedCorsOrigins();
        var allowedHeaders = new[] { "Authorization", "Content-Type", "Content-Range", "Range" };

        // Azure CLI requires space-separated origins
        var originsArg = string.Join(" ", allowedOrigins);
        var headersArg = string.Join(" ", allowedHeaders);

        await ExecuteAzCommandAsync(cancellationToken, "storage", "cors", "add",
            "--services", "b",
            "--methods", "GET", "HEAD",
            "--origins", originsArg,
            "--allowed-headers", headersArg,
            "--max-age", "3000",
            "--account-name", accountName);

        _logger.LogInformation("Azure Storage configured with CORS origins: {Origins}", string.Join(", ", allowedOrigins));
    }

    private async Task ConfigureGCSBucket(string bucketName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Configuring GCS bucket: {BucketName}", bucketName);

        // Enable versioning
        await ExecuteGcloudCommandAsync(cancellationToken, "storage", "buckets", "update",
            bucketName,
            "--versioning");

        // Configure CORS with restrictive policy
        // In production, retrieve allowed origins from configuration/environment
        var allowedOrigins = GetAllowedCorsOrigins();
        var allowedHeaders = new[] { "Authorization", "Content-Type", "Content-Range", "Range" };

        var corsConfig = new[]
        {
            new
            {
                origin = allowedOrigins,
                method = new[] { "GET", "HEAD" },
                responseHeader = allowedHeaders,
                maxAgeSeconds = 3000
            }
        };

        var corsJson = System.Text.Json.JsonSerializer.Serialize(corsConfig);
        var corsFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cors-{_state.DeploymentId}.json");
        await System.IO.File.WriteAllTextAsync(corsFile, corsJson, cancellationToken);

        await ExecuteGcloudCommandAsync(cancellationToken, "storage", "buckets", "update",
            bucketName,
            "--cors-file", corsFile);

        _logger.LogInformation("GCS bucket configured with CORS origins: {Origins}", string.Join(", ", allowedOrigins));
    }

    protected virtual async Task ConfigureRoute53(string dnsName, string endpoint, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating Route53 CNAME record: {DnsName} -> {Endpoint}", dnsName, endpoint);

        try
        {
            using var route53Client = new AmazonRoute53Client();

            string hostedZoneId;

            // Use zone ID from deployment state if provided
            if (!string.IsNullOrEmpty(_state.DnsHostedZoneId))
            {
                hostedZoneId = _state.DnsHostedZoneId;
                _logger.LogInformation("Using hosted zone ID from deployment state: {HostedZoneId}", hostedZoneId);
            }
            else
            {
                // Determine zone name: use from state if provided, otherwise extract from DNS name
                var zoneName = _state.DnsZoneName ?? ExtractZoneName(dnsName);
                _logger.LogInformation("Looking up hosted zone for: {ZoneName}", zoneName);

                // Find the hosted zone
                hostedZoneId = await FindHostedZoneAsync(route53Client, zoneName, cancellationToken);

                if (string.IsNullOrEmpty(hostedZoneId))
                {
                    var errorMsg = $"Route53 hosted zone for {zoneName} not found. Manual DNS configuration required or set DnsHostedZoneId in deployment state.";
                    _logger.LogError(errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }

                _logger.LogInformation("Found hosted zone ID: {HostedZoneId}", hostedZoneId);
            }

            // Create or update CNAME record
            var changeRequest = new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = hostedZoneId,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>
                    {
                        new Change
                        {
                            Action = ChangeAction.UPSERT,
                            ResourceRecordSet = new ResourceRecordSet
                            {
                                Name = dnsName,
                                Type = RRType.CNAME,
                                TTL = 300,
                                ResourceRecords = new List<ResourceRecord>
                                {
                                    new ResourceRecord { Value = endpoint }
                                }
                            }
                        }
                    }
                }
            };

            var response = await route53Client.ChangeResourceRecordSetsAsync(changeRequest, cancellationToken);
            _logger.LogInformation("DNS record created successfully. Change ID: {ChangeId}", response.ChangeInfo.Id);

            // Wait for DNS propagation
            await WaitForRoute53PropagationAsync(route53Client, response.ChangeInfo.Id, cancellationToken);

            // Verify DNS record is resolvable
            await VerifyDnsRecordAsync(dnsName, endpoint, cancellationToken);

            _logger.LogInformation("Route53 DNS configuration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure Route53 DNS record");
            throw;
        }
    }

    protected virtual string ExtractZoneName(string dnsName)
    {
        // Extract the zone name from a full DNS name
        // e.g., "api.staging.example.com" -> "example.com"
        // Logic: Take the last two parts of the domain
        var parts = dnsName.TrimEnd('.').Split('.');
        if (parts.Length >= 2)
        {
            return $"{parts[^2]}.{parts[^1]}";
        }
        return dnsName;
    }

    protected virtual async Task ConfigureAzureDNS(string dnsName, string endpoint, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating Azure DNS CNAME record: {DnsName} -> {Endpoint}", dnsName, endpoint);

        try
        {
            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);

            // Extract zone name from DNS name (e.g., "api.example.com" -> "example.com")
            var zoneName = ExtractZoneName(dnsName);
            var recordName = dnsName.Replace($".{zoneName}", "");

            _logger.LogInformation("Extracted zone name: {ZoneName} and record name: {RecordName} from DNS name: {DnsName}",
                zoneName, recordName, dnsName);

            // Find the DNS zone
            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            if (string.IsNullOrEmpty(subscriptionId))
            {
                var errorMsg = "AZURE_SUBSCRIPTION_ID environment variable not set. Cannot configure Azure DNS.";
                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            // Get resource group from environment or state
            var resourceGroupName = _state.InfrastructureOutputs?.GetValueOrDefault("dns_resource_group", "")
                ?? Environment.GetEnvironmentVariable("AZURE_DNS_RESOURCE_GROUP");

            if (string.IsNullOrEmpty(resourceGroupName))
            {
                var errorMsg = "Azure DNS resource group not found. Set AZURE_DNS_RESOURCE_GROUP environment variable or include 'dns_resource_group' in infrastructure outputs.";
                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            _logger.LogInformation("Looking for DNS zone {ZoneName} in resource group {ResourceGroup}", zoneName, resourceGroupName);

            var subscriptionResourceId = global::Azure.ResourceManager.Resources.SubscriptionResource.CreateResourceIdentifier(subscriptionId);
            var subscription = armClient.GetSubscriptionResource(subscriptionResourceId);
            var resourceGroupResourceId = global::Azure.ResourceManager.Resources.ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName);

            var dnsZoneResourceId = DnsZoneResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, zoneName);
            var dnsZone = armClient.GetDnsZoneResource(dnsZoneResourceId);

            // Create or update CNAME record
            _logger.LogInformation("Creating CNAME record {RecordName} in zone {ZoneName}", recordName, zoneName);

            var recordSetData = new DnsCnameRecordData
            {
                TtlInSeconds = 300,
                Cname = endpoint
            };

            var recordSetCollection = dnsZone.GetDnsCnameRecords();
            var operation = await recordSetCollection.CreateOrUpdateAsync(global::Azure.WaitUntil.Completed, recordName, recordSetData, cancellationToken: cancellationToken);

            _logger.LogInformation("Azure DNS CNAME record created successfully");

            // Verify DNS record is resolvable
            await VerifyDnsRecordAsync(dnsName, endpoint, cancellationToken);

            _logger.LogInformation("Azure DNS configuration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure Azure DNS record");
            throw;
        }
    }

    protected virtual async Task ConfigureCloudDNS(string dnsName, string endpoint, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating Cloud DNS CNAME record: {DnsName} -> {Endpoint}", dnsName, endpoint);

        try
        {
            var projectId = _state.GcpProjectId
                ?? Environment.GetEnvironmentVariable("GCP_PROJECT_ID")
                ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");

            if (string.IsNullOrEmpty(projectId))
            {
                var errorMsg = "GCP project ID not found. Set GCP_PROJECT_ID or GOOGLE_CLOUD_PROJECT environment variable.";
                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            // Determine Cloud DNS zone name from DNS name or infrastructure outputs
            // Cloud DNS zone names use hyphens instead of dots (e.g., "example-com" for "example.com")
            var domainZoneName = ExtractZoneName(dnsName);
            var zoneName = _state.InfrastructureOutputs?.GetValueOrDefault("cloud_dns_zone_name", "")
                ?? Environment.GetEnvironmentVariable("CLOUD_DNS_ZONE_NAME")
                ?? domainZoneName.Replace(".", "-");

            _logger.LogInformation("Using Cloud DNS zone name: {ZoneName} for domain zone: {DomainZoneName}",
                zoneName, domainZoneName);

            var recordName = dnsName.EndsWith(".") ? dnsName : $"{dnsName}.";
            var endpointWithDot = endpoint.EndsWith(".") ? endpoint : $"{endpoint}.";

            _logger.LogInformation("Creating CNAME record {RecordName} in Cloud DNS zone {ZoneName} for project {ProjectId}",
                recordName, zoneName, projectId);

            // Use gcloud CLI for Cloud DNS operations
            // First, check if record exists and delete it if needed
            try
            {
                await ExecuteGcloudCommandAsync(cancellationToken, "dns", "record-sets", "delete",
                    recordName,
                    "--type=CNAME",
                    "--zone", zoneName,
                    "--project", projectId,
                    "--quiet");
                _logger.LogInformation("Deleted existing CNAME record");
            }
            catch
            {
                // Record doesn't exist, this is fine
                _logger.LogInformation("No existing CNAME record to delete");
            }

            // Create the new CNAME record
            await ExecuteGcloudCommandAsync(cancellationToken, "dns", "record-sets", "create",
                recordName,
                "--type=CNAME",
                "--ttl=300",
                $"--rrdatas={endpointWithDot}",
                "--zone", zoneName,
                "--project", projectId);

            _logger.LogInformation("Cloud DNS CNAME record created successfully");

            // Verify DNS record is resolvable
            await VerifyDnsRecordAsync(dnsName.TrimEnd('.'), endpoint.TrimEnd('.'), cancellationToken);

            _logger.LogInformation("Cloud DNS configuration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure Cloud DNS record");
            throw;
        }
    }

    private async Task<string> ExecuteAwsCommandAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        return await _awsCli.ExecuteAsync(cancellationToken, arguments);
    }

    private async Task<string> ExecuteAzCommandAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        return await _azureCli.ExecuteAsync(cancellationToken, arguments);
    }

    private async Task<string> ExecuteGcloudCommandAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        return await _gcloudCli.ExecuteAsync(cancellationToken, arguments);
    }

    private async Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Use ArgumentList to prevent command injection
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException($"Failed to start {command} process");

        // Read stdout and stderr concurrently to prevent deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

        var output = await stdoutTask;
        var error = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError("{Command} command failed: {Error}", command, error);
            throw new InvalidOperationException($"{command} command failed: {error}");
        }

        return output;
    }

    private async Task<string?> FindHostedZoneAsync(IAmazonRoute53 route53Client, string domainName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Looking for Route53 hosted zone for domain {DomainName}", domainName);

        // Paginate through all hosted zones
        string? marker = null;
        bool isTruncated;
        int pageCount = 0;

        do
        {
            var request = new ListHostedZonesRequest();
            if (!string.IsNullOrEmpty(marker))
            {
                request.Marker = marker;
            }

            var response = await route53Client.ListHostedZonesAsync(request, cancellationToken);
            pageCount++;
            _logger.LogInformation("Processing page {PageCount} of hosted zones ({Count} zones in this page)",
                pageCount, response.HostedZones.Count);

            foreach (var zone in response.HostedZones)
            {
                var zoneName = zone.Name.TrimEnd('.');
                if (zoneName.Equals(domainName, StringComparison.OrdinalIgnoreCase))
                {
                    // Strip /hostedzone/ prefix if present
                    var zoneId = zone.Id;
                    if (zoneId.StartsWith("/hostedzone/", StringComparison.OrdinalIgnoreCase))
                    {
                        zoneId = zoneId.Substring("/hostedzone/".Length);
                    }

                    _logger.LogInformation("Found hosted zone {ZoneName} with ID {ZoneId}", zoneName, zoneId);
                    return zoneId;
                }
            }

            // AWS Route53 pagination: check IsTruncated flag and use NextMarker if available
            isTruncated = response.IsTruncated ?? false;
            marker = isTruncated ? response.NextMarker : null;

            // If truncated but no marker, this shouldn't happen per AWS API but handle defensively
            if (isTruncated && string.IsNullOrEmpty(marker))
            {
                _logger.LogWarning("Response is truncated but NextMarker is null. Stopping pagination.");
                break;
            }

        } while (isTruncated);

        _logger.LogWarning("No Route53 hosted zone found for domain {DomainName} after searching {PageCount} pages",
            domainName, pageCount);
        return null;
    }

    protected virtual async Task WaitForRoute53PropagationAsync(IAmazonRoute53 route53Client, string changeId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Waiting for Route53 DNS propagation...");

        var request = new GetChangeRequest { Id = changeId };

        for (int i = 0; i < 30; i++)
        {
            var response = await route53Client.GetChangeAsync(request, cancellationToken);

            if (response.ChangeInfo.Status == ChangeStatus.INSYNC)
            {
                _logger.LogInformation("Route53 DNS change propagated successfully");
                return;
            }

            _logger.LogInformation("Waiting for DNS propagation... (attempt {Attempt}/30)", i + 1);
            await Task.Delay(2000, cancellationToken);
        }

        _logger.LogWarning("Route53 DNS propagation check timed out after 60 seconds");
    }

    protected virtual async Task VerifyDnsRecordAsync(string dnsName, string expectedValue, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Verifying DNS record for {DnsName}", dnsName);

        var lookup = new LookupClient();
        var maxRetries = 10;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var result = await lookup.QueryAsync(dnsName, QueryType.CNAME);
                var cnameRecords = result.Answers.CnameRecords().ToList();

                if (cnameRecords.Any(r => r.CanonicalName.Value.TrimEnd('.') == expectedValue.TrimEnd('.')))
                {
                    _logger.LogInformation("DNS record verified successfully: {DnsName} -> {Value}", dnsName, expectedValue);
                    return;
                }

                _logger.LogInformation("DNS record not yet resolvable, retrying... (attempt {Attempt}/{MaxRetries})", i + 1, maxRetries);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DNS lookup failed, retrying... (attempt {Attempt}/{MaxRetries})", i + 1, maxRetries);
            }

            // Wait before retry
            await Task.Delay(5000, cancellationToken);
        }

        // Log warning but don't fail - DNS might take longer to propagate globally
        _logger.LogWarning("DNS record verification timed out. Record may take longer to propagate globally.");
    }
}
