// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;
using Dapper;
using FluentValidation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.SaaS;

public class DemoSignupFunction
{
    private readonly ILogger<DemoSignupFunction> _logger;
    private readonly string _connectionString;
    private readonly string _subscriptionId;
    private readonly string _resourceGroupName;
    private readonly string _dnsZoneName;
    private readonly int _trialDurationDays;

    public DemoSignupFunction(ILogger<DemoSignupFunction> logger)
    {
        _logger = logger;
        _connectionString = Environment.GetEnvironmentVariable("PostgresConnectionString")
            ?? throw new InvalidOperationException("PostgresConnectionString not configured");
        _subscriptionId = Environment.GetEnvironmentVariable("AzureSubscriptionId")
            ?? throw new InvalidOperationException("AzureSubscriptionId not configured");
        _resourceGroupName = Environment.GetEnvironmentVariable("DnsResourceGroupName")
            ?? throw new InvalidOperationException("DnsResourceGroupName not configured");
        _dnsZoneName = Environment.GetEnvironmentVariable("DnsZoneName") ?? "honua.io";
        _trialDurationDays = int.TryParse(Environment.GetEnvironmentVariable("TrialDurationDays"), out var days) ? days : 14;
    }

    [Function("DemoSignup")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "demo/signup")] HttpRequestData req)
    {
        _logger.LogInformation("Demo signup request received");

        // Parse request body
        DemoSignupRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<DemoSignupRequest>(req.Body);
            if (request == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request body");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in signup request");
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid JSON");
        }

        // Validate request
        var validator = new DemoSignupRequestValidator();
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            _logger.LogWarning("Validation failed: {Errors}", errors);
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, errors);
        }

        // Generate tenant ID from organization name
        var tenantId = GenerateTenantId(request.OrganizationName!);

        // Check if tenant ID is available
        var isAvailable = await IsTenantIdAvailableAsync(tenantId);
        if (!isAvailable)
        {
            // Try with random suffix
            tenantId = $"{tenantId}-{Random.Shared.Next(1000, 9999)}";
            isAvailable = await IsTenantIdAvailableAsync(tenantId);
            if (!isAvailable)
            {
                _logger.LogWarning("Tenant ID {TenantId} not available", tenantId);
                return await CreateErrorResponse(req, HttpStatusCode.Conflict, "Organization name already taken");
            }
        }

        try
        {
            // Create customer and license in database
            var customerId = await CreateCustomerAsync(tenantId, request);
            _logger.LogInformation("Created customer {CustomerId} with tenant ID {TenantId}", customerId, tenantId);

            // Create DNS A record pointing to YARP gateway
            await CreateDnsRecordAsync(tenantId);
            _logger.LogInformation("Created DNS record for {TenantId}.{DnsZone}", tenantId, _dnsZoneName);

            // Create successful response
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new DemoSignupResponse
            {
                TenantId = tenantId,
                OrganizationName = request.OrganizationName!,
                Email = request.Email!,
                Url = $"https://{tenantId}.{_dnsZoneName}",
                TrialExpiresAt = DateTimeOffset.UtcNow.AddDays(_trialDurationDays),
                Message = $"Demo environment created successfully! Your {_trialDurationDays}-day trial has started."
            });

            _logger.LogInformation("Demo signup completed for {TenantId}", tenantId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating demo tenant {TenantId}", tenantId);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to create demo environment");
        }
    }

    private async Task<bool> IsTenantIdAvailableAsync(string tenantId)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM customers WHERE customer_id = @TenantId AND deleted_at IS NULL",
            new { TenantId = tenantId });

        return count == 0;
    }

    private async Task<Guid> CreateCustomerAsync(string tenantId, DemoSignupRequest request)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Insert customer
            var customerId = await connection.ExecuteScalarAsync<Guid>(@"
                INSERT INTO customers (customer_id, organization_name, contact_email, contact_name, subscription_status, tier)
                VALUES (@TenantId, @OrganizationName, @Email, @Name, 'trial', 'core')
                RETURNING id",
                new
                {
                    TenantId = tenantId,
                    request.OrganizationName,
                    request.Email,
                    request.Name
                },
                transaction);

            // Insert license
            var trialExpiresAt = DateTimeOffset.UtcNow.AddDays(_trialDurationDays);
            var licenseKey = GenerateLicenseKey();
            var licenseKeyHash = HashLicenseKey(licenseKey);

            await connection.ExecuteAsync(@"
                INSERT INTO licenses (
                    customer_id, license_key, license_key_hash, tier, status,
                    trial_expires_at, max_builds_per_month, max_registries, max_concurrent_builds
                )
                VALUES (@TenantId, @LicenseKey, @LicenseKeyHash, 'core', 'trial', @TrialExpiresAt, 100, 3, 1)",
                new
                {
                    TenantId = tenantId,
                    LicenseKey = licenseKey,
                    LicenseKeyHash = licenseKeyHash,
                    TrialExpiresAt = trialExpiresAt
                },
                transaction);

            await transaction.CommitAsync();
            return customerId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task CreateDnsRecordAsync(string tenantId)
    {
        var credential = new DefaultAzureCredential();
        var armClient = new ArmClient(credential);

        // Get DNS zone
        var subscriptionResource = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{_subscriptionId}"));
        var resourceGroup = await subscriptionResource.GetResourceGroupAsync(_resourceGroupName);
        var dnsZone = await resourceGroup.Value.GetDnsZoneAsync(_dnsZoneName);

        // Get gateway IP from environment variable
        var gatewayIp = Environment.GetEnvironmentVariable("GatewayPublicIp")
            ?? throw new InvalidOperationException("GatewayPublicIp not configured");

        // Create A record for tenant subdomain
        var aRecordData = new DnsARecordData
        {
            TtlInSeconds = 300,
        };
        aRecordData.DnsARecords.Add(new DnsARecordInfo { IPv4Address = IPAddress.Parse(gatewayIp) });

        await dnsZone.Value.GetDnsARecords().CreateOrUpdateAsync(
            WaitUntil.Completed,
            tenantId,
            aRecordData);
    }

    private string GenerateTenantId(string organizationName)
    {
        // Convert to lowercase, remove special characters, replace spaces with hyphens
        var tenantId = organizationName.ToLowerInvariant();
        tenantId = Regex.Replace(tenantId, @"[^a-z0-9\s-]", "");
        tenantId = Regex.Replace(tenantId, @"\s+", "-");
        tenantId = Regex.Replace(tenantId, @"-+", "-");
        tenantId = tenantId.Trim('-');

        // Ensure it's between 3-20 characters
        if (tenantId.Length < 3) tenantId = tenantId.PadRight(3, 'x');
        if (tenantId.Length > 20) tenantId = tenantId.Substring(0, 20);

        return tenantId;
    }

    private string GenerateLicenseKey()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 24);
    }

    private string HashLicenseKey(string licenseKey)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(licenseKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }
}

public class DemoSignupRequest
{
    public string? Email { get; set; }
    public string? OrganizationName { get; set; }
    public string? Name { get; set; }
}

public class DemoSignupRequestValidator : AbstractValidator<DemoSignupRequest>
{
    public DemoSignupRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("Valid email address is required");

        RuleFor(x => x.OrganizationName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100)
            .WithMessage("Organization name must be between 2 and 100 characters");

        RuleFor(x => x.Name)
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.Name));
    }
}

public class DemoSignupResponse
{
    public string TenantId { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset TrialExpiresAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
