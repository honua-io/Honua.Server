// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using BenchmarkState = Honua.Cli.AI.Services.Processes.State.BenchmarkState;

namespace Honua.Cli.AI.Services.Processes.Steps.Benchmark;

/// <summary>
/// Executes the benchmark test against target endpoint.
/// </summary>
public class RunBenchmarkStep : KernelProcessStep<BenchmarkState>, IProcessStepTimeout
{
    private readonly ILogger<RunBenchmarkStep> _logger;
    private BenchmarkState _state = new();

    /// <summary>
    /// Benchmark execution can run for extended duration based on test configuration.
    /// Default timeout: 15 minutes
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(15);

    public RunBenchmarkStep(ILogger<RunBenchmarkStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<BenchmarkState> state)
    {
        _state = state.State ?? new BenchmarkState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("RunBenchmark")]
    public async Task RunBenchmarkAsync(KernelProcessStepContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running benchmark {BenchmarkName} for {Duration}s at {Concurrency} concurrency",
            _state.BenchmarkName, _state.Duration, _state.Concurrency);

        _state.Status = "Running";

        try
        {
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            // Execute load test
            await ExecuteLoadTest(cancellationToken);

            // Collect metrics
            await CollectMetrics(cancellationToken);

            _logger.LogInformation("Benchmark {BenchmarkName} completed. RPS: {RPS}, P95: {P95}ms",
                _state.BenchmarkName, _state.RequestsPerSecond, _state.P95Latency);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "BenchmarkCompleted",
                Data = _state
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Benchmark cancelled: {BenchmarkName}", _state.BenchmarkName);
            _state.Status = "Cancelled";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "BenchmarkCancelled",
                Data = new { _state.BenchmarkName }
            });
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Benchmark failed: {BenchmarkName}", _state.BenchmarkName);
            _state.Status = "Failed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "BenchmarkFailed",
                Data = new { _state.BenchmarkName, Error = ex.Message }
            });
        }
    }

    private async Task ExecuteLoadTest(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing load test against {Endpoint}", _state.TargetEndpoint);

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(_state.TargetEndpoint),
            Timeout = TimeSpan.FromSeconds(30)
        };

        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddSeconds(_state.Duration);
        var latencies = new List<double>();
        long totalRequests = 0;
        long successfulRequests = 0;
        long failedRequests = 0;

        // Create concurrent worker tasks based on concurrency setting
        var tasks = new List<Task>();
        for (int i = 0; i < _state.Concurrency; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
                {
                    var requestStart = DateTime.UtcNow;
                    try
                    {
                        var response = await httpClient.GetAsync("", cancellationToken).ConfigureAwait(false);
                        var requestEnd = DateTime.UtcNow;
                        var latencyMs = (requestEnd - requestStart).TotalMilliseconds;

                        lock (latencies)
                        {
                            latencies.Add(latencyMs);
                            totalRequests++;
                            if (response.IsSuccessStatusCode)
                            {
                                successfulRequests++;
                            }
                            else
                            {
                                failedRequests++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Request failed during load test");
                        lock (latencies)
                        {
                            totalRequests++;
                            failedRequests++;
                        }
                    }

                    // Small delay between requests to avoid overwhelming the client
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Calculate metrics
        var actualDuration = (DateTime.UtcNow - startTime).TotalSeconds;
        _state.RequestsPerSecond = totalRequests / actualDuration;
        _state.ErrorRate = totalRequests > 0 ? (double)failedRequests / totalRequests : 0;

        if (latencies.Count > 0)
        {
            latencies.Sort();
            var p95Index = (int)(latencies.Count * 0.95);
            var p99Index = (int)(latencies.Count * 0.99);
            _state.P95Latency = latencies[Math.Min(p95Index, latencies.Count - 1)];
            _state.P99Latency = latencies[Math.Min(p99Index, latencies.Count - 1)];
        }
        else
        {
            _state.P95Latency = 0;
            _state.P99Latency = 0;
        }

        _logger.LogInformation(
            "Load test completed: {TotalRequests} total requests, {SuccessfulRequests} successful, {FailedRequests} failed, {RPS:F2} RPS",
            totalRequests, successfulRequests, failedRequests, _state.RequestsPerSecond);
    }

    private async Task CollectMetrics(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Collecting metrics");

        // Metrics are already collected in ExecuteLoadTest
        // This method could be extended to scrape additional metrics from Prometheus or other sources
        // For now, log the collected metrics
        await Task.CompletedTask;
    }
}
