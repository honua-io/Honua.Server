// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Partial class containing Checkov security scanning functionality for AI-generated Terraform code.
/// </summary>
public partial class GenerateInfrastructureCodeStep
{
    /// <summary>
    /// Validates generated Terraform code using Checkov security scanner.
    /// </summary>
    /// <param name="workspacePath">Path to Terraform workspace containing main.tf and related files</param>
    /// <param name="throwOnFailure">If true, throws exception on blocking issues. If false, returns results.</param>
    /// <returns>Checkov scan results, or null if Checkov is not installed</returns>
    /// <exception cref="InvalidOperationException">Thrown when critical security issues are found and throwOnFailure is true</exception>
    private async Task<CheckovScanResults?> ValidateWithCheckovAsync(string workspacePath, bool throwOnFailure = true)
    {
        _logger.LogInformation("Running Checkov security scan on generated Terraform code at {WorkspacePath}", workspacePath);

        try
        {
            // Check if checkov is installed
            if (!await IsCheckovInstalledAsync())
            {
                _logger.LogWarning(
                    "Checkov is not installed. Skipping security scan. " +
                    "Install with: pip install checkov");
                return null;
            }

            // Run Checkov scan with JSON output
            var (exitCode, jsonOutput) = await RunCheckovScanAsync(workspacePath);

            // Parse Checkov results
            var scanResults = ParseCheckovResults(jsonOutput);

            // Log summary
            LogCheckovSummary(scanResults);

            // Determine if we should block based on severity
            if (ShouldBlockDeployment(scanResults))
            {
                var errorMessage = FormatBlockingMessage(scanResults);
                _logger.LogError("Checkov found critical security issues.");

                if (throwOnFailure)
                {
                    throw new InvalidOperationException(errorMessage);
                }
            }

            if (scanResults.HasWarnings)
            {
                _logger.LogWarning(
                    "Checkov found {WarningCount} non-critical security issues. Review recommended but not blocking deployment.",
                    scanResults.MediumCount + scanResults.LowCount);
            }
            else
            {
                _logger.LogInformation("Checkov security scan passed with no issues found.");
            }

            return scanResults;
        }
        catch (InvalidOperationException) when (throwOnFailure)
        {
            // Re-throw blocking exceptions only if throwOnFailure is true
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkov scan failed due to unexpected error. Failing safely by blocking deployment.");

            if (throwOnFailure)
            {
                throw new InvalidOperationException(
                    $"Security scan failed: {ex.Message}. Deployment blocked to ensure security validation.", ex);
            }

            return null;
        }
    }

    /// <summary>
    /// Checks if Checkov is installed on the system.
    /// </summary>
    private async Task<bool> IsCheckovInstalledAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "checkov",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Runs Checkov scan and returns exit code and JSON output.
    /// </summary>
    private async Task<(int exitCode, string jsonOutput)> RunCheckovScanAsync(string workspacePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "checkov",
            WorkingDirectory = workspacePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Checkov arguments:
        // -d . : scan current directory
        // --framework terraform : only scan Terraform files
        // --compact : reduce output verbosity
        // --quiet : suppress progress bars
        // -o json : output in JSON format for parsing
        // --soft-fail : don't use exit codes to indicate failures (we'll parse JSON instead)
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(".");
        psi.ArgumentList.Add("--framework");
        psi.ArgumentList.Add("terraform");
        psi.ArgumentList.Add("--compact");
        psi.ArgumentList.Add("--quiet");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add("--soft-fail");

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start Checkov process");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger.LogDebug("Checkov stderr: {Stderr}", stderr);
        }

        return (process.ExitCode, stdout);
    }

    /// <summary>
    /// Parses Checkov JSON output into a structured result object.
    /// </summary>
    private CheckovScanResults ParseCheckovResults(string jsonOutput)
    {
        var results = new CheckovScanResults();

        if (string.IsNullOrWhiteSpace(jsonOutput))
        {
            _logger.LogWarning("Checkov produced no output. Assuming scan failed.");
            return results;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonOutput);
            var root = doc.RootElement;

            // Checkov JSON structure has a "results" object with "failed_checks" array
            if (root.TryGetProperty("results", out var resultsObj))
            {
                if (resultsObj.TryGetProperty("failed_checks", out var failedChecks))
                {
                    foreach (var check in failedChecks.EnumerateArray())
                    {
                        var severity = GetSeverity(check);
                        var checkId = GetStringProperty(check, "check_id");
                        var checkName = GetStringProperty(check, "check_name");
                        var resource = GetStringProperty(check, "resource");
                        var filePath = GetStringProperty(check, "file_path");

                        results.FailedChecks.Add(new CheckovFailedCheck
                        {
                            CheckId = checkId,
                            CheckName = checkName,
                            Severity = severity,
                            Resource = resource,
                            FilePath = filePath
                        });

                        // Count by severity
                        switch (severity.ToUpperInvariant())
                        {
                            case "CRITICAL":
                                results.CriticalCount++;
                                break;
                            case "HIGH":
                                results.HighCount++;
                                break;
                            case "MEDIUM":
                                results.MediumCount++;
                                break;
                            case "LOW":
                                results.LowCount++;
                                break;
                        }
                    }
                }

                // Get passed checks count
                if (resultsObj.TryGetProperty("passed_checks", out var passedChecks) &&
                    passedChecks.ValueKind == JsonValueKind.Array)
                {
                    results.PassedCount = passedChecks.GetArrayLength();
                }

                // Get skipped checks count
                if (resultsObj.TryGetProperty("skipped_checks", out var skippedChecks) &&
                    skippedChecks.ValueKind == JsonValueKind.Array)
                {
                    results.SkippedCount = skippedChecks.GetArrayLength();
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Checkov JSON output");
            throw new InvalidOperationException("Failed to parse security scan results", ex);
        }

        return results;
    }

    private string GetSeverity(JsonElement check)
    {
        // Checkov uses "severity" field in newer versions
        if (check.TryGetProperty("severity", out var severity))
        {
            return severity.GetString() ?? "UNKNOWN";
        }

        // Fallback: infer from check_id or default to MEDIUM
        var checkId = GetStringProperty(check, "check_id");
        if (checkId.Contains("CKV2_"))
            return "HIGH"; // Graph-based checks are typically more important

        return "MEDIUM";
    }

    private string GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    /// <summary>
    /// Logs a summary of Checkov scan results.
    /// </summary>
    private void LogCheckovSummary(CheckovScanResults results)
    {
        _logger.LogInformation(
            "Checkov scan completed: {Passed} passed, {Failed} failed ({Critical} critical, {High} high, {Medium} medium, {Low} low), {Skipped} skipped",
            results.PassedCount,
            results.TotalFailedCount,
            results.CriticalCount,
            results.HighCount,
            results.MediumCount,
            results.LowCount,
            results.SkippedCount);

        // Log each critical and high severity issue
        foreach (var check in results.FailedChecks)
        {
            if (check.Severity == "CRITICAL" || check.Severity == "HIGH")
            {
                _logger.LogWarning(
                    "[{Severity}] {CheckId}: {CheckName} in {Resource} ({FilePath})",
                    check.Severity,
                    check.CheckId,
                    check.CheckName,
                    check.Resource,
                    check.FilePath);
            }
        }
    }

    /// <summary>
    /// Determines if deployment should be blocked based on scan results.
    /// Current policy: block on CRITICAL or HIGH severity issues.
    /// </summary>
    private bool ShouldBlockDeployment(CheckovScanResults results)
    {
        // Block if any CRITICAL issues found
        if (results.CriticalCount > 0)
        {
            _logger.LogError("Found {Count} CRITICAL severity issues. Deployment blocked.", results.CriticalCount);
            return true;
        }

        // Block if any HIGH issues found
        if (results.HighCount > 0)
        {
            _logger.LogError("Found {Count} HIGH severity issues. Deployment blocked.", results.HighCount);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Formats a detailed blocking message when security issues are found.
    /// </summary>
    private string FormatBlockingMessage(CheckovScanResults results)
    {
        var message = $"Checkov security scan found {results.CriticalCount} CRITICAL and {results.HighCount} HIGH severity issues.\n\n";
        message += "Critical/High severity issues:\n";

        foreach (var check in results.FailedChecks)
        {
            if (check.Severity == "CRITICAL" || check.Severity == "HIGH")
            {
                message += $"  [{check.Severity}] {check.CheckId}: {check.CheckName}\n";
                message += $"    Resource: {check.Resource}\n";
                message += $"    File: {check.FilePath}\n\n";
            }
        }

        message += "Deployment blocked. Fix these security issues before proceeding.\n";
        message += "For details, run: checkov -d <terraform-dir> --framework terraform\n";

        return message;
    }

    /// <summary>
    /// Formats security issues as feedback for AI to regenerate Terraform with fixes.
    /// Provides concise, actionable guidance for the Terraform generator.
    /// </summary>
    private string FormatSecurityFeedbackForAI(CheckovScanResults results)
    {
        var feedback = new StringBuilder();
        feedback.AppendLine("SECURITY ISSUES FOUND - APPLY THESE FIXES:\n");

        // Group issues by severity
        var criticalIssues = results.FailedChecks.Where(c => c.Severity == "CRITICAL").ToList();
        var highIssues = results.FailedChecks.Where(c => c.Severity == "HIGH").ToList();

        if (criticalIssues.Any())
        {
            feedback.AppendLine($"CRITICAL ({criticalIssues.Count} issues):");
            foreach (var issue in criticalIssues)
            {
                feedback.AppendLine($"  - {issue.CheckName}");
                feedback.AppendLine($"    Resource: {issue.Resource}");
                feedback.AppendLine($"    Fix: {GetSecurityFix(issue.CheckId)}");
            }
            feedback.AppendLine();
        }

        if (highIssues.Any())
        {
            feedback.AppendLine($"HIGH ({highIssues.Count} issues):");
            foreach (var issue in highIssues)
            {
                feedback.AppendLine($"  - {issue.CheckName}");
                feedback.AppendLine($"    Resource: {issue.Resource}");
                feedback.AppendLine($"    Fix: {GetSecurityFix(issue.CheckId)}");
            }
            feedback.AppendLine();
        }

        feedback.AppendLine("REQUIREMENTS:");
        feedback.AppendLine("- Ensure ALL resources have encryption at rest enabled");
        feedback.AppendLine("- Use private endpoints and disable public access where possible");
        feedback.AppendLine("- Enable logging and monitoring on all resources");
        feedback.AppendLine("- Follow principle of least privilege for IAM/RBAC");
        feedback.AppendLine("- Add explicit security group rules (no 0.0.0.0/0 for sensitive ports)");

        return feedback.ToString();
    }

    /// <summary>
    /// Provides specific fix guidance for common Checkov security checks.
    /// </summary>
    private string GetSecurityFix(string checkId)
    {
        return checkId switch
        {
            // AWS S3
            "CKV_AWS_18" => "Enable S3 bucket logging",
            "CKV_AWS_19" => "Enable S3 bucket encryption (AES256 or aws:kms)",
            "CKV_AWS_20" => "Remove public ACLs, use private bucket access",
            "CKV_AWS_21" => "Enable S3 bucket versioning",
            "CKV_AWS_145" => "Enable S3 bucket encryption with KMS",

            // AWS EBS/EC2
            "CKV_AWS_46" => "Enable EBS volume encryption",
            "CKV_AWS_8" => "Enable EBS volume encryption with KMS",
            "CKV_AWS_126" => "Enable detailed monitoring for EC2",

            // AWS RDS
            "CKV_AWS_16" => "Enable RDS encryption at rest",
            "CKV_AWS_17" => "Enable RDS backup retention",
            "CKV_AWS_118" => "Enable RDS enhanced monitoring",
            "CKV_AWS_129" => "Enable RDS deletion protection",
            "CKV_AWS_133" => "Enable automated backups for RDS",
            "CKV_AWS_161" => "Enable RDS encryption with KMS",

            // AWS Security Groups
            "CKV_AWS_23" => "Add explicit description to security group",
            "CKV_AWS_24" => "Restrict security group SSH access (no 0.0.0.0/0 on port 22)",
            "CKV_AWS_25" => "Restrict security group RDP access (no 0.0.0.0/0 on port 3389)",

            // AWS IAM
            "CKV_AWS_109" => "Add IAM policy to restrict resource access",
            "CKV_AWS_111" => "Ensure IAM policies don't allow full '*:*' permissions",

            // Azure Storage
            "CKV_AZURE_33" => "Enable Azure Storage account network rules",
            "CKV_AZURE_35" => "Enable encryption at rest for Storage account",
            "CKV_AZURE_43" => "Enable secure transfer (HTTPS) for Storage account",
            "CKV_AZURE_44" => "Enable storage account blob encryption",

            // Azure Database
            "CKV_AZURE_28" => "Enable SSL enforcement for PostgreSQL",
            "CKV_AZURE_54" => "Enable PostgreSQL connection throttling",
            "CKV_AZURE_68" => "Enable PostgreSQL infrastructure encryption",

            // GCP Storage
            "CKV_GCP_29" => "Enable GCS bucket encryption with CMEK",
            "CKV_GCP_62" => "Enable uniform bucket-level access for GCS",
            "CKV_GCP_78" => "Enable GCS bucket versioning",

            // GCP Database
            "CKV_GCP_6" => "Enable automated backups for Cloud SQL",
            "CKV_GCP_14" => "Enable SSL for Cloud SQL",
            "CKV_GCP_79" => "Enable Cloud SQL high availability",

            // Default
            _ => $"Review Checkov documentation for {checkId}"
        };
    }

    /// <summary>
    /// Results from a Checkov security scan.
    /// </summary>
    private class CheckovScanResults
    {
        public List<CheckovFailedCheck> FailedChecks { get; } = new();
        public int PassedCount { get; set; }
        public int SkippedCount { get; set; }
        public int CriticalCount { get; set; }
        public int HighCount { get; set; }
        public int MediumCount { get; set; }
        public int LowCount { get; set; }

        public int TotalFailedCount => CriticalCount + HighCount + MediumCount + LowCount;
        public bool HasWarnings => MediumCount > 0 || LowCount > 0;
        public bool HasBlockingIssues => CriticalCount > 0 || HighCount > 0;
    }

    /// <summary>
    /// Represents a single failed Checkov check.
    /// </summary>
    private class CheckovFailedCheck
    {
        public string CheckId { get; set; } = string.Empty;
        public string CheckName { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }
}
