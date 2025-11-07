// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL.Resilience;

public class CircuitBreakerTests
{
    private readonly InMemoryCircuitBreakerService _circuitBreaker;

    public CircuitBreakerTests()
    {
        var options = Options.Create(new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            TimeoutSeconds = 60
        });

        _circuitBreaker = new InMemoryCircuitBreakerService(
            NullLogger<InMemoryCircuitBreakerService>.Instance,
            options);
    }

    [Fact]
    public async Task IsOpen_InitialState_ReturnsFalse()
    {
        var isOpen = await _circuitBreaker.IsOpenAsync("test-node");

        Assert.False(isOpen);
    }

    [Fact]
    public async Task GetState_InitialState_ReturnsClosed()
    {
        var state = await _circuitBreaker.GetStateAsync("test-node");

        Assert.Equal(CircuitState.Closed, state);
    }

    [Fact]
    public async Task RecordFailure_BelowThreshold_CircuitRemainsClosed()
    {
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error 1"));
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error 2"));

        var isOpen = await _circuitBreaker.IsOpenAsync("test-node");

        Assert.False(isOpen);
    }

    [Fact]
    public async Task RecordFailure_ExceedsThreshold_CircuitOpens()
    {
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error 1"));
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error 2"));
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error 3"));

        var isOpen = await _circuitBreaker.IsOpenAsync("test-node");
        var state = await _circuitBreaker.GetStateAsync("test-node");

        Assert.True(isOpen);
        Assert.Equal(CircuitState.Open, state);
    }

    [Fact]
    public async Task RecordSuccess_ResetsConsecutiveFailures()
    {
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error 1"));
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error 2"));
        await _circuitBreaker.RecordSuccessAsync("test-node");
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error 3"));

        var isOpen = await _circuitBreaker.IsOpenAsync("test-node");

        Assert.False(isOpen); // Only 1 consecutive failure after success
    }

    [Fact]
    public async Task Reset_OpensCircuit_CircuitCloses()
    {
        // Open the circuit
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error 1"));
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error 2"));
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error 3"));

        Assert.True(await _circuitBreaker.IsOpenAsync("test-node"));

        // Reset
        await _circuitBreaker.ResetAsync("test-node");

        var isOpen = await _circuitBreaker.IsOpenAsync("test-node");
        var state = await _circuitBreaker.GetStateAsync("test-node");

        Assert.False(isOpen);
        Assert.Equal(CircuitState.Closed, state);
    }

    [Fact]
    public async Task GetStats_ReturnsCorrectStatistics()
    {
        await _circuitBreaker.RecordSuccessAsync("node-a");
        await _circuitBreaker.RecordSuccessAsync("node-a");
        await _circuitBreaker.RecordFailureAsync("node-a", new Exception("Error"));

        await _circuitBreaker.RecordFailureAsync("node-b", new Exception("Error 1"));
        await _circuitBreaker.RecordFailureAsync("node-b", new Exception("Error 2"));
        await _circuitBreaker.RecordFailureAsync("node-b", new Exception("Error 3"));

        var stats = await _circuitBreaker.GetStatsAsync();

        Assert.Equal(2, stats.NodeTypeStats.Count);
        Assert.Equal(2, stats.NodeTypeStats["node-a"].TotalSuccesses);
        Assert.Equal(1, stats.NodeTypeStats["node-a"].TotalFailures);
        Assert.Equal(CircuitState.Closed, stats.NodeTypeStats["node-a"].State);

        Assert.Equal(0, stats.NodeTypeStats["node-b"].TotalSuccesses);
        Assert.Equal(3, stats.NodeTypeStats["node-b"].TotalFailures);
        Assert.Equal(CircuitState.Open, stats.NodeTypeStats["node-b"].State);
    }

    [Fact]
    public async Task FailureRate_CalculatesCorrectly()
    {
        await _circuitBreaker.RecordSuccessAsync("test-node");
        await _circuitBreaker.RecordSuccessAsync("test-node");
        await _circuitBreaker.RecordSuccessAsync("test-node");
        await _circuitBreaker.RecordFailureAsync("test-node", new Exception("Error"));

        var stats = await _circuitBreaker.GetStatsAsync();
        var nodeStats = stats.NodeTypeStats["test-node"];

        Assert.Equal(0.25, nodeStats.FailureRate, 2); // 1 failure / 4 total = 0.25
    }
}
