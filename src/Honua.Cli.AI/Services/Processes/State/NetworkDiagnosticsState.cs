// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Cli.AI.Services.Processes.State;

/// <summary>
/// State object for Network Diagnostics Process workflow.
/// Tracks systematic network troubleshooting across DNS, connectivity, security groups, SSL, load balancers, and databases.
/// Persists across step invocations for checkpointing and resume.
/// </summary>
public class NetworkDiagnosticsState
{
    public string DiagnosticId { get; set; } = string.Empty;
    public string ReportedIssue { get; set; } = string.Empty;
    public DateTime IssueTimestamp { get; set; }
    public string TargetHost { get; set; } = string.Empty;
    public int? TargetPort { get; set; }
    public List<string> AffectedEndpoints { get; set; } = new();
    public List<string> Symptoms { get; set; } = new();
    public List<DiagnosticTest> TestsRun { get; set; } = new();
    public Dictionary<string, TestResult> TestResults { get; set; } = new();

    // DNS Results
    public Dictionary<string, DnsResult> DNSResults { get; set; } = new();

    // Connectivity Results
    public string? ConnectivityStatus { get; set; }
    public List<string> TraceRoute { get; set; } = new();
    public double? LatencyMs { get; set; }

    // Network Tests
    public List<NetworkTest> NetworkTests { get; set; } = new();

    // Security Group Results
    public List<SecurityGroupRule> SecurityGroupRules { get; set; } = new();
    public bool? SecurityGroupsValid { get; set; }

    // SSL/TLS Results
    public SslTestResult? SslTestResult { get; set; }

    // Load Balancer Results
    public LoadBalancerHealth? LoadBalancerHealth { get; set; }

    // Database Results
    public DatabaseConnectivityResult? DatabaseConnectivity { get; set; }

    // Findings and Root Cause
    public List<Finding> Findings { get; set; } = new();
    public string? RootCause { get; set; }
    public string? RootCauseCategory { get; set; } // DNS, Network, Security, SSL, LoadBalancer, Database
    public List<string> RecommendedFixes { get; set; } = new();
    public List<string> LogExcerpts { get; set; } = new();

    // Process tracking
    public DateTime DiagnosticStartTime { get; set; }
    public DateTime? DiagnosticCompleteTime { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Running, Completed, Failed
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents a diagnostic test that was executed.
/// </summary>
public class DiagnosticTest
{
    public string TestName { get; set; } = string.Empty;
    public string TestType { get; set; } = string.Empty; // DNS, Connectivity, Security, SSL, etc.
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of a single test.
/// </summary>
public class TestResult
{
    public bool Passed { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// DNS resolution result.
/// </summary>
public class DnsResult
{
    public string Domain { get; set; } = string.Empty;
    public List<string> IpAddresses { get; set; } = new();
    public bool Resolved { get; set; }
    public int TtlSeconds { get; set; }
    public List<string> CnameChain { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Network connectivity test result.
/// </summary>
public class NetworkTest
{
    public string TestType { get; set; } = string.Empty; // Ping, Telnet, TraceRoute
    public string TargetHost { get; set; } = string.Empty;
    public int? Port { get; set; }
    public bool Success { get; set; }
    public double? LatencyMs { get; set; }
    public int? HopCount { get; set; }
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Security group rule configuration.
/// </summary>
public class SecurityGroupRule
{
    public string RuleId { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // Inbound, Outbound
    public string Protocol { get; set; } = string.Empty;
    public string PortRange { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // Allow, Deny
}

/// <summary>
/// SSL/TLS certificate test result.
/// </summary>
public class SslTestResult
{
    public bool CertificateValid { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Issuer { get; set; }
    public List<string> SubjectAlternativeNames { get; set; } = new();
    public bool HostnameMatches { get; set; }
    public bool ChainValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public string? Protocol { get; set; }
    public string? CipherSuite { get; set; }
}

/// <summary>
/// Load balancer health check result.
/// </summary>
public class LoadBalancerHealth
{
    public string LoadBalancerName { get; set; } = string.Empty;
    public bool Healthy { get; set; }
    public int HealthyTargetCount { get; set; }
    public int UnhealthyTargetCount { get; set; }
    public List<TargetHealth> Targets { get; set; } = new();
    public Dictionary<string, string> HealthCheckSettings { get; set; } = new();
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// Individual target health status.
/// </summary>
public class TargetHealth
{
    public string TargetId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public string State { get; set; } = string.Empty; // Healthy, Unhealthy, Draining
    public string? Reason { get; set; }
}

/// <summary>
/// Database connectivity test result.
/// </summary>
public class DatabaseConnectivityResult
{
    public bool CanConnect { get; set; }
    public string? DatabaseEndpoint { get; set; }
    public int? Port { get; set; }
    public double? ConnectionTimeMs { get; set; }
    public bool? CanExecuteQuery { get; set; }
    public int? ActiveConnections { get; set; }
    public int? MaxConnections { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Diagnostic finding from analysis.
/// </summary>
public class Finding
{
    public string Category { get; set; } = string.Empty; // DNS, Network, Security, SSL, LoadBalancer, Database
    public string Severity { get; set; } = string.Empty; // Critical, High, Medium, Low, Info
    public string Description { get; set; } = string.Empty;
    public string? Recommendation { get; set; }
    public List<string> Evidence { get; set; } = new();
}
