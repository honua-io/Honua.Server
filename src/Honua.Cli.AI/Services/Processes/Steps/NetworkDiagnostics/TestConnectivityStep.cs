// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NetworkDiagnosticsState = Honua.Cli.AI.Services.Processes.State.NetworkDiagnosticsState;
using Honua.Cli.AI.Services.Processes.State;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Diagnostics;

namespace Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;

/// <summary>
/// Tests network reachability using ICMP ping and TCP connectivity.
/// Verifies that the target is reachable at the network layer.
/// </summary>
public class TestConnectivityStep : KernelProcessStep<NetworkDiagnosticsState>, IProcessStepTimeout
{
    private readonly ILogger<TestConnectivityStep> _logger;
    private NetworkDiagnosticsState _state = new();

    /// <summary>
    /// Connectivity tests (ping, TCP) should complete quickly.
    /// Default timeout: 2 minutes
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(2);

    public TestConnectivityStep(ILogger<TestConnectivityStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<NetworkDiagnosticsState> state)
    {
        _state = state.State ?? new NetworkDiagnosticsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("TestConnectivity")]
    public async Task TestConnectivityAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Testing network connectivity to {Host}", _state.TargetHost);
        _state.Status = "Testing Connectivity";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // Perform real ICMP ping test
                    var pingTest = await PerformPingTestAsync(_state.TargetHost);
                    _state.NetworkTests.Add(pingTest);
                    _state.ConnectivityStatus = pingTest.Success ? "Reachable" : "Unreachable";
                    _state.LatencyMs = pingTest.LatencyMs;

                    // Record diagnostic test
                    _state.TestsRun.Add(new DiagnosticTest
                    {
                        TestName = "ICMP Ping",
                        TestType = "Network",
                        Timestamp = DateTime.UtcNow,
                        Success = pingTest.Success,
                        Output = pingTest.Output,
                        ErrorMessage = pingTest.ErrorMessage
                    });

                    // Test TCP connectivity if port is specified
                    if (_state.TargetPort.HasValue)
                    {
                        var tcpTest = await PerformTcpTestAsync(_state.TargetHost, _state.TargetPort.Value);
                        _state.NetworkTests.Add(tcpTest);

                        _state.TestsRun.Add(new DiagnosticTest
                        {
                            TestName = $"TCP Port {_state.TargetPort}",
                            TestType = "Network",
                            Timestamp = DateTime.UtcNow,
                            Success = tcpTest.Success,
                            Output = tcpTest.Output,
                            ErrorMessage = tcpTest.ErrorMessage
                        });

                        if (!tcpTest.Success)
                        {
                            _logger.LogWarning("TCP connectivity failed for {Host}:{Port}",
                                _state.TargetHost, _state.TargetPort);

                            _state.Findings.Add(new Finding
                            {
                                Category = "Network",
                                Severity = "High",
                                Description = $"TCP port {_state.TargetPort} is not accessible on {_state.TargetHost}",
                                Recommendation = "Check firewall rules, security groups, and port configuration",
                                Evidence = new List<string> { tcpTest.ErrorMessage ?? "Connection refused or timeout" }
                            });

                            await context.EmitEventAsync(new KernelProcessEvent
                            {
                                Id = "ReachabilityFailure",
                                Data = _state
                            });
                            return;
                        }
                    }

                    _logger.LogInformation("Network connectivity test passed for {Host}", _state.TargetHost);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "ReachabilityTestComplete",
                        Data = _state
                    });
                },
                _logger,
                "TestConnectivity");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test connectivity after retries");
            _state.Status = "Connectivity Test Failed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ReachabilityFailure",
                Data = _state
            });
        }
    }

    private async Task<NetworkTest> PerformPingTestAsync(string host)
    {
        var pingTest = new NetworkTest
        {
            TestType = "Ping",
            TargetHost = host,
            Success = false
        };

        try
        {
            using var ping = new Ping();
            var stopwatch = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(host, 5000); // 5 second timeout
            stopwatch.Stop();

            pingTest.Success = reply.Status == IPStatus.Success;
            pingTest.LatencyMs = reply.Status == IPStatus.Success ? reply.RoundtripTime : null;

            if (reply.Status == IPStatus.Success)
            {
                pingTest.Output = $"Reply from {reply.Address}: bytes={reply.Buffer.Length} time={reply.RoundtripTime}ms TTL={reply.Options?.Ttl ?? 0}";
            }
            else
            {
                pingTest.ErrorMessage = $"Ping failed: {reply.Status}";
                pingTest.Output = pingTest.ErrorMessage;
            }
        }
        catch (PingException ex)
        {
            pingTest.ErrorMessage = $"Ping exception: {ex.Message}";
            pingTest.Output = pingTest.ErrorMessage;
            _logger.LogWarning(ex, "Ping failed for {Host}", host);
        }
        catch (Exception ex)
        {
            pingTest.ErrorMessage = $"Unexpected error during ping: {ex.Message}";
            pingTest.Output = pingTest.ErrorMessage;
            _logger.LogError(ex, "Unexpected error pinging {Host}", host);
        }

        return pingTest;
    }

    private async Task<NetworkTest> PerformTcpTestAsync(string host, int port)
    {
        var tcpTest = new NetworkTest
        {
            TestType = "TCP",
            TargetHost = host,
            Port = port,
            Success = false
        };

        try
        {
            using var tcpClient = new TcpClient();
            var stopwatch = Stopwatch.StartNew();

            // Set connection timeout to 5 seconds
            var connectTask = tcpClient.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(5000);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            stopwatch.Stop();

            if (completedTask == connectTask && tcpClient.Connected)
            {
                tcpTest.Success = true;
                tcpTest.LatencyMs = stopwatch.Elapsed.TotalMilliseconds;
                tcpTest.Output = $"Successfully connected to {host}:{port} (latency: {tcpTest.LatencyMs:F2}ms)";
            }
            else if (completedTask == timeoutTask)
            {
                tcpTest.ErrorMessage = $"Connection timeout after 5 seconds";
                tcpTest.Output = tcpTest.ErrorMessage;
            }
            else
            {
                tcpTest.ErrorMessage = $"Connection failed to {host}:{port}";
                tcpTest.Output = tcpTest.ErrorMessage;
            }
        }
        catch (SocketException ex)
        {
            tcpTest.ErrorMessage = $"Socket error: {ex.Message} (ErrorCode: {ex.ErrorCode})";
            tcpTest.Output = tcpTest.ErrorMessage;
            _logger.LogWarning(ex, "TCP connection failed to {Host}:{Port}", host, port);
        }
        catch (Exception ex)
        {
            tcpTest.ErrorMessage = $"Unexpected error: {ex.Message}";
            tcpTest.Output = tcpTest.ErrorMessage;
            _logger.LogError(ex, "Unexpected error connecting to {Host}:{Port}", host, port);
        }

        return tcpTest;
    }
}
