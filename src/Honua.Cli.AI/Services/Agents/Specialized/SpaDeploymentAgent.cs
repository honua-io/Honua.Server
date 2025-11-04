// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent for SPA (Single Page Application) deployment scenarios.
/// Handles CORS configuration, subdomain deployment, API Gateway routing, and SPA integration patterns.
/// </summary>
public sealed class SpaDeploymentAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<SpaDeploymentAgent> _logger;

    public SpaDeploymentAgent(
        Kernel kernel,
        ILlmProvider llmProvider,
        ILogger<SpaDeploymentAgent> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes deployment request and generates SPA-specific configuration.
    /// </summary>
    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Analyzing SPA deployment requirements: {Request}", request);

            // Step 1: Detect if this is a SPA deployment
            var spaAnalysis = await AnalyzeSpaRequirementsAsync(request, cancellationToken);

            if (!spaAnalysis.IsSpaDeployment)
            {
                return new AgentStepResult
                {
                    AgentName = "SpaDeployment",
                    Action = "DetectSpa",
                    Success = true,
                    Message = "This does not appear to be a SPA deployment. No SPA-specific configuration needed.",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Step 2: Generate CORS configuration
            var corsConfig = GenerateCorsConfiguration(spaAnalysis);

            // Step 3: Determine deployment architecture
            var architecture = await RecommendDeploymentArchitectureAsync(spaAnalysis, cancellationToken);

            // Step 4: Generate response with configuration
            var responseBuilder = new StringBuilder();
            responseBuilder.AppendLine("## SPA Deployment Configuration");
            responseBuilder.AppendLine();
            responseBuilder.AppendLine($"**Detected SPA Framework:** {spaAnalysis.Framework}");
            responseBuilder.AppendLine();

            // CORS Configuration
            responseBuilder.AppendLine("### CORS Configuration (metadata.json)");
            responseBuilder.AppendLine();
            responseBuilder.AppendLine("Add this to your Honua metadata.json:");
            responseBuilder.AppendLine();
            responseBuilder.AppendLine("```json");
            responseBuilder.AppendLine(corsConfig);
            responseBuilder.AppendLine("```");
            responseBuilder.AppendLine();

            // Architecture Recommendation
            responseBuilder.AppendLine("### Recommended Deployment Architecture");
            responseBuilder.AppendLine();
            responseBuilder.AppendLine($"**Approach:** {architecture.Type}");
            responseBuilder.AppendLine();
            responseBuilder.AppendLine(architecture.Description);
            responseBuilder.AppendLine();

            if (architecture.TerraformTemplate != null)
            {
                responseBuilder.AppendLine("### Terraform Configuration");
                responseBuilder.AppendLine();
                responseBuilder.AppendLine("```hcl");
                responseBuilder.AppendLine(architecture.TerraformTemplate);
                responseBuilder.AppendLine("```");
                responseBuilder.AppendLine();
            }

            // Integration Examples
            if (!spaAnalysis.Framework.IsNullOrEmpty())
            {
                var integrationExample = GenerateIntegrationExample(spaAnalysis.Framework);
                responseBuilder.AppendLine("### SPA Integration Example");
                responseBuilder.AppendLine();
                responseBuilder.AppendLine(integrationExample);
                responseBuilder.AppendLine();
            }

            responseBuilder.AppendLine("### Next Steps");
            responseBuilder.AppendLine("1. Add CORS configuration to metadata.json");
            responseBuilder.AppendLine("2. Deploy infrastructure using recommended architecture");
            responseBuilder.AppendLine("3. Update SPA code to call Honua API endpoints");
            responseBuilder.AppendLine("4. Test cross-origin requests");

            return new AgentStepResult
            {
                AgentName = "SpaDeployment",
                Action = "GenerateSpaConfiguration",
                Success = true,
                Message = responseBuilder.ToString(),
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SPA deployment request");
            return new AgentStepResult
            {
                AgentName = "SpaDeployment",
                Action = "ProcessRequest",
                Success = false,
                Message = $"Error: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<SpaAnalysis> AnalyzeSpaRequirementsAsync(string request, CancellationToken cancellationToken)
    {
        var prompt = $@"Analyze this deployment request for SPA (Single Page Application) characteristics:

Request: {request}

Determine:
1. Is this a SPA deployment? (mentions React, Vue, Angular, Svelte, web app, frontend, UI, CORS, cross-origin, same-domain)
2. Which SPA framework? (React, Vue, Angular, Svelte, or other)
3. Deployment domains: frontend domain and API domain (if mentioned)
4. Is subdomain deployment mentioned? (app.example.com vs api.example.com)
5. Is API Gateway/CloudFront routing mentioned?
6. Expected user traffic (small/medium/large scale)

Respond in JSON:
{{
  ""isSpaDeployment"": true,
  ""framework"": ""React"",
  ""frontendDomain"": ""app.example.com"",
  ""apiDomain"": ""api.example.com"",
  ""subdomainDeployment"": true,
  ""apiGatewayRouting"": false,
  ""scale"": ""medium"",
  ""localDevOrigins"": [""http://localhost:3000""]
}}";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 800,
            Temperature = 0.3
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            _logger.LogError("LLM request failed");
            return new SpaAnalysis
            {
                IsSpaDeployment = false,
                Framework = "Unknown",
                SubdomainDeployment = false,
                ApiGatewayRouting = false
            };
        }

        // Parse JSON
        var jsonStart = response.Content.IndexOf('{');
        var jsonEnd = response.Content.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonStr = response.Content.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var data = JsonSerializer.Deserialize<JsonElement>(jsonStr);

            return new SpaAnalysis
            {
                IsSpaDeployment = data.GetProperty("isSpaDeployment").GetBoolean(),
                Framework = data.TryGetProperty("framework", out var fw) ? fw.GetString() : null,
                FrontendDomain = data.TryGetProperty("frontendDomain", out var fd) ? fd.GetString() : null,
                ApiDomain = data.TryGetProperty("apiDomain", out var ad) ? ad.GetString() : null,
                SubdomainDeployment = data.TryGetProperty("subdomainDeployment", out var sd) && sd.GetBoolean(),
                ApiGatewayRouting = data.TryGetProperty("apiGatewayRouting", out var ag) && ag.GetBoolean(),
                Scale = data.TryGetProperty("scale", out var sc) ? sc.GetString() ?? "medium" : "medium",
                LocalDevOrigins = data.TryGetProperty("localDevOrigins", out var local)
                    ? local.EnumerateArray().Select(o => o.GetString() ?? "").ToList()
                    : new List<string> { "http://localhost:3000" }
            };
        }

        return new SpaAnalysis { IsSpaDeployment = false };
    }

    private string GenerateCorsConfiguration(SpaAnalysis analysis)
    {
        var allowedOrigins = new List<string>();

        if (!analysis.FrontendDomain.IsNullOrEmpty())
        {
            allowedOrigins.Add($"https://{analysis.FrontendDomain}");
        }

        // Add wildcard for dev/staging subdomains if subdomain deployment
        if (analysis.SubdomainDeployment && !analysis.FrontendDomain.IsNullOrEmpty())
        {
            var parts = analysis.FrontendDomain.Split('.');
            if (parts.Length >= 2)
            {
                var rootDomain = string.Join(".", parts.Skip(parts.Length - 2));
                allowedOrigins.Add($"https://*.{rootDomain}");
            }
        }

        // Add local dev origins
        allowedOrigins.AddRange(analysis.LocalDevOrigins ?? new List<string>());

        var config = new
        {
            server = new
            {
                cors = new
                {
                    enabled = true,
                    allowedOrigins = allowedOrigins,
                    allowedMethods = new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH" },
                    allowedHeaders = new[] { "Content-Type", "Authorization", "X-Requested-With", "Accept" },
                    exposedHeaders = new[] { "X-Total-Count", "X-Page-Size", "X-Page-Number" },
                    allowCredentials = true,
                    maxAge = 3600
                }
            }
        };

        return JsonSerializer.Serialize(config, CliJsonOptions.Indented);
    }

    private Task<DeploymentArchitecture> RecommendDeploymentArchitectureAsync(
        SpaAnalysis analysis,
        CancellationToken cancellationToken)
    {
        // If API Gateway routing explicitly requested or large scale, recommend API Gateway
        if (analysis.ApiGatewayRouting || analysis.Scale == "large")
        {
            return Task.FromResult(new DeploymentArchitecture
            {
                Type = "API Gateway with Path Routing",
                Description = @"**Single Domain Architecture**

Deploy SPA and API through a single domain using path-based routing:
- `https://app.example.com/` → SPA (S3/Blob/GCS)
- `https://app.example.com/api/*` → Honua API
- `https://app.example.com/geoservices/*` → Honua GIS services

**Benefits:**
- True same-origin (no CORS needed)
- Single SSL certificate
- CDN caching for SPA
- Professional architecture for large-scale deployments",
                TerraformTemplate = GenerateApiGatewayTerraform(analysis)
            });
        }

        // Default: Subdomain deployment
        return Task.FromResult(new DeploymentArchitecture
        {
            Type = "Subdomain Deployment",
            Description = $@"**Subdomain Architecture**

Deploy SPA and API on separate subdomains:
- SPA: `https://{analysis.FrontendDomain ?? "app.example.com"}`
- API: `https://{analysis.ApiDomain ?? "api.example.com"}`

**Benefits:**
- Clean separation of concerns
- Shared cookies for authentication (same root domain)
- CORS enabled for flexibility
- Standard industry pattern (GitHub, AWS Console, etc.)

**Configuration:**
- Wildcard SSL certificate or separate certificates
- CORS configured with wildcard subdomain support",
            TerraformTemplate = null  // Subdomain is standard cloud deployment
        });
    }

    private string GenerateApiGatewayTerraform(SpaAnalysis analysis)
    {
        return @"# CloudFront Distribution with SPA + API routing
resource ""aws_cloudfront_distribution"" ""spa_unified"" {
  enabled = true
  aliases = [""app.example.com""]

  # Origin: SPA in S3
  origin {
    domain_name = aws_s3_bucket.spa.bucket_regional_domain_name
    origin_id   = ""spa-origin""

    s3_origin_config {
      origin_access_identity = aws_cloudfront_origin_access_identity.spa.cloudfront_access_identity_path
    }
  }

  # Origin: Honua API (ALB)
  origin {
    domain_name = aws_lb.honua.dns_name
    origin_id   = ""api-origin""

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = ""https-only""
    }
  }

  # Default behavior: Serve SPA
  default_cache_behavior {
    target_origin_id       = ""spa-origin""
    viewer_protocol_policy = ""redirect-to-https""
    allowed_methods        = [""GET"", ""HEAD""]
    cached_methods         = [""GET"", ""HEAD""]

    forwarded_values {
      query_string = false
      cookies { forward = ""none"" }
    }

    min_ttl     = 0
    default_ttl = 3600
    max_ttl     = 86400
  }

  # API behavior: Route to Honua
  ordered_cache_behavior {
    path_pattern           = ""/api/*""
    target_origin_id       = ""api-origin""
    viewer_protocol_policy = ""https-only""
    allowed_methods        = [""GET"", ""HEAD"", ""OPTIONS"", ""PUT"", ""POST"", ""PATCH"", ""DELETE""]
    cached_methods         = [""GET"", ""HEAD""]

    # No caching for API
    min_ttl     = 0
    default_ttl = 0
    max_ttl     = 0

    forwarded_values {
      query_string = true
      headers      = [""Authorization"", ""Content-Type"", ""Origin""]
      cookies { forward = ""all"" }
    }
  }

  # GIS services behavior: Cache tiles, don't cache queries
  ordered_cache_behavior {
    path_pattern           = ""/geoservices/*""
    target_origin_id       = ""api-origin""
    viewer_protocol_policy = ""https-only""
    allowed_methods        = [""GET"", ""HEAD"", ""OPTIONS"", ""POST""]
    cached_methods         = [""GET"", ""HEAD""]

    # Cache tiles aggressively
    min_ttl     = 300
    default_ttl = 3600
    max_ttl     = 86400

    forwarded_values {
      query_string = true
      headers      = [""Authorization""]
      cookies { forward = ""none"" }
    }
  }

  viewer_certificate {
    acm_certificate_arn = aws_acm_certificate.app.arn
    ssl_support_method  = ""sni-only""
  }

  restrictions {
    geo_restriction {
      restriction_type = ""none""
    }
  }

  tags = {
    Name    = ""Honua-SPA-Unified""
    Purpose = ""SPA+API-Routing""
  }
}";
    }

    private string GenerateIntegrationExample(string framework)
    {
        return framework?.ToLowerInvariant() switch
        {
            "react" => GenerateReactExample(),
            "vue" => GenerateVueExample(),
            "angular" => GenerateAngularExample(),
            _ => GenerateGenericExample()
        };
    }

    private string GenerateReactExample()
    {
        return @"**React Integration (using axios):**

```javascript
// src/api/honua.js
import axios from 'axios';

const api = axios.create({
  baseURL: process.env.REACT_APP_API_BASE_URL || 'https://api.example.com',
  headers: {
    'Content-Type': 'application/json'
  }
});

// Add auth token to requests
api.interceptors.request.use(config => {
  const token = localStorage.getItem('auth_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Query GIS features
export const queryFeatures = async (layerUrl, where = '1=1') => {
  const response = await api.get(`${layerUrl}/query`, {
    params: { where, outFields: '*', f: 'json' }
  });
  return response.data;
};

export default api;
```

```javascript
// Usage in component
import { queryFeatures } from './api/honua';

const MapComponent = () => {
  useEffect(() => {
    queryFeatures('/geoservices/rest/services/parcels/FeatureServer/0')
      .then(data => console.log(data.features))
      .catch(err => console.error(err));
  }, []);
};
```";
    }

    private string GenerateVueExample()
    {
        return @"**Vue Integration (using fetch + Pinia):**

```javascript
// src/stores/honua.js
import { defineStore } from 'pinia';

export const useHonuaStore = defineStore('honua', {
  state: () => ({
    baseURL: import.meta.env.VITE_API_BASE_URL || 'https://api.example.com'
  }),
  actions: {
    async queryFeatures(layerUrl, where = '1=1') {
      const token = localStorage.getItem('auth_token');
      const url = `${this.baseURL}${layerUrl}/query?where=${where}&outFields=*&f=json`;

      const response = await fetch(url, {
        headers: {
          'Authorization': token ? `Bearer ${token}` : '',
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) throw new Error('Query failed');
      return response.json();
    }
  }
});
```";
    }

    private string GenerateAngularExample()
    {
        return @"**Angular Integration (using HttpClient):**

```typescript
// src/app/services/honua.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

@Injectable({ providedIn: 'root' })
export class HonuaService {
  private baseURL = environment.apiBaseUrl;

  constructor(private http: HttpClient) {}

  queryFeatures(layerUrl: string, where: string = '1=1'): Observable<any> {
    const token = localStorage.getItem('auth_token');
    const headers = new HttpHeaders({
      'Authorization': token ? `Bearer ${token}` : '',
      'Content-Type': 'application/json'
    });

    return this.http.get(`${this.baseURL}${layerUrl}/query`, {
      headers,
      params: { where, outFields: '*', f: 'json' }
    });
  }
}
```";
    }

    private string GenerateGenericExample()
    {
        return @"**Generic JavaScript Integration:**

```javascript
const API_BASE_URL = 'https://api.example.com';

async function queryHonuaFeatures(layerUrl, where = '1=1') {
  const token = localStorage.getItem('auth_token');

  const response = await fetch(
    `${API_BASE_URL}${layerUrl}/query?where=${where}&outFields=*&f=json`,
    {
      headers: {
        'Authorization': token ? `Bearer ${token}` : '',
        'Content-Type': 'application/json'
      }
    }
  );

  if (!response.ok) {
    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
  }

  return response.json();
}

// Usage
queryHonuaFeatures('/geoservices/rest/services/parcels/FeatureServer/0')
  .then(data => console.log(data.features))
  .catch(err => console.error(err));
```";
    }
}

// Supporting types

public sealed class SpaAnalysis
{
    public bool IsSpaDeployment { get; init; }
    public string? Framework { get; init; }
    public string? FrontendDomain { get; init; }
    public string? ApiDomain { get; init; }
    public bool SubdomainDeployment { get; init; }
    public bool ApiGatewayRouting { get; init; }
    public string Scale { get; init; } = "medium";
    public List<string> LocalDevOrigins { get; init; } = new();
}

public sealed class DeploymentArchitecture
{
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? TerraformTemplate { get; init; }
}
