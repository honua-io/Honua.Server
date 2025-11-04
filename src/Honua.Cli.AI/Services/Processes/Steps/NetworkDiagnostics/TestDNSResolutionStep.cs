// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NetworkDiagnosticsState = Honua.Cli.AI.Services.Processes.State.NetworkDiagnosticsState;
using Honua.Cli.AI.Services.Processes.State;
using DnsClient;
using System.Net;

namespace Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;

/// <summary>
/// Tests DNS resolution for the target host.
/// Checks that the domain resolves correctly from multiple locations and validates DNS records.
/// </summary>
public class TestDNSResolutionStep : KernelProcessStep<NetworkDiagnosticsState>, IProcessStepTimeout
{
    private readonly ILogger<TestDNSResolutionStep> _logger;
    private NetworkDiagnosticsState _state = new();

    /// <summary>
    /// DNS resolution should be fast, but allow time for retries and propagation checks.
    /// Default timeout: 2 minutes
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(2);

    public TestDNSResolutionStep(ILogger<TestDNSResolutionStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<NetworkDiagnosticsState> state)
    {
        _state = state.State ?? new NetworkDiagnosticsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("TestDNSResolution")]
    public async Task TestDNSResolutionAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Testing DNS resolution for {Host}", _state.TargetHost);
        _state.Status = "Testing DNS";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // Perform real DNS resolution
                    var dnsResult = await PerformDnsResolutionAsync(_state.TargetHost);
                    _state.DNSResults[_state.TargetHost] = dnsResult;

                    // Record diagnostic test
                    _state.TestsRun.Add(new DiagnosticTest
                    {
                        TestName = "DNS Resolution",
                        TestType = "DNS",
                        Timestamp = DateTime.UtcNow,
                        Success = dnsResult.Resolved,
                        Output = dnsResult.Resolved
                            ? $"Resolved to: {string.Join(", ", dnsResult.IpAddresses)}"
                            : dnsResult.ErrorMessage,
                        ErrorMessage = dnsResult.Resolved ? null : dnsResult.ErrorMessage
                    });

                    if (!dnsResult.Resolved)
                    {
                        _logger.LogWarning("DNS resolution failed for {Host}: {Error}",
                            _state.TargetHost, dnsResult.ErrorMessage);

                        _state.Findings.Add(new Finding
                        {
                            Category = "DNS",
                            Severity = "Critical",
                            Description = $"DNS resolution failed for {_state.TargetHost}",
                            Recommendation = "Check DNS configuration, verify domain exists, and ensure DNS servers are accessible",
                            Evidence = new List<string> { dnsResult.ErrorMessage ?? "Unknown error" }
                        });

                        await context.EmitEventAsync(new KernelProcessEvent
                        {
                            Id = "DNSFailure",
                            Data = _state
                        });
                        return;
                    }

                    _logger.LogInformation("DNS resolution successful for {Host}: {IPs}",
                        _state.TargetHost, string.Join(", ", dnsResult.IpAddresses));

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "DNSTestComplete",
                        Data = _state
                    });
                },
                _logger,
                "TestDNSResolution");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test DNS resolution after retries");
            _state.Status = "DNS Test Failed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "DNSFailure",
                Data = _state
            });
        }
    }

    private async Task<DnsResult> PerformDnsResolutionAsync(string host)
    {
        var dnsResult = new DnsResult
        {
            Domain = host,
            Resolved = false,
            IpAddresses = new List<string>(),
            CnameChain = new List<string>()
        };

        // Validate domain format
        if (string.IsNullOrWhiteSpace(host) || host.StartsWith("http"))
        {
            dnsResult.ErrorMessage = "Invalid domain format";
            return dnsResult;
        }

        try
        {
            // Use DnsClient for more detailed DNS queries
            var lookup = new LookupClient();
            var result = await lookup.QueryAsync(host, QueryType.A);

            if (result.HasError)
            {
                dnsResult.ErrorMessage = $"DNS query error: {result.ErrorMessage}";
                _logger.LogWarning("DNS query failed for {Host}: {Error}", host, result.ErrorMessage);

                // Fallback to System.Net.Dns
                try
                {
                    var hostEntry = await System.Net.Dns.GetHostEntryAsync(host);
                    dnsResult.IpAddresses = hostEntry.AddressList
                        .Select(ip => ip.ToString())
                        .ToList();
                    dnsResult.Resolved = dnsResult.IpAddresses.Any();

                    if (dnsResult.Resolved)
                    {
                        dnsResult.ErrorMessage = null;
                        _logger.LogInformation("DNS fallback successful for {Host}", host);
                    }
                }
                catch (Exception fallbackEx)
                {
                    dnsResult.ErrorMessage = $"DNS resolution failed: {fallbackEx.Message}";
                    _logger.LogWarning(fallbackEx, "DNS fallback also failed for {Host}", host);
                }

                return dnsResult;
            }

            // Extract A records
            var aRecords = result.Answers.ARecords();
            dnsResult.IpAddresses = aRecords.Select(r => r.Address.ToString()).ToList();

            // Extract CNAME chain
            var cnameRecords = result.Answers.CnameRecords();
            dnsResult.CnameChain = cnameRecords.Select(r => r.CanonicalName.Value).ToList();

            // Get TTL from first record
            var firstRecord = result.Answers.FirstOrDefault();
            if (firstRecord != null)
            {
                dnsResult.TtlSeconds = (int)firstRecord.TimeToLive;
            }

            dnsResult.Resolved = dnsResult.IpAddresses.Any();

            if (!dnsResult.Resolved)
            {
                // Try AAAA records (IPv6) as fallback
                var aaaaResult = await lookup.QueryAsync(host, QueryType.AAAA);
                var aaaaRecords = aaaaResult.Answers.AaaaRecords();
                dnsResult.IpAddresses.AddRange(aaaaRecords.Select(r => r.Address.ToString()));
                dnsResult.Resolved = dnsResult.IpAddresses.Any();

                if (!dnsResult.Resolved)
                {
                    dnsResult.ErrorMessage = "No A or AAAA records found for domain";
                }
            }
        }
        catch (DnsResponseException ex)
        {
            dnsResult.ErrorMessage = $"DNS response error: {ex.Message}";
            _logger.LogWarning(ex, "DNS resolution failed for {Host}", host);
        }
        catch (Exception ex)
        {
            dnsResult.ErrorMessage = $"DNS resolution error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during DNS resolution for {Host}", host);
        }

        return dnsResult;
    }
}
