// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using CertificateRenewalState = Honua.Cli.AI.Services.Processes.State.CertificateRenewalState;
using Honua.Cli.AI.Services.Processes.State;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using System.Security.Cryptography.X509Certificates;

namespace Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;

/// <summary>
/// Obtains the issued certificate from ACME provider after successful validation.
/// Downloads certificate and full chain, verifies validity.
/// </summary>
public class IssueCertificateStep : KernelProcessStep<CertificateRenewalState>
{
    private readonly ILogger<IssueCertificateStep> _logger;
    private CertificateRenewalState _state = new();
    private static readonly Uri LetsEncryptStagingV2 = WellKnownServers.LetsEncryptStagingV2;
    private static readonly Uri LetsEncryptV2 = WellKnownServers.LetsEncryptV2;

    public IssueCertificateStep(ILogger<IssueCertificateStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<CertificateRenewalState> state)
    {
        _state = state.State ?? new CertificateRenewalState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ObtainCertificate")]
    public async Task ObtainCertificateAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Obtaining certificates from {Provider} for {Count} domains",
            _state.CertificateProvider, _state.DomainNames.Count);

        _state.Status = "Issuing Certificate";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // Retrieve the order URL from state
                    if (!_state.DnsChallengeRecords.TryGetValue("_order_url", out var orderUrlString))
                    {
                        throw new InvalidOperationException("Order URL not found in state. Certificate request step may have failed.");
                    }

                    // Determine ACME server
                    var isProduction = _state.CertificateProvider.Equals("LetsEncrypt", StringComparison.OrdinalIgnoreCase) ||
                                       _state.CertificateProvider.Equals("LetsEncryptProduction", StringComparison.OrdinalIgnoreCase);
                    var acmeServer = isProduction ? LetsEncryptV2 : LetsEncryptStagingV2;

                    // Load account key
                    var accountKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".honua", "acme-account.pem");
                    var accountKey = await LoadAccountKeyAsync(accountKeyPath);

                    // Create ACME context
                    var acme = new AcmeContext(acmeServer, accountKey);

                    // Load the order
                    var order = acme.Order(new Uri(orderUrlString));

                    _logger.LogInformation("Finalizing order and requesting certificate issuance");

                    // Generate certificate private key (RSA 2048 or EC)
                    var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);

                    // Finalize the order with CSR
                    await order.Finalize(new CsrInfo
                    {
                        CommonName = _state.DomainNames.First()
                    }, privateKey);

                    // Wait for certificate to be ready
                    int attempts = 0;
                    const int maxAttempts = 30;
                    while (attempts < maxAttempts)
                    {
                        var orderResource = await order.Resource();

                        if (orderResource.Status == OrderStatus.Valid)
                        {
                            _logger.LogInformation("Certificate ready for download");
                            break;
                        }

                        if (orderResource.Status == OrderStatus.Invalid)
                        {
                            throw new InvalidOperationException("Order became invalid during finalization");
                        }

                        attempts++;
                        await Task.Delay(2000);
                    }

                    if (attempts >= maxAttempts)
                    {
                        throw new TimeoutException("Certificate finalization timed out");
                    }

                    // Download certificate chain
                    var certificateChain = await order.Download();
                    var certificatePem = certificateChain.ToPem();
                    var privateKeyPem = privateKey.ToPem();

                    _logger.LogInformation("Certificate downloaded successfully");

                    // Parse certificate to extract metadata
                    var certBytes = System.Text.Encoding.UTF8.GetBytes(certificatePem);
                    using var x509 = X509CertificateLoader.LoadCertificate(certBytes);

                    var primaryDomain = _state.DomainNames.First();
                    var certificatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".honua", "certificates", $"{primaryDomain}.pem");
                    var privateKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".honua", "certificates", $"{primaryDomain}.key");

                    // Ensure certificate directory exists
                    var certDir = Path.GetDirectoryName(certificatePath);
                    if (!string.IsNullOrEmpty(certDir) && !System.IO.Directory.Exists(certDir))
                    {
                        System.IO.Directory.CreateDirectory(certDir);
                    }

                    // Save certificate and private key
                    await File.WriteAllTextAsync(certificatePath, certificatePem);
                    await File.WriteAllTextAsync(privateKeyPath, privateKeyPem);

                    // Set restrictive permissions on Unix systems
                    if (!OperatingSystem.IsWindows())
                    {
                        File.SetUnixFileMode(certificatePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                        File.SetUnixFileMode(privateKeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    }

                    _logger.LogInformation("Certificate saved to {CertPath}", certificatePath);

                    // Create certificate info for state
                    var newCert = new CertificateInfo
                    {
                        Domain = primaryDomain,
                        Thumbprint = x509.Thumbprint,
                        IssueDate = x509.NotBefore.ToUniversalTime(),
                        ExpiryDate = x509.NotAfter.ToUniversalTime(),
                        Issuer = x509.Issuer,
                        SubjectAlternativeNames = _state.DomainNames,
                        KeyType = "ECDSA",
                        KeySize = 256,
                        CertificatePath = certificatePath,
                        IsWildcard = _state.DomainNames.Any(d => d.StartsWith("*."))
                    };

                    _state.NewCertificates = new List<CertificateInfo> { newCert };

                    foreach (var domain in _state.DomainNames)
                    {
                        _state.NewExpiryDates[domain] = newCert.ExpiryDate;
                        _state.RenewedDomains.Add(domain);
                    }

                    _logger.LogInformation("Certificate obtained for {Domains}, expires {ExpiryDate}",
                        string.Join(", ", _state.DomainNames), newCert.ExpiryDate);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "CertificateObtained",
                        Data = _state
                    });
                },
                _logger,
                "ObtainCertificate",
                maxRetries: 5,
                initialDelayMs: 2000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain certificate after retries");
            _state.Status = "Certificate Issuance Failed";
            _state.ErrorMessage = ex.Message;
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "CertificateIssuanceFailed",
                Data = new { Error = ex.Message }
            });
        }
    }

    private async Task<IKey> LoadAccountKeyAsync(string accountKeyPath)
    {
        if (!File.Exists(accountKeyPath))
        {
            throw new FileNotFoundException($"ACME account key not found at {accountKeyPath}. Run RequestRenewal step first.");
        }

        var pemKey = await File.ReadAllTextAsync(accountKeyPath);
        return KeyFactory.FromPem(pemKey);
    }
}
