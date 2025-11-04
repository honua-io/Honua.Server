// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using CertificateRenewalState = Honua.Cli.AI.Services.Processes.State.CertificateRenewalState;
using Honua.Cli.AI.Services.Processes.State;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;

/// <summary>
/// Scans for existing certificates in load balancers, Kubernetes, and other deployment targets.
/// Identifies certificates that may need renewal based on the check window.
/// </summary>
public class ScanCertificatesStep : KernelProcessStep<CertificateRenewalState>
{
    private readonly ILogger<ScanCertificatesStep> _logger;
    private CertificateRenewalState _state = new();

    public ScanCertificatesStep(ILogger<ScanCertificatesStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<CertificateRenewalState> state)
    {
        _state = state.State ?? new CertificateRenewalState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ScanCertificates")]
    public async Task ScanCertificatesAsync(
        KernelProcessStepContext context,
        CertificateRenewalRequest request)
    {
        _logger.LogInformation("Scanning certificates for domains: {Domains} with check window of {Days} days",
            string.Join(", ", request.DomainNames), request.CheckWindowDays);

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // Initialize state
                    _state.RenewalId = Guid.NewGuid().ToString();
                    _state.DomainNames = request.DomainNames;
                    _state.CertificateProvider = request.CertificateProvider;
                    _state.ValidationMethod = request.ValidationMethod;
                    _state.CheckWindowDays = request.CheckWindowDays;
                    _state.RenewalStartTime = DateTime.UtcNow;
                    _state.Status = "Scanning";

                    var expiringCerts = new List<CertificateInfo>();

                    // Scan for certificates from multiple sources
                    foreach (var domain in request.DomainNames)
                    {
                        // Skip wildcard domains for scanning (they can't be directly connected to)
                        if (domain.StartsWith("*."))
                        {
                            _logger.LogInformation("Skipping wildcard domain {Domain} in certificate scan", domain);
                            continue;
                        }

                        _logger.LogInformation("Scanning certificate for domain: {Domain}", domain);

                        try
                        {
                            // Scan from deployed endpoint
                            var endpointCert = await ScanCertificateFromEndpointAsync(domain, 443);
                            if (endpointCert != null)
                            {
                                var daysUntilExpiry = (endpointCert.ExpiryDate - DateTime.UtcNow).TotalDays;

                                if (daysUntilExpiry <= request.CheckWindowDays)
                                {
                                    _logger.LogInformation("Found expiring certificate for {Domain}: expires in {Days} days",
                                        domain, (int)daysUntilExpiry);
                                    expiringCerts.Add(endpointCert);
                                }
                                else
                                {
                                    _logger.LogInformation("Certificate for {Domain} is valid for {Days} more days, no renewal needed",
                                        domain, (int)daysUntilExpiry);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Could not retrieve certificate for {Domain} from endpoint", domain);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to scan certificate for {Domain}", domain);
                        }

                        // Also check local file system storage
                        try
                        {
                            var localCert = await ScanCertificateFromFileSystemAsync(domain);
                            if (localCert != null)
                            {
                                var daysUntilExpiry = (localCert.ExpiryDate - DateTime.UtcNow).TotalDays;

                                // Only add if not already found from endpoint and is expiring
                                if (!expiringCerts.Any(c => c.Domain == domain) && daysUntilExpiry <= request.CheckWindowDays)
                                {
                                    _logger.LogInformation("Found expiring local certificate for {Domain}: expires in {Days} days",
                                        domain, (int)daysUntilExpiry);
                                    expiringCerts.Add(localCert);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "No local certificate found for {Domain}", domain);
                        }
                    }

                    _state.ExpiringCertificates = expiringCerts;

                    if (expiringCerts.Count == 0)
                    {
                        _logger.LogInformation("No certificates expiring within {Days} days", request.CheckWindowDays);
                        await context.EmitEventAsync(new KernelProcessEvent
                        {
                            Id = "NoCertificatesExpiring",
                            Data = _state
                        });
                        return;
                    }

                    _logger.LogInformation("Found {Count} certificates expiring within {Days} days",
                        expiringCerts.Count, request.CheckWindowDays);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "ExpiringCertificatesFound",
                        Data = _state
                    });
                },
                _logger,
                "ScanCertificates");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan certificates after retries");
            _state.Status = "ScanFailed";
            _state.ErrorMessage = ex.Message;
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ScanFailed",
                Data = new { Error = ex.Message }
            });
        }
    }

    private async Task<CertificateInfo?> ScanCertificateFromEndpointAsync(string hostname, int port)
    {
        try
        {
            using var client = new TcpClient();

            // Set connection timeout
            var connectTask = client.ConnectAsync(hostname, port);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

            if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
            {
                _logger.LogWarning("Connection timeout for {Hostname}:{Port}", hostname, port);
                return null;
            }

            await connectTask;

            X509Certificate2? certificate = null;

#pragma warning disable CA5359 // Intentionally accepting all certificates for scanning purposes
            using var sslStream = new SslStream(
                client.GetStream(),
                false,
                (sender, cert, chain, errors) =>
                {
                    if (cert != null)
                    {
                        certificate = new X509Certificate2(cert);
                    }
                    return true; // Accept all certificates for scanning purposes
                });
#pragma warning restore CA5359

            await sslStream.AuthenticateAsClientAsync(hostname);

            if (certificate == null)
            {
                return null;
            }

            // Extract Subject Alternative Names
            var sanList = new List<string>();
            foreach (var extension in certificate.Extensions)
            {
                if (extension.Oid?.Value == "2.5.29.17")
                {
                    var asnData = new System.Security.Cryptography.AsnEncodedData(extension.Oid, extension.RawData);
                    var sanString = asnData.Format(false);
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

            if (sanList.Count == 0)
            {
                sanList.Add(hostname);
            }

            // Determine key size safely
            int keySize = 0;
            try
            {
                using var rsa = certificate.GetRSAPublicKey();
                if (rsa != null)
                {
                    keySize = rsa.KeySize;
                }
                else
                {
                    using var ecdsa = certificate.GetECDsaPublicKey();
                    if (ecdsa != null)
                    {
                        keySize = ecdsa.KeySize;
                    }
                }
            }
            catch
            {
                keySize = 0;
            }

            return new CertificateInfo
            {
                Domain = hostname,
                Thumbprint = certificate.Thumbprint,
                ExpiryDate = certificate.NotAfter.ToUniversalTime(),
                IssueDate = certificate.NotBefore.ToUniversalTime(),
                Issuer = certificate.Issuer,
                SubjectAlternativeNames = sanList,
                KeyType = certificate.PublicKey.Oid.FriendlyName ?? "Unknown",
                KeySize = keySize,
                CertificatePath = $"endpoint://{hostname}:{port}",
                IsWildcard = sanList.Any(s => s.StartsWith("*."))
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to scan certificate from endpoint {Hostname}:{Port}", hostname, port);
            return null;
        }
    }

    private async Task<CertificateInfo?> ScanCertificateFromFileSystemAsync(string domain)
    {
        // Check multiple common certificate storage locations
        var possiblePaths = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".honua", "certificates", $"{domain}.pem"),
            Path.Combine("/etc/honua/certificates", $"{domain}.pem"),
            Path.Combine("/etc/ssl/certs", $"{domain}.pem"),
            Path.Combine("/etc/letsencrypt/live", domain, "cert.pem")
        };

        if (OperatingSystem.IsWindows())
        {
            possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Honua", "certificates", $"{domain}.pem"));
        }

        foreach (var certPath in possiblePaths)
        {
            if (File.Exists(certPath))
            {
                try
                {
                    var certBytes = await File.ReadAllBytesAsync(certPath);
                    var certificate = X509CertificateLoader.LoadCertificate(certBytes);

                    // Extract SANs
                    var sanList = new List<string>();
                    foreach (var extension in certificate.Extensions)
                    {
                        if (extension.Oid?.Value == "2.5.29.17")
                        {
                            var asnData = new System.Security.Cryptography.AsnEncodedData(extension.Oid, extension.RawData);
                            var sanString = asnData.Format(false);
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

                    if (sanList.Count == 0)
                    {
                        sanList.Add(domain);
                    }

                    _logger.LogInformation("Found local certificate at {Path}", certPath);

                    // Determine key size safely
                    int keySize = 0;
                    try
                    {
                        using var rsa = certificate.GetRSAPublicKey();
                        if (rsa != null)
                        {
                            keySize = rsa.KeySize;
                        }
                        else
                        {
                            using var ecdsa = certificate.GetECDsaPublicKey();
                            if (ecdsa != null)
                            {
                                keySize = ecdsa.KeySize;
                            }
                        }
                    }
                    catch
                    {
                        keySize = 0;
                    }

                    return new CertificateInfo
                    {
                        Domain = domain,
                        Thumbprint = certificate.Thumbprint,
                        ExpiryDate = certificate.NotAfter.ToUniversalTime(),
                        IssueDate = certificate.NotBefore.ToUniversalTime(),
                        Issuer = certificate.Issuer,
                        SubjectAlternativeNames = sanList,
                        KeyType = certificate.PublicKey.Oid.FriendlyName ?? "Unknown",
                        KeySize = keySize,
                        CertificatePath = certPath,
                        IsWildcard = sanList.Any(s => s.StartsWith("*."))
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to load certificate from {Path}", certPath);
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Request object for certificate renewal scan.
/// </summary>
public record CertificateRenewalRequest(
    List<string> DomainNames,
    string CertificateProvider = "LetsEncrypt",
    string ValidationMethod = "DNS-01",
    int CheckWindowDays = 30);
