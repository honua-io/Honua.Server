// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Configuration options for process step timeouts.
/// Provides default timeout values for different categories of process steps.
/// </summary>
public class ProcessStepTimeoutOptions
{
    /// <summary>
    /// Default timeout for simple, fast-executing steps (e.g., validation, configuration checks).
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan DefaultStepTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Timeout for deployment-related steps (e.g., infrastructure provisioning, application deployment).
    /// These operations can involve significant resource creation and setup.
    /// Default: 30 minutes
    /// </summary>
    public TimeSpan DeploymentStepTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Timeout for network diagnostic steps (e.g., connectivity tests, DNS resolution, port checks).
    /// These should complete quickly as they're diagnostic operations.
    /// Default: 2 minutes
    /// </summary>
    public TimeSpan NetworkDiagnosticsTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Timeout for benchmark execution steps.
    /// Benchmarks can run for extended periods depending on test configuration.
    /// Default: 15 minutes
    /// </summary>
    public TimeSpan BenchmarkTimeout { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Timeout for GitOps synchronization and drift detection steps.
    /// Default: 10 minutes
    /// </summary>
    public TimeSpan GitOpsTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Timeout for metadata extraction and STAC catalog operations.
    /// Default: 10 minutes
    /// </summary>
    public TimeSpan MetadataTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Timeout for certificate renewal operations.
    /// Default: 15 minutes
    /// </summary>
    public TimeSpan CertificateRenewalTimeout { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Timeout for upgrade process steps (backup, blue-green deployment, traffic switching).
    /// Default: 30 minutes
    /// </summary>
    public TimeSpan UpgradeTimeout { get; set; } = TimeSpan.FromMinutes(30);
}
