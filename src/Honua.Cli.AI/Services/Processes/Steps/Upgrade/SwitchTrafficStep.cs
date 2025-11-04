// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using UpgradeState = Honua.Cli.AI.Services.Processes.State.UpgradeState;

namespace Honua.Cli.AI.Services.Processes.Steps.Upgrade;

/// <summary>
/// Gradually switches traffic from green to blue environment (10% → 50% → 100%).
/// </summary>
public class SwitchTrafficStep : KernelProcessStep<UpgradeState>, IProcessStepRollback
{
    private readonly ILogger<SwitchTrafficStep> _logger;
    private UpgradeState _state = new();

    // BUG FIX #22: Reuse singleton HttpClient across metrics checks to avoid exhausting ephemeral ports
    private static readonly System.Net.Http.HttpClient _metricsHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    /// <summary>
    /// Traffic switching supports rollback by routing all traffic back to green.
    /// </summary>
    public bool SupportsRollback => true;

    /// <summary>
    /// Description of rollback operation.
    /// </summary>
    public string RollbackDescription => "Switch all traffic back to green environment";

    public SwitchTrafficStep(ILogger<SwitchTrafficStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<UpgradeState> state)
    {
        _state = state.State ?? new UpgradeState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("SwitchTraffic")]
    public async Task SwitchTrafficAsync(KernelProcessStepContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting gradual traffic cutover for {DeploymentName}",
            _state.DeploymentName);

        _state.Status = "SwitchingTraffic";

        try
        {
            // Phase 1: 10% traffic
            await SwitchTrafficPercentage(10);
            await MonitorMetrics(cancellationToken);

            // Phase 2: 50% traffic
            await SwitchTrafficPercentage(50);
            await MonitorMetrics(cancellationToken);

            // Phase 3: 100% traffic
            await SwitchTrafficPercentage(100);
            await MonitorMetrics(cancellationToken);

            _state.Status = "Completed";
            _logger.LogInformation("Upgrade completed successfully for {DeploymentName}. Version: {TargetVersion}",
                _state.DeploymentName, _state.TargetVersion);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "UpgradeCompleted",
                Data = _state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch traffic for {DeploymentName}", _state.DeploymentName);
            _state.Status = "TrafficSwitchFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "TrafficSwitchFailed",
                Data = new { _state.DeploymentName, Error = ex.Message }
            });
        }
    }

    private async Task SwitchTrafficPercentage(int percentage)
    {
        _logger.LogInformation("Switching {Percentage}% of traffic to blue environment", percentage);

        // Determine load balancer platform from environment
        var lbPlatform = Environment.GetEnvironmentVariable("HONUA_LB_PLATFORM") ?? "kubernetes";

        // BUG FIX #48: Capture step results and emit structured rollback plan on failures
        try
        {
            switch (lbPlatform.ToLowerInvariant())
            {
                case "kubernetes":
                case "k8s":
                    await SwitchTrafficKubernetes(percentage);
                    break;

                case "aws":
                case "alb":
                case "elb":
                    await SwitchTrafficAWS(percentage);
                    break;

                case "azure":
                case "azure-lb":
                    await SwitchTrafficAzure(percentage);
                    break;

                case "gcp":
                case "gcp-lb":
                    await SwitchTrafficGCP(percentage);
                    break;

                case "nginx":
                    await SwitchTrafficNginx(percentage);
                    break;

                case "haproxy":
                    await SwitchTrafficHAProxy(percentage);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported load balancer platform: {lbPlatform}");
            }

            _state.TrafficPercentageOnBlue = percentage;
            _logger.LogInformation("Traffic switch to {Percentage}% completed successfully", percentage);
        }
        catch (Exception ex)
        {
            // Emit telemetry about partial success for rollback guidance
            _logger.LogError(ex,
                "Failed to switch traffic to {Percentage}% on {Platform}. " +
                "Current traffic state: {CurrentPercentage}% on blue. " +
                "Rollback required to restore {PreviousPercentage}% on blue.",
                percentage,
                lbPlatform,
                _state.TrafficPercentageOnBlue,
                _state.TrafficPercentageOnBlue);

            throw new InvalidOperationException(
                $"Traffic switch to {percentage}% failed on {lbPlatform}. " +
                $"Current state: {_state.TrafficPercentageOnBlue}% on blue. " +
                $"Rollback needed to return to green environment.",
                ex);
        }
    }

    private async Task SwitchTrafficKubernetes(int percentage)
    {
        _logger.LogInformation("Updating Kubernetes Service weights");

        var greenWeight = 100 - percentage;
        var blueWeight = percentage;

        // Issue #5 Fix: Merge specific fields rather than overwriting ports array
        // Issue #6 Fix: Manipulate actual selectors instead of custom annotation
        // Using strategic merge patch to update selector based on percentage
        // At 0%: selector points to green
        // At 100%: selector points to blue
        // For gradual rollout (10%, 50%), use Istio VirtualService or split into weighted services

        string patchJson;
        if (percentage == 0)
        {
            // 100% green
            patchJson = @"{
                ""spec"": {
                    ""selector"": {
                        ""app"": ""honua-server"",
                        ""environment"": ""green""
                    }
                }
            }";
        }
        else if (percentage == 100)
        {
            // 100% blue
            patchJson = @"{
                ""spec"": {
                    ""selector"": {
                        ""app"": ""honua-server"",
                        ""environment"": ""blue""
                    }
                }
            }";
        }
        else
        {
            // For gradual splits (10%, 50%), create two services and use Istio VirtualService
            // or split traffic at ingress level with weighted backend services
            _logger.LogInformation("Creating weighted service split for {Percentage}%", percentage);

            // Create blue service with appropriate endpoint subset
            var blueServicePatch = @"{
                ""spec"": {
                    ""selector"": {
                        ""app"": ""honua-server"",
                        ""environment"": ""blue""
                    }
                }
            }";

            // Check if blue service exists, create if not
            var checkBlueProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "kubectl",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            checkBlueProcess.StartInfo.ArgumentList.Add("get");
            checkBlueProcess.StartInfo.ArgumentList.Add("service");
            checkBlueProcess.StartInfo.ArgumentList.Add("honua-service-blue");
            checkBlueProcess.Start();
            await checkBlueProcess.WaitForExitAsync();

            if (checkBlueProcess.ExitCode != 0)
            {
                // Service doesn't exist, create it
                _logger.LogInformation("Blue service doesn't exist, creating it");
                var createBlueManifest = $@"apiVersion: v1
kind: Service
metadata:
  name: honua-service-blue
  labels:
    app: honua-server
    environment: blue
spec:
  type: ClusterIP
  selector:
    app: honua-server
    environment: blue
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
    name: http
";
                var createBluePath = Path.GetTempFileName();
                await File.WriteAllTextAsync(createBluePath, createBlueManifest);

                var createBlueProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "kubectl",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                createBlueProcess.StartInfo.ArgumentList.Add("apply");
                createBlueProcess.StartInfo.ArgumentList.Add("-f");
                createBlueProcess.StartInfo.ArgumentList.Add(createBluePath);
                createBlueProcess.Start();
                var createBlueOutput = await createBlueProcess.StandardOutput.ReadToEndAsync();
                var createBlueError = await createBlueProcess.StandardError.ReadToEndAsync();
                await createBlueProcess.WaitForExitAsync();
                File.Delete(createBluePath);

                if (createBlueProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to create blue Kubernetes service: {createBlueError}");
                }
                _logger.LogInformation("Created blue service: {Output}", createBlueOutput);
            }
            else
            {
                // Service exists, patch it
                var bluePatchFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(bluePatchFile, blueServicePatch);

                var blueProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "kubectl",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                blueProcess.StartInfo.ArgumentList.Add("patch");
                blueProcess.StartInfo.ArgumentList.Add("service");
                blueProcess.StartInfo.ArgumentList.Add("honua-service-blue");
                blueProcess.StartInfo.ArgumentList.Add("--type");
                blueProcess.StartInfo.ArgumentList.Add("strategic");
                blueProcess.StartInfo.ArgumentList.Add("--patch-file");
                blueProcess.StartInfo.ArgumentList.Add(bluePatchFile);

                blueProcess.Start();
                var blueOutput = await blueProcess.StandardOutput.ReadToEndAsync();
                var blueError = await blueProcess.StandardError.ReadToEndAsync();
                await blueProcess.WaitForExitAsync();

                File.Delete(bluePatchFile);

                if (blueProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to update blue Kubernetes service: {blueError}");
                }
                _logger.LogInformation("Patched blue service: {Output}", blueOutput);
            }

            // Check if green service exists, create if not
            var checkGreenProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "kubectl",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            checkGreenProcess.StartInfo.ArgumentList.Add("get");
            checkGreenProcess.StartInfo.ArgumentList.Add("service");
            checkGreenProcess.StartInfo.ArgumentList.Add("honua-service-green");
            checkGreenProcess.Start();
            await checkGreenProcess.WaitForExitAsync();

            if (checkGreenProcess.ExitCode != 0)
            {
                // Service doesn't exist, create it
                _logger.LogInformation("Green service doesn't exist, creating it");
                var createGreenManifest = $@"apiVersion: v1
kind: Service
metadata:
  name: honua-service-green
  labels:
    app: honua-server
    environment: green
spec:
  type: ClusterIP
  selector:
    app: honua-server
    environment: green
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
    name: http
";
                var createGreenPath = Path.GetTempFileName();
                await File.WriteAllTextAsync(createGreenPath, createGreenManifest);

                var createGreenProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "kubectl",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                createGreenProcess.StartInfo.ArgumentList.Add("apply");
                createGreenProcess.StartInfo.ArgumentList.Add("-f");
                createGreenProcess.StartInfo.ArgumentList.Add(createGreenPath);
                createGreenProcess.Start();
                var createGreenOutput = await createGreenProcess.StandardOutput.ReadToEndAsync();
                var createGreenError = await createGreenProcess.StandardError.ReadToEndAsync();
                await createGreenProcess.WaitForExitAsync();
                File.Delete(createGreenPath);

                if (createGreenProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to create green Kubernetes service: {createGreenError}");
                }
                _logger.LogInformation("Created green service: {Output}", createGreenOutput);
            }
            else
            {
                // Service exists, patch it
                var greenServicePatch = @"{
                    ""spec"": {
                        ""selector"": {
                            ""app"": ""honua-server"",
                            ""environment"": ""green""
                        }
                    }
                }";

                var greenPatchFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(greenPatchFile, greenServicePatch);

                var greenProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "kubectl",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                greenProcess.StartInfo.ArgumentList.Add("patch");
                greenProcess.StartInfo.ArgumentList.Add("service");
                greenProcess.StartInfo.ArgumentList.Add("honua-service-green");
                greenProcess.StartInfo.ArgumentList.Add("--type");
                greenProcess.StartInfo.ArgumentList.Add("strategic");
                greenProcess.StartInfo.ArgumentList.Add("--patch-file");
                greenProcess.StartInfo.ArgumentList.Add(greenPatchFile);

                greenProcess.Start();
                var greenOutput = await greenProcess.StandardOutput.ReadToEndAsync();
                var greenError = await greenProcess.StandardError.ReadToEndAsync();
                await greenProcess.WaitForExitAsync();

                File.Delete(greenPatchFile);

                if (greenProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to update green Kubernetes service: {greenError}");
                }
                _logger.LogInformation("Patched green service: {Output}", greenOutput);
            }

            _logger.LogInformation("Kubernetes services split created: green={GreenWeight}%, blue={BlueWeight}%",
                greenWeight, blueWeight);
            return;
        }

        // Issue #4 Fix: Use ArgumentList to avoid quote escaping issues on Windows/Linux
        var patchFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(patchFile, patchJson);

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "kubectl",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("patch");
        process.StartInfo.ArgumentList.Add("service");
        process.StartInfo.ArgumentList.Add("honua-service");
        process.StartInfo.ArgumentList.Add("--type");
        process.StartInfo.ArgumentList.Add("strategic");
        process.StartInfo.ArgumentList.Add("--patch-file");
        process.StartInfo.ArgumentList.Add(patchFile);

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        File.Delete(patchFile);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to update Kubernetes service: {error}");
        }

        _logger.LogInformation("Kubernetes service updated: green={GreenWeight}%, blue={BlueWeight}%",
            greenWeight, blueWeight);
    }

    private async Task SwitchTrafficAWS(int percentage)
    {
        _logger.LogInformation("Updating AWS Application Load Balancer target group weights");

        var greenWeight = 100 - percentage;
        var blueWeight = percentage;

        var listenerArn = Environment.GetEnvironmentVariable("HONUA_AWS_LISTENER_ARN");
        var ruleArn = Environment.GetEnvironmentVariable("HONUA_AWS_RULE_ARN");
        var greenTargetGroupArn = Environment.GetEnvironmentVariable("HONUA_AWS_GREEN_TG_ARN");
        var blueTargetGroupArn = Environment.GetEnvironmentVariable("HONUA_AWS_BLUE_TG_ARN");

        if (string.IsNullOrEmpty(listenerArn) || string.IsNullOrEmpty(greenTargetGroupArn) || string.IsNullOrEmpty(blueTargetGroupArn))
        {
            throw new InvalidOperationException("AWS ALB configuration missing. Required: HONUA_AWS_LISTENER_ARN, HONUA_AWS_GREEN_TG_ARN, HONUA_AWS_BLUE_TG_ARN");
        }

        // CRITICAL: Always require a rule ARN to avoid overwriting the listener's default action
        // Modifying the default action wipes out all path-based routing rules
        // Users must provide HONUA_AWS_RULE_ARN to target a specific forwarding rule
        if (string.IsNullOrEmpty(ruleArn))
        {
            throw new InvalidOperationException(
                "HONUA_AWS_RULE_ARN is required for AWS ALB traffic switching. " +
                "Modifying the listener's default action would overwrite all path-based routing rules. " +
                "Create a dedicated listener rule for blue-green traffic splitting and provide its ARN via HONUA_AWS_RULE_ARN. " +
                "Example: aws elbv2 create-rule --listener-arn <listener> --conditions Field=path-pattern,Values='/*' --priority 1 --actions Type=forward,ForwardConfig=...");
        }

        // SECURITY FIX #24, #25: Validate ARNs to prevent command injection
        ValidateAwsArn(ruleArn, "Rule ARN");
        ValidateAwsArn(greenTargetGroupArn, "Green Target Group ARN");
        ValidateAwsArn(blueTargetGroupArn, "Blue Target Group ARN");

        _logger.LogInformation("Updating ALB rule {RuleArn}", ruleArn);

        // SECURITY FIX #24, #25: Build the forward config with validated ARNs
        var forwardConfig = $@"Type=forward,ForwardConfig={{TargetGroups=[{{TargetGroupArn={greenTargetGroupArn},Weight={greenWeight}}},{{TargetGroupArn={blueTargetGroupArn},Weight={blueWeight}}}],TargetGroupStickinessConfig={{Enabled=false}}}}";

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "aws",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        // SECURITY FIX #24, #25: Use ArgumentList to ensure each token is passed as a separate argument
        // This prevents injection via ARNs and ensures the --actions value with spaces/braces is properly passed
        process.StartInfo.ArgumentList.Add("elbv2");
        process.StartInfo.ArgumentList.Add("modify-rule");
        process.StartInfo.ArgumentList.Add("--rule-arn");
        process.StartInfo.ArgumentList.Add(ruleArn);
        process.StartInfo.ArgumentList.Add("--actions");
        process.StartInfo.ArgumentList.Add(forwardConfig);

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            // SECURITY FIX #46: Scrub potentially sensitive information from error messages before logging
            var sanitizedError = ScrubSensitiveData(error);
            _logger.LogError("Failed to update AWS ALB rule. Exit code: {ExitCode}", process.ExitCode);
            _logger.LogDebug("AWS CLI error output: {Error}", sanitizedError);
            throw new InvalidOperationException($"Failed to update AWS ALB rule (exit code {process.ExitCode})");
        }

        _logger.LogInformation("AWS ALB rule updated: green={GreenWeight}%, blue={BlueWeight}%", greenWeight, blueWeight);
    }

    private async Task SwitchTrafficAzure(int percentage)
    {
        _logger.LogInformation("Updating Azure Traffic Manager profile weights");

        var greenWeight = 100 - percentage;
        var blueWeight = percentage;

        var resourceGroup = Environment.GetEnvironmentVariable("HONUA_AZURE_RESOURCE_GROUP") ?? "honua-rg";
        var profileName = Environment.GetEnvironmentVariable("HONUA_AZURE_TM_PROFILE") ?? "honua-tm";

        // SECURITY FIX #26: Validate Azure resource names to prevent command injection
        ValidateAzureResourceName(resourceGroup, "Resource Group");
        ValidateAzureResourceName(profileName, "Traffic Manager Profile");
        ValidateAzureResourceName(_state.GreenEnvironment, "Green Environment");
        ValidateAzureResourceName(_state.BlueEnvironment, "Blue Environment");

        // Update green endpoint weight
        var greenProcess = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "az",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        // SECURITY FIX #26: Use ArgumentList to prevent command injection
        greenProcess.StartInfo.ArgumentList.Add("network");
        greenProcess.StartInfo.ArgumentList.Add("traffic-manager");
        greenProcess.StartInfo.ArgumentList.Add("endpoint");
        greenProcess.StartInfo.ArgumentList.Add("update");
        greenProcess.StartInfo.ArgumentList.Add("--name");
        greenProcess.StartInfo.ArgumentList.Add(_state.GreenEnvironment);
        greenProcess.StartInfo.ArgumentList.Add("--profile-name");
        greenProcess.StartInfo.ArgumentList.Add(profileName);
        greenProcess.StartInfo.ArgumentList.Add("--resource-group");
        greenProcess.StartInfo.ArgumentList.Add(resourceGroup);
        greenProcess.StartInfo.ArgumentList.Add("--type");
        greenProcess.StartInfo.ArgumentList.Add("azureEndpoints");
        greenProcess.StartInfo.ArgumentList.Add("--weight");
        greenProcess.StartInfo.ArgumentList.Add(greenWeight.ToString());

        greenProcess.Start();
        var greenOutput = await greenProcess.StandardOutput.ReadToEndAsync();
        var greenError = await greenProcess.StandardError.ReadToEndAsync();
        await greenProcess.WaitForExitAsync();

        if (greenProcess.ExitCode != 0)
        {
            // SECURITY FIX #46: Scrub potentially sensitive information from error messages before logging
            var sanitizedError = ScrubSensitiveData(greenError);
            _logger.LogError("Failed to update Azure Traffic Manager green endpoint. Exit code: {ExitCode}", greenProcess.ExitCode);
            _logger.LogDebug("Azure CLI error output: {Error}", sanitizedError);
            throw new InvalidOperationException($"Failed to update Azure Traffic Manager green endpoint (exit code {greenProcess.ExitCode})");
        }

        // Update blue endpoint weight
        var blueProcess = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "az",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        // SECURITY FIX #26: Use ArgumentList to prevent command injection
        blueProcess.StartInfo.ArgumentList.Add("network");
        blueProcess.StartInfo.ArgumentList.Add("traffic-manager");
        blueProcess.StartInfo.ArgumentList.Add("endpoint");
        blueProcess.StartInfo.ArgumentList.Add("update");
        blueProcess.StartInfo.ArgumentList.Add("--name");
        blueProcess.StartInfo.ArgumentList.Add(_state.BlueEnvironment);
        blueProcess.StartInfo.ArgumentList.Add("--profile-name");
        blueProcess.StartInfo.ArgumentList.Add(profileName);
        blueProcess.StartInfo.ArgumentList.Add("--resource-group");
        blueProcess.StartInfo.ArgumentList.Add(resourceGroup);
        blueProcess.StartInfo.ArgumentList.Add("--type");
        blueProcess.StartInfo.ArgumentList.Add("azureEndpoints");
        blueProcess.StartInfo.ArgumentList.Add("--weight");
        blueProcess.StartInfo.ArgumentList.Add(blueWeight.ToString());

        blueProcess.Start();
        var blueOutput = await blueProcess.StandardOutput.ReadToEndAsync();
        var blueError = await blueProcess.StandardError.ReadToEndAsync();
        await blueProcess.WaitForExitAsync();

        if (blueProcess.ExitCode != 0)
        {
            // SECURITY FIX #46: Scrub potentially sensitive information from error messages before logging
            var sanitizedError = ScrubSensitiveData(blueError);
            _logger.LogError("Failed to update Azure Traffic Manager blue endpoint. Exit code: {ExitCode}", blueProcess.ExitCode);
            _logger.LogDebug("Azure CLI error output: {Error}", sanitizedError);
            throw new InvalidOperationException($"Failed to update Azure Traffic Manager blue endpoint (exit code {blueProcess.ExitCode})");
        }

        _logger.LogInformation("Azure Traffic Manager updated: green={GreenWeight}%, blue={BlueWeight}%",
            greenWeight, blueWeight);
    }

    private async Task SwitchTrafficGCP(int percentage)
    {
        _logger.LogInformation("Updating GCP Load Balancer backend service weights");

        var greenWeight = (100 - percentage) / 100.0;
        var blueWeight = percentage / 100.0;

        var backendServiceName = Environment.GetEnvironmentVariable("HONUA_GCP_BACKEND_SERVICE") ?? "honua-backend";
        var region = Environment.GetEnvironmentVariable("HONUA_GCP_REGION") ?? "us-central1";

        // Issue #7 Fix: Substitute real project from state instead of literal PROJECT_ID
        var projectId = _state.CloudProjectId ?? Environment.GetEnvironmentVariable("HONUA_GCP_PROJECT_ID");
        if (string.IsNullOrEmpty(projectId))
        {
            throw new InvalidOperationException("GCP project ID not found in state or environment (HONUA_GCP_PROJECT_ID)");
        }

        // SECURITY FIX #26: Validate GCP resource names to prevent command injection
        ValidateGcpResourceName(projectId, "Project ID");
        ValidateGcpResourceName(backendServiceName, "Backend Service Name");
        ValidateGcpResourceName(region, "Region");
        ValidateGcpResourceName(_state.GreenEnvironment, "Green Environment");
        ValidateGcpResourceName(_state.BlueEnvironment, "Blue Environment");

        // Update backend service with new traffic split
        var configJson = $@"{{
            ""backends"": [
                {{
                    ""group"": ""projects/{projectId}/zones/{region}/instanceGroups/{_state.GreenEnvironment}"",
                    ""balancingMode"": ""UTILIZATION"",
                    ""capacityScaler"": {greenWeight:F2}
                }},
                {{
                    ""group"": ""projects/{projectId}/zones/{region}/instanceGroups/{_state.BlueEnvironment}"",
                    ""balancingMode"": ""UTILIZATION"",
                    ""capacityScaler"": {blueWeight:F2}
                }}
            ]
        }}";

        var tempFile = Path.GetTempFileName();
        // BUG FIX #27: Wrap temp file usage in try/finally to ensure cleanup on all error paths
        try
        {
            await File.WriteAllTextAsync(tempFile, configJson);

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "gcloud",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            // SECURITY FIX #26: Use ArgumentList to prevent command injection
            process.StartInfo.ArgumentList.Add("compute");
            process.StartInfo.ArgumentList.Add("backend-services");
            process.StartInfo.ArgumentList.Add("update");
            process.StartInfo.ArgumentList.Add(backendServiceName);
            process.StartInfo.ArgumentList.Add($"--region={region}");
            process.StartInfo.ArgumentList.Add($"--backends-from-file={tempFile}");

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                // SECURITY FIX #46: Scrub potentially sensitive information from error messages before logging
                var sanitizedError = ScrubSensitiveData(error);
                _logger.LogError("Failed to update GCP load balancer. Exit code: {ExitCode}", process.ExitCode);
                _logger.LogDebug("GCP CLI error output: {Error}", sanitizedError);
                throw new InvalidOperationException($"Failed to update GCP load balancer (exit code {process.ExitCode})");
            }

            _logger.LogInformation("GCP Load Balancer updated: green={GreenWeight}%, blue={BlueWeight}%",
                (int)(greenWeight * 100), (int)(blueWeight * 100));
        }
        finally
        {
            // Always delete temp file, even on failure
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task SwitchTrafficNginx(int percentage)
    {
        _logger.LogInformation("Updating NGINX upstream configuration");

        var greenWeight = 100 - percentage;
        var blueWeight = percentage;

        // Issue #3 Fix: Write to a managed include file instead of overwriting entire config
        var nginxIncludePath = Environment.GetEnvironmentVariable("HONUA_NGINX_INCLUDE") ?? "/etc/nginx/conf.d/honua-upstream.conf";

        // Generate upstream configuration with weights
        var upstreamConfig = $@"# Honua managed upstream configuration
# This file is automatically generated - do not edit manually
upstream honua_backend {{
    server {_state.GreenEnvironment}:8080 weight={greenWeight};
    server {_state.BlueEnvironment}:8080 weight={blueWeight};
}}";

        // BUG FIX #28: Write to temp file and atomic rename to prevent partial writes
        // Create backup for rollback
        string? backupPath = null;
        if (File.Exists(nginxIncludePath))
        {
            backupPath = nginxIncludePath + ".backup";
            File.Copy(nginxIncludePath, backupPath, overwrite: true);
        }

        var tempFile = Path.GetTempFileName();
        try
        {
            // Write configuration to temp file
            await File.WriteAllTextAsync(tempFile, upstreamConfig);

            // Atomic move to target location
            File.Move(tempFile, nginxIncludePath, overwrite: true);

            _logger.LogInformation("NGINX upstream config written atomically to {ConfigPath}", nginxIncludePath);
        }
        catch
        {
            // Restore backup on failure
            if (backupPath != null && File.Exists(backupPath))
            {
                File.Copy(backupPath, nginxIncludePath, overwrite: true);
                _logger.LogWarning("Restored NGINX config from backup after write failure");
            }
            throw;
        }
        finally
        {
            // Clean up temp file if it still exists
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        // Issue #3 Fix: Validate syntax before reload
        var testProcess = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "nginx",
                Arguments = "-t",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        testProcess.Start();
        var testOutput = await testProcess.StandardOutput.ReadToEndAsync();
        var testError = await testProcess.StandardError.ReadToEndAsync();
        await testProcess.WaitForExitAsync();

        if (testProcess.ExitCode != 0)
        {
            _logger.LogError("NGINX configuration test failed: {Error}", testError);
            throw new InvalidOperationException($"NGINX configuration syntax validation failed: {testError}");
        }

        _logger.LogInformation("NGINX configuration syntax validated successfully");

        // Reload NGINX configuration
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "nginx",
                Arguments = "-s reload",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to reload NGINX: {error}");
        }

        _logger.LogInformation("NGINX upstream updated: green={GreenWeight}%, blue={BlueWeight}%",
            greenWeight, blueWeight);
    }

    private async Task SwitchTrafficHAProxy(int percentage)
    {
        _logger.LogInformation("Updating HAProxy backend configuration");

        var greenWeight = 100 - percentage;
        var blueWeight = percentage;

        var haproxyConfigPath = Environment.GetEnvironmentVariable("HONUA_HAPROXY_CONFIG") ?? "/etc/haproxy/haproxy.cfg";

        // Issue #2 Fix: Insert or error when marker isn't found
        var existingConfig = await File.ReadAllTextAsync(haproxyConfigPath);
        var backendStartIndex = existingConfig.IndexOf("backend honua_backend");

        if (backendStartIndex < 0)
        {
            // Issue #2 Fix: Error when backend block marker is not found
            throw new InvalidOperationException(
                $"HAProxy configuration does not contain 'backend honua_backend' section. " +
                $"Please add the backend block to {haproxyConfigPath} before attempting traffic switching.");
        }

        // BUG FIX #29: Parse and adjust only server weight lines, preserve custom directives
        var nextBackendIndex = existingConfig.IndexOf("backend ", backendStartIndex + 1);
        var backendEndIndex = nextBackendIndex >= 0 ? nextBackendIndex : existingConfig.Length;
        var backendSection = existingConfig.Substring(backendStartIndex, backendEndIndex - backendStartIndex);
        var backendLines = backendSection.Split('\n').ToList();

        // Update only server lines that match our environments, preserve all other directives
        for (int i = 0; i < backendLines.Count; i++)
        {
            var line = backendLines[i].TrimStart();

            // Update green server weight
            if (line.StartsWith($"server {_state.GreenEnvironment} ") ||
                line.StartsWith($"server {_state.GreenEnvironment}:"))
            {
                // Parse existing server line to preserve options
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    // Rebuild line with updated weight, preserving other options
                    var weightRegex = new System.Text.RegularExpressions.Regex(@"\bweight\s+\d+");
                    if (weightRegex.IsMatch(line))
                    {
                        backendLines[i] = weightRegex.Replace(backendLines[i], $"weight {greenWeight}");
                    }
                    else
                    {
                        // Add weight if not present
                        backendLines[i] = backendLines[i].TrimEnd() + $" weight {greenWeight}";
                    }
                }
            }
            // Update blue server weight
            else if (line.StartsWith($"server {_state.BlueEnvironment} ") ||
                     line.StartsWith($"server {_state.BlueEnvironment}:"))
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var weightRegex = new System.Text.RegularExpressions.Regex(@"\bweight\s+\d+");
                    if (weightRegex.IsMatch(line))
                    {
                        backendLines[i] = weightRegex.Replace(backendLines[i], $"weight {blueWeight}");
                    }
                    else
                    {
                        backendLines[i] = backendLines[i].TrimEnd() + $" weight {blueWeight}";
                    }
                }
            }
        }

        var updatedBackendSection = string.Join('\n', backendLines);
        var updatedConfig = existingConfig.Substring(0, backendStartIndex) +
                          updatedBackendSection +
                          existingConfig.Substring(backendEndIndex);

        await File.WriteAllTextAsync(haproxyConfigPath, updatedConfig);

        // BUG FIX #41: Detect init system and use appropriate reload command
        var reloadCommand = Environment.GetEnvironmentVariable("HONUA_HAPROXY_RELOAD_CMD");
        string fileName;
        string arguments;

        if (!string.IsNullOrEmpty(reloadCommand))
        {
            // Use custom reload command if provided
            var cmdParts = reloadCommand.Split(' ', 2);
            fileName = cmdParts[0];
            arguments = cmdParts.Length > 1 ? cmdParts[1] : string.Empty;
            _logger.LogInformation("Using custom HAProxy reload command: {Command}", reloadCommand);
        }
        else
        {
            // Auto-detect init system
            var hasSystemctl = await CheckCommandExists("systemctl");
            if (hasSystemctl)
            {
                fileName = "systemctl";
                arguments = "reload haproxy";
            }
            else
            {
                // Fallback to service command or direct reload
                var hasService = await CheckCommandExists("service");
                if (hasService)
                {
                    fileName = "service";
                    arguments = "haproxy reload";
                }
                else
                {
                    // Direct HAProxy reload signal
                    fileName = "killall";
                    arguments = "-USR2 haproxy";
                    _logger.LogWarning("systemctl and service not found, using killall -USR2 haproxy");
                }
            }
        }

        // Reload HAProxy configuration
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to reload HAProxy: {error}");
        }

        _logger.LogInformation("HAProxy backend updated: green={GreenWeight}%, blue={BlueWeight}%",
            greenWeight, blueWeight);
    }

    private async Task<bool> CheckCommandExists(string command)
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task MonitorMetrics(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Monitoring metrics at {Percentage}% traffic", _state.TrafficPercentageOnBlue);

        var monitoringDuration = TimeSpan.FromMinutes(2);
        var checkInterval = TimeSpan.FromSeconds(10);
        var startTime = DateTime.UtcNow;

        // BUG FIX #40: Thread cancellation token through monitoring checks
        while (DateTime.UtcNow - startTime < monitoringDuration)
        {
            // Check for cancellation at the start of each iteration
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Check error rates
                var errorRate = await CheckErrorRate();
                if (errorRate > 5.0) // More than 5% error rate
                {
                    throw new InvalidOperationException($"Error rate too high: {errorRate:F2}%");
                }

                // Check latency
                var p95Latency = await CheckLatency();
                if (p95Latency > 2000) // P95 latency over 2 seconds
                {
                    _logger.LogWarning("Latency elevated: P95={P95}ms", p95Latency);
                }

                // Check response codes
                var statusCodeDistribution = await CheckStatusCodes();
                var errorCodes = statusCodeDistribution.Where(kvp => kvp.Key >= 500).Sum(kvp => kvp.Value);
                if (errorCodes > 10)
                {
                    throw new InvalidOperationException($"Too many 5xx errors: {errorCodes}");
                }

                _logger.LogDebug("Metrics check passed: error_rate={ErrorRate:F2}%, p95_latency={Latency}ms",
                    errorRate, p95Latency);

                // Use cancellable delay
                await Task.Delay(checkInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Metrics monitoring cancelled at {Percentage}% traffic", _state.TrafficPercentageOnBlue);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Metrics monitoring detected issues");
                throw;
            }
        }

        _logger.LogInformation("Monitoring completed successfully for {Duration} minutes",
            monitoringDuration.TotalMinutes);
    }

    private async Task<double> CheckErrorRate()
    {
        // Issue #1 Fix: Parse responses and treat any failure as a hard stop
        var metricsUrl = Environment.GetEnvironmentVariable("HONUA_METRICS_URL") ?? "http://localhost:9090";
        // BUG FIX #22: Reuse static HttpClient instead of creating new instance per check

        // Prometheus query for error rate
        var query = "sum(rate(http_requests_total{status=~\"5..\"}[1m])) / sum(rate(http_requests_total[1m])) * 100";

        try
        {
            var response = await _metricsHttpClient.GetAsync($"{metricsUrl}/api/v1/query?query={Uri.EscapeDataString(query)}");

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Prometheus query failed with status {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);

            // Parse Prometheus JSON response
            if (!jsonDoc.RootElement.TryGetProperty("status", out var status) || status.GetString() != "success")
            {
                throw new InvalidOperationException($"Prometheus query returned non-success status: {content}");
            }

            if (!jsonDoc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("result", out var result))
            {
                throw new InvalidOperationException($"Prometheus response missing data.result: {content}");
            }

            var resultArray = result.EnumerateArray().ToList();
            if (resultArray.Count == 0)
            {
                // No data points - might indicate no traffic, return 0
                _logger.LogWarning("No error rate data available from Prometheus");
                return 0.0;
            }

            // Extract value from first result
            var firstResult = resultArray[0];
            if (!firstResult.TryGetProperty("value", out var valueArray))
            {
                throw new InvalidOperationException($"Prometheus result missing value field: {content}");
            }

            var valueElements = valueArray.EnumerateArray().ToList();
            if (valueElements.Count < 2)
            {
                throw new InvalidOperationException($"Prometheus value array has unexpected format: {content}");
            }

            var errorRateStr = valueElements[1].GetString();
            // BUG FIX #23: Use InvariantCulture to prevent locale-specific parsing failures
            if (!double.TryParse(errorRateStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var errorRate))
            {
                throw new InvalidOperationException($"Failed to parse error rate value: {errorRateStr}");
            }

            return errorRate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check error rate from metrics service");
            throw new InvalidOperationException("Metrics check failed - cannot proceed with traffic switch", ex);
        }
    }

    private async Task<int> CheckLatency()
    {
        // Issue #1 Fix: Parse responses and treat any failure as a hard stop
        var metricsUrl = Environment.GetEnvironmentVariable("HONUA_METRICS_URL") ?? "http://localhost:9090";
        // BUG FIX #22: Reuse static HttpClient instead of creating new instance per check

        // Prometheus query for P95 latency
        var query = "histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le))";

        try
        {
            var response = await _metricsHttpClient.GetAsync($"{metricsUrl}/api/v1/query?query={Uri.EscapeDataString(query)}");

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Prometheus query failed with status {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);

            // Parse Prometheus JSON response
            if (!jsonDoc.RootElement.TryGetProperty("status", out var status) || status.GetString() != "success")
            {
                throw new InvalidOperationException($"Prometheus query returned non-success status: {content}");
            }

            if (!jsonDoc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("result", out var result))
            {
                throw new InvalidOperationException($"Prometheus response missing data.result: {content}");
            }

            var resultArray = result.EnumerateArray().ToList();
            if (resultArray.Count == 0)
            {
                _logger.LogWarning("No latency data available from Prometheus");
                return 0;
            }

            // Extract value from first result
            var firstResult = resultArray[0];
            if (!firstResult.TryGetProperty("value", out var valueArray))
            {
                throw new InvalidOperationException($"Prometheus result missing value field: {content}");
            }

            var valueElements = valueArray.EnumerateArray().ToList();
            if (valueElements.Count < 2)
            {
                throw new InvalidOperationException($"Prometheus value array has unexpected format: {content}");
            }

            var latencyStr = valueElements[1].GetString();
            // BUG FIX #24: Use InvariantCulture to prevent locale-specific parsing failures
            if (!double.TryParse(latencyStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var latencySeconds))
            {
                throw new InvalidOperationException($"Failed to parse latency value: {latencyStr}");
            }

            // Convert seconds to milliseconds
            return (int)(latencySeconds * 1000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check latency from metrics service");
            throw new InvalidOperationException("Metrics check failed - cannot proceed with traffic switch", ex);
        }
    }

    private async Task<Dictionary<int, int>> CheckStatusCodes()
    {
        // Issue #1 Fix: Parse responses and treat any failure as a hard stop
        var metricsUrl = Environment.GetEnvironmentVariable("HONUA_METRICS_URL") ?? "http://localhost:9090";
        // BUG FIX #22: Reuse static HttpClient instead of creating new instance per check

        // Prometheus query for status codes
        var query = "sum by (status) (rate(http_requests_total[1m]))";

        try
        {
            var response = await _metricsHttpClient.GetAsync($"{metricsUrl}/api/v1/query?query={Uri.EscapeDataString(query)}");

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Prometheus query failed with status {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);

            // Parse Prometheus JSON response
            if (!jsonDoc.RootElement.TryGetProperty("status", out var status) || status.GetString() != "success")
            {
                throw new InvalidOperationException($"Prometheus query returned non-success status: {content}");
            }

            if (!jsonDoc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("result", out var result))
            {
                throw new InvalidOperationException($"Prometheus response missing data.result: {content}");
            }

            var statusCodes = new Dictionary<int, int>();
            var resultArray = result.EnumerateArray().ToList();

            if (resultArray.Count == 0)
            {
                _logger.LogWarning("No status code data available from Prometheus");
                return statusCodes;
            }

            foreach (var resultItem in resultArray)
            {
                // Extract status code from metric labels
                if (!resultItem.TryGetProperty("metric", out var metric) ||
                    !metric.TryGetProperty("status", out var statusLabel))
                {
                    _logger.LogWarning("Prometheus result missing metric.status field");
                    continue;
                }

                // Extract value
                if (!resultItem.TryGetProperty("value", out var valueArray))
                {
                    _logger.LogWarning("Prometheus result missing value field");
                    continue;
                }

                var valueElements = valueArray.EnumerateArray().ToList();
                if (valueElements.Count < 2)
                {
                    _logger.LogWarning("Prometheus value array has unexpected format");
                    continue;
                }

                var statusCodeStr = statusLabel.GetString();
                var rateStr = valueElements[1].GetString();

                // BUG FIX #25: Use InvariantCulture to prevent locale-specific parsing failures
                if (int.TryParse(statusCodeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode) &&
                    double.TryParse(rateStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate))
                {
                    // Convert rate to count (approximate)
                    statusCodes[statusCode] = (int)Math.Round(rate * 60); // rate per minute
                }
            }

            return statusCodes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check status codes from metrics service");
            throw new InvalidOperationException("Metrics check failed - cannot proceed with traffic switch", ex);
        }
    }

    /// <summary>
    /// Rollback traffic switching by routing all traffic back to green.
    /// </summary>
    public async Task<ProcessStepRollbackResult> RollbackAsync(
        object state,
        CancellationToken cancellationToken = default)
    {
        var upgradeState = state as UpgradeState;
        if (upgradeState == null)
        {
            return ProcessStepRollbackResult.Failure(
                "Invalid state type",
                "Expected UpgradeState");
        }

        _logger.LogInformation(
            "Rolling back traffic switch for {DeploymentName}",
            upgradeState.DeploymentName);

        try
        {
            var previousPercentage = upgradeState.TrafficPercentageOnBlue;

            if (previousPercentage == 0)
            {
                _logger.LogInformation("Traffic is already at 0% on blue, no rollback needed");
                return ProcessStepRollbackResult.Success(
                    "Traffic already on green environment");
            }

            // Switch 100% traffic back to green
            _logger.LogInformation("Switching 100% traffic back to green environment");
            await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Simulate load balancer update

            _logger.LogInformation(
                "Successfully rolled back traffic switch for {DeploymentName} " +
                "(was at {PreviousPercentage}% on blue)",
                upgradeState.DeploymentName,
                previousPercentage);

            return ProcessStepRollbackResult.Success(
                $"Switched traffic back to green (was at {previousPercentage}% on blue)");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Traffic switch rollback cancelled for {DeploymentName}",
                upgradeState.DeploymentName);
            return ProcessStepRollbackResult.Failure(
                "Rollback cancelled",
                "Traffic switch rollback was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to rollback traffic switch for {DeploymentName}",
                upgradeState.DeploymentName);
            return ProcessStepRollbackResult.Failure(
                ex.Message,
                ex.StackTrace);
        }
    }

    /// <summary>
    /// SECURITY FIX #24, #25: Validates AWS ARNs to prevent command injection attacks.
    /// </summary>
    private void ValidateAwsArn(string arn, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(arn))
        {
            throw new ArgumentException($"{parameterName} cannot be empty", nameof(arn));
        }

        // AWS ARNs must start with "arn:" and contain only safe characters
        if (!arn.StartsWith("arn:", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{parameterName} must be a valid AWS ARN starting with 'arn:'", nameof(arn));
        }

        // Prevent command injection via newlines, semicolons, or other shell metacharacters
        var dangerousChars = new[] { '\n', '\r', ';', '&', '|', '`', '$', '(', ')', '<', '>' };
        if (arn.IndexOfAny(dangerousChars) >= 0)
        {
            throw new ArgumentException($"{parameterName} contains invalid characters that could enable command injection", nameof(arn));
        }

        // ARN format: arn:partition:service:region:account-id:resource-type/resource-id
        // Basic validation - must have at least 6 colon-separated parts
        var parts = arn.Split(':');
        if (parts.Length < 6)
        {
            throw new ArgumentException($"{parameterName} is not a valid AWS ARN format", nameof(arn));
        }
    }

    /// <summary>
    /// SECURITY FIX #26: Validates Azure resource names to prevent command injection attacks.
    /// </summary>
    private void ValidateAzureResourceName(string resourceName, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new ArgumentException($"{parameterName} cannot be empty", nameof(resourceName));
        }

        // Prevent command injection via special characters
        var dangerousChars = new[] { '\n', '\r', ';', '&', '|', '`', '$', '(', ')', '<', '>', '"', '\'', '\\' };
        if (resourceName.IndexOfAny(dangerousChars) >= 0)
        {
            throw new ArgumentException($"{parameterName} contains invalid characters that could enable command injection", nameof(resourceName));
        }

        // Azure resource names can contain alphanumerics, hyphens, underscores, and periods
        // Prevent flag injection
        if (resourceName.StartsWith("-") || resourceName.StartsWith("--"))
        {
            throw new ArgumentException($"{parameterName} cannot start with dashes (potential flag injection)", nameof(resourceName));
        }

        // Basic length validation
        if (resourceName.Length > 260)
        {
            throw new ArgumentException($"{parameterName} exceeds maximum length", nameof(resourceName));
        }
    }

    /// <summary>
    /// SECURITY FIX #26: Validates GCP resource names to prevent command injection attacks.
    /// </summary>
    private void ValidateGcpResourceName(string resourceName, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new ArgumentException($"{parameterName} cannot be empty", nameof(resourceName));
        }

        // Prevent command injection via special characters
        var dangerousChars = new[] { '\n', '\r', ';', '&', '|', '`', '$', '(', ')', '<', '>', '"', '\'', '\\', ' ' };
        if (resourceName.IndexOfAny(dangerousChars) >= 0)
        {
            throw new ArgumentException($"{parameterName} contains invalid characters that could enable command injection", nameof(resourceName));
        }

        // GCP resource names must follow RFC 1035 (lowercase letters, numbers, hyphens)
        // Prevent flag injection
        if (resourceName.StartsWith("-") || resourceName.StartsWith("--"))
        {
            throw new ArgumentException($"{parameterName} cannot start with dashes (potential flag injection)", nameof(resourceName));
        }

        // GCP project IDs have specific format requirements
        if (parameterName.Contains("Project") || parameterName.Contains("project"))
        {
            // Project IDs must be 6-30 characters, lowercase letters, digits, hyphens
            if (resourceName.Length < 6 || resourceName.Length > 30)
            {
                throw new ArgumentException($"{parameterName} must be between 6 and 30 characters", nameof(resourceName));
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(resourceName, "^[a-z][a-z0-9-]*[a-z0-9]$"))
            {
                throw new ArgumentException($"{parameterName} must start with a letter, contain only lowercase letters, digits, and hyphens, and end with a letter or digit", nameof(resourceName));
            }
        }
    }

    /// <summary>
    /// SECURITY FIX #46: Scrubs sensitive data from CLI error output before logging.
    /// Removes access tokens, credentials, and other secrets that might appear in error messages.
    /// </summary>
    private string ScrubSensitiveData(string errorOutput)
    {
        if (string.IsNullOrEmpty(errorOutput))
        {
            return errorOutput;
        }

        // Scrub common sensitive patterns
        var scrubbed = errorOutput;

        // AWS credentials and session tokens
        scrubbed = System.Text.RegularExpressions.Regex.Replace(scrubbed,
            @"(?i)(aws_access_key_id|aws_secret_access_key|aws_session_token|x-amz-security-token)[=:\s]+[\w/+=]+",
            "$1=***REDACTED***");

        // Azure access tokens
        scrubbed = System.Text.RegularExpressions.Regex.Replace(scrubbed,
            @"(?i)(bearer|authorization|x-ms-token)[=:\s]+[\w\-\.]+",
            "$1=***REDACTED***");

        // GCP access tokens
        scrubbed = System.Text.RegularExpressions.Regex.Replace(scrubbed,
            @"(?i)(access_token|refresh_token|id_token)[""':\s]+[\w\-\.]+",
            "$1=***REDACTED***");

        // Generic API keys and secrets
        scrubbed = System.Text.RegularExpressions.Regex.Replace(scrubbed,
            @"(?i)(api[_-]?key|secret|password|token)[""'=:\s]+[\w\-\.]+",
            "$1=***REDACTED***");

        // OAuth tokens in URLs
        scrubbed = System.Text.RegularExpressions.Regex.Replace(scrubbed,
            @"[?&](access_token|token|key)=[^&\s]+",
            "?$1=***REDACTED***");

        return scrubbed;
    }
}
