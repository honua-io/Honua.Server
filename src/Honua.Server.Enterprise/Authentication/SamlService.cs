// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Enterprise.Authentication;

/// <summary>
/// Implementation of SAML 2.0 service provider operations
/// </summary>
public class SamlService : ISamlService
{
    private readonly ISamlIdentityProviderStore _idpStore;
    private readonly ISamlSessionStore _sessionStore;
    private readonly SamlSsoOptions _options;
    private readonly ILogger<SamlService> _logger;

    // SAML namespaces
    private const string SamlpNamespace = "urn:oasis:names:tc:SAML:2.0:protocol";
    private const string SamlNamespace = "urn:oasis:names:tc:SAML:2.0:assertion";
    private const string XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";

    public SamlService(
        ISamlIdentityProviderStore idpStore,
        ISamlSessionStore sessionStore,
        IOptions<SamlSsoOptions> options,
        ILogger<SamlService> logger)
    {
        _idpStore = idpStore ?? throw new ArgumentNullException(nameof(idpStore));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SamlAuthenticationRequest> CreateAuthenticationRequestAsync(
        Guid tenantId,
        string? relayState = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating SAML authentication request for tenant {TenantId}", tenantId);

        // Get IdP configuration for tenant
        var idpConfig = await _idpStore.GetEnabledByTenantIdAsync(tenantId, cancellationToken);
        if (idpConfig == null)
        {
            throw new InvalidOperationException($"No enabled SAML IdP configuration found for tenant {tenantId}");
        }

        // Generate request ID
        var requestId = $"_{Guid.NewGuid():N}";
        var issueInstant = DateTimeOffset.UtcNow;

        // Build AuthnRequest XML
        var authnRequest = new XElement(
            XName.Get("AuthnRequest", SamlpNamespace),
            new XAttribute("ID", requestId),
            new XAttribute("Version", "2.0"),
            new XAttribute("IssueInstant", issueInstant.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")),
            new XAttribute("Destination", idpConfig.SingleSignOnServiceUrl),
            new XAttribute("ProtocolBinding", "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"),
            new XAttribute("AssertionConsumerServiceURL", GetAssertionConsumerServiceUrl()),
            new XElement(
                XName.Get("Issuer", SamlNamespace),
                _options.ServiceProvider.EntityId
            ),
            new XElement(
                XName.Get("NameIDPolicy", SamlpNamespace),
                new XAttribute("Format", idpConfig.NameIdFormat),
                new XAttribute("AllowCreate", "true")
            )
        );

        // Sign request if required
        if (idpConfig.SignAuthenticationRequests && !_options.ServiceProvider.SigningCertificate.IsNullOrEmpty())
        {
            authnRequest = SignXmlDocument(authnRequest);
        }

        // Convert to string
        var authnRequestXml = authnRequest.ToString(SaveOptions.DisableFormatting);

        if (_options.EnableDebugLogging)
        {
            _logger.LogDebug("SAML AuthnRequest: {AuthnRequestXml}", authnRequestXml);
        }

        // Encode for transmission
        var samlRequestEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(authnRequestXml));

        // Create session
        var validityPeriod = TimeSpan.FromMinutes(_options.ServiceProvider.AuthnRequestValidityMinutes);
        await _sessionStore.CreateSessionAsync(
            tenantId,
            idpConfig.Id,
            requestId,
            relayState,
            validityPeriod,
            cancellationToken);

        // Build redirect URL based on binding type
        string redirectUrl;
        if (idpConfig.BindingType == SamlBindingType.HttpPost)
        {
            // For POST binding, we'll need to render an HTML form
            redirectUrl = idpConfig.SingleSignOnServiceUrl;
        }
        else
        {
            // HTTP-Redirect binding
            var queryString = $"SAMLRequest={Uri.EscapeDataString(samlRequestEncoded)}";
            if (!relayState.IsNullOrEmpty())
            {
                queryString += $"&RelayState={Uri.EscapeDataString(relayState)}";
            }
            redirectUrl = $"{idpConfig.SingleSignOnServiceUrl}?{queryString}";
        }

        _logger.LogInformation(
            "Created SAML authentication request {RequestId} for tenant {TenantId} to IdP {IdpName}",
            requestId, tenantId, idpConfig.Name);

        return new SamlAuthenticationRequest
        {
            RequestId = requestId,
            RedirectUrl = redirectUrl,
            SamlRequest = samlRequestEncoded,
            RelayState = relayState,
            BindingType = idpConfig.BindingType,
            ExpiresAt = issueInstant.Add(validityPeriod)
        };
    }

    public async Task<SamlAssertionResult> ValidateResponseAsync(
        string samlResponse,
        string? relayState = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating SAML response");

        try
        {
            // Decode SAML response
            var responseXml = Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse));

            if (_options.EnableDebugLogging)
            {
                _logger.LogDebug("SAML Response: {ResponseXml}", responseXml);
            }

            // Parse XML
            var doc = XDocument.Parse(responseXml);
            var response = doc.Root;

            if (response == null || response.Name != XName.Get("Response", SamlpNamespace))
            {
                return new SamlAssertionResult
                {
                    IsValid = false,
                    Errors = { "Invalid SAML response format" }
                };
            }

            // Extract InResponseTo to find session
            var inResponseTo = response.Attribute("InResponseTo")?.Value;
            if (inResponseTo.IsNullOrEmpty())
            {
                // IdP-initiated SSO
                _logger.LogWarning("Received IdP-initiated SAML response (no InResponseTo)");
                // We'll need to determine tenant from another attribute
            }

            // Get session and atomically consume it if we have InResponseTo
            SamlAuthenticationSession? session = null;
            if (!inResponseTo.IsNullOrEmpty())
            {
                session = await _sessionStore.GetSessionByRequestIdAsync(inResponseTo, cancellationToken);
                if (session == null)
                {
                    return new SamlAssertionResult
                    {
                        IsValid = false,
                        Errors = { "SAML session not found or expired" }
                    };
                }

                // Atomically consume the session to prevent replay attacks
                var consumed = await _sessionStore.TryConsumeSessionAsync(inResponseTo, cancellationToken);
                if (!consumed)
                {
                    _logger.LogWarning(
                        "SAML replay attack detected for request {RequestId} - session already consumed",
                        inResponseTo);
                    return new SamlAssertionResult
                    {
                        IsValid = false,
                        Errors = { "SAML response already consumed (replay attack detected)" }
                    };
                }
            }

            // Get IdP configuration
            SamlIdentityProviderConfiguration? idpConfig = null;
            if (session != null)
            {
                idpConfig = await _idpStore.GetByIdAsync(session.IdpConfigurationId, cancellationToken);
            }

            if (idpConfig == null)
            {
                return new SamlAssertionResult
                {
                    IsValid = false,
                    Errors = { "IdP configuration not found" }
                };
            }

            // Validate status
            var status = response.Element(XName.Get("Status", SamlpNamespace));
            var statusCode = status?.Element(XName.Get("StatusCode", SamlpNamespace))?.Attribute("Value")?.Value;

            if (statusCode != "urn:oasis:names:tc:SAML:2.0:status:Success")
            {
                var statusMessage = status?.Element(XName.Get("StatusMessage", SamlpNamespace))?.Value;
                return new SamlAssertionResult
                {
                    IsValid = false,
                    Errors = { $"SAML authentication failed: {statusMessage ?? statusCode}" }
                };
            }

            // Get assertion
            var assertion = response.Element(XName.Get("Assertion", SamlNamespace));
            if (assertion == null)
            {
                return new SamlAssertionResult
                {
                    IsValid = false,
                    Errors = { "No assertion found in SAML response" }
                };
            }

            // Verify signature if required
            if (idpConfig.WantAssertionsSigned)
            {
                var isSignatureValid = VerifyXmlSignature(assertion, idpConfig.SigningCertificate);
                if (!isSignatureValid)
                {
                    _logger.LogWarning("SAML assertion signature validation failed for IdP {IdpName}", idpConfig.Name);
                    return new SamlAssertionResult
                    {
                        IsValid = false,
                        Errors = { "SAML assertion signature validation failed" }
                    };
                }
            }

            // Validate conditions
            var conditions = assertion.Element(XName.Get("Conditions", SamlNamespace));
            if (conditions != null)
            {
                var validationResult = ValidateConditions(conditions);
                if (!validationResult.IsValid)
                {
                    return validationResult;
                }
            }

            // Extract NameID
            var subject = assertion.Element(XName.Get("Subject", SamlNamespace));
            var nameId = subject?.Element(XName.Get("NameID", SamlNamespace))?.Value;

            if (nameId.IsNullOrEmpty())
            {
                return new SamlAssertionResult
                {
                    IsValid = false,
                    Errors = { "NameID not found in assertion" }
                };
            }

            // Extract attributes
            var attributes = ExtractAttributes(assertion, idpConfig.AttributeMappings);

            // Extract session info
            var authnStatement = assertion.Element(XName.Get("AuthnStatement", SamlNamespace));
            var sessionIndex = authnStatement?.Attribute("SessionIndex")?.Value;
            var sessionNotOnOrAfter = authnStatement?.Attribute("SessionNotOnOrAfter")?.Value;

            _logger.LogInformation("Successfully validated SAML response for NameID {NameId}", nameId);

            return new SamlAssertionResult
            {
                IsValid = true,
                NameId = nameId,
                Attributes = attributes,
                SessionIndex = sessionIndex,
                SessionNotOnOrAfter = sessionNotOnOrAfter != null
                    ? DateTimeOffset.Parse(sessionNotOnOrAfter)
                    : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating SAML response");
            return new SamlAssertionResult
            {
                IsValid = false,
                Errors = { $"Validation error: {ex.Message}" }
            };
        }
    }

    public Task<string> GenerateServiceProviderMetadataAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating SP metadata for tenant {TenantId}", tenantId);

        var sp = _options.ServiceProvider;

        var metadata = new XElement(
            XName.Get("EntityDescriptor", "urn:oasis:names:tc:SAML:2.0:metadata"),
            new XAttribute("entityID", sp.EntityId),
            new XElement(
                XName.Get("SPSSODescriptor", "urn:oasis:names:tc:SAML:2.0:metadata"),
                new XAttribute("AuthnRequestsSigned", sp.SigningCertificate != null),
                new XAttribute("WantAssertionsSigned", "true"),
                new XAttribute("protocolSupportEnumeration", "urn:oasis:names:tc:SAML:2.0:protocol"),
                // NameID formats
                new XElement(XName.Get("NameIDFormat", "urn:oasis:names:tc:SAML:2.0:metadata"),
                    "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"),
                // Assertion Consumer Service
                new XElement(
                    XName.Get("AssertionConsumerService", "urn:oasis:names:tc:SAML:2.0:metadata"),
                    new XAttribute("Binding", "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"),
                    new XAttribute("Location", GetAssertionConsumerServiceUrl()),
                    new XAttribute("index", "0"),
                    new XAttribute("isDefault", "true")
                )
            )
        );

        // Add signing certificate if available
        if (!sp.SigningCertificate.IsNullOrEmpty())
        {
            var cert = LoadCertificate(sp.SigningCertificate);
            var certData = Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks);

            var keyDescriptor = new XElement(
                XName.Get("KeyDescriptor", "urn:oasis:names:tc:SAML:2.0:metadata"),
                new XAttribute("use", "signing"),
                new XElement(
                    XName.Get("KeyInfo", XmlDsigNamespace),
                    new XElement(
                        XName.Get("X509Data", XmlDsigNamespace),
                        new XElement(XName.Get("X509Certificate", XmlDsigNamespace), certData)
                    )
                )
            );

            metadata.Element(XName.Get("SPSSODescriptor", "urn:oasis:names:tc:SAML:2.0:metadata"))
                ?.AddFirst(keyDescriptor);
        }

        // Add organization info
        if (!sp.OrganizationName.IsNullOrEmpty())
        {
            var organization = new XElement(
                XName.Get("Organization", "urn:oasis:names:tc:SAML:2.0:metadata"),
                new XElement(XName.Get("OrganizationName", "urn:oasis:names:tc:SAML:2.0:metadata"),
                    new XAttribute(XNamespace.Xml + "lang", "en"), sp.OrganizationName),
                new XElement(XName.Get("OrganizationDisplayName", "urn:oasis:names:tc:SAML:2.0:metadata"),
                    new XAttribute(XNamespace.Xml + "lang", "en"), sp.OrganizationDisplayName ?? sp.OrganizationName),
                new XElement(XName.Get("OrganizationURL", "urn:oasis:names:tc:SAML:2.0:metadata"),
                    new XAttribute(XNamespace.Xml + "lang", "en"), sp.OrganizationUrl ?? sp.BaseUrl)
            );
            metadata.Add(organization);
        }

        return Task.FromResult(metadata.ToString(SaveOptions.None));
    }

    public Task<SamlIdentityProviderConfiguration> ImportIdpMetadataAsync(
        string metadataXml,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Importing IdP metadata");

        var doc = XDocument.Parse(metadataXml);
        var entityDescriptor = doc.Root;

        if (entityDescriptor == null)
        {
            throw new InvalidDataException("Invalid metadata XML");
        }

        var ns = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:metadata");

        var entityId = entityDescriptor.Attribute("entityID")?.Value;
        if (entityId.IsNullOrEmpty())
        {
            throw new InvalidDataException("EntityID not found in metadata");
        }

        var idpSsoDescriptor = entityDescriptor.Element(ns + "IDPSSODescriptor");
        if (idpSsoDescriptor == null)
        {
            throw new InvalidDataException("IDPSSODescriptor not found in metadata");
        }

        // Get SSO URL
        var ssoService = idpSsoDescriptor.Elements(ns + "SingleSignOnService")
            .FirstOrDefault(e => e.Attribute("Binding")?.Value == "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST");
        var ssoUrl = ssoService?.Attribute("Location")?.Value;

        if (ssoUrl.IsNullOrEmpty())
        {
            throw new InvalidDataException("SingleSignOnService not found in metadata");
        }

        // Get signing certificate
        var keyDescriptor = idpSsoDescriptor.Elements(ns + "KeyDescriptor")
            .FirstOrDefault(e => e.Attribute("use")?.Value == "signing" || e.Attribute("use") == null);

        var certElement = keyDescriptor?.Descendants(XName.Get("X509Certificate", XmlDsigNamespace)).FirstOrDefault();
        var certificate = certElement?.Value.Replace("\n", "").Replace("\r", "").Trim();

        if (certificate.IsNullOrEmpty())
        {
            throw new InvalidDataException("Signing certificate not found in metadata");
        }

        var config = new SamlIdentityProviderConfiguration
        {
            Id = Guid.NewGuid(),
            EntityId = entityId,
            SingleSignOnServiceUrl = ssoUrl,
            SigningCertificate = $"-----BEGIN CERTIFICATE-----\n{certificate}\n-----END CERTIFICATE-----",
            MetadataXml = metadataXml,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(config);
    }

    /// <summary>
    /// Creates a SAML logout request for Single Logout (SLO)
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="nameId">User NameID from the authentication assertion</param>
    /// <param name="sessionIndex">Session index from authentication (if available)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Logout request result with redirect URL</returns>
    /// <exception cref="InvalidOperationException">Thrown when IdP configuration is not found or logout not supported</exception>
    public async Task<SamlLogoutRequest> CreateLogoutRequestAsync(
        Guid tenantId,
        string nameId,
        string? sessionIndex = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating SAML logout request for tenant {TenantId}", tenantId);

        if (nameId.IsNullOrEmpty())
        {
            throw new ArgumentException("NameID cannot be null or empty", nameof(nameId));
        }

        // Get IdP configuration for tenant
        var idpConfig = await _idpStore.GetEnabledByTenantIdAsync(tenantId, cancellationToken);
        if (idpConfig == null)
        {
            throw new InvalidOperationException($"No enabled SAML IdP configuration found for tenant {tenantId}");
        }

        // Check if IdP supports Single Logout
        if (idpConfig.SingleLogoutServiceUrl.IsNullOrEmpty())
        {
            throw new InvalidOperationException($"IdP {idpConfig.Name} does not have Single Logout configured");
        }

        // Generate request ID
        var requestId = $"_{Guid.NewGuid():N}";
        var issueInstant = DateTimeOffset.UtcNow;

        // Build LogoutRequest XML
        var logoutRequest = new XElement(
            XName.Get("LogoutRequest", SamlpNamespace),
            new XAttribute("ID", requestId),
            new XAttribute("Version", "2.0"),
            new XAttribute("IssueInstant", issueInstant.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")),
            new XAttribute("Destination", idpConfig.SingleLogoutServiceUrl),
            new XElement(
                XName.Get("Issuer", SamlNamespace),
                _options.ServiceProvider.EntityId
            ),
            new XElement(
                XName.Get("NameID", SamlNamespace),
                new XAttribute("Format", idpConfig.NameIdFormat),
                nameId
            )
        );

        // Add SessionIndex if provided (required for proper session termination)
        if (!sessionIndex.IsNullOrEmpty())
        {
            logoutRequest.Add(
                new XElement(
                    XName.Get("SessionIndex", SamlpNamespace),
                    sessionIndex
                )
            );
        }

        // Sign request if required
        if (idpConfig.SignAuthenticationRequests && !_options.ServiceProvider.SigningCertificate.IsNullOrEmpty())
        {
            logoutRequest = SignXmlDocument(logoutRequest);
        }

        // Convert to string
        var logoutRequestXml = logoutRequest.ToString(SaveOptions.DisableFormatting);

        if (_options.EnableDebugLogging)
        {
            _logger.LogDebug("SAML LogoutRequest: {LogoutRequestXml}", logoutRequestXml);
        }

        // Encode for transmission
        var samlRequestEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(logoutRequestXml));

        // Create session for tracking the logout response
        var validityPeriod = TimeSpan.FromMinutes(_options.ServiceProvider.AuthnRequestValidityMinutes);
        await _sessionStore.CreateSessionAsync(
            tenantId,
            idpConfig.Id,
            requestId,
            null, // No relay state for logout
            validityPeriod,
            cancellationToken);

        // Build redirect URL based on binding type
        string redirectUrl;
        if (idpConfig.BindingType == SamlBindingType.HttpPost)
        {
            // For POST binding, we'll need to render an HTML form
            redirectUrl = idpConfig.SingleLogoutServiceUrl;
        }
        else
        {
            // HTTP-Redirect binding
            var queryString = $"SAMLRequest={Uri.EscapeDataString(samlRequestEncoded)}";
            redirectUrl = $"{idpConfig.SingleLogoutServiceUrl}?{queryString}";
        }

        _logger.LogInformation(
            "Created SAML logout request {RequestId} for tenant {TenantId} to IdP {IdpName}",
            requestId, tenantId, idpConfig.Name);

        return new SamlLogoutRequest
        {
            RequestId = requestId,
            RedirectUrl = redirectUrl,
            SamlRequest = samlRequestEncoded,
            BindingType = idpConfig.BindingType
        };
    }

    /// <summary>
    /// Validates a SAML logout response from the identity provider
    /// </summary>
    /// <param name="samlResponse">Base64-encoded SAML logout response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if logout was successful, false otherwise</returns>
    public async Task<bool> ValidateLogoutResponseAsync(
        string samlResponse,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating SAML logout response");

        try
        {
            // Decode SAML response
            var responseXml = Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse));

            if (_options.EnableDebugLogging)
            {
                _logger.LogDebug("SAML LogoutResponse: {ResponseXml}", responseXml);
            }

            // Parse XML
            var doc = XDocument.Parse(responseXml);
            var response = doc.Root;

            if (response == null || response.Name != XName.Get("LogoutResponse", SamlpNamespace))
            {
                _logger.LogWarning("Invalid SAML logout response format");
                return false;
            }

            // Extract InResponseTo to find session
            var inResponseTo = response.Attribute("InResponseTo")?.Value;
            if (inResponseTo.IsNullOrEmpty())
            {
                _logger.LogWarning("SAML logout response missing InResponseTo attribute");
                return false;
            }

            // Get session and atomically consume it to prevent replay attacks
            var session = await _sessionStore.GetSessionByRequestIdAsync(inResponseTo, cancellationToken);
            if (session == null)
            {
                _logger.LogWarning(
                    "SAML logout session not found or expired for request {RequestId}",
                    inResponseTo);
                return false;
            }

            // Atomically consume the session to prevent replay attacks
            var consumed = await _sessionStore.TryConsumeSessionAsync(inResponseTo, cancellationToken);
            if (!consumed)
            {
                _logger.LogWarning(
                    "SAML logout replay attack detected for request {RequestId} - session already consumed",
                    inResponseTo);
                return false;
            }

            // Get IdP configuration
            var idpConfig = await _idpStore.GetByIdAsync(session.IdpConfigurationId, cancellationToken);
            if (idpConfig == null)
            {
                _logger.LogWarning(
                    "IdP configuration {IdpConfigId} not found for logout session",
                    session.IdpConfigurationId);
                return false;
            }

            // Validate status
            var status = response.Element(XName.Get("Status", SamlpNamespace));
            var statusCode = status?.Element(XName.Get("StatusCode", SamlpNamespace))?.Attribute("Value")?.Value;

            if (statusCode != "urn:oasis:names:tc:SAML:2.0:status:Success")
            {
                var statusMessage = status?.Element(XName.Get("StatusMessage", SamlpNamespace))?.Value;
                _logger.LogWarning(
                    "SAML logout failed with status {StatusCode}: {StatusMessage}",
                    statusCode, statusMessage);
                return false;
            }

            // Verify signature if required
            if (idpConfig.WantAssertionsSigned)
            {
                var isSignatureValid = VerifyXmlSignature(response, idpConfig.SigningCertificate);
                if (!isSignatureValid)
                {
                    _logger.LogWarning(
                        "SAML logout response signature validation failed for IdP {IdpName}",
                        idpConfig.Name);
                    return false;
                }
            }

            _logger.LogInformation(
                "Successfully validated SAML logout response for request {RequestId}",
                inResponseTo);

            return true;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Invalid SAML logout response format - cannot decode Base64");
            return false;
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "Invalid SAML logout response - malformed XML");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating SAML logout response");
            return false;
        }
    }

    // Helper methods

    private string GetAssertionConsumerServiceUrl()
    {
        var sp = _options.ServiceProvider;
        return $"{sp.BaseUrl}{sp.AssertionConsumerServicePath}";
    }

    private XElement SignXmlDocument(XElement element)
    {
        try
        {
            var sp = _options.ServiceProvider;

            if (sp.SigningCertificate.IsNullOrEmpty() || sp.SigningPrivateKey.IsNullOrEmpty())
            {
                _logger.LogWarning("Cannot sign XML: signing certificate or private key not configured");
                return element;
            }

            // Load certificate and private key
            var cert = LoadCertificate(sp.SigningCertificate);
            var privateKey = LoadPrivateKey(sp.SigningPrivateKey);

            // Convert XElement to XmlDocument for signing
            var xmlDoc = new XmlDocument { PreserveWhitespace = true };
            using (var reader = element.CreateReader())
            {
                xmlDoc.Load(reader);
            }

            // Create SignedXml object
            var signedXml = new SignedXml(xmlDoc);
            signedXml.SigningKey = privateKey;

            // Set the signature method
            signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;
            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;

            // Create reference to the document
            var reference = new Reference("");
            reference.Uri = "";
            reference.DigestMethod = SignedXml.XmlDsigSHA256Url;

            // Add enveloped signature transform
            var env = new XmlDsigEnvelopedSignatureTransform();
            reference.AddTransform(env);

            // Add exclusive canonicalization transform
            var c14n = new XmlDsigExcC14NTransform();
            reference.AddTransform(c14n);

            signedXml.AddReference(reference);

            // Add KeyInfo with certificate
            var keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(cert));
            signedXml.KeyInfo = keyInfo;

            // Compute the signature
            signedXml.ComputeSignature();

            // Get the XML representation of the signature
            var signatureElement = signedXml.GetXml();

            // Import signature into the document
            var importedSignature = xmlDoc.ImportNode(signatureElement, true);

            // Insert signature as the last child of the root element
            xmlDoc.DocumentElement?.AppendChild(importedSignature);

            // Convert back to XElement
            return XElement.Load(new XmlNodeReader(xmlDoc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing XML document");
            throw new InvalidOperationException("Failed to sign XML document", ex);
        }
    }

    private bool VerifyXmlSignature(XElement element, string certificatePem)
    {
        try
        {
            // Check if signature exists
            var signature = element.Element(XName.Get("Signature", XmlDsigNamespace));
            if (signature == null)
            {
                _logger.LogWarning("No signature found in XML element");
                return false;
            }

            // Load certificate
            var cert = LoadCertificate(certificatePem);

            // Validate certificate
            if (!ValidateCertificate(cert))
            {
                _logger.LogWarning("Certificate validation failed");
                return false;
            }

            // Convert XElement to XmlDocument for verification
            var xmlDoc = new XmlDocument { PreserveWhitespace = true };
            using (var reader = element.CreateReader())
            {
                xmlDoc.Load(reader);
            }

            // Create SignedXml object
            var signedXml = new SignedXml(xmlDoc);

            // Load the signature node
            var signatureNode = xmlDoc.GetElementsByTagName("Signature", XmlDsigNamespace)[0];
            if (signatureNode == null)
            {
                _logger.LogWarning("Signature node not found in document");
                return false;
            }

            signedXml.LoadXml((XmlElement)signatureNode);

            // Verify the signature using the certificate's public key
            var isValid = signedXml.CheckSignature(cert, true);

            if (!isValid)
            {
                _logger.LogWarning("XML signature verification failed - signature is invalid");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying XML signature");
            return false;
        }
    }

    private bool ValidateCertificate(X509Certificate2 certificate)
    {
        try
        {
            // Check if certificate is within validity period
            var now = DateTime.UtcNow;
            if (now < certificate.NotBefore || now > certificate.NotAfter)
            {
                _logger.LogWarning(
                    "Certificate is outside validity period. NotBefore: {NotBefore}, NotAfter: {NotAfter}, Now: {Now}",
                    certificate.NotBefore, certificate.NotAfter, now);
                return false;
            }

            // Build certificate chain
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.VerificationTime = DateTime.Now;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(30);

            // Build the chain
            var chainBuilt = chain.Build(certificate);

            if (!chainBuilt)
            {
                _logger.LogWarning("Certificate chain validation failed");
                foreach (var status in chain.ChainStatus)
                {
                    // Allow self-signed certificates for development
                    if (status.Status == X509ChainStatusFlags.UntrustedRoot)
                    {
                        _logger.LogWarning(
                            "Certificate has untrusted root (self-signed). Status: {Status}, Info: {Info}",
                            status.Status, status.StatusInformation);
                        continue;
                    }

                    // All other errors are fatal
                    _logger.LogError(
                        "Certificate chain error: {Status}, Info: {Info}",
                        status.Status, status.StatusInformation);
                    return false;
                }
            }

            // Check key usage
            foreach (var extension in certificate.Extensions)
            {
                if (extension is X509KeyUsageExtension keyUsage)
                {
                    if (!keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
                    {
                        _logger.LogWarning("Certificate does not have DigitalSignature key usage");
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating certificate");
            return false;
        }
    }

    private SamlAssertionResult ValidateConditions(XElement conditions)
    {
        var notBefore = conditions.Attribute("NotBefore")?.Value;
        var notOnOrAfter = conditions.Attribute("NotOnOrAfter")?.Value;

        var now = DateTimeOffset.UtcNow;
        var clockSkew = TimeSpan.FromMinutes(_options.MaximumClockSkewMinutes);

        if (notBefore != null)
        {
            var notBeforeTime = DateTimeOffset.Parse(notBefore);
            if (now.Add(clockSkew) < notBeforeTime)
            {
                return new SamlAssertionResult
                {
                    IsValid = false,
                    Errors = { "Assertion not yet valid (NotBefore condition)" }
                };
            }
        }

        if (notOnOrAfter != null)
        {
            var notOnOrAfterTime = DateTimeOffset.Parse(notOnOrAfter);
            if (now.Subtract(clockSkew) >= notOnOrAfterTime)
            {
                return new SamlAssertionResult
                {
                    IsValid = false,
                    Errors = { "Assertion expired (NotOnOrAfter condition)" }
                };
            }
        }

        // Validate AudienceRestriction
        var audienceRestriction = conditions.Element(XName.Get("AudienceRestriction", SamlNamespace));
        if (audienceRestriction != null)
        {
            var audiences = audienceRestriction.Elements(XName.Get("Audience", SamlNamespace))
                .Select(e => e.Value)
                .ToList();

            if (!audiences.Contains(_options.ServiceProvider.EntityId))
            {
                return new SamlAssertionResult
                {
                    IsValid = false,
                    Errors = { "Assertion not intended for this service provider (Audience mismatch)" }
                };
            }
        }

        return new SamlAssertionResult { IsValid = true };
    }

    private Dictionary<string, string> ExtractAttributes(XElement assertion, Dictionary<string, string> mappings)
    {
        var result = new Dictionary<string, string>();

        var attributeStatement = assertion.Element(XName.Get("AttributeStatement", SamlNamespace));
        if (attributeStatement == null)
        {
            return result;
        }

        var attributes = attributeStatement.Elements(XName.Get("Attribute", SamlNamespace));

        foreach (var attribute in attributes)
        {
            var name = attribute.Attribute("Name")?.Value;
            var value = attribute.Element(XName.Get("AttributeValue", SamlNamespace))?.Value;

            if (name.IsNullOrEmpty() || value.IsNullOrEmpty())
            {
                continue;
            }

            // Check if we have a mapping for this attribute
            if (mappings.TryGetValue(name, out var mappedName))
            {
                result[mappedName] = value;
            }
            else
            {
                // Store unmapped attributes with original name
                result[name] = value;
            }
        }

        return result;
    }

    private X509Certificate2 LoadCertificate(string certificatePem)
    {
        var certData = certificatePem
            .Replace("-----BEGIN CERTIFICATE-----", "")
            .Replace("-----END CERTIFICATE-----", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Trim();

        var certBytes = Convert.FromBase64String(certData);
        return X509CertificateLoader.LoadCertificate(certBytes);
    }

    private RSA LoadPrivateKey(string privateKeyPem)
    {
        var keyData = privateKeyPem
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
            .Replace("-----END RSA PRIVATE KEY-----", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Trim();

        var keyBytes = Convert.FromBase64String(keyData);

        var rsa = RSA.Create();

        try
        {
            // Try PKCS#8 format first
            rsa.ImportPkcs8PrivateKey(keyBytes, out _);
        }
        catch
        {
            try
            {
                // Fall back to RSA format
                rsa.ImportRSAPrivateKey(keyBytes, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import private key");
                throw new InvalidOperationException("Failed to load private key. Ensure it is in PKCS#8 or RSA format.", ex);
            }
        }

        return rsa;
    }
}
