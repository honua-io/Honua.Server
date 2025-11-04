// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using CertificateRenewalState = Honua.Cli.AI.Services.Processes.State.CertificateRenewalState;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;

/// <summary>
/// Validates certificate deployment by testing HTTPS endpoints.
/// Verifies certificate chain, expiry date, and correct SSL/TLS configuration.
/// </summary>
public class ValidateCertificateDeploymentStep : KernelProcessStep<CertificateRenewalState>
{
    private readonly ILogger<ValidateCertificateDeploymentStep> _logger;
    private CertificateRenewalState _state = new();

    public ValidateCertificateDeploymentStep(ILogger<ValidateCertificateDeploymentStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<CertificateRenewalState> state)
    {
        _state = state.State ?? new CertificateRenewalState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ValidateDeployment")]
    public async Task ValidateDeploymentAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Validating certificate deployment for {Count} domains",
            _state.DomainNames.Count);

        _state.Status = "Validating Deployment";

        var validationErrors = new List<string>();

        foreach (var domain in _state.DomainNames)
        {
            // Skip wildcard domains (can't directly validate *.example.com)
            if (domain.StartsWith("*."))
            {
                _logger.LogInformation("Skipping validation for wildcard domain {Domain} (requires subdomain test)", domain);
                continue;
            }

            _logger.LogInformation("Testing HTTPS endpoint for {Domain}", domain);

            try
            {
                // Validate TLS certificate by establishing connection
                var validationResult = await ValidateTlsCertificateAsync(domain, 443);

                if (!validationResult.IsValid)
                {
                    validationErrors.Add($"{domain}: {validationResult.ErrorMessage}");
                    _logger.LogError("Certificate validation failed for {Domain}: {Error}",
                        domain, validationResult.ErrorMessage);
                }
                else
                {
                    _logger.LogInformation("Certificate validation passed for {Domain}. " +
                        "Issuer: {Issuer}, Expires: {Expiry}",
                        domain, validationResult.Issuer, validationResult.ExpiryDate);
                }
            }
            catch (Exception ex)
            {
                var error = $"Validation failed for {domain}: {ex.Message}";
                validationErrors.Add(error);
                _logger.LogError(ex, "Certificate validation failed for {Domain}", domain);
            }
        }

        if (validationErrors.Any())
        {
            _state.Status = "Validation Failed";
            _state.ErrorMessage = string.Join("; ", validationErrors);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ValidationFailed",
                Data = _state
            });
            return;
        }

        _logger.LogInformation("Certificate deployment validated successfully for all domains");

        await context.EmitEventAsync(new KernelProcessEvent
        {
            Id = "ValidationPassed",
            Data = _state
        });
    }

    private async Task<TlsValidationResult> ValidateTlsCertificateAsync(string hostname, int port)
    {
        var result = new TlsValidationResult { IsValid = false };

        try
        {
            using var client = new TcpClient();

            // Set connection timeout
            var connectTask = client.ConnectAsync(hostname, port);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

            if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
            {
                result.ErrorMessage = "Connection timeout";
                return result;
            }

            await connectTask;

#pragma warning disable CA5359 // Intentionally accepting certificates to inspect validation errors
            using var sslStream = new SslStream(
                client.GetStream(),
                false,
                (sender, certificate, chain, errors) =>
                {
                    // Store certificate info for validation
                    if (certificate != null)
                    {
                        var cert = new X509Certificate2(certificate);
                        result.Issuer = cert.Issuer;
                        result.ExpiryDate = cert.NotAfter;
                        result.Subject = cert.Subject;

                        // Check if certificate matches hostname
                        var sans = GetSubjectAlternativeNames(cert);
                        result.MatchesHostname = sans.Contains(hostname) ||
                                                 cert.Subject.Contains($"CN={hostname}");
                    }

                    // Allow validation to complete even with errors so we can report them
                    result.SslPolicyErrors = errors;
                    return true; // Continue to get certificate info
                });
#pragma warning restore CA5359

            // Attempt TLS handshake
            await sslStream.AuthenticateAsClientAsync(hostname);

            // Get the remote certificate
            var remoteCert = sslStream.RemoteCertificate;
            if (remoteCert == null)
            {
                result.ErrorMessage = "No certificate presented by server";
                return result;
            }

            var x509Cert = new X509Certificate2(remoteCert);

            // Validate expiry
            if (x509Cert.NotAfter < DateTime.UtcNow)
            {
                result.ErrorMessage = $"Certificate has expired on {x509Cert.NotAfter:yyyy-MM-dd}";
                return result;
            }

            if (x509Cert.NotBefore > DateTime.UtcNow)
            {
                result.ErrorMessage = $"Certificate is not yet valid (valid from {x509Cert.NotBefore:yyyy-MM-dd})";
                return result;
            }

            // Check if hostname matches
            if (!result.MatchesHostname)
            {
                result.ErrorMessage = $"Certificate does not match hostname {hostname}";
                return result;
            }

            // Check SSL policy errors
            if (result.SslPolicyErrors != SslPolicyErrors.None)
            {
                result.ErrorMessage = $"SSL policy errors: {result.SslPolicyErrors}";
                // If only RemoteCertificateNameMismatch but we already validated SANs, ignore it
                if (result.SslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch && result.MatchesHostname)
                {
                    result.ErrorMessage = null;
                }
                else if (result.ErrorMessage != null)
                {
                    return result;
                }
            }

            // All validations passed
            result.IsValid = true;
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private List<string> GetSubjectAlternativeNames(X509Certificate2 certificate)
    {
        var sanList = new List<string>();

        foreach (var extension in certificate.Extensions)
        {
            if (extension.Oid?.Value == "2.5.29.17") // Subject Alternative Name OID
            {
                var asnData = new System.Security.Cryptography.AsnEncodedData(extension.Oid, extension.RawData);
                var sanString = asnData.Format(false);

                // Parse the SAN string (format: "DNS Name=example.com, DNS Name=www.example.com")
                var parts = sanString.Split(',');
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (trimmed.StartsWith("DNS Name="))
                    {
                        sanList.Add(trimmed.Substring("DNS Name=".Length));
                    }
                }
            }
        }

        return sanList;
    }

    private class TlsValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string Issuer { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public string Subject { get; set; } = string.Empty;
        public bool MatchesHostname { get; set; }
        public SslPolicyErrors SslPolicyErrors { get; set; }
    }
}
