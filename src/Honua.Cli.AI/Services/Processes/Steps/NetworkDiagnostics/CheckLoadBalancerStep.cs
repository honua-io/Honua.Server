// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NetworkDiagnosticsState = Honua.Cli.AI.Services.Processes.State.NetworkDiagnosticsState;
using Honua.Cli.AI.Services.Processes.State;
using System.Net.Http;
using System.Net.Sockets;
using System.Diagnostics;

namespace Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;

/// <summary>
/// Checks load balancer health and target status.
/// Validates that load balancer targets are healthy and routing is correct.
/// </summary>
public class CheckLoadBalancerStep : KernelProcessStep<NetworkDiagnosticsState>
{
    private readonly ILogger<CheckLoadBalancerStep> _logger;
    private NetworkDiagnosticsState _state = new();

    public CheckLoadBalancerStep(ILogger<CheckLoadBalancerStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<NetworkDiagnosticsState> state)
    {
        _state = state.State ?? new NetworkDiagnosticsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("CheckLoadBalancer")]
    public async Task CheckLoadBalancerAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Checking load balancer health for {Host}", _state.TargetHost);
        _state.Status = "Checking Load Balancer";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // Perform real load balancer health check
                    var lbHealth = await CheckLoadBalancerHealthAsync(_state.TargetHost, _state.TargetPort ?? 443);
                    _state.LoadBalancerHealth = lbHealth;

                    // Record diagnostic test
                    _state.TestsRun.Add(new DiagnosticTest
                    {
                        TestName = "Load Balancer Health Check",
                        TestType = "LoadBalancer",
                        Timestamp = DateTime.UtcNow,
                        Success = lbHealth.Healthy,
                        Output = lbHealth.Healthy
                            ? $"Load balancer healthy: {lbHealth.HealthyTargetCount} healthy targets"
                            : $"Load balancer issues: {lbHealth.UnhealthyTargetCount} unhealthy targets",
                        ErrorMessage = lbHealth.Healthy ? null : string.Join(", ", lbHealth.Issues)
                    });

                    if (!lbHealth.Healthy || lbHealth.UnhealthyTargetCount > 0)
                    {
                        _logger.LogWarning("Load balancer health issues detected: {UnhealthyCount} unhealthy targets",
                            lbHealth.UnhealthyTargetCount);

                        _state.Findings.Add(new Finding
                        {
                            Category = "LoadBalancer",
                            Severity = lbHealth.HealthyTargetCount == 0 ? "Critical" : "High",
                            Description = $"Load balancer has {lbHealth.UnhealthyTargetCount} unhealthy targets",
                            Recommendation = "Check target instance health, verify health check configuration, and review application logs",
                            Evidence = lbHealth.Targets
                                .Where(t => t.State != "Healthy")
                                .Select(t => $"{t.TargetId}: {t.State} - {t.Reason}")
                                .ToList()
                        });

                        await context.EmitEventAsync(new KernelProcessEvent
                        {
                            Id = "LoadBalancerIssue",
                            Data = _state
                        });
                        return;
                    }

                    _logger.LogInformation("Load balancer health check passed: {HealthyCount} healthy targets",
                        lbHealth.HealthyTargetCount);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "LoadBalancerHealthy",
                        Data = _state
                    });
                },
                _logger,
                "CheckLoadBalancer");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check load balancer health after retries");
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "LoadBalancerIssue",
                Data = _state
            });
        }
    }

    private async Task<LoadBalancerHealth> CheckLoadBalancerHealthAsync(string host, int port)
    {
        var lbHealth = new LoadBalancerHealth
        {
            LoadBalancerName = $"lb-{host}",
            Healthy = false,
            HealthyTargetCount = 0,
            UnhealthyTargetCount = 0,
            Targets = new List<TargetHealth>(),
            HealthCheckSettings = new Dictionary<string, string>
            {
                { "Protocol", "HTTP/HTTPS" },
                { "Path", "/health" },
                { "Method", "GET" }
            },
            Issues = new List<string>()
        };

        // Attempt to detect if this is a load-balanced endpoint
        // by checking for multiple resolved IPs and testing connectivity to each
        var resolvedIps = await ResolveHostIpsAsync(host);

        if (resolvedIps.Count == 0)
        {
            lbHealth.Issues.Add("Unable to resolve host");
            lbHealth.UnhealthyTargetCount = 1;
            return lbHealth;
        }

        // If only one IP, might not be load balanced
        if (resolvedIps.Count == 1)
        {
            _logger.LogInformation("Single IP detected for {Host} - may not be load balanced", host);
            lbHealth.HealthCheckSettings["Note"] = "Single backend detected - may not be using load balancer";
        }

        // Test connectivity to each resolved IP
        var tasks = resolvedIps.Select(ip => TestTargetHealthAsync(ip, port, resolvedIps.IndexOf(ip)));
        var targetHealthResults = await Task.WhenAll(tasks);

        lbHealth.Targets = targetHealthResults.ToList();
        lbHealth.HealthyTargetCount = lbHealth.Targets.Count(t => t.State == "Healthy");
        lbHealth.UnhealthyTargetCount = lbHealth.Targets.Count(t => t.State != "Healthy");
        lbHealth.Healthy = lbHealth.HealthyTargetCount > 0;

        if (lbHealth.UnhealthyTargetCount > 0)
        {
            lbHealth.Issues.Add($"{lbHealth.UnhealthyTargetCount} target(s) are unhealthy");
        }

        // Check for HTTP health endpoint if applicable
        if (port == 80 || port == 443 || port == 8080)
        {
            var healthEndpointCheck = await CheckHttpHealthEndpointAsync(host, port);
            if (!string.IsNullOrEmpty(healthEndpointCheck))
            {
                lbHealth.HealthCheckSettings["HealthEndpointStatus"] = healthEndpointCheck;
            }
        }

        return lbHealth;
    }

    private async Task<List<string>> ResolveHostIpsAsync(string host)
    {
        var ips = new List<string>();

        try
        {
            var hostEntry = await System.Net.Dns.GetHostEntryAsync(host);
            ips = hostEntry.AddressList.Select(ip => ip.ToString()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve IPs for {Host}", host);
        }

        return ips;
    }

    private async Task<TargetHealth> TestTargetHealthAsync(string ipAddress, int port, int targetIndex)
    {
        var targetHealth = new TargetHealth
        {
            TargetId = $"target-{targetIndex}",
            IpAddress = ipAddress,
            Port = port,
            State = "Unhealthy",
            Reason = "Connection failed"
        };

        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(ipAddress, port);
            var timeoutTask = Task.Delay(3000);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == connectTask && tcpClient.Connected)
            {
                targetHealth.State = "Healthy";
                targetHealth.Reason = "TCP connection successful";
            }
            else if (completedTask == timeoutTask)
            {
                targetHealth.State = "Unhealthy";
                targetHealth.Reason = "Connection timeout";
            }
        }
        catch (SocketException ex)
        {
            targetHealth.State = "Unhealthy";
            targetHealth.Reason = ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => "Connection refused - service not running",
                SocketError.TimedOut => "Connection timeout",
                SocketError.HostUnreachable => "Host unreachable",
                _ => $"Socket error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            targetHealth.State = "Unhealthy";
            targetHealth.Reason = $"Error: {ex.Message}";
        }

        return targetHealth;
    }

    private async Task<string> CheckHttpHealthEndpointAsync(string host, int port)
    {
        try
        {
            var protocol = port == 443 ? "https" : "http";
            var healthPaths = new[] { "/health", "/healthz", "/api/health", "/_health" };

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            foreach (var healthPath in healthPaths)
            {
                try
                {
                    var url = $"{protocol}://{host}:{port}{healthPath}";
                    var response = await httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        return $"Health endpoint {healthPath} returned {(int)response.StatusCode}";
                    }
                }
                catch
                {
                    // Continue to next health path
                }
            }

            return "No standard health endpoint found";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not check HTTP health endpoint");
            return "HTTP health check not available";
        }
    }
}
