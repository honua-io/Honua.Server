// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NetworkDiagnosticsState = Honua.Cli.AI.Services.Processes.State.NetworkDiagnosticsState;
using Honua.Cli.AI.Services.Processes.State;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;

/// <summary>
/// Tests SSL/TLS certificate validity for HTTPS endpoints.
/// Verifies certificate is valid, not expired, and matches the domain.
/// </summary>
public class CheckCertificateStep : KernelProcessStep<NetworkDiagnosticsState>, IProcessStepTimeout
{
    private readonly ILogger<CheckCertificateStep> _logger;
    private NetworkDiagnosticsState _state = new();

    /// <summary>
    /// Certificate validation includes SSL handshake and chain verification.
    /// Default timeout: 2 minutes
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(2);

    public CheckCertificateStep(ILogger<CheckCertificateStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<NetworkDiagnosticsState> state)
    {
        _state = state.State ?? new NetworkDiagnosticsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("CheckCertificate")]
    public async Task CheckCertificateAsync(KernelProcessStepContext context)
    {
        var port = _state.TargetPort ?? 443;

        // Only check SSL for HTTPS ports
        if (port != 443 && port != 8443)
        {
            _logger.LogInformation("Skipping SSL check for non-HTTPS port {Port}", port);
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "SSLTestComplete",
                Data = _state
            });
            return;
        }

        _logger.LogInformation("Checking SSL certificate for {Host}:{Port}", _state.TargetHost, port);
        _state.Status = "Checking SSL Certificate";

        var sslResult = new SslTestResult();

        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(_state.TargetHost, port);

            using var sslStream = new SslStream(
                tcpClient.GetStream(),
                false,
                (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // Capture certificate details even if validation fails
                    if (certificate != null)
                    {
                        var cert = new X509Certificate2(certificate);
                        sslResult.ExpiryDate = cert.NotAfter;
                        sslResult.Issuer = cert.Issuer;
                        sslResult.CertificateValid = sslPolicyErrors == SslPolicyErrors.None;
                        sslResult.ChainValid = (sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) == 0;
                        sslResult.HostnameMatches = (sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) == 0;

                        // Extract SANs
                        foreach (var extension in cert.Extensions)
                        {
                            if (extension is X509SubjectAlternativeNameExtension sanExtension)
                            {
                                sslResult.SubjectAlternativeNames.AddRange(sanExtension.EnumerateDnsNames());
                            }
                        }

                        // Record validation errors
                        if (sslPolicyErrors != SslPolicyErrors.None)
                        {
                            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
                            {
                                sslResult.ValidationErrors.Add("Certificate chain validation failed");
                            }
                            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
                            {
                                sslResult.ValidationErrors.Add("Certificate hostname does not match");
                            }
                            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
                            {
                                sslResult.ValidationErrors.Add("Certificate not available");
                            }
                        }
                    }

                    return sslPolicyErrors == SslPolicyErrors.None;
                });

            // Authenticate and capture protocol details
            await sslStream.AuthenticateAsClientAsync(_state.TargetHost);

            sslResult.Protocol = sslStream.SslProtocol.ToString();
            sslResult.CipherSuite = sslStream.CipherAlgorithm.ToString();

            _logger.LogInformation("SSL certificate retrieved successfully for {Host}", _state.TargetHost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check SSL certificate for {Host}:{Port}", _state.TargetHost, port);
            sslResult.CertificateValid = false;
            sslResult.ValidationErrors.Add($"Connection failed: {ex.Message}");
        }

        _state.SslTestResult = sslResult;

        // Record diagnostic test
        _state.TestsRun.Add(new DiagnosticTest
        {
            TestName = "SSL Certificate Validation",
            TestType = "SSL",
            Timestamp = DateTime.UtcNow,
            Success = sslResult.CertificateValid,
            Output = sslResult.CertificateValid
                ? $"Certificate valid, expires {sslResult.ExpiryDate:yyyy-MM-dd}"
                : "Certificate validation failed",
            ErrorMessage = sslResult.CertificateValid ? null : string.Join(", ", sslResult.ValidationErrors)
        });

        if (!sslResult.CertificateValid || !sslResult.ChainValid || !sslResult.HostnameMatches)
        {
            _logger.LogWarning("SSL certificate validation failed for {Host}", _state.TargetHost);

            var issues = new List<string>();
            if (!sslResult.CertificateValid) issues.Add("Certificate is not valid");
            if (!sslResult.ChainValid) issues.Add("Certificate chain is invalid");
            if (!sslResult.HostnameMatches) issues.Add("Certificate hostname does not match");

            _state.Findings.Add(new Finding
            {
                Category = "SSL",
                Severity = "Critical",
                Description = $"SSL certificate validation failed: {string.Join(", ", issues)}",
                Recommendation = "Renew or replace the SSL certificate, ensure it matches the domain, and verify the certificate chain",
                Evidence = sslResult.ValidationErrors
            });

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "SSLFailure",
                Data = _state
            });
            return;
        }

        // Check if certificate is expiring soon (within 30 days)
        var daysUntilExpiry = (sslResult.ExpiryDate.GetValueOrDefault() - DateTime.UtcNow).Days;
        if (daysUntilExpiry < 30)
        {
            _state.Findings.Add(new Finding
            {
                Category = "SSL",
                Severity = "Medium",
                Description = $"SSL certificate expires in {daysUntilExpiry} days",
                Recommendation = "Renew SSL certificate soon to avoid service disruption",
                Evidence = new List<string> { $"Expiry date: {sslResult.ExpiryDate:yyyy-MM-dd}" }
            });
        }

        _logger.LogInformation("SSL certificate validation passed for {Host}", _state.TargetHost);

        await context.EmitEventAsync(new KernelProcessEvent
        {
            Id = "SSLTestComplete",
            Data = _state
        });
    }
}
