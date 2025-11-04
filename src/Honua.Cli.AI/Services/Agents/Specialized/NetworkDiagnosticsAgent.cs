// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Diagnoses network connectivity issues - DNS, ports, firewalls, routing.
/// Critical for fast diagnosis of deployment failures caused by network configuration.
/// </summary>
public sealed class NetworkDiagnosticsAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<NetworkDiagnosticsAgent> _logger;

    public NetworkDiagnosticsAgent(
        Kernel kernel,
        ILlmProvider llmProvider,
        ILogger<NetworkDiagnosticsAgent> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs comprehensive network diagnostics for a deployment endpoint.
    /// Fast execution (5-10 seconds) for rapid troubleshooting.
    /// </summary>
    public async Task<NetworkDiagnosticsResult> DiagnoseAsync(
        NetworkDiagnosticsRequest request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running network diagnostics for {Target}", request.TargetUrl ?? request.TargetHost);

        var checks = new List<NetworkCheck>();
        var sw = Stopwatch.StartNew();

        try
        {
            // Parse target
            string targetHost;
            int targetPort = 80;

            if (!request.TargetUrl.IsNullOrEmpty())
            {
                var uri = new Uri(request.TargetUrl);
                targetHost = uri.Host;
                targetPort = uri.Port;
            }
            else
            {
                targetHost = request.TargetHost!;
                targetPort = request.TargetPort ?? 80;
            }

            // 1. DNS Resolution Check (parallel for speed)
            var dnsTask = CheckDnsResolutionAsync(targetHost, cancellationToken);

            // 2. Ping Check (parallel)
            var pingTask = CheckPingAsync(targetHost, cancellationToken);

            // 3. Port Connectivity Check (parallel)
            var portTask = CheckPortConnectivityAsync(targetHost, targetPort, cancellationToken);

            // Wait for all parallel checks
            await Task.WhenAll(dnsTask, pingTask, portTask).ConfigureAwait(false);

            checks.Add(await dnsTask);
            checks.Add(await pingTask);
            checks.Add(await portTask);

            // 4. Additional port checks if specified
            if (request.AdditionalPorts?.Any() == true)
            {
                var additionalPortTasks = request.AdditionalPorts
                    .Select(port => CheckPortConnectivityAsync(targetHost, port, cancellationToken))
                    .ToList();

                var additionalResults = await Task.WhenAll(additionalPortTasks).ConfigureAwait(false);
                checks.AddRange(additionalResults);
            }

            // 5. Traceroute (only if requested - takes longer)
            if (request.IncludeTraceroute)
            {
                checks.Add(await CheckTracerouteAsync(targetHost, cancellationToken));
            }

            // 6. Firewall/Security Group detection
            if (request.DetectFirewallIssues)
            {
                checks.Add(DetectFirewallIssues(checks, targetHost, targetPort));
            }

            sw.Stop();

            // Analyze results
            var failed = checks.Count(c => c.Status == NetworkCheckStatus.Failed);
            var warnings = checks.Count(c => c.Status == NetworkCheckStatus.Warning);
            var passed = checks.Count(c => c.Status == NetworkCheckStatus.Passed);

            var overallStatus = failed > 0 ? NetworkCheckStatus.Failed :
                               warnings > 0 ? NetworkCheckStatus.Warning :
                               NetworkCheckStatus.Passed;

            return new NetworkDiagnosticsResult
            {
                TargetHost = targetHost,
                TargetPort = targetPort,
                OverallStatus = overallStatus,
                Checks = checks,
                PassedChecks = passed,
                WarningChecks = warnings,
                FailedChecks = failed,
                TotalDurationMs = (int)sw.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow,
                Summary = GenerateSummary(checks, overallStatus, targetHost, targetPort),
                Recommendations = GenerateRecommendations(checks, targetHost, targetPort)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Network diagnostics failed");

            return new NetworkDiagnosticsResult
            {
                TargetHost = request.TargetHost ?? "unknown",
                TargetPort = request.TargetPort ?? 0,
                OverallStatus = NetworkCheckStatus.Failed,
                Checks = checks,
                PassedChecks = 0,
                WarningChecks = 0,
                FailedChecks = checks.Count,
                TotalDurationMs = (int)sw.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow,
                Summary = $"Diagnostics error: {ex.Message}",
                Recommendations = new List<string> { "Check target URL/host format", "Verify network connectivity" }
            };
        }
    }

    /// <summary>
    /// Checks DNS resolution for the target host.
    /// </summary>
    private async Task<NetworkCheck> CheckDnsResolutionAsync(string host, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(host, cancellationToken);
            sw.Stop();

            if (addresses.Length == 0)
            {
                return new NetworkCheck
                {
                    CheckType = "DNS Resolution",
                    Status = NetworkCheckStatus.Failed,
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    Message = $"DNS resolution failed: No IP addresses found for {host}",
                    Details = new Dictionary<string, string>
                    {
                        ["host"] = host,
                        ["result"] = "no_addresses"
                    }
                };
            }

            var ipv4Addresses = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToArray();
            var ipv6Addresses = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6).ToArray();

            return new NetworkCheck
            {
                CheckType = "DNS Resolution",
                Status = NetworkCheckStatus.Passed,
                DurationMs = (int)sw.ElapsedMilliseconds,
                Message = $"DNS resolved: {addresses.Length} address(es) ({ipv4Addresses.Length} IPv4, {ipv6Addresses.Length} IPv6)",
                Details = new Dictionary<string, string>
                {
                    ["host"] = host,
                    ["ipv4_addresses"] = string.Join(", ", ipv4Addresses.Select(a => a.ToString())),
                    ["ipv6_addresses"] = string.Join(", ", ipv6Addresses.Select(a => a.ToString())),
                    ["resolution_time_ms"] = sw.ElapsedMilliseconds.ToString()
                }
            };
        }
        catch (SocketException ex)
        {
            sw.Stop();
            return new NetworkCheck
            {
                CheckType = "DNS Resolution",
                Status = NetworkCheckStatus.Failed,
                DurationMs = (int)sw.ElapsedMilliseconds,
                Message = $"DNS resolution failed: {ex.Message}",
                Details = new Dictionary<string, string>
                {
                    ["host"] = host,
                    ["error"] = ex.SocketErrorCode.ToString(),
                    ["error_code"] = ((int)ex.SocketErrorCode).ToString()
                }
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new NetworkCheck
            {
                CheckType = "DNS Resolution",
                Status = NetworkCheckStatus.Failed,
                DurationMs = (int)sw.ElapsedMilliseconds,
                Message = $"DNS resolution error: {ex.Message}",
                Details = new Dictionary<string, string>
                {
                    ["host"] = host,
                    ["error_type"] = ex.GetType().Name
                }
            };
        }
    }

    /// <summary>
    /// Checks ICMP ping connectivity (if allowed by firewall).
    /// </summary>
    private async Task<NetworkCheck> CheckPingAsync(string host, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 2000); // 2 second timeout
            sw.Stop();

            if (reply.Status == IPStatus.Success)
            {
                return new NetworkCheck
                {
                    CheckType = "ICMP Ping",
                    Status = NetworkCheckStatus.Passed,
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    Message = $"Ping successful: {reply.RoundtripTime}ms, TTL={reply.Options?.Ttl}",
                    Details = new Dictionary<string, string>
                    {
                        ["host"] = host,
                        ["ip_address"] = reply.Address?.ToString() ?? "unknown",
                        ["roundtrip_ms"] = reply.RoundtripTime.ToString(),
                        ["ttl"] = reply.Options?.Ttl.ToString() ?? "unknown"
                    }
                };
            }
            else if (reply.Status == IPStatus.TimedOut)
            {
                return new NetworkCheck
                {
                    CheckType = "ICMP Ping",
                    Status = NetworkCheckStatus.Warning,
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    Message = "Ping timeout (ICMP may be blocked by firewall)",
                    Details = new Dictionary<string, string>
                    {
                        ["host"] = host,
                        ["status"] = "timeout",
                        ["note"] = "Many firewalls block ICMP - this may not indicate a problem"
                    }
                };
            }
            else
            {
                return new NetworkCheck
                {
                    CheckType = "ICMP Ping",
                    Status = NetworkCheckStatus.Warning,
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    Message = $"Ping failed: {reply.Status}",
                    Details = new Dictionary<string, string>
                    {
                        ["host"] = host,
                        ["status"] = reply.Status.ToString()
                    }
                };
            }
        }
        catch (PingException ex)
        {
            sw.Stop();
            return new NetworkCheck
            {
                CheckType = "ICMP Ping",
                Status = NetworkCheckStatus.Warning,
                DurationMs = (int)sw.ElapsedMilliseconds,
                Message = $"Ping error (may be blocked): {ex.Message}",
                Details = new Dictionary<string, string>
                {
                    ["host"] = host,
                    ["note"] = "ICMP may be blocked by firewall - check TCP connectivity instead"
                }
            };
        }
    }

    /// <summary>
    /// Checks TCP port connectivity.
    /// </summary>
    private async Task<NetworkCheck> CheckPortConnectivityAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var tcpClient = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3)); // 3 second timeout per port

            await tcpClient.ConnectAsync(host, port, cts.Token);
            sw.Stop();

            return new NetworkCheck
            {
                CheckType = $"TCP Port {port}",
                Status = NetworkCheckStatus.Passed,
                DurationMs = (int)sw.ElapsedMilliseconds,
                Message = $"Port {port} is open and accepting connections",
                Details = new Dictionary<string, string>
                {
                    ["host"] = host,
                    ["port"] = port.ToString(),
                    ["connect_time_ms"] = sw.ElapsedMilliseconds.ToString(),
                    ["local_endpoint"] = tcpClient.Client.LocalEndPoint?.ToString() ?? "unknown"
                }
            };
        }
        catch (SocketException ex)
        {
            sw.Stop();

            var status = ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => NetworkCheckStatus.Failed,
                SocketError.TimedOut => NetworkCheckStatus.Failed,
                SocketError.HostUnreachable => NetworkCheckStatus.Failed,
                SocketError.NetworkUnreachable => NetworkCheckStatus.Failed,
                _ => NetworkCheckStatus.Warning
            };

            var message = ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => $"Port {port} closed (connection refused) - service not running or firewall blocking",
                SocketError.TimedOut => $"Port {port} timeout - firewall may be dropping packets",
                SocketError.HostUnreachable => $"Host unreachable - routing issue or host down",
                SocketError.NetworkUnreachable => $"Network unreachable - routing configuration problem",
                _ => $"Port {port} connection error: {ex.Message}"
            };

            return new NetworkCheck
            {
                CheckType = $"TCP Port {port}",
                Status = status,
                DurationMs = (int)sw.ElapsedMilliseconds,
                Message = message,
                Details = new Dictionary<string, string>
                {
                    ["host"] = host,
                    ["port"] = port.ToString(),
                    ["error"] = ex.SocketErrorCode.ToString(),
                    ["error_code"] = ((int)ex.SocketErrorCode).ToString()
                }
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new NetworkCheck
            {
                CheckType = $"TCP Port {port}",
                Status = NetworkCheckStatus.Failed,
                DurationMs = (int)sw.ElapsedMilliseconds,
                Message = $"Port {port} connection timeout - firewall likely blocking traffic",
                Details = new Dictionary<string, string>
                {
                    ["host"] = host,
                    ["port"] = port.ToString(),
                    ["timeout_ms"] = "3000"
                }
            };
        }
    }

    /// <summary>
    /// Performs traceroute to identify routing issues.
    /// </summary>
    private async Task<NetworkCheck> CheckTracerouteAsync(string host, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var hops = new List<string>();
            var maxHops = 15;
            var timeout = 1000;

            using var ping = new Ping();
            var options = new PingOptions(1, true); // Start with TTL=1

            for (int ttl = 1; ttl <= maxHops; ttl++)
            {
                options.Ttl = ttl;

                try
                {
                    var reply = await ping.SendPingAsync(host, timeout, new byte[32], options);

                    if (reply.Status == IPStatus.Success)
                    {
                        hops.Add($"{ttl}: {reply.Address} ({reply.RoundtripTime}ms) - Destination reached");
                        break;
                    }
                    else if (reply.Status == IPStatus.TtlExpired)
                    {
                        hops.Add($"{ttl}: {reply.Address} ({reply.RoundtripTime}ms)");
                    }
                    else if (reply.Status == IPStatus.TimedOut)
                    {
                        hops.Add($"{ttl}: * * * (timeout)");
                    }
                    else
                    {
                        hops.Add($"{ttl}: {reply.Status}");
                    }
                }
                catch
                {
                    hops.Add($"{ttl}: * * * (error)");
                }

                if (cancellationToken.IsCancellationRequested)
                    break;
            }

            sw.Stop();

            return new NetworkCheck
            {
                CheckType = "Traceroute",
                Status = NetworkCheckStatus.Passed,
                DurationMs = (int)sw.ElapsedMilliseconds,
                Message = $"Traceroute completed: {hops.Count} hops",
                Details = new Dictionary<string, string>
                {
                    ["host"] = host,
                    ["hops"] = string.Join("\n", hops),
                    ["hop_count"] = hops.Count.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new NetworkCheck
            {
                CheckType = "Traceroute",
                Status = NetworkCheckStatus.Warning,
                DurationMs = (int)sw.ElapsedMilliseconds,
                Message = $"Traceroute failed: {ex.Message}",
                Details = new Dictionary<string, string>
                {
                    ["host"] = host,
                    ["error"] = ex.Message
                }
            };
        }
    }

    /// <summary>
    /// Analyzes check results to detect firewall/security group issues.
    /// </summary>
    private NetworkCheck DetectFirewallIssues(List<NetworkCheck> checks, string host, int port)
    {
        var dnsCheck = checks.FirstOrDefault(c => c.CheckType == "DNS Resolution");
        var pingCheck = checks.FirstOrDefault(c => c.CheckType == "ICMP Ping");
        var portCheck = checks.FirstOrDefault(c => c.CheckType == $"TCP Port {port}");

        var issues = new List<string>();

        // Pattern 1: DNS works, ping fails, port fails
        if (dnsCheck?.Status == NetworkCheckStatus.Passed &&
            pingCheck?.Status != NetworkCheckStatus.Passed &&
            portCheck?.Status == NetworkCheckStatus.Failed)
        {
            if (portCheck.Message.Contains("timeout"))
            {
                issues.Add("Firewall is likely dropping packets (silent drop)");
                issues.Add($"Security group or firewall blocking port {port}");
            }
            else if (portCheck.Message.Contains("refused"))
            {
                issues.Add("Service not running or firewall explicitly rejecting connections");
            }
        }

        // Pattern 2: DNS works, everything else fails
        if (dnsCheck?.Status == NetworkCheckStatus.Passed &&
            pingCheck?.Status != NetworkCheckStatus.Passed &&
            portCheck?.Status == NetworkCheckStatus.Failed)
        {
            issues.Add("Host is resolvable but unreachable");
            issues.Add("Possible causes: Host down, network partition, or firewall blocking all traffic");
        }

        // Pattern 3: DNS works, ping times out (common with cloud firewalls)
        if (dnsCheck?.Status == NetworkCheckStatus.Passed &&
            pingCheck?.Status == NetworkCheckStatus.Warning &&
            pingCheck.Message.Contains("timeout"))
        {
            issues.Add("ICMP blocked (expected in most cloud environments)");
        }

        var status = issues.Any(i => i.Contains("likely") || i.Contains("blocking"))
            ? NetworkCheckStatus.Warning
            : NetworkCheckStatus.Passed;

        return new NetworkCheck
        {
            CheckType = "Firewall Detection",
            Status = status,
            DurationMs = 0,
            Message = issues.Any()
                ? $"Detected {issues.Count} potential firewall/security issues"
                : "No obvious firewall issues detected",
            Details = new Dictionary<string, string>
            {
                ["issues"] = string.Join("\n", issues.Select((issue, i) => $"{i + 1}. {issue}")),
                ["dns_status"] = dnsCheck?.Status.ToString() ?? "unknown",
                ["ping_status"] = pingCheck?.Status.ToString() ?? "unknown",
                ["port_status"] = portCheck?.Status.ToString() ?? "unknown"
            }
        };
    }

    private string GenerateSummary(List<NetworkCheck> checks, NetworkCheckStatus overallStatus, string host, int port)
    {
        var sb = new StringBuilder();

        if (overallStatus == NetworkCheckStatus.Passed)
        {
            sb.AppendLine($"✅ Network connectivity to {host}:{port} is healthy");
        }
        else if (overallStatus == NetworkCheckStatus.Warning)
        {
            sb.AppendLine($"⚠️  Network connectivity to {host}:{port} has warnings");
        }
        else
        {
            sb.AppendLine($"❌ Network connectivity to {host}:{port} failed");
        }

        sb.AppendLine();
        sb.AppendLine($"Checks: {checks.Count(c => c.Status == NetworkCheckStatus.Passed)} passed, " +
                     $"{checks.Count(c => c.Status == NetworkCheckStatus.Warning)} warnings, " +
                     $"{checks.Count(c => c.Status == NetworkCheckStatus.Failed)} failed");

        var failedChecks = checks.Where(c => c.Status == NetworkCheckStatus.Failed).ToList();
        if (failedChecks.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Failed checks:");
            foreach (var check in failedChecks)
            {
                sb.AppendLine($"  • {check.CheckType}: {check.Message}");
            }
        }

        return sb.ToString();
    }

    private List<string> GenerateRecommendations(List<NetworkCheck> checks, string host, int port)
    {
        var recommendations = new List<string>();

        var dnsCheck = checks.FirstOrDefault(c => c.CheckType == "DNS Resolution");
        var portCheck = checks.FirstOrDefault(c => c.CheckType == $"TCP Port {port}");

        // DNS failed
        if (dnsCheck?.Status == NetworkCheckStatus.Failed)
        {
            recommendations.Add($"DNS resolution failed for {host} - verify hostname is correct");
            recommendations.Add("Check DNS configuration or use IP address directly");
            return recommendations;
        }

        // Port connection issues
        if (portCheck?.Status == NetworkCheckStatus.Failed)
        {
            if (portCheck.Message.Contains("timeout"))
            {
                recommendations.Add($"Port {port} timeout - check security group/firewall rules");
                recommendations.Add("Verify inbound rules allow traffic on port " + port);
                recommendations.Add("For cloud deployments: Check VPC security groups, NACLs, and firewall rules");
            }
            else if (portCheck.Message.Contains("refused"))
            {
                recommendations.Add($"Port {port} connection refused - service may not be running");
                recommendations.Add($"Verify the application is listening on port {port}");
                recommendations.Add("Check service status and logs");
            }
            else if (portCheck.Message.Contains("unreachable"))
            {
                recommendations.Add("Host unreachable - check routing configuration");
                recommendations.Add("Verify VPC routing tables and internet gateway configuration");
            }
        }

        // Localhost detection - suggest tunnel
        if (host == "localhost" || host == "127.0.0.1" || host == "0.0.0.0")
        {
            recommendations.Add("💡 Running locally? Use 'honua tunnel start' to expose your local Honua instance to the web");
            recommendations.Add("Tunnel options: ngrok, Cloudflare Tunnel, localtunnel, or localhost.run");
        }

        // All checks passed
        if (!recommendations.Any())
        {
            recommendations.Add("Network connectivity is healthy");
            recommendations.Add("If application still not responding, check application logs and health endpoints");
        }

        return recommendations;
    }

    /// <summary>
    /// Creates a tunnel to expose local Honua to the internet.
    /// </summary>
    public async Task<TunnelResult> CreateTunnelAsync(
        TunnelRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating {Provider} tunnel for port {Port}", request.Provider, request.LocalPort);

        try
        {
            return request.Provider.ToLower() switch
            {
                "ngrok" => await CreateNgrokTunnelAsync(request.LocalPort, request.AuthToken, cancellationToken),
                "cloudflare" => await CreateCloudflareTunnelAsync(request.LocalPort, cancellationToken),
                "localtunnel" => await CreateLocaltunnelAsync(request.LocalPort, request.Subdomain, cancellationToken),
                "localhost.run" => await CreateLocalhostRunTunnelAsync(request.LocalPort, cancellationToken),
                _ => throw new ArgumentException($"Unsupported tunnel provider: {request.Provider}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tunnel");
            return new TunnelResult
            {
                Success = false,
                Provider = request.Provider,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<TunnelResult> CreateNgrokTunnelAsync(int port, string? authToken, CancellationToken cancellationToken)
    {
        // Check if ngrok is installed and available on PATH
        var ngrokCheck = await RunCommandAsync("ngrok", "version", cancellationToken);
        if (!ngrokCheck.success)
        {
            // Check if ngrok exists in common installation paths
            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ngrok", "ngrok.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ngrok", "ngrok.exe"),
                "/usr/local/bin/ngrok",
                "/usr/bin/ngrok"
            };

            var ngrokPath = commonPaths.FirstOrDefault(File.Exists);
            if (ngrokPath == null)
            {
                return new TunnelResult
                {
                    Success = false,
                    Provider = "ngrok",
                    ErrorMessage = "ngrok not found on PATH or in common installation locations",
                    InstallInstructions = "Download ngrok from https://ngrok.com/download or use: brew install ngrok (macOS), choco install ngrok (Windows), or snap install ngrok (Linux)"
                };
            }

            _logger.LogWarning("ngrok not on PATH but found at {NgrokPath}. Consider adding to PATH.", ngrokPath);
        }

        // Set auth token if provided
        // Write directly to ngrok config file to prevent exposure in process list and shell history
        if (!authToken.IsNullOrEmpty())
        {
            try
            {
                // Get ngrok config directory
                var configDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var ngrokConfigPath = Path.Combine(configDir, ".ngrok2", "ngrok.yml");

                // Ensure config directory exists
                var ngrokDir = Path.GetDirectoryName(ngrokConfigPath);
                if (!Directory.Exists(ngrokDir))
                {
                    Directory.CreateDirectory(ngrokDir!);
                }

                // Read existing config or create new
                string configContent;
                if (File.Exists(ngrokConfigPath))
                {
                    configContent = await File.ReadAllTextAsync(ngrokConfigPath, cancellationToken);

                    // Update or add authtoken line
                    if (configContent.Contains("authtoken:"))
                    {
                        // Replace existing authtoken
                        var lines = configContent.Split('\n');
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].TrimStart().StartsWith("authtoken:"))
                            {
                                lines[i] = $"authtoken: {authToken}";
                                break;
                            }
                        }
                        configContent = string.Join('\n', lines);
                    }
                    else
                    {
                        // Add authtoken to existing config
                        configContent = $"authtoken: {authToken}\n{configContent}";
                    }
                }
                else
                {
                    // Create minimal config with authtoken
                    configContent = $"authtoken: {authToken}\nversion: 2\n";
                }

                // Write config file securely (not visible in process list)
                await File.WriteAllTextAsync(ngrokConfigPath, configContent, cancellationToken);

                // Set secure file permissions (readable only by owner)
                if (!OperatingSystem.IsWindows())
                {
                    // On Unix-like systems, set permissions to 600 (owner read/write only)
                    var chmodProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    chmodProcess.StartInfo.ArgumentList.Add("600");
                    chmodProcess.StartInfo.ArgumentList.Add(ngrokConfigPath);
                    chmodProcess.Start();
                    await chmodProcess.WaitForExitAsync(cancellationToken);
                }

                _logger.LogInformation("Configured ngrok authtoken securely via config file");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write ngrok config file, attempting fallback method");

                // Fallback: Use ArgumentList (safer than string interpolation but still has process exposure)
                var configProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ngrok",
                        Arguments = $"config add-authtoken {authToken}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                configProcess.Start();
                await configProcess.WaitForExitAsync(cancellationToken);
            }
        }

        // Start ngrok in background
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ngrok",
                Arguments = $"http {port} --log=stdout",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Wait for tunnel to be ready and parse URL
        await Task.Delay(3000, cancellationToken).ConfigureAwait(false); // Give ngrok time to start

        // Get tunnel URL from ngrok API
        using var httpClient = new System.Net.Http.HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var response = await httpClient.GetStringAsync("http://localhost:4040/api/tunnels", cancellationToken);
        var json = System.Text.Json.JsonDocument.Parse(response);
        var publicUrl = json.RootElement
            .GetProperty("tunnels")[0]
            .GetProperty("public_url")
            .GetString();

        _logger.LogInformation("ngrok tunnel created: {Url}", publicUrl);

        return new TunnelResult
        {
            Success = true,
            Provider = "ngrok",
            PublicUrl = publicUrl ?? string.Empty,
            LocalPort = port,
            ProcessId = process.Id,
            Message = $"Tunnel active at {publicUrl}. Dashboard: http://localhost:4040",
            Process = process
        };
    }

    private async Task<TunnelResult> CreateCloudflareTunnelAsync(int port, CancellationToken cancellationToken)
    {
        var check = await RunCommandAsync("cloudflared", "version", cancellationToken);
        if (!check.success)
        {
            return new TunnelResult
            {
                Success = false,
                Provider = "cloudflare",
                ErrorMessage = "cloudflared not found",
                InstallInstructions = "Install from https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/installation/"
            };
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cloudflared",
                Arguments = $"tunnel --url http://localhost:{port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        await Task.Delay(5000, cancellationToken).ConfigureAwait(false);

        // Parse URL from output (cloudflared prints it)
        var output = await process.StandardError.ReadLineAsync(cancellationToken);
        var url = output?.Contains("https://") == true
            ? output.Substring(output.IndexOf("https://"))
            : "Check cloudflared output for URL";

        return new TunnelResult
        {
            Success = true,
            Provider = "cloudflare",
            PublicUrl = url,
            LocalPort = port,
            ProcessId = process.Id,
            Message = "Cloudflare Tunnel active (free, no account required)",
            Process = process
        };
    }

    private async Task<TunnelResult> CreateLocaltunnelAsync(int port, string? subdomain, CancellationToken cancellationToken)
    {
        var check = await RunCommandAsync("lt", "--version", cancellationToken);
        if (!check.success)
        {
            return new TunnelResult
            {
                Success = false,
                Provider = "localtunnel",
                ErrorMessage = "localtunnel (lt) not found",
                InstallInstructions = "Install with: npm install -g localtunnel"
            };
        }

        var args = subdomain.IsNullOrEmpty()
            ? $"--port {port}"
            : $"--port {port} --subdomain {subdomain}";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "lt",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var url = await process.StandardOutput.ReadLineAsync(cancellationToken);

        return new TunnelResult
        {
            Success = true,
            Provider = "localtunnel",
            PublicUrl = url ?? "Check lt output",
            LocalPort = port,
            ProcessId = process.Id,
            Message = "Localtunnel active (completely free)",
            Process = process
        };
    }

    private Task<TunnelResult> CreateLocalhostRunTunnelAsync(int port, CancellationToken cancellationToken)
    {
        return Task.FromResult(new TunnelResult
        {
            Success = true,
            Provider = "localhost.run",
            PublicUrl = $"Use: ssh -R 80:localhost:{port} nokey@localhost.run",
            LocalPort = port,
            Message = "Run this SSH command to create tunnel (no installation needed)"
        });
    }

    private async Task<(bool success, string output)> RunCommandAsync(string command, string args, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Read stdout and stderr concurrently to prevent pipe buffer deadlocks
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;

            return (process.ExitCode == 0, output);
        }
        catch
        {
            return (false, string.Empty);
        }
    }
}

// Request/Response models

public sealed class NetworkDiagnosticsRequest
{
    public string? TargetUrl { get; init; }
    public string? TargetHost { get; init; }
    public int? TargetPort { get; init; }
    public List<int>? AdditionalPorts { get; init; }
    public bool IncludeTraceroute { get; init; } = false;
    public bool DetectFirewallIssues { get; init; } = true;
}

public sealed class NetworkDiagnosticsResult
{
    public string TargetHost { get; init; } = string.Empty;
    public int TargetPort { get; init; }
    public NetworkCheckStatus OverallStatus { get; init; }
    public List<NetworkCheck> Checks { get; init; } = new();
    public int PassedChecks { get; init; }
    public int WarningChecks { get; init; }
    public int FailedChecks { get; init; }
    public int TotalDurationMs { get; init; }
    public DateTime Timestamp { get; init; }
    public string Summary { get; init; } = string.Empty;
    public List<string> Recommendations { get; init; } = new();
}

public sealed class NetworkCheck
{
    public string CheckType { get; init; } = string.Empty;
    public NetworkCheckStatus Status { get; init; }
    public int DurationMs { get; init; }
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, string> Details { get; init; } = new();
}

public enum NetworkCheckStatus
{
    Passed,
    Warning,
    Failed
}

public sealed class TunnelRequest
{
    public string Provider { get; init; } = "ngrok";
    public int LocalPort { get; init; } = 8080;
    public string? AuthToken { get; init; }
    public string? Subdomain { get; init; }
}

public sealed class TunnelResult
{
    public bool Success { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string PublicUrl { get; init; } = string.Empty;
    public int LocalPort { get; init; }
    public int ProcessId { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public string? InstallInstructions { get; init; }
    public System.Diagnostics.Process? Process { get; init; }
}
