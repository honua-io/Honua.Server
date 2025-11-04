# DNS Challenge Providers for ACME

This directory contains DNS challenge providers for ACME (Automated Certificate Management Environment) DNS-01 validation. DNS-01 challenges are required for wildcard certificates and can be used as an alternative to HTTP-01 challenges.

## Available Providers

### Cloudflare DNS Provider

The `CloudflareDnsProvider` integrates with Cloudflare's DNS API to automatically manage TXT records for ACME DNS-01 challenges.

#### Features

- Automatic Zone ID discovery from domain names
- Support for apex domains and subdomains
- Configurable DNS propagation wait times
- Automatic cleanup of challenge records
- Comprehensive error handling and logging
- Built-in retry logic for DNS propagation verification

#### Configuration

```csharp
// Basic configuration with API token
var options = new CloudflareDnsProviderOptions
{
    ApiToken = "your-cloudflare-api-token",
    PropagationWaitSeconds = 30 // Optional: default is 30 seconds
};

// Advanced configuration with explicit Zone ID
var options = new CloudflareDnsProviderOptions
{
    ApiToken = "your-cloudflare-api-token",
    ZoneId = "your-zone-id", // Optional: auto-discovered if not specified
    PropagationWaitSeconds = 60
};

// Create provider
var httpClient = new HttpClient();
var logger = loggerFactory.CreateLogger<CloudflareDnsProvider>();
var provider = new CloudflareDnsProvider(httpClient, options, logger);
```

#### Creating a Cloudflare API Token

1. Log in to the [Cloudflare Dashboard](https://dash.cloudflare.com/)
2. Go to **My Profile** > **API Tokens**
3. Click **Create Token**
4. Use the **Edit zone DNS** template, or create a custom token with:
   - **Permissions:**
     - Zone > DNS > Edit
     - Zone > Zone > Read
   - **Zone Resources:**
     - Include > Specific zone > (select your domain)
5. Copy the generated token and store it securely

#### Getting Your Zone ID (Optional)

The Zone ID is automatically discovered from the domain name, but you can provide it explicitly for better performance:

1. Log in to the [Cloudflare Dashboard](https://dash.cloudflare.com/)
2. Select your domain
3. The Zone ID is displayed in the **API** section of the overview page (right sidebar)

#### Usage Example

```csharp
using Honua.Cli.AI.Services.Certificates;
using Honua.Cli.AI.Services.Certificates.DnsChallenge;

// Configure the DNS provider
var dnsOptions = new CloudflareDnsProviderOptions
{
    ApiToken = Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN")!,
    PropagationWaitSeconds = 30
};

var httpClient = new HttpClient();
var logger = loggerFactory.CreateLogger<CloudflareDnsProvider>();
var dnsProvider = new CloudflareDnsProvider(httpClient, dnsOptions, logger);

// Configure certificate service
var certLogger = loggerFactory.CreateLogger<AcmeCertificateService>();
var certificateService = new AcmeCertificateService(certLogger);

// Acquire a wildcard certificate
var domains = new List<string> { "*.example.com", "example.com" };
var result = await certificateService.AcquireCertificateAsync(
    domains: domains,
    email: "admin@example.com",
    challengeType: "Dns01",
    isProduction: true,
    accountKeyPath: "/path/to/acme-account-key.pem",
    challengeProvider: dnsProvider,
    cancellationToken: CancellationToken.None
);

if (result.Success)
{
    Console.WriteLine($"Certificate acquired successfully!");
    Console.WriteLine($"Domains: {string.Join(", ", result.Domains)}");
    Console.WriteLine($"Expires: {result.ExpiresAt}");

    // Save the certificate and private key
    await File.WriteAllTextAsync("certificate.pem", result.CertificatePem);
    await File.WriteAllTextAsync("private-key.pem", result.PrivateKeyPem);
}
else
{
    Console.WriteLine($"Failed to acquire certificate: {result.Message}");
}
```

#### Environment Variables

For production deployments, store the API token securely using environment variables:

```bash
# Linux/macOS
export CLOUDFLARE_API_TOKEN="your-api-token"

# Windows PowerShell
$env:CLOUDFLARE_API_TOKEN="your-api-token"

# Docker
docker run -e CLOUDFLARE_API_TOKEN="your-api-token" ...

# Kubernetes Secret
kubectl create secret generic cloudflare-credentials \
  --from-literal=api-token='your-api-token'
```

#### Configuration in appsettings.json

```json
{
  "Cloudflare": {
    "ApiToken": "your-api-token-or-reference-to-secret",
    "ZoneId": "optional-zone-id",
    "PropagationWaitSeconds": 30
  }
}
```

#### Troubleshooting

**Problem: "Failed to find Cloudflare zone for domain"**
- Ensure the domain is added to your Cloudflare account
- Verify your API token has `Zone:Read` permission
- Check that the domain is active (not pending)

**Problem: "Failed to create DNS record: 401 Unauthorized"**
- Verify your API token is correct
- Ensure the token hasn't expired
- Check that the token has `DNS:Edit` permission for the zone

**Problem: "Challenge validation timed out"**
- Increase `PropagationWaitSeconds` (try 60 or 90 seconds)
- Check Cloudflare's status page for API issues
- Verify the TXT record was created in the Cloudflare dashboard

**Problem: "Invalid API token"**
- Regenerate your API token
- Ensure you're using an **API Token**, not a Global API Key
- Verify the token permissions match the requirements above

### AWS Route53 DNS Provider

The `DnsRoute53ChallengeProvider` integrates with AWS Route53 for DNS-01 challenges.

#### Configuration

```csharp
var route53Client = new AmazonRoute53Client();
var logger = loggerFactory.CreateLogger<DnsRoute53ChallengeProvider>();
var provider = new DnsRoute53ChallengeProvider(
    route53Client,
    hostedZoneId: "Z1234567890ABC",
    logger
);
```

See [AWS Route53 Documentation](https://docs.aws.amazon.com/route53/) for details on obtaining your Hosted Zone ID.

## Implementing a Custom DNS Provider

To implement a custom DNS provider, create a class that implements `IChallengeProvider`:

```csharp
public interface IChallengeProvider
{
    Task DeployChallengeAsync(
        string domain,
        string token,
        string keyAuthz,
        string challengeType,
        CancellationToken cancellationToken);

    Task CleanupChallengeAsync(
        string domain,
        string token,
        string keyAuthz,
        string challengeType,
        CancellationToken cancellationToken);
}
```

### Implementation Guidelines

1. **DeployChallengeAsync**: Create a TXT record at `_acme-challenge.{domain}` with the value of `keyAuthz`
2. **CleanupChallengeAsync**: Remove the TXT record created during deployment
3. **Error Handling**: Log errors but don't throw exceptions in cleanup
4. **DNS Propagation**: Wait for DNS changes to propagate (typically 30-60 seconds)
5. **Challenge Type**: Only handle `Dns01` challenges, throw `ArgumentException` for others

### Example Custom Provider

```csharp
public sealed class CustomDnsProvider : IChallengeProvider
{
    private readonly ICustomDnsClient _client;
    private readonly ILogger<CustomDnsProvider> _logger;

    public CustomDnsProvider(ICustomDnsClient client, ILogger<CustomDnsProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task DeployChallengeAsync(
        string domain,
        string token,
        string keyAuthz,
        string challengeType,
        CancellationToken cancellationToken)
    {
        if (challengeType != "Dns01")
        {
            throw new ArgumentException("Only DNS-01 challenges are supported");
        }

        var recordName = $"_acme-challenge.{domain}";
        await _client.CreateTxtRecordAsync(recordName, keyAuthz, cancellationToken);

        // Wait for propagation
        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
    }

    public async Task CleanupChallengeAsync(
        string domain,
        string token,
        string keyAuthz,
        string challengeType,
        CancellationToken cancellationToken)
    {
        if (challengeType != "Dns01") return;

        try
        {
            var recordName = $"_acme-challenge.{domain}";
            await _client.DeleteTxtRecordAsync(recordName, keyAuthz, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup DNS record for {Domain}", domain);
        }
    }
}
```

## Security Considerations

1. **API Token Storage**: Never commit API tokens to source control
2. **Least Privilege**: Use tokens with minimal required permissions
3. **Token Rotation**: Rotate API tokens regularly
4. **Audit Logging**: Monitor DNS changes for unexpected modifications
5. **Rate Limiting**: Be aware of DNS provider API rate limits

## References

- [ACME Protocol Specification (RFC 8555)](https://tools.ietf.org/html/rfc8555)
- [Cloudflare API Documentation](https://developers.cloudflare.com/api/)
- [AWS Route53 API Documentation](https://docs.aws.amazon.com/route53/latest/APIReference/)
- [Let's Encrypt Documentation](https://letsencrypt.org/docs/)
