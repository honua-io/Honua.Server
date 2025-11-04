// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DeploymentState = Honua.Cli.AI.Services.Processes.State.DeploymentState;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Deploys observability stack (Prometheus, Grafana, OpenTelemetry).
/// </summary>
public class ConfigureObservabilityStep : KernelProcessStep<DeploymentState>
{
    private readonly ILogger<ConfigureObservabilityStep> _logger;
    private DeploymentState _state = new();

    public ConfigureObservabilityStep(ILogger<ConfigureObservabilityStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
    {
        _state = state.State ?? new DeploymentState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ConfigureObservability")]
    public async Task ConfigureObservabilityAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Configuring observability for deployment {DeploymentId}", _state.DeploymentId);

        _state.Status = "ConfiguringObservability";

        // Track temporary files for cleanup
        var tempFiles = new List<string>();

        try
        {
            // Deploy Prometheus
            var prometheusValuesPath = await DeployPrometheus();
            if (!string.IsNullOrEmpty(prometheusValuesPath))
                tempFiles.Add(prometheusValuesPath);

            // Deploy Grafana
            var grafanaValuesPath = await DeployGrafana();
            if (!string.IsNullOrEmpty(grafanaValuesPath))
                tempFiles.Add(grafanaValuesPath);

            // Configure OpenTelemetry
            var otelConfigPath = await ConfigureOpenTelemetry();
            if (!string.IsNullOrEmpty(otelConfigPath))
                tempFiles.Add(otelConfigPath);

            _state.Status = "Completed";
            _logger.LogInformation("Deployment {DeploymentId} completed successfully", _state.DeploymentId);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "DeploymentCompleted",
                Data = _state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure observability for {DeploymentId}", _state.DeploymentId);
            _state.Status = "ObservabilityFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ObservabilityFailed",
                Data = new { _state.DeploymentId, Error = ex.Message }
            });
        }
        finally
        {
            // SECURITY: Clean up temporary files containing sensitive data (Grafana admin password, etc.)
            CleanupTempFiles(tempFiles);
        }
    }

    private async Task<string> DeployPrometheus()
    {
        _logger.LogInformation("Deploying Prometheus");

        string valuesPath = string.Empty;

        try
        {
            // Add Prometheus Helm repository
            _logger.LogInformation("Adding Prometheus Helm repository");
            await ExecuteHelmCommandAsync("repo", "add", "prometheus-community",
                "https://prometheus-community.github.io/helm-charts");
            await ExecuteHelmCommandAsync("repo", "update");

            // Generate Prometheus values
            var prometheusValues = GeneratePrometheusValues();
            valuesPath = CreateSecureTempFile($"prometheus-values-{_state.DeploymentId}.yaml");
            await File.WriteAllTextAsync(valuesPath, prometheusValues);

            // Install or upgrade Prometheus
            _logger.LogInformation("Installing Prometheus");
            try
            {
                await ExecuteHelmCommandAsync("install", "prometheus",
                    "prometheus-community/prometheus",
                    "--namespace", "monitoring",
                    "--create-namespace",
                    "--values", valuesPath);
            }
            catch
            {
                // Might already exist, try upgrading
                _logger.LogInformation("Prometheus already exists, upgrading instead");
                await ExecuteHelmCommandAsync("upgrade", "prometheus",
                    "prometheus-community/prometheus",
                    "--namespace", "monitoring",
                    "--values", valuesPath);
            }

            _logger.LogInformation("Prometheus deployed successfully");
            return valuesPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy Prometheus");
            // Clean up on failure
            if (!string.IsNullOrEmpty(valuesPath) && File.Exists(valuesPath))
            {
                SecureDeleteFile(valuesPath);
            }
            throw;
        }
    }

    private async Task<string> DeployGrafana()
    {
        _logger.LogInformation("Deploying Grafana");

        string valuesPath = string.Empty;

        try
        {
            // Add Grafana Helm repository
            _logger.LogInformation("Adding Grafana Helm repository");
            await ExecuteHelmCommandAsync("repo", "add", "grafana",
                "https://grafana.github.io/helm-charts");
            await ExecuteHelmCommandAsync("repo", "update");

            // Generate Grafana values (contains admin password)
            var grafanaValues = GenerateGrafanaValues();
            valuesPath = CreateSecureTempFile($"grafana-values-{_state.DeploymentId}.yaml");
            await File.WriteAllTextAsync(valuesPath, grafanaValues);
            // Set restrictive permissions on file containing Grafana admin password
            SetRestrictiveFilePermissions(valuesPath);

            // Install or upgrade Grafana
            _logger.LogInformation("Installing Grafana");
            try
            {
                await ExecuteHelmCommandAsync("install", "grafana",
                    "grafana/grafana",
                    "--namespace", "monitoring",
                    "--create-namespace",
                    "--values", valuesPath);
            }
            catch
            {
                // Might already exist, try upgrading
                _logger.LogInformation("Grafana already exists, upgrading instead");
                await ExecuteHelmCommandAsync("upgrade", "grafana",
                    "grafana/grafana",
                    "--namespace", "monitoring",
                    "--values", valuesPath);
            }

            _logger.LogInformation("Grafana deployed successfully");
            return valuesPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy Grafana");
            // Clean up on failure
            if (!string.IsNullOrEmpty(valuesPath) && File.Exists(valuesPath))
            {
                SecureDeleteFile(valuesPath);
            }
            throw;
        }
    }

    private async Task<string> ConfigureOpenTelemetry()
    {
        _logger.LogInformation("Configuring OpenTelemetry collector");

        string configPath = string.Empty;

        try
        {
            // Generate OpenTelemetry collector configuration
            var otelConfig = GenerateOTelCollectorConfig();
            configPath = CreateSecureTempFile($"otel-collector-{_state.DeploymentId}.yaml");
            await File.WriteAllTextAsync(configPath, otelConfig);

            // Deploy OpenTelemetry collector using kubectl
            _logger.LogInformation("Deploying OpenTelemetry collector");
            await ExecuteKubectlCommandAsync("apply", "-f", configPath,
                "--namespace", "monitoring");

            _logger.LogInformation("OpenTelemetry collector configured successfully");
            return configPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure OpenTelemetry");
            // Clean up on failure
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            {
                SecureDeleteFile(configPath);
            }
            throw;
        }
    }

    private string GeneratePrometheusValues()
    {
        return $@"server:
  persistentVolume:
    size: 10Gi
  retention: 30d
  global:
    scrape_interval: 15s
    evaluation_interval: 15s
    external_labels:
      deployment: '{_state.DeploymentName}'
      environment: 'production'

alertmanager:
  enabled: true
  persistentVolume:
    size: 2Gi

nodeExporter:
  enabled: true

pushgateway:
  enabled: true

serverFiles:
  prometheus.yml:
    scrape_configs:
      - job_name: 'honua-gis'
        kubernetes_sd_configs:
          - role: pod
        relabel_configs:
          - source_labels: [__meta_kubernetes_pod_label_app]
            action: keep
            regex: honua-gis
          - source_labels: [__meta_kubernetes_pod_label_deployment]
            action: keep
            regex: '{_state.DeploymentName}'
";
    }

    private string GenerateGrafanaValues()
    {
        // Generate secure random password and store in deployment state
        var grafanaPassword = GenerateSecurePassword(32);
        _state.InfrastructureOutputs["grafana_admin_password"] = grafanaPassword;
        _logger.LogInformation("Generated secure Grafana admin password and stored in deployment state");

        return $@"adminPassword: '{grafanaPassword}'

datasources:
  datasources.yaml:
    apiVersion: 1
    datasources:
      - name: Prometheus
        type: prometheus
        url: http://prometheus-server:80
        access: proxy
        isDefault: true

dashboardProviders:
  dashboardproviders.yaml:
    apiVersion: 1
    providers:
      - name: 'honua'
        orgId: 1
        folder: 'Honua GIS'
        type: file
        disableDeletion: false
        editable: true
        options:
          path: /var/lib/grafana/dashboards/honua

service:
  type: LoadBalancer

env:
  GF_INSTALL_PLUGINS: 'grafana-piechart-panel,grafana-worldmap-panel'
  GF_SERVER_ROOT_URL: 'https://grafana.{_state.DeploymentName}.honua.io'
";
    }

    private string GenerateOTelCollectorConfig()
    {
        return $@"apiVersion: v1
kind: ConfigMap
metadata:
  name: otel-collector-config
  namespace: monitoring
data:
  config.yaml: |
    receivers:
      otlp:
        protocols:
          grpc:
            endpoint: 0.0.0.0:4317
          http:
            endpoint: 0.0.0.0:4318
      prometheus:
        config:
          scrape_configs:
            - job_name: 'otel-collector'
              scrape_interval: 10s
              static_configs:
                - targets: ['localhost:8888']

    processors:
      batch:
        timeout: 10s
        send_batch_size: 1024
      memory_limiter:
        check_interval: 1s
        limit_mib: 512
      attributes:
        actions:
          - key: deployment
            value: {_state.DeploymentName}
            action: insert

    exporters:
      prometheus:
        endpoint: 0.0.0.0:8889
      logging:
        loglevel: info

    service:
      pipelines:
        traces:
          receivers: [otlp]
          processors: [memory_limiter, batch, attributes]
          exporters: [logging]
        metrics:
          receivers: [otlp, prometheus]
          processors: [memory_limiter, batch, attributes]
          exporters: [prometheus, logging]
        logs:
          receivers: [otlp]
          processors: [memory_limiter, batch, attributes]
          exporters: [logging]
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: otel-collector
  namespace: monitoring
spec:
  replicas: 1
  selector:
    matchLabels:
      app: otel-collector
  template:
    metadata:
      labels:
        app: otel-collector
    spec:
      containers:
      - name: otel-collector
        image: otel/opentelemetry-collector:latest
        args:
          - --config=/conf/config.yaml
        ports:
        - containerPort: 4317
          name: otlp-grpc
        - containerPort: 4318
          name: otlp-http
        - containerPort: 8889
          name: prometheus
        volumeMounts:
        - name: config
          mountPath: /conf
      volumes:
      - name: config
        configMap:
          name: otel-collector-config
---
apiVersion: v1
kind: Service
metadata:
  name: otel-collector
  namespace: monitoring
spec:
  selector:
    app: otel-collector
  ports:
  - name: otlp-grpc
    port: 4317
    targetPort: 4317
  - name: otlp-http
    port: 4318
    targetPort: 4318
  - name: prometheus
    port: 8889
    targetPort: 8889
  type: ClusterIP
";
    }

    private string GenerateSecurePassword(int length = 32)
    {
        // Generate cryptographically secure random password
        const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+-=[]{}|;:,.<>?";
        var result = new StringBuilder(length);

        using var rng = RandomNumberGenerator.Create();
        var buffer = new byte[sizeof(uint)];

        for (int i = 0; i < length; i++)
        {
            rng.GetBytes(buffer);
            var num = BitConverter.ToUInt32(buffer, 0);
            result.Append(validChars[(int)(num % (uint)validChars.Length)]);
        }

        return result.ToString();
    }

    private async Task<string> ExecuteHelmCommandAsync(params string[] arguments)
    {
        return await ExecuteCommandAsync("helm", arguments);
    }

    private async Task<string> ExecuteKubectlCommandAsync(params string[] arguments)
    {
        return await ExecuteCommandAsync("kubectl", arguments);
    }

    private async Task<string> ExecuteCommandAsync(string command, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Use ArgumentList to prevent command injection
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException($"Failed to start {command} process");

        // Read stdout and stderr concurrently to prevent deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync();

        var output = await stdoutTask;
        var error = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError("{Command} command failed: {Error}", command, error);
            throw new InvalidOperationException($"{command} command failed: {error}");
        }

        return output;
    }

    /// <summary>
    /// Creates a secure temporary file with restricted permissions in a dedicated directory.
    /// </summary>
    private string CreateSecureTempFile(string fileName)
    {
        var secureTempDir = Path.Combine(Path.GetTempPath(), "honua-observability", _state.DeploymentId);
        Directory.CreateDirectory(secureTempDir);

        // On Unix systems, restrict directory permissions to owner only (700)
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(secureTempDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set Unix directory permissions on {Path}", secureTempDir);
            }
        }

        return Path.Combine(secureTempDir, fileName);
    }

    /// <summary>
    /// Sets restrictive permissions on a file containing sensitive data.
    /// </summary>
    private void SetRestrictiveFilePermissions(string filePath)
    {
        // On Unix systems, restrict file permissions to owner read/write only (600)
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                _logger.LogDebug("Set secure permissions (600) on file {Path}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set Unix file permissions on {Path}", filePath);
            }
        }
    }

    /// <summary>
    /// Securely deletes a file by overwriting it with random data before deletion.
    /// </summary>
    private void SecureDeleteFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 0)
            {
                // Overwrite with random data before deletion
                var randomData = new byte[fileInfo.Length];
                RandomNumberGenerator.Fill(randomData);
                File.WriteAllBytes(filePath, randomData);
            }

            File.Delete(filePath);
            _logger.LogDebug("Securely deleted file: {Path}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to securely delete file {Path}", filePath);
        }
    }

    /// <summary>
    /// Cleans up temporary files containing sensitive data.
    /// </summary>
    private void CleanupTempFiles(List<string> tempFiles)
    {
        if (tempFiles == null || !tempFiles.Any())
            return;

        _logger.LogInformation("Cleaning up {Count} temporary configuration files", tempFiles.Count);

        foreach (var file in tempFiles)
        {
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                SecureDeleteFile(file);
            }
        }

        // Also try to clean up the secure temp directory if empty
        try
        {
            var secureTempDir = Path.Combine(Path.GetTempPath(), "honua-observability", _state.DeploymentId);
            if (Directory.Exists(secureTempDir))
            {
                var remainingFiles = Directory.GetFiles(secureTempDir);
                if (remainingFiles.Length == 0)
                {
                    Directory.Delete(secureTempDir);
                    _logger.LogInformation("Cleaned up temporary directory: {Path}", secureTempDir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temporary directory");
        }
    }
}
