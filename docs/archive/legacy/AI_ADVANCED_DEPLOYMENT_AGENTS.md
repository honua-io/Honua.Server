# AI Consultant: Advanced Deployment, SSL, and DNS Automation

## Overview

This document defines three specialized AI agents that provide advanced automation capabilities for Honua deployments:

1. **CertificateManagementAgent** - Let's Encrypt SSL/TLS certificate automation
2. **DnsConfigurationAgent** - DNS provider integration (Route53, Cloudflare, Azure DNS)
3. **BlueGreenDeploymentAgent** - YARP-based zero-downtime deployments

**Support Scope**: These agents **only support YARP** as the reverse proxy. Alternative proxies (Nginx, Traefik, etc.) are unsupported and receive no AI assistance.

---

## 1. CertificateManagementAgent

### Purpose
Automates SSL/TLS certificate acquisition, renewal, and deployment using Let's Encrypt ACME protocol.

### Capabilities

**User Request Examples**:
- "Setup SSL with Let's Encrypt for honua.example.com"
- "Configure automatic certificate renewal"
- "Use DNS-01 challenge for wildcard certificate"
- "Store certificates in Azure Key Vault"

**What the Agent Does**:
1. Analyzes domain requirements (single domain, wildcard, SAN)
2. Selects appropriate ACME challenge method (HTTP-01 or DNS-01)
3. Generates ACME client configuration (using Certes library for .NET)
4. Configures DNS provider for DNS-01 challenges (via DnsConfigurationAgent)
5. Generates YARP configuration for certificate binding
6. Sets up automatic renewal (30 days before expiry)
7. Configures certificate storage (file system, Key Vault, AWS Secrets Manager)

### Architecture

```csharp
public class CertificateManagementAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider? _llmProvider;
    private readonly DnsConfigurationAgent _dnsAgent;

    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // 1. Analyze certificate requirements
        var requirements = await AnalyzeCertificateRequirementsAsync(request, context, cancellationToken);

        // 2. Select ACME challenge method
        var challengeMethod = SelectChallengeMethod(requirements);

        // 3. Generate ACME client configuration
        var acmeConfig = await GenerateAcmeConfigurationAsync(requirements, challengeMethod, context, cancellationToken);

        // 4. If DNS-01, configure DNS provider
        if (challengeMethod == AcmeChallengeMethod.Dns01)
        {
            await _dnsAgent.ConfigureDnsChallengeRecordsAsync(requirements.Domains, context, cancellationToken);
        }

        // 5. Generate YARP certificate binding
        var yarpConfig = await GenerateYarpCertificateConfigAsync(requirements, context, cancellationToken);

        // 6. Setup automatic renewal
        var renewalConfig = await GenerateRenewalConfigurationAsync(requirements, context, cancellationToken);

        // 7. Save configurations
        if (!context.DryRun)
        {
            await SaveCertificateConfigurationAsync(acmeConfig, yarpConfig, renewalConfig, context, cancellationToken);
        }

        return new AgentStepResult
        {
            AgentName = "CertificateManagement",
            Action = "ConfigureSSL",
            Success = true,
            Message = $"Configured SSL for {string.Join(", ", requirements.Domains)} using {challengeMethod}"
        };
    }
}

public class CertificateRequirements
{
    public List<string> Domains { get; set; } = new();
    public bool IsWildcard { get; set; }
    public bool IsSAN { get; set; } // Subject Alternative Name
    public string Email { get; set; } = string.Empty;
    public CertificateStorage StorageMethod { get; set; } = CertificateStorage.FileSystem;
    public string StorageLocation { get; set; } = "/etc/honua/certificates";
    public int RenewalDaysBefore { get; set; } = 30;
}

public enum AcmeChallengeMethod
{
    Http01,  // HTTP challenge (requires port 80)
    Dns01    // DNS challenge (works with firewalls, supports wildcards)
}

public enum CertificateStorage
{
    FileSystem,
    AzureKeyVault,
    AwsSecretsManager,
    GcpSecretManager
}
```

### ACME Client Configuration (Certes)

```csharp
public class AcmeConfiguration
{
    public string AcmeServer { get; set; } = "https://acme-v02.api.letsencrypt.org/directory"; // Production
    // public string AcmeServer { get; set; } = "https://acme-staging-v02.api.letsencrypt.org/directory"; // Staging

    public string Email { get; set; } = string.Empty;
    public List<string> Domains { get; set; } = new();
    public AcmeChallengeMethod ChallengeMethod { get; set; }
    public string AccountKeyPath { get; set; } = "/etc/honua/acme/account.key";

    // HTTP-01 specific
    public string ChallengeResponsePath { get; set; } = "/var/www/honua/.well-known/acme-challenge";

    // DNS-01 specific
    public DnsProviderType? DnsProvider { get; set; }
    public Dictionary<string, string> DnsCredentials { get; set; } = new();
}

// Example Certes implementation
public class CertesAcmeClient
{
    public async Task<X509Certificate2> ObtainCertificateAsync(AcmeConfiguration config)
    {
        var acme = new AcmeContext(config.AcmeServer);

        // Create or load account
        var account = await acme.NewAccount(config.Email, true);

        // Create order
        var order = await acme.NewOrder(config.Domains);

        // Get authorization
        var authz = await order.Authorizations();

        foreach (var auth in authz)
        {
            if (config.ChallengeMethod == AcmeChallengeMethod.Http01)
            {
                var httpChallenge = await auth.Http();
                var keyAuthz = httpChallenge.KeyAuthz;

                // Write challenge response file
                var challengePath = Path.Combine(config.ChallengeResponsePath, httpChallenge.Token);
                await File.WriteAllTextAsync(challengePath, keyAuthz);

                await httpChallenge.Validate();
            }
            else if (config.ChallengeMethod == AcmeChallengeMethod.Dns01)
            {
                var dnsChallenge = await auth.Dns();
                var dnsTxt = acme.AccountKey.DnsTxt(dnsChallenge.Token);

                // Create DNS TXT record via DNS provider
                await CreateDnsTxtRecordAsync($"_acme-challenge.{auth.Identifier.Value}", dnsTxt);

                await dnsChallenge.Validate();
            }
        }

        // Generate CSR
        var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
        var cert = await order.Generate(new CsrInfo
        {
            CommonName = config.Domains[0]
        }, privateKey);

        // Export certificate
        var pfxBuilder = cert.ToPfx(privateKey);
        var pfx = pfxBuilder.Build("Honua Certificate", string.Empty);

        return new X509Certificate2(pfx, string.Empty, X509KeyStorageFlags.Exportable);
    }
}
```

### YARP Certificate Binding

```csharp
public class YarpCertificateConfiguration
{
    public string ClusterId { get; set; } = "honua-cluster";
    public string RouteId { get; set; } = "honua-https-route";
    public List<YarpCertificateBinding> Bindings { get; set; } = new();
}

public class YarpCertificateBinding
{
    public string Domain { get; set; } = string.Empty;
    public string CertificatePath { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public bool EnableHttp2 { get; set; } = true;
    public bool EnableHttp3 { get; set; } = false;
    public List<string> TlsProtocols { get; set; } = new() { "Tls12", "Tls13" };
}

// YARP appsettings.json structure
{
  "ReverseProxy": {
    "Routes": {
      "honua-https-route": {
        "ClusterId": "honua-cluster",
        "Match": {
          "Hosts": ["honua.example.com"]
        }
      }
    },
    "Clusters": {
      "honua-cluster": {
        "Destinations": {
          "blue": { "Address": "http://localhost:5000" },
          "green": { "Address": "http://localhost:5001" }
        },
        "HttpsClientCertificate": {
          "Path": "/etc/honua/certificates/honua.example.com.pfx",
          "Password": ""
        }
      }
    }
  },
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:443",
        "Certificate": {
          "Path": "/etc/honua/certificates/honua.example.com.pfx",
          "Password": ""
        }
      }
    }
  }
}
```

### Certificate Renewal

```csharp
public class CertificateRenewalConfiguration
{
    public bool AutoRenewEnabled { get; set; } = true;
    public int RenewalDaysBefore { get; set; } = 30;
    public string RenewalCronExpression { get; set; } = "0 0 2 * * ?"; // Daily at 2 AM
    public string RenewalScriptPath { get; set; } = "/usr/local/bin/honua-cert-renew.sh";
    public List<string> NotificationEmails { get; set; } = new();
    public WebhookNotification? Webhook { get; set; }
}

// Systemd timer unit for renewal
[Unit]
Description=Honua Certificate Renewal
After=network.target

[Timer]
OnCalendar=daily
Persistent=true

[Install]
WantedBy=timers.target
```

---

## 2. DnsConfigurationAgent

### Purpose
Automates DNS record configuration across multiple DNS providers for ACME challenges, domain verification, and service routing.

### Supported DNS Providers

1. **AWS Route53** ✅
2. **Cloudflare** ✅
3. **Azure DNS** ✅
4. **Google Cloud DNS** ✅
5. **Manual DNS** (provides instructions, no automation)

### Capabilities

**User Request Examples**:
- "Configure DNS for honua.example.com pointing to 203.0.113.5"
- "Setup ACME DNS-01 challenge records"
- "Create CNAME for blue-green deployments"
- "Configure wildcard DNS for *.api.example.com"

**What the Agent Does**:
1. Analyzes DNS requirements from user request
2. Detects DNS provider from domain or accepts explicit provider
3. Generates provider-specific API configuration
4. Creates A, AAAA, CNAME, TXT records as needed
5. Validates DNS propagation
6. Generates verification commands

### Architecture

```csharp
public class DnsConfigurationAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider? _llmProvider;
    private readonly Dictionary<DnsProviderType, IDnsProvider> _dnsProviders;

    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // 1. Analyze DNS requirements
        var requirements = await AnalyzeDnsRequirementsAsync(request, context, cancellationToken);

        // 2. Detect or select DNS provider
        var provider = await SelectDnsProviderAsync(requirements, context, cancellationToken);

        // 3. Generate DNS records
        var records = await GenerateDnsRecordsAsync(requirements, context, cancellationToken);

        // 4. Apply DNS changes (if provider is automated)
        if (provider.IsAutomated && !context.DryRun)
        {
            await provider.ApplyRecordsAsync(records, cancellationToken);
        }
        else
        {
            // Generate manual instructions
            await GenerateManualDnsInstructionsAsync(records, context, cancellationToken);
        }

        // 5. Verify DNS propagation
        if (!context.DryRun)
        {
            await VerifyDnsPropagationAsync(records, cancellationToken);
        }

        return new AgentStepResult
        {
            AgentName = "DnsConfiguration",
            Action = "ConfigureDNS",
            Success = true,
            Message = $"Configured {records.Count} DNS records for {requirements.Domain}"
        };
    }
}

public class DnsRequirements
{
    public string Domain { get; set; } = string.Empty;
    public DnsProviderType Provider { get; set; }
    public List<DnsRecordRequest> Records { get; set; } = new();
    public bool VerifyPropagation { get; set; } = true;
    public int PropagationTimeoutSeconds { get; set; } = 120;
}

public class DnsRecordRequest
{
    public string Name { get; set; } = string.Empty;
    public DnsRecordType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public int TTL { get; set; } = 300;
    public int Priority { get; set; } = 0; // For MX records
}

public enum DnsProviderType
{
    Route53,
    Cloudflare,
    AzureDns,
    GoogleCloudDns,
    Manual
}

public enum DnsRecordType
{
    A,
    AAAA,
    CNAME,
    TXT,
    MX,
    SRV
}
```

### DNS Provider Implementations

#### AWS Route53

```csharp
public class Route53DnsProvider : IDnsProvider
{
    private readonly IAmazonRoute53 _route53Client;

    public async Task ApplyRecordsAsync(List<DnsRecordRequest> records, CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            var request = new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = GetHostedZoneId(record.Name),
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>
                    {
                        new Change
                        {
                            Action = ChangeAction.UPSERT,
                            ResourceRecordSet = new ResourceRecordSet
                            {
                                Name = record.Name,
                                Type = ConvertRecordType(record.Type),
                                TTL = record.TTL,
                                ResourceRecords = new List<ResourceRecord>
                                {
                                    new ResourceRecord { Value = record.Value }
                                }
                            }
                        }
                    }
                }
            };

            await _route53Client.ChangeResourceRecordSetsAsync(request, cancellationToken);
        }
    }
}

// Configuration
{
  "DnsProviders": {
    "Route53": {
      "AccessKeyId": "AKIA...",
      "SecretAccessKey": "...",
      "Region": "us-east-1",
      "HostedZoneId": "Z1234567890ABC"
    }
  }
}
```

#### Cloudflare

```csharp
public class CloudflareDnsProvider : IDnsProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiToken;
    private readonly string _zoneId;

    public async Task ApplyRecordsAsync(List<DnsRecordRequest> records, CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            var request = new
            {
                type = record.Type.ToString().ToUpper(),
                name = record.Name,
                content = record.Value,
                ttl = record.TTL,
                proxied = false
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"https://api.cloudflare.com/client/v4/zones/{_zoneId}/dns_records",
                content,
                cancellationToken
            );

            response.EnsureSuccessStatusCode();
        }
    }
}

// Configuration
{
  "DnsProviders": {
    "Cloudflare": {
      "ApiToken": "...",
      "ZoneId": "abc123...",
      "Email": "admin@example.com"
    }
  }
}
```

#### Azure DNS

```csharp
public class AzureDnsProvider : IDnsProvider
{
    private readonly DnsManagementClient _dnsClient;

    public async Task ApplyRecordsAsync(List<DnsRecordRequest> records, CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            var recordSet = new RecordSet
            {
                TTL = record.TTL
            };

            switch (record.Type)
            {
                case DnsRecordType.A:
                    recordSet.ARecords = new List<ARecord> { new ARecord(record.Value) };
                    break;
                case DnsRecordType.CNAME:
                    recordSet.CnameRecord = new CnameRecord(record.Value);
                    break;
                case DnsRecordType.TXT:
                    recordSet.TxtRecords = new List<TxtRecord> { new TxtRecord(new[] { record.Value }) };
                    break;
            }

            await _dnsClient.RecordSets.CreateOrUpdateAsync(
                resourceGroupName: "honua-rg",
                zoneName: "example.com",
                relativeRecordSetName: record.Name,
                recordType: ConvertRecordType(record.Type),
                parameters: recordSet,
                cancellationToken: cancellationToken
            );
        }
    }
}

// Configuration
{
  "DnsProviders": {
    "AzureDns": {
      "TenantId": "...",
      "ClientId": "...",
      "ClientSecret": "...",
      "SubscriptionId": "...",
      "ResourceGroup": "honua-rg",
      "ZoneName": "example.com"
    }
  }
}
```

### DNS Propagation Verification

```csharp
public class DnsPropagationVerifier
{
    public async Task<bool> VerifyRecordAsync(DnsRecordRequest record, int timeoutSeconds)
    {
        var endTime = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var lookup = new LookupClient();
                var result = await lookup.QueryAsync(record.Name, ConvertToQueryType(record.Type));

                if (result.Answers.Any(a => a.ToString().Contains(record.Value)))
                {
                    return true;
                }
            }
            catch (DnsResponseException)
            {
                // Record not yet propagated
            }

            await Task.Delay(5000); // Check every 5 seconds
        }

        return false;
    }
}
```

---

## 3. BlueGreenDeploymentAgent

### Purpose
Orchestrates zero-downtime blue-green and canary deployments using YARP, including automatic rollback based on telemetry.

### Capabilities

**User Request Examples**:
- "Deploy version 1.2.4 using blue-green strategy"
- "Canary deploy with 10% traffic to start"
- "Rollback to previous version"
- "Promote green slot to production"

**What the Agent Does**:
1. Analyzes deployment request (version, strategy, target)
2. Validates current deployment state
3. Generates YARP traffic splitting configuration
4. Deploys to inactive slot (green if blue is active)
5. Runs health checks and smoke tests
6. Orchestrates traffic cutover (instant for blue-green, gradual for canary)
7. Monitors telemetry for automatic rollback triggers
8. Executes rollback if thresholds breached

### Architecture

```csharp
public class BlueGreenDeploymentAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider? _llmProvider;
    private readonly YarpConfigurationService _yarpService;
    private readonly TelemetryService _telemetryService;

    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // 1. Analyze deployment request
        var deployment = await AnalyzeDeploymentRequestAsync(request, context, cancellationToken);

        // 2. Validate current state
        var currentState = await ValidateCurrentStateAsync(deployment, context, cancellationToken);

        // 3. Deploy to inactive slot
        var inactiveSlot = currentState.ActiveSlot == "blue" ? "green" : "blue";
        await DeployToSlotAsync(deployment, inactiveSlot, context, cancellationToken);

        // 4. Run health checks
        var healthCheckResult = await RunHealthChecksAsync(inactiveSlot, deployment, cancellationToken);
        if (!healthCheckResult.Success)
        {
            return new AgentStepResult
            {
                AgentName = "BlueGreenDeployment",
                Action = "Deploy",
                Success = false,
                Message = $"Health checks failed: {healthCheckResult.Message}"
            };
        }

        // 5. Execute traffic cutover
        if (deployment.Strategy == DeploymentStrategy.BlueGreen)
        {
            await ExecuteBlueGreenCutoverAsync(inactiveSlot, context, cancellationToken);
        }
        else if (deployment.Strategy == DeploymentStrategy.Canary)
        {
            await ExecuteCanaryRolloutAsync(inactiveSlot, deployment, context, cancellationToken);
        }

        // 6. Monitor telemetry
        var monitoringResult = await MonitorDeploymentAsync(deployment, cancellationToken);

        // 7. Automatic rollback if needed
        if (monitoringResult.ShouldRollback)
        {
            await ExecuteRollbackAsync(currentState.ActiveSlot, deployment, cancellationToken);
        }

        return new AgentStepResult
        {
            AgentName = "BlueGreenDeployment",
            Action = "Deploy",
            Success = true,
            Message = $"Successfully deployed {deployment.Version} to {inactiveSlot}"
        };
    }
}

public class DeploymentRequest
{
    public string Version { get; set; } = string.Empty;
    public DeploymentStrategy Strategy { get; set; }
    public DeploymentTarget Target { get; set; }
    public CanaryConfiguration? CanaryConfig { get; set; }
    public RollbackPolicy RollbackPolicy { get; set; } = new();
}

public enum DeploymentStrategy
{
    BlueGreen,  // Instant cutover
    Canary      // Gradual rollout
}

public enum DeploymentTarget
{
    ServerVersion,      // New Honua version
    MetadataChange,     // Metadata/config change only
    Both                // Version + metadata
}

public class CanaryConfiguration
{
    public int InitialTrafficPercent { get; set; } = 5;
    public int IncrementPercent { get; set; } = 10;
    public TimeSpan StageInterval { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxTrafficPercent { get; set; } = 100;
    public List<PromotionCriteria> PromotionCriteria { get; set; } = new();
}

public class PromotionCriteria
{
    public string MetricName { get; set; } = string.Empty;
    public double Threshold { get; set; }
    public ComparisonOperator Operator { get; set; }
}

public class RollbackPolicy
{
    public bool AutomaticRollback { get; set; } = true;
    public double MaxErrorRate { get; set; } = 0.05; // 5%
    public double MinSuccessRate { get; set; } = 0.95; // 95%
    public double MaxResponseTimeDegradation { get; set; } = 0.5; // 50% slower
    public TimeSpan ObservationWindow { get; set; } = TimeSpan.FromMinutes(5);
    public int ConsecutiveFailuresBeforeRollback { get; set; } = 3;
}
```

### YARP Traffic Splitting Configuration

```csharp
public class YarpTrafficConfiguration
{
    public string ActiveSlot { get; set; } = "blue";
    public Dictionary<string, int> TrafficWeights { get; set; } = new()
    {
        { "blue", 100 },
        { "green", 0 }
    };
}

// Dynamic YARP configuration
public class YarpConfigurationService
{
    public async Task UpdateTrafficWeightsAsync(Dictionary<string, int> weights)
    {
        var config = new
        {
            ReverseProxy = new
            {
                Routes = new
                {
                    honua_route = new
                    {
                        ClusterId = "honua-cluster",
                        Match = new { Hosts = new[] { "honua.example.com" } }
                    }
                },
                Clusters = new
                {
                    honua_cluster = new
                    {
                        LoadBalancingPolicy = "WeightedRoundRobin",
                        Destinations = new
                        {
                            blue = new
                            {
                                Address = "http://localhost:5000",
                                Weight = weights["blue"]
                            },
                            green = new
                            {
                                Address = "http://localhost:5001",
                                Weight = weights["green"]
                            }
                        }
                    }
                }
            }
        };

        await SaveYarpConfigurationAsync(config);
        await ReloadYarpConfigurationAsync();
    }
}
```

### Canary Rollout Orchestration

```csharp
public class CanaryOrchestrator
{
    public async Task ExecuteCanaryRolloutAsync(
        string targetSlot,
        DeploymentRequest deployment,
        CancellationToken cancellationToken)
    {
        var currentWeight = deployment.CanaryConfig.InitialTrafficPercent;

        while (currentWeight <= deployment.CanaryConfig.MaxTrafficPercent)
        {
            // Update traffic weights
            await _yarpService.UpdateTrafficWeightsAsync(new Dictionary<string, int>
            {
                { "blue", 100 - currentWeight },
                { targetSlot, currentWeight }
            });

            // Monitor for observation window
            await Task.Delay(deployment.CanaryConfig.StageInterval, cancellationToken);

            // Check telemetry
            var metrics = await _telemetryService.GetMetricsAsync(targetSlot, cancellationToken);

            // Evaluate promotion criteria
            if (!EvaluatePromotionCriteria(metrics, deployment.CanaryConfig.PromotionCriteria))
            {
                // Rollback
                await _yarpService.UpdateTrafficWeightsAsync(new Dictionary<string, int>
                {
                    { "blue", 100 },
                    { targetSlot, 0 }
                });

                throw new CanaryRollbackException("Promotion criteria not met");
            }

            // Increment traffic
            currentWeight += deployment.CanaryConfig.IncrementPercent;
        }
    }
}
```

---

## Integration with Existing Agents

### SemanticAgentCoordinator Integration

```csharp
public class SemanticAgentCoordinator
{
    private readonly CertificateManagementAgent _certificateAgent;
    private readonly DnsConfigurationAgent _dnsAgent;
    private readonly BlueGreenDeploymentAgent _deploymentAgent;

    public async Task<AgentStepResult> RouteRequestAsync(string userRequest)
    {
        // Keyword detection
        if (ContainsKeywords(userRequest, "ssl", "certificate", "https", "letsencrypt"))
        {
            return await _certificateAgent.ProcessAsync(userRequest, context, cancellationToken);
        }

        if (ContainsKeywords(userRequest, "dns", "domain", "route53", "cloudflare"))
        {
            return await _dnsAgent.ProcessAsync(userRequest, context, cancellationToken);
        }

        if (ContainsKeywords(userRequest, "deploy", "blue-green", "canary", "rollback"))
        {
            return await _deploymentAgent.ProcessAsync(userRequest, context, cancellationToken);
        }

        // ... existing agent routing
    }
}
```

---

## CLI Commands

### Certificate Management

```bash
# Setup SSL with Let's Encrypt
honua cert setup --domain honua.example.com --email admin@example.com

# Wildcard certificate with DNS-01
honua cert setup --domain *.api.example.com --challenge dns01 --dns-provider route53

# Renew certificates manually
honua cert renew --all

# Check certificate status
honua cert status
```

### DNS Configuration

```bash
# Configure DNS for domain
honua dns setup --domain honua.example.com --ip 203.0.113.5 --provider route53

# Create ACME challenge record
honua dns acme-challenge --domain honua.example.com --token abc123...

# Verify DNS propagation
honua dns verify --domain honua.example.com --record-type A --expected-value 203.0.113.5
```

### Blue-Green Deployment

```bash
# Blue-green deploy
honua deploy --version 1.2.4 --strategy blue-green

# Canary deploy
honua deploy --version 1.2.4 --strategy canary --initial-traffic 5 --max-traffic 100

# Check deployment status
honua deploy status

# Manual promotion
honua deploy promote --slot green

# Rollback
honua deploy rollback
```

---

## Required NuGet Packages

```xml
<!-- ACME/Let's Encrypt -->
<PackageReference Include="Certes" Version="3.0.0" />

<!-- DNS Providers -->
<PackageReference Include="AWSSDK.Route53" Version="3.7.300" />
<PackageReference Include="CloudFlare.Client" Version="1.5.0" />
<PackageReference Include="Azure.ResourceManager.Dns" Version="1.1.0" />
<PackageReference Include="Google.Cloud.Dns.V1" Version="2.5.0" />
<PackageReference Include="DnsClient" Version="1.7.0" />

<!-- YARP -->
<PackageReference Include="Yarp.ReverseProxy" Version="2.1.0" />

<!-- Certificate Storage -->
<PackageReference Include="Azure.Security.KeyVault.Certificates" Version="4.5.0" />
<PackageReference Include="AWSSDK.SecretsManager" Version="3.7.300" />
```

---

## Security Considerations

1. **API Credentials Storage**:
   - Never store credentials in plain text
   - Use Azure Key Vault, AWS Secrets Manager, or environment variables
   - Rotate credentials regularly

2. **ACME Account Key Protection**:
   - Store account key securely (600 permissions)
   - Back up account key to secure location
   - Use separate keys for staging vs production

3. **Certificate Private Key Security**:
   - 600 permissions on certificate files
   - Use hardware security modules (HSM) for production
   - Enable certificate rotation

4. **DNS Provider Permissions**:
   - Use least-privilege IAM roles
   - Restrict DNS API access to specific zones
   - Enable audit logging

5. **YARP Configuration Security**:
   - Validate configuration before applying
   - Enable TLS 1.3 only
   - Disable weak cipher suites

---

## Testing Strategy

1. **Unit Tests**: Mock DNS providers, ACME clients, YARP service
2. **Integration Tests**: Use Let's Encrypt staging environment
3. **E2E Tests**: Full deployment flow with real DNS and certificates (test domains)
4. **Rollback Tests**: Trigger automatic rollback scenarios
5. **Chaos Tests**: Simulate certificate expiry, DNS failures, deployment errors
