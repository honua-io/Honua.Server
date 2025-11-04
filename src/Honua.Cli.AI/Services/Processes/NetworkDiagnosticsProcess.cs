// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Process builder for Network Diagnostics workflow.
/// Orchestrates 10 steps: collect symptoms → test DNS → test connectivity → check firewall → test port → check cert → analyze latency → traceroute → identify root cause → generate report.
/// Automates systematic network troubleshooting for connectivity, DNS, SSL, load balancer, and database issues.
/// </summary>
public static class NetworkDiagnosticsProcess
{
    public static ProcessBuilder BuildProcess()
    {
        var builder = new ProcessBuilder("NetworkDiagnostics");

        // Add all 10 steps
        var collectSymptomsStep = builder.AddStepFromType<CollectSymptomsStep>();
        var testDNSStep = builder.AddStepFromType<TestDNSResolutionStep>();
        var testConnectivityStep = builder.AddStepFromType<TestConnectivityStep>();
        var checkFirewallStep = builder.AddStepFromType<CheckFirewallRulesStep>();
        var testPortStep = builder.AddStepFromType<TestPortAccessStep>();
        var checkCertStep = builder.AddStepFromType<CheckCertificateStep>();
        var analyzeLatencyStep = builder.AddStepFromType<AnalyzeLatencyStep>();
        var traceRouteStep = builder.AddStepFromType<TraceRouteStep>();
        var identifyRootCauseStep = builder.AddStepFromType<IdentifyRootCauseStep>();
        var generateReportStep = builder.AddStepFromType<GenerateReportStep>();

        // Wire event routing

        // Start: external event → collect symptoms
        builder
            .OnInputEvent("StartNetworkDiagnostics")
            .SendEventTo(new ProcessFunctionTargetBuilder(collectSymptomsStep, "CollectSymptoms"));

        // Collect symptoms → Test DNS
        collectSymptomsStep
            .OnEvent("SymptomsCollected")
            .SendEventTo(new ProcessFunctionTargetBuilder(testDNSStep, "TestDNSResolution"));

        // DNS test complete → Test connectivity
        testDNSStep
            .OnEvent("DNSTestComplete")
            .SendEventTo(new ProcessFunctionTargetBuilder(testConnectivityStep, "TestConnectivity"));

        // DNS failure → Skip to root cause analysis
        testDNSStep
            .OnEvent("DNSFailure")
            .SendEventTo(new ProcessFunctionTargetBuilder(identifyRootCauseStep, "IdentifyRootCause"));

        // Connectivity test complete → Check firewall
        testConnectivityStep
            .OnEvent("ReachabilityTestComplete")
            .SendEventTo(new ProcessFunctionTargetBuilder(checkFirewallStep, "CheckFirewallRules"));

        // Connectivity failure → Root cause analysis
        testConnectivityStep
            .OnEvent("ReachabilityFailure")
            .SendEventTo(new ProcessFunctionTargetBuilder(identifyRootCauseStep, "IdentifyRootCause"));

        // Security groups valid → Test port access
        checkFirewallStep
            .OnEvent("SecurityGroupsValid")
            .SendEventTo(new ProcessFunctionTargetBuilder(testPortStep, "TestPortAccess"));

        // Security group issue → Root cause analysis
        checkFirewallStep
            .OnEvent("SecurityGroupIssue")
            .SendEventTo(new ProcessFunctionTargetBuilder(identifyRootCauseStep, "IdentifyRootCause"));

        // Port access success → Check certificate
        testPortStep
            .OnEvent("PortAccessSuccess")
            .SendEventTo(new ProcessFunctionTargetBuilder(checkCertStep, "CheckCertificate"));

        // Port access failure → Root cause analysis
        testPortStep
            .OnEvent("PortAccessFailure")
            .SendEventTo(new ProcessFunctionTargetBuilder(identifyRootCauseStep, "IdentifyRootCause"));

        // SSL test complete → Check load balancer
        checkCertStep
            .OnEvent("SSLTestComplete")
            .SendEventTo(new ProcessFunctionTargetBuilder(analyzeLatencyStep, "AnalyzeLatency"));

        // SSL failure → Root cause analysis
        checkCertStep
            .OnEvent("SSLFailure")
            .SendEventTo(new ProcessFunctionTargetBuilder(identifyRootCauseStep, "IdentifyRootCause"));

        // Latency analyzed → Traceroute
        analyzeLatencyStep
            .OnEvent("LatencyAnalyzed")
            .SendEventTo(new ProcessFunctionTargetBuilder(traceRouteStep, "TraceRoute"));

        // Traceroute complete → Root cause analysis
        traceRouteStep
            .OnEvent("TraceRouteComplete")
            .SendEventTo(new ProcessFunctionTargetBuilder(identifyRootCauseStep, "IdentifyRootCause"));

        // Root cause identified → Generate report
        identifyRootCauseStep
            .OnEvent("RootCauseIdentified")
            .SendEventTo(new ProcessFunctionTargetBuilder(generateReportStep, "GenerateReport"));

        // Report generated → Process complete (terminal state)
        generateReportStep
            .OnEvent("ReportGenerated")
            .StopProcess();

        return builder;
    }
}
