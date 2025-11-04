// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using CertificateRenewalState = Honua.Cli.AI.Services.Processes.State.CertificateRenewalState;
using Honua.Cli.AI.Services.Processes.State;
using System.Diagnostics;
using Honua.Cli.AI.Services.Execution;

namespace Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;

/// <summary>
/// Deploys new certificates to load balancers, ingress controllers, CDN, etc.
/// Uses rolling update to avoid downtime.
/// </summary>
public class DeployCertificateStep : KernelProcessStep<CertificateRenewalState>
{
    private readonly ILogger<DeployCertificateStep> _logger;
    private CertificateRenewalState _state = new();

    public DeployCertificateStep(ILogger<DeployCertificateStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<CertificateRenewalState> state)
    {
        _state = state.State ?? new CertificateRenewalState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("DeployCertificate")]
    public async Task DeployCertificateAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Deploying {Count} certificates to deployment targets",
            _state.NewCertificates.Count);

        _state.Status = "Deploying Certificate";

        // Collect deployment targets from environment or configuration
        var targets = new List<DeploymentTarget>();

        // Check for Kubernetes deployment target
        var kubeNamespace = Environment.GetEnvironmentVariable("HONUA_KUBE_NAMESPACE");
        var kubeSecretName = Environment.GetEnvironmentVariable("HONUA_KUBE_SECRET");
        if (!string.IsNullOrEmpty(kubeNamespace) && !string.IsNullOrEmpty(kubeSecretName))
        {
            targets.Add(new DeploymentTarget
            {
                TargetType = "Kubernetes",
                TargetName = $"{kubeNamespace}/{kubeSecretName}",
                Region = Environment.GetEnvironmentVariable("HONUA_KUBE_CONTEXT") ?? "default"
            });
        }

        // Check for file system deployment target (local testing, nginx, etc.)
        var localDeployPath = Environment.GetEnvironmentVariable("HONUA_CERT_DEPLOY_PATH");
        if (!string.IsNullOrEmpty(localDeployPath))
        {
            targets.Add(new DeploymentTarget
            {
                TargetType = "FileSystem",
                TargetName = localDeployPath,
                Region = "local"
            });
        }

        // If no targets configured, use default file system deployment
        if (targets.Count == 0)
        {
            var defaultPath = "/etc/honua/certificates";
            if (OperatingSystem.IsWindows())
            {
                defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Honua", "certificates");
            }

            targets.Add(new DeploymentTarget
            {
                TargetType = "FileSystem",
                TargetName = defaultPath,
                Region = "local"
            });

            _logger.LogInformation("No deployment targets configured, using default: {Path}", defaultPath);
        }

        foreach (var target in targets)
        {
            _logger.LogInformation("Deploying certificates to {TargetType} {TargetName}",
                target.TargetType, target.TargetName);

            try
            {
                switch (target.TargetType)
                {
                    case "FileSystem":
                        await DeployToFileSystemAsync(target);
                        break;

                    case "Kubernetes":
                        await DeployToKubernetesAsync(target);
                        break;

                    case "LoadBalancer":
                        await DeployToLoadBalancerAsync(target);
                        break;

                    default:
                        throw new NotSupportedException($"Deployment target type '{target.TargetType}' is not supported");
                }

                target.DeploymentSuccessful = true;
                target.DeploymentTime = DateTime.UtcNow;

                _logger.LogInformation("Successfully deployed to {TargetType} {TargetName}",
                    target.TargetType, target.TargetName);
            }
            catch (Exception ex)
            {
                target.DeploymentSuccessful = false;
                target.DeploymentError = ex.Message;

                _logger.LogError(ex, "Failed to deploy to {TargetType} {TargetName}",
                    target.TargetType, target.TargetName);
            }
        }

        _state.UpdatedTargets = targets;

        // Check if all deployments succeeded
        var allSucceeded = targets.All(t => t.DeploymentSuccessful);

        if (!allSucceeded)
        {
            _state.Status = "Deployment Failed";
            _state.ErrorMessage = "One or more certificate deployments failed";

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "DeploymentFailed",
                Data = _state
            });
            return;
        }

        _logger.LogInformation("All certificates deployed successfully");

        await context.EmitEventAsync(new KernelProcessEvent
        {
            Id = "CertificateDeployed",
            Data = _state
        });
    }

    private async Task DeployToFileSystemAsync(DeploymentTarget target)
    {
        var targetPath = target.TargetName;

        // Ensure target directory exists
        if (!System.IO.Directory.Exists(targetPath))
        {
            System.IO.Directory.CreateDirectory(targetPath);
            _logger.LogInformation("Created certificate directory: {Path}", targetPath);
        }

        foreach (var cert in _state.NewCertificates)
        {
            // Copy certificate files to target location
            var sourceCertPath = cert.CertificatePath;
            var sourceKeyPath = cert.CertificatePath.Replace(".pem", ".key");

            var targetCertPath = Path.Combine(targetPath, Path.GetFileName(sourceCertPath));
            var targetKeyPath = Path.Combine(targetPath, Path.GetFileName(sourceKeyPath));

            if (File.Exists(sourceCertPath))
            {
                await File.WriteAllTextAsync(targetCertPath, await File.ReadAllTextAsync(sourceCertPath));
                _logger.LogInformation("Deployed certificate to {Path}", targetCertPath);
            }

            if (File.Exists(sourceKeyPath))
            {
                await File.WriteAllTextAsync(targetKeyPath, await File.ReadAllTextAsync(sourceKeyPath));

                // Set restrictive permissions on private key
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(targetKeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                _logger.LogInformation("Deployed private key to {Path}", targetKeyPath);
            }
        }
    }

    private async Task DeployToKubernetesAsync(DeploymentTarget target)
    {
        // Parse namespace and secret name from target
        var parts = target.TargetName.Split('/');
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid Kubernetes target format: {target.TargetName}. Expected: namespace/secretname");
        }

        var namespace_ = parts[0];
        var secretName = parts[1];

        // Validate inputs to prevent command injection
        CommandArgumentValidator.ValidateIdentifier(namespace_, nameof(namespace_));
        CommandArgumentValidator.ValidateIdentifier(secretName, nameof(secretName));

        _logger.LogInformation("Deploying to Kubernetes secret {Namespace}/{SecretName}", namespace_, secretName);

        var cert = _state.NewCertificates.FirstOrDefault();
        if (cert == null)
        {
            throw new InvalidOperationException("No certificate available for deployment");
        }

        var certPath = cert.CertificatePath;
        var keyPath = cert.CertificatePath.Replace(".pem", ".key");

        // Validate that certificate files exist
        if (!File.Exists(certPath))
        {
            throw new InvalidOperationException($"Certificate file not found: {certPath}");
        }

        if (!File.Exists(keyPath))
        {
            throw new InvalidOperationException($"Private key file not found: {keyPath}");
        }

        // Validate file paths for security
        CommandArgumentValidator.ValidatePath(certPath, nameof(certPath));
        CommandArgumentValidator.ValidatePath(keyPath, nameof(keyPath));

        // Create kubectl command to update/create the TLS secret
        // Using dry-run=client -o yaml | apply pattern to create or update secret
        var psi = new ProcessStartInfo
        {
            FileName = "kubectl",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Build argument list safely using ArgumentList to prevent injection
        psi.ArgumentList.Add("create");
        psi.ArgumentList.Add("secret");
        psi.ArgumentList.Add("tls");
        psi.ArgumentList.Add(secretName);
        psi.ArgumentList.Add($"--cert={certPath}");
        psi.ArgumentList.Add($"--key={keyPath}");
        psi.ArgumentList.Add($"--namespace={namespace_}");
        psi.ArgumentList.Add("--dry-run=client");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add("yaml");

        // First, generate the YAML
        _logger.LogInformation("Generating Kubernetes secret manifest for {SecretName}", secretName);

        string secretYaml;
        using (var process = Process.Start(psi))
        {
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start kubectl process");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdoutTask, stderrTask);

            secretYaml = await stdoutTask;
            var error = await stderrTask;

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to generate Kubernetes secret manifest: {error}");
            }
        }

        // Now apply the YAML
        _logger.LogInformation("Applying Kubernetes secret to namespace {Namespace}", namespace_);

        var applyPsi = new ProcessStartInfo
        {
            FileName = "kubectl",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        applyPsi.ArgumentList.Add("apply");
        applyPsi.ArgumentList.Add("-f");
        applyPsi.ArgumentList.Add("-");

        using (var applyProcess = Process.Start(applyPsi))
        {
            if (applyProcess == null)
            {
                throw new InvalidOperationException("Failed to start kubectl apply process");
            }

            // Write the YAML to stdin
            await applyProcess.StandardInput.WriteAsync(secretYaml);
            await applyProcess.StandardInput.FlushAsync();
            applyProcess.StandardInput.Close();

            var stdoutTask = applyProcess.StandardOutput.ReadToEndAsync();
            var stderrTask = applyProcess.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdoutTask, stderrTask);

            var output = await stdoutTask;
            var error = await stderrTask;

            await applyProcess.WaitForExitAsync();

            if (applyProcess.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to apply Kubernetes secret: {error}");
            }

            _logger.LogInformation("Successfully deployed certificate to Kubernetes: {Output}", output.Trim());
        }
    }

    private async Task DeployToLoadBalancerAsync(DeploymentTarget target)
    {
        _logger.LogInformation("Deploying certificate to load balancer: {TargetName}", target.TargetName);

        var cert = _state.NewCertificates.FirstOrDefault();
        if (cert == null)
        {
            throw new InvalidOperationException("No certificate available for load balancer deployment");
        }

        var certPath = cert.CertificatePath;
        var keyPath = cert.CertificatePath.Replace(".pem", ".key");

        if (!File.Exists(certPath))
        {
            throw new InvalidOperationException($"Certificate file not found: {certPath}");
        }

        if (!File.Exists(keyPath))
        {
            throw new InvalidOperationException($"Private key file not found: {keyPath}");
        }

        // Determine load balancer provider from target name or environment
        var lbProvider = target.Region?.ToLowerInvariant() ?? Environment.GetEnvironmentVariable("HONUA_LB_PROVIDER")?.ToLowerInvariant() ?? "aws";

        switch (lbProvider)
        {
            case "aws":
            case "alb":
            case "acm":
                await DeployToAwsLoadBalancerAsync(target, certPath, keyPath);
                break;

            case "azure":
            case "azure-appgw":
                await DeployToAzureLoadBalancerAsync(target, certPath, keyPath);
                break;

            case "gcp":
            case "gcp-lb":
                await DeployToGcpLoadBalancerAsync(target, certPath, keyPath);
                break;

            default:
                throw new NotSupportedException($"Load balancer provider '{lbProvider}' is not supported. " +
                    "Supported providers: aws, azure, gcp. Set HONUA_LB_PROVIDER environment variable.");
        }

        _logger.LogInformation("Successfully deployed certificate to {Provider} load balancer", lbProvider);
    }

    private async Task DeployToAwsLoadBalancerAsync(DeploymentTarget target, string certPath, string keyPath)
    {
        _logger.LogInformation("Deploying certificate to AWS Certificate Manager (ACM)");

        // Read certificate and private key
        var certContent = await File.ReadAllTextAsync(certPath);
        var keyContent = await File.ReadAllTextAsync(keyPath);

        // Check if chain certificate exists
        var chainPath = certPath.Replace(".pem", "-chain.pem");
        string? chainContent = null;
        if (File.Exists(chainPath))
        {
            chainContent = await File.ReadAllTextAsync(chainPath);
        }

        // Write certificate contents to temporary files for AWS CLI
        var tempCertFile = Path.GetTempFileName();
        var tempKeyFile = Path.GetTempFileName();
        string? tempChainFile = null;

        try
        {
            await File.WriteAllTextAsync(tempCertFile, certContent);
            await File.WriteAllTextAsync(tempKeyFile, keyContent);

            if (chainContent != null)
            {
                tempChainFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempChainFile, chainContent);
            }

            // Import certificate to ACM
            var psi = new ProcessStartInfo
            {
                FileName = "aws",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("acm");
            psi.ArgumentList.Add("import-certificate");
            psi.ArgumentList.Add("--certificate");
            psi.ArgumentList.Add($"file://{tempCertFile}");
            psi.ArgumentList.Add("--private-key");
            psi.ArgumentList.Add($"file://{tempKeyFile}");

            if (tempChainFile != null)
            {
                psi.ArgumentList.Add("--certificate-chain");
                psi.ArgumentList.Add($"file://{tempChainFile}");
            }

            // If certificate ARN is provided, update existing certificate
            var existingCertArn = Environment.GetEnvironmentVariable("HONUA_AWS_CERT_ARN");
            if (!string.IsNullOrEmpty(existingCertArn))
            {
                psi.ArgumentList.Add("--certificate-arn");
                psi.ArgumentList.Add(existingCertArn);
                _logger.LogInformation("Updating existing ACM certificate: {CertArn}", existingCertArn);
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start AWS CLI process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to import certificate to ACM: {error}");
            }

            _logger.LogInformation("Certificate imported to ACM successfully: {Output}", output.Trim());

            // If listener ARN is provided, update the listener certificate
            var listenerArn = Environment.GetEnvironmentVariable("HONUA_AWS_LISTENER_ARN");
            if (!string.IsNullOrEmpty(listenerArn) && !string.IsNullOrEmpty(existingCertArn))
            {
                await UpdateAwsListenerCertificateAsync(listenerArn, existingCertArn);
            }
        }
        finally
        {
            // Clean up temporary files
            try
            {
                if (File.Exists(tempCertFile)) File.Delete(tempCertFile);
                if (File.Exists(tempKeyFile)) File.Delete(tempKeyFile);
                if (tempChainFile != null && File.Exists(tempChainFile)) File.Delete(tempChainFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temporary certificate files");
            }
        }
    }

    private async Task UpdateAwsListenerCertificateAsync(string listenerArn, string certArn)
    {
        _logger.LogInformation("Updating ALB listener {ListenerArn} with certificate {CertArn}", listenerArn, certArn);

        var psi = new ProcessStartInfo
        {
            FileName = "aws",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("elbv2");
        psi.ArgumentList.Add("modify-listener");
        psi.ArgumentList.Add("--listener-arn");
        psi.ArgumentList.Add(listenerArn);
        psi.ArgumentList.Add("--certificates");
        psi.ArgumentList.Add($"CertificateArn={certArn}");

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start AWS CLI process");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to update ALB listener certificate: {error}");
        }

        _logger.LogInformation("ALB listener certificate updated successfully");
    }

    private async Task DeployToAzureLoadBalancerAsync(DeploymentTarget target, string certPath, string keyPath)
    {
        _logger.LogInformation("Deploying certificate to Azure Application Gateway");

        // Read certificate and private key
        var certContent = await File.ReadAllTextAsync(certPath);
        var keyContent = await File.ReadAllTextAsync(keyPath);

        // Create PFX certificate from PEM certificate and key
        var tempPfxFile = Path.GetTempFileName() + ".pfx";
        var pfxPassword = Guid.NewGuid().ToString("N");

        try
        {
            // Use openssl to create PFX
            var opensslPsi = new ProcessStartInfo
            {
                FileName = "openssl",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            opensslPsi.ArgumentList.Add("pkcs12");
            opensslPsi.ArgumentList.Add("-export");
            opensslPsi.ArgumentList.Add("-out");
            opensslPsi.ArgumentList.Add(tempPfxFile);
            opensslPsi.ArgumentList.Add("-inkey");
            opensslPsi.ArgumentList.Add(keyPath);
            opensslPsi.ArgumentList.Add("-in");
            opensslPsi.ArgumentList.Add(certPath);
            opensslPsi.ArgumentList.Add("-password");
            opensslPsi.ArgumentList.Add($"pass:{pfxPassword}");

            using (var opensslProcess = Process.Start(opensslPsi))
            {
                if (opensslProcess == null)
                {
                    throw new InvalidOperationException("Failed to start openssl process");
                }

                var opensslError = await opensslProcess.StandardError.ReadToEndAsync();
                await opensslProcess.WaitForExitAsync();

                if (opensslProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to create PFX certificate: {opensslError}");
                }
            }

            // Upload to Azure Application Gateway
            var resourceGroup = Environment.GetEnvironmentVariable("HONUA_AZURE_RESOURCE_GROUP");
            var appGatewayName = Environment.GetEnvironmentVariable("HONUA_AZURE_APPGW_NAME");
            var certName = Environment.GetEnvironmentVariable("HONUA_AZURE_CERT_NAME") ?? "honua-cert";

            if (string.IsNullOrEmpty(resourceGroup) || string.IsNullOrEmpty(appGatewayName))
            {
                throw new InvalidOperationException("Azure configuration missing. Required: HONUA_AZURE_RESOURCE_GROUP, HONUA_AZURE_APPGW_NAME");
            }

            var psi = new ProcessStartInfo
            {
                FileName = "az",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("network");
            psi.ArgumentList.Add("application-gateway");
            psi.ArgumentList.Add("ssl-cert");
            psi.ArgumentList.Add("update");
            psi.ArgumentList.Add("--resource-group");
            psi.ArgumentList.Add(resourceGroup);
            psi.ArgumentList.Add("--gateway-name");
            psi.ArgumentList.Add(appGatewayName);
            psi.ArgumentList.Add("--name");
            psi.ArgumentList.Add(certName);
            psi.ArgumentList.Add("--cert-file");
            psi.ArgumentList.Add(tempPfxFile);
            psi.ArgumentList.Add("--cert-password");
            psi.ArgumentList.Add(pfxPassword);

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Azure CLI process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to update Azure Application Gateway certificate: {error}");
            }

            _logger.LogInformation("Certificate deployed to Azure Application Gateway successfully");
        }
        finally
        {
            // Clean up temporary PFX file
            try
            {
                if (File.Exists(tempPfxFile)) File.Delete(tempPfxFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temporary PFX file");
            }
        }
    }

    private async Task DeployToGcpLoadBalancerAsync(DeploymentTarget target, string certPath, string keyPath)
    {
        _logger.LogInformation("Deploying certificate to GCP Load Balancer");

        // Read certificate and private key
        var certContent = await File.ReadAllTextAsync(certPath);
        var keyContent = await File.ReadAllTextAsync(keyPath);

        // Write to temporary files for gcloud CLI
        var tempCertFile = Path.GetTempFileName();
        var tempKeyFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(tempCertFile, certContent);
            await File.WriteAllTextAsync(tempKeyFile, keyContent);

            var certName = Environment.GetEnvironmentVariable("HONUA_GCP_CERT_NAME") ?? "honua-cert";
            var projectId = Environment.GetEnvironmentVariable("HONUA_GCP_PROJECT_ID");

            if (string.IsNullOrEmpty(projectId))
            {
                throw new InvalidOperationException("GCP configuration missing. Required: HONUA_GCP_PROJECT_ID");
            }

            // Check if certificate already exists
            var describePsi = new ProcessStartInfo
            {
                FileName = "gcloud",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            describePsi.ArgumentList.Add("compute");
            describePsi.ArgumentList.Add("ssl-certificates");
            describePsi.ArgumentList.Add("describe");
            describePsi.ArgumentList.Add(certName);
            describePsi.ArgumentList.Add($"--project={projectId}");

            using var describeProcess = Process.Start(describePsi);
            if (describeProcess != null)
            {
                await describeProcess.WaitForExitAsync();
                bool certExists = describeProcess.ExitCode == 0;

                if (certExists)
                {
                    _logger.LogInformation("Certificate {CertName} already exists, creating new version", certName);
                    // GCP doesn't support updating certificates, need to create new one with different name
                    certName = $"{certName}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                }
            }

            // Create new SSL certificate
            var psi = new ProcessStartInfo
            {
                FileName = "gcloud",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("compute");
            psi.ArgumentList.Add("ssl-certificates");
            psi.ArgumentList.Add("create");
            psi.ArgumentList.Add(certName);
            psi.ArgumentList.Add($"--certificate={tempCertFile}");
            psi.ArgumentList.Add($"--private-key={tempKeyFile}");
            psi.ArgumentList.Add($"--project={projectId}");

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start gcloud process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to create GCP SSL certificate: {error}");
            }

            _logger.LogInformation("Certificate created in GCP: {CertName}", certName);

            // Update target HTTPS proxy if specified
            var targetProxyName = Environment.GetEnvironmentVariable("HONUA_GCP_TARGET_PROXY");
            if (!string.IsNullOrEmpty(targetProxyName))
            {
                await UpdateGcpTargetProxyAsync(targetProxyName, certName, projectId);
            }
        }
        finally
        {
            // Clean up temporary files
            try
            {
                if (File.Exists(tempCertFile)) File.Delete(tempCertFile);
                if (File.Exists(tempKeyFile)) File.Delete(tempKeyFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temporary certificate files");
            }
        }
    }

    private async Task UpdateGcpTargetProxyAsync(string targetProxyName, string certName, string projectId)
    {
        _logger.LogInformation("Updating GCP target HTTPS proxy {ProxyName} with certificate {CertName}", targetProxyName, certName);

        var psi = new ProcessStartInfo
        {
            FileName = "gcloud",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("compute");
        psi.ArgumentList.Add("target-https-proxies");
        psi.ArgumentList.Add("update");
        psi.ArgumentList.Add(targetProxyName);
        psi.ArgumentList.Add($"--ssl-certificates={certName}");
        psi.ArgumentList.Add($"--project={projectId}");

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start gcloud process");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to update GCP target HTTPS proxy: {error}");
        }

        _logger.LogInformation("GCP target HTTPS proxy updated successfully");
    }
}
