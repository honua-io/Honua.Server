// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using CertificateRenewalState = Honua.Cli.AI.Services.Processes.State.CertificateRenewalState;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;

namespace Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;

/// <summary>
/// Requests new certificate from ACME provider (Let's Encrypt, ZeroSSL, etc.).
/// Creates ACME order and receives challenge tokens.
/// </summary>
public class RequestRenewalStep : KernelProcessStep<CertificateRenewalState>
{
    private readonly ILogger<RequestRenewalStep> _logger;
    private CertificateRenewalState _state = new();
    private static readonly Uri LetsEncryptStagingV2 = WellKnownServers.LetsEncryptStagingV2;
    private static readonly Uri LetsEncryptV2 = WellKnownServers.LetsEncryptV2;

    public RequestRenewalStep(ILogger<RequestRenewalStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<CertificateRenewalState> state)
    {
        _state = state.State ?? new CertificateRenewalState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("RequestCertificate")]
    public async Task RequestCertificateAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Requesting new certificate from {Provider} for domains: {Domains}",
            _state.CertificateProvider, string.Join(", ", _state.DomainNames));

        _state.Status = "Requesting Certificate";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // Determine if we're using production or staging Let's Encrypt
                    var isProduction = _state.CertificateProvider.Equals("LetsEncrypt", StringComparison.OrdinalIgnoreCase) ||
                                       _state.CertificateProvider.Equals("LetsEncryptProduction", StringComparison.OrdinalIgnoreCase);
                    var acmeServer = isProduction ? LetsEncryptV2 : LetsEncryptStagingV2;

                    _logger.LogInformation("Using ACME server: {Server}", isProduction ? "Production" : "Staging");

                    // Load or create ACME account key
                    var accountKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".honua", "acme-account.pem");
                    var accountKey = await LoadOrCreateAccountKeyAsync(accountKeyPath);

                    // Create ACME context
                    var acme = new AcmeContext(acmeServer, accountKey);

                    // Create or retrieve account (email should be stored in state or configuration)
                    var email = Environment.GetEnvironmentVariable("ACME_EMAIL") ?? "admin@honua.io";
                    var account = await acme.NewAccount(email, termsOfServiceAgreed: true);
                    _logger.LogInformation("ACME account ready: {AccountId}", account.Location);

                    // Create ACME order for all domains
                    var order = await acme.NewOrder(_state.DomainNames);
                    _logger.LogInformation("Created ACME order: {OrderUrl}", order.Location);

                    // Store order URL for later use
                    _state.DnsChallengeRecords["_order_url"] = order.Location.ToString();

                    // Get authorizations and extract challenge tokens
                    var authorizations = await order.Authorizations();

                    foreach (var auth in authorizations)
                    {
                        var authz = await auth.Resource();
                        var domain = authz.Identifier.Value;

                        _logger.LogInformation("Processing authorization for: {Domain}", domain);

                        // Get the appropriate challenge based on validation method
                        IChallengeContext challenge;
                        if (_state.ValidationMethod.Equals("DNS-01", StringComparison.OrdinalIgnoreCase))
                        {
                            challenge = await auth.Dns();
                        }
                        else if (_state.ValidationMethod.Equals("HTTP-01", StringComparison.OrdinalIgnoreCase))
                        {
                            challenge = await auth.Http();
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unsupported validation method: {_state.ValidationMethod}");
                        }

                        // Get challenge details
                        var challengeDetails = await challenge.Resource();
                        var token = challengeDetails.Token;
                        var keyAuthz = challenge.KeyAuthz;

                        // Store the challenge value and token for later use
                        _state.DnsChallengeRecords[domain] = keyAuthz;
                        _state.DnsChallengeRecords[$"{domain}_token"] = token;
                        _state.DnsChallengeRecords[$"{domain}_challengeType"] = _state.ValidationMethod;

                        _logger.LogInformation("Generated {Method} challenge for {Domain}: {Token}",
                            _state.ValidationMethod, domain, token);
                    }

                    _logger.LogInformation("Certificate requested successfully from {Provider}", _state.CertificateProvider);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "CertificateRequested",
                        Data = _state
                    });
                },
                _logger,
                "RequestCertificate");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request certificate after retries");
            _state.Status = "Certificate Request Failed";
            _state.ErrorMessage = ex.Message;
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "CertificateRequestFailed",
                Data = new { Error = ex.Message }
            });
        }
    }

    private async Task<IKey> LoadOrCreateAccountKeyAsync(string accountKeyPath)
    {
        if (File.Exists(accountKeyPath))
        {
            _logger.LogInformation("Loading existing ACME account key from {Path}", accountKeyPath);
            var existingPemKey = await File.ReadAllTextAsync(accountKeyPath);
            return KeyFactory.FromPem(existingPemKey);
        }

        _logger.LogInformation("Creating new ACME account key at {Path}", accountKeyPath);
        var key = KeyFactory.NewKey(KeyAlgorithm.ES256);
        var pemKey = key.ToPem();

        var directory = Path.GetDirectoryName(accountKeyPath);
        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(accountKeyPath, pemKey);

        // Set restrictive permissions on Unix systems
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(accountKeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return key;
    }
}
