// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NetworkDiagnosticsState = Honua.Cli.AI.Services.Processes.State.NetworkDiagnosticsState;
using Honua.Cli.AI.Services.Processes.State;
using System.Net.Sockets;
using System.Diagnostics;

namespace Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;

/// <summary>
/// Validates security group and firewall rules.
/// Checks that inbound and outbound rules allow the required traffic.
/// </summary>
public class CheckFirewallRulesStep : KernelProcessStep<NetworkDiagnosticsState>
{
    private readonly ILogger<CheckFirewallRulesStep> _logger;
    private NetworkDiagnosticsState _state = new();

    public CheckFirewallRulesStep(ILogger<CheckFirewallRulesStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<NetworkDiagnosticsState> state)
    {
        _state = state.State ?? new NetworkDiagnosticsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("CheckFirewallRules")]
    public async Task CheckFirewallRulesAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Checking firewall rules for {Host}:{Port}",
            _state.TargetHost, _state.TargetPort ?? 443);
        _state.Status = "Checking Firewall";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    var port = _state.TargetPort ?? 443;

                    // Attempt to check local firewall rules and perform connectivity test
                    var firewallCheck = await CheckFirewallAccessibilityAsync(_state.TargetHost, port);
                    _state.SecurityGroupRules = firewallCheck.Rules;
                    _state.SecurityGroupsValid = firewallCheck.IsAccessible;

                    // Record diagnostic test
                    _state.TestsRun.Add(new DiagnosticTest
                    {
                        TestName = "Firewall/Security Group Validation",
                        TestType = "Security",
                        Timestamp = DateTime.UtcNow,
                        Success = firewallCheck.IsAccessible,
                        Output = firewallCheck.Output,
                        ErrorMessage = firewallCheck.ErrorMessage
                    });

                    if (!firewallCheck.IsAccessible)
                    {
                        _logger.LogWarning("Firewall or security groups may be blocking port {Port} for {Host}", port, _state.TargetHost);

                        _state.Findings.Add(new Finding
                        {
                            Category = "Security",
                            Severity = "High",
                            Description = $"Firewall or security group rules may be blocking traffic on port {port}",
                            Recommendation = firewallCheck.Recommendation ?? "Check firewall rules, security groups (AWS/Azure), and network ACLs to ensure traffic is allowed",
                            Evidence = firewallCheck.Evidence
                        });

                        await context.EmitEventAsync(new KernelProcessEvent
                        {
                            Id = "SecurityGroupIssue",
                            Data = _state
                        });
                        return;
                    }

                    _logger.LogInformation("Firewall accessibility check passed for port {Port}", port);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "SecurityGroupsValid",
                        Data = _state
                    });
                },
                _logger,
                "CheckFirewallRules");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check firewall rules after retries");
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "SecurityGroupIssue",
                Data = _state
            });
        }
    }

    private async Task<FirewallCheckResult> CheckFirewallAccessibilityAsync(string host, int port)
    {
        var result = new FirewallCheckResult
        {
            IsAccessible = false,
            Rules = new List<SecurityGroupRule>(),
            Evidence = new List<string>()
        };

        // Check local firewall rules if on Windows/Linux
        await CheckLocalFirewallRulesAsync(result, port);

        // Perform actual connectivity test to determine if firewall allows traffic
        var connectivityTest = await TestFirewallConnectivityAsync(host, port);

        if (connectivityTest.Success)
        {
            result.IsAccessible = true;
            result.Output = $"Port {port} is accessible - firewall rules appear to allow traffic";
            result.Evidence.Add($"Successfully connected to {host}:{port}");
        }
        else
        {
            result.IsAccessible = false;
            result.Output = $"Port {port} is not accessible - may be blocked by firewall or security groups";
            result.ErrorMessage = connectivityTest.ErrorMessage;
            result.Evidence.Add($"Connection failed: {connectivityTest.ErrorMessage}");
            result.Recommendation = "Verify firewall rules allow inbound traffic on port " + port +
                ". For cloud deployments, check security groups (AWS), NSGs (Azure), or firewall rules (GCP).";
        }

        return result;
    }

    private async Task CheckLocalFirewallRulesAsync(FirewallCheckResult result, int port)
    {
        try
        {
            // Check Windows Firewall on Windows
            if (OperatingSystem.IsWindows())
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall show rule name=all | findstr {port}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        result.Evidence.Add($"Windows Firewall rules found for port {port}");
                        result.Rules.Add(new SecurityGroupRule
                        {
                            RuleId = "local-windows-fw",
                            Direction = "Inbound",
                            Protocol = "TCP",
                            PortRange = port.ToString(),
                            Source = "Any",
                            Action = "Check manually"
                        });
                    }
                }
            }
            // Check iptables on Linux
            else if (OperatingSystem.IsLinux())
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "iptables",
                    Arguments = "-L -n",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        result.Evidence.Add("Local iptables rules detected - manual review recommended");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Local firewall check is best-effort, don't fail if we can't check
            _logger.LogDebug(ex, "Could not check local firewall rules");
        }
    }

    private async Task<NetworkTest> TestFirewallConnectivityAsync(string host, int port)
    {
        var test = new NetworkTest
        {
            TestType = "FirewallConnectivity",
            TargetHost = host,
            Port = port,
            Success = false
        };

        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(3000); // Shorter timeout for firewall check

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == connectTask && tcpClient.Connected)
            {
                test.Success = true;
                test.Output = "Connection successful";
            }
            else if (completedTask == timeoutTask)
            {
                test.ErrorMessage = "Connection timeout - likely blocked by firewall";
            }
            else
            {
                test.ErrorMessage = "Connection failed";
            }
        }
        catch (SocketException ex)
        {
            switch (ex.SocketErrorCode)
            {
                case SocketError.ConnectionRefused:
                    test.ErrorMessage = "Connection refused - service not running or firewall blocking";
                    break;
                case SocketError.TimedOut:
                    test.ErrorMessage = "Connection timed out - likely firewall filtering traffic";
                    break;
                default:
                    test.ErrorMessage = $"Socket error: {ex.Message}";
                    break;
            }
        }
        catch (Exception ex)
        {
            test.ErrorMessage = $"Error: {ex.Message}";
        }

        return test;
    }

    private class FirewallCheckResult
    {
        public bool IsAccessible { get; set; }
        public string Output { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public List<SecurityGroupRule> Rules { get; set; } = new();
        public List<string> Evidence { get; set; } = new();
        public string? Recommendation { get; set; }
    }
}
