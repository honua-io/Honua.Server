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
/// Tests TCP port accessibility using telnet or netcat.
/// Verifies that the specific port is open and accepting connections.
/// </summary>
public class TestPortAccessStep : KernelProcessStep<NetworkDiagnosticsState>, IProcessStepTimeout
{
    private readonly ILogger<TestPortAccessStep> _logger;
    private NetworkDiagnosticsState _state = new();

    /// <summary>
    /// Port access tests should complete quickly.
    /// Default timeout: 2 minutes
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(2);

    public TestPortAccessStep(ILogger<TestPortAccessStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<NetworkDiagnosticsState> state)
    {
        _state = state.State ?? new NetworkDiagnosticsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("TestPortAccess")]
    public async Task TestPortAccessAsync(KernelProcessStepContext context)
    {
        var port = _state.TargetPort ?? 443;
        _logger.LogInformation("Testing port access: {Host}:{Port}", _state.TargetHost, port);
        _state.Status = "Testing Port Access";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // Perform real port access test
                    var portTest = await PerformPortAccessTestAsync(_state.TargetHost, port);
                    _state.NetworkTests.Add(portTest);

                    // Record diagnostic test
                    _state.TestsRun.Add(new DiagnosticTest
                    {
                        TestName = $"Port Access Test ({port})",
                        TestType = "Network",
                        Timestamp = DateTime.UtcNow,
                        Success = portTest.Success,
                        Output = portTest.Output,
                        ErrorMessage = portTest.ErrorMessage
                    });

                    if (!portTest.Success)
                    {
                        _logger.LogWarning("Port {Port} is not accessible on {Host}", port, _state.TargetHost);

                        _state.Findings.Add(new Finding
                        {
                            Category = "Network",
                            Severity = "High",
                            Description = $"Port {port} is not accessible on {_state.TargetHost}",
                            Recommendation = "Check if service is running, firewall allows traffic, and port is correctly configured",
                            Evidence = new List<string> { portTest.ErrorMessage ?? "Connection refused or timeout" }
                        });

                        await context.EmitEventAsync(new KernelProcessEvent
                        {
                            Id = "PortAccessFailure",
                            Data = _state
                        });
                        return;
                    }

                    _logger.LogInformation("Port {Port} is accessible on {Host}", port, _state.TargetHost);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "PortAccessSuccess",
                        Data = _state
                    });
                },
                _logger,
                "TestPortAccess");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test port access after retries");
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "PortAccessFailure",
                Data = _state
            });
        }
    }

    private async Task<NetworkTest> PerformPortAccessTestAsync(string host, int port)
    {
        var portTest = new NetworkTest
        {
            TestType = "PortAccess",
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
                portTest.Success = true;
                portTest.LatencyMs = stopwatch.Elapsed.TotalMilliseconds;
                portTest.Output = $"Port {port} is open and accepting connections (latency: {portTest.LatencyMs:F2}ms)";
            }
            else if (completedTask == timeoutTask)
            {
                portTest.ErrorMessage = $"Connection timeout after 5 seconds";
                portTest.Output = portTest.ErrorMessage;
            }
            else
            {
                portTest.ErrorMessage = $"Port {port} appears to be closed or filtered";
                portTest.Output = portTest.ErrorMessage;
            }
        }
        catch (SocketException ex)
        {
            portTest.ErrorMessage = $"Socket error: {ex.Message} (ErrorCode: {ex.ErrorCode})";
            portTest.Output = portTest.ErrorMessage;

            // Provide more specific error messages based on error code
            switch (ex.SocketErrorCode)
            {
                case SocketError.ConnectionRefused:
                    portTest.ErrorMessage = $"Connection refused - port {port} is closed or service is not running";
                    break;
                case SocketError.TimedOut:
                    portTest.ErrorMessage = $"Connection timed out - port {port} may be filtered by firewall";
                    break;
                case SocketError.HostNotFound:
                    portTest.ErrorMessage = $"Host not found - unable to resolve {host}";
                    break;
                case SocketError.NetworkUnreachable:
                    portTest.ErrorMessage = $"Network unreachable - unable to reach {host}";
                    break;
            }

            _logger.LogWarning(ex, "Port access test failed for {Host}:{Port}", host, port);
        }
        catch (Exception ex)
        {
            portTest.ErrorMessage = $"Unexpected error: {ex.Message}";
            portTest.Output = portTest.ErrorMessage;
            _logger.LogError(ex, "Unexpected error testing port access for {Host}:{Port}", host, port);
        }

        return portTest;
    }
}
