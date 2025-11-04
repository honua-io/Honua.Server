# Azure AI Stack Architecture for Deployment Consultant

## Executive Summary

Leverage Azure free credits to build production-grade AI deployment consultant using Azure's managed AI services.

**Monthly Cost Estimate** (after free credits):
- Azure OpenAI: ~$100-200/month (embeddings + GPT-4)
- Azure AI Search: ~$250/month (Standard tier)
- Azure Monitor: ~$50/month (Application Insights)
- **Total**: ~$400-500/month
- **Free Credits**: $200/month for 12 months (Azure for Startups)

---

## Azure AI Services Stack

```
┌─────────────────────────────────────────────────────────────┐
│                     Customer Request                         │
│              (Natural language requirements)                 │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│               Azure OpenAI Service                           │
│  - GPT-4 Turbo: Requirements extraction, explanations       │
│  - text-embedding-3-large: Vector embeddings                │
│  - Managed endpoint with SLA                                │
└────────────────────┬────────────────────────────────────────┘
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ Semantic     │ │ Azure AI     │ │ Azure        │
│ Kernel       │ │ Search       │ │ Monitor      │
│              │ │              │ │              │
│ - Agent      │ │ - Vector DB  │ │ - App        │
│   orchestr.  │ │ - Hybrid     │ │   Insights   │
│ - Plugin     │ │   search     │ │ - Logs       │
│   execution  │ │ - Filters    │ │ - Metrics    │
└──────────────┘ └──────────────┘ └──────────────┘
                     │
                     ▼
        ┌────────────────────────────┐
        │   Deployment Telemetry     │
        │   (PostgreSQL on Azure)    │
        └────────────────────────────┘
                     │
                     ▼
        ┌────────────────────────────┐
        │   Pattern Analysis         │
        │   (Azure Functions)        │
        └────────────────────────────┘
```

---

## Component Breakdown

### 1. Azure OpenAI Service

**Why Azure OpenAI vs OpenAI API?**
- ✅ Enterprise SLA (99.9% uptime)
- ✅ Private networking (VNet integration)
- ✅ Data residency (stays in Azure region)
- ✅ Enterprise support
- ✅ Microsoft Entra ID (AAD) integration
- ✅ No data used for training (guaranteed)
- ✅ HIPAA/SOC2/ISO compliant

**Configuration**:
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://honua-openai.openai.azure.com/",
    "ApiKey": "from-key-vault",
    "DeploymentName": "gpt-4-turbo",
    "EmbeddingDeploymentName": "text-embedding-3-large",
    "ApiVersion": "2024-02-01"
  }
}
```

**Usage**:
```csharp
using Azure.AI.OpenAI;
using Azure.Identity;

public sealed class AzureOpenAIProvider : ILlmProvider
{
    private readonly OpenAIClient _client;
    private readonly string _deploymentName;
    private readonly string _embeddingDeploymentName;

    public AzureOpenAIProvider(IConfiguration config)
    {
        var endpoint = new Uri(config["AzureOpenAI:Endpoint"]);

        // Use Managed Identity in production, API key for dev
        var credential = new DefaultAzureCredential(); // or new AzureKeyCredential(apiKey)

        _client = new OpenAIClient(endpoint, credential);
        _deploymentName = config["AzureOpenAI:DeploymentName"];
        _embeddingDeploymentName = config["AzureOpenAI:EmbeddingDeploymentName"];
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        var chatOptions = new ChatCompletionsOptions(_deploymentName, new[]
        {
            new ChatRequestSystemMessage("You are a geospatial infrastructure consultant."),
            new ChatRequestUserMessage(request.UserPrompt)
        })
        {
            Temperature = (float)request.Temperature,
            MaxTokens = request.MaxTokens,
            ResponseFormat = ChatCompletionsResponseFormat.Text
        };

        var response = await _client.GetChatCompletionsAsync(chatOptions, ct);
        var choice = response.Value.Choices[0];

        return new LlmResponse
        {
            Success = true,
            Content = choice.Message.Content,
            TokensUsed = response.Value.Usage.TotalTokens,
            Model = _deploymentName
        };
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        var embeddingOptions = new EmbeddingsOptions(_embeddingDeploymentName, new[] { text });
        var response = await _client.GetEmbeddingsAsync(embeddingOptions, ct);

        return response.Value.Data[0].Embedding.ToArray();
    }
}
```

**Cost**:
- GPT-4 Turbo: $10 per 1M input tokens, $30 per 1M output tokens
- text-embedding-3-large: $0.13 per 1M tokens
- Estimate: 10K deployments/month = ~$150-200/month

---

### 2. Azure AI Search (Vector Database)

**Why Azure AI Search vs Pinecone/Weaviate?**
- ✅ Integrated with Azure OpenAI (automatic embeddings)
- ✅ Hybrid search (vector + keyword + filters) in one query
- ✅ Fully managed, auto-scaling
- ✅ Built-in security (VNet, private endpoints)
- ✅ Same region as OpenAI (low latency)
- ✅ Semantic ranking (Microsoft's proprietary algorithm)

**Setup**:
```bash
# Create Azure AI Search resource
az search service create \
  --name honua-search \
  --resource-group honua-rg \
  --sku standard \
  --location westus2

# Get admin key
az search admin-key show \
  --resource-group honua-rg \
  --service-name honua-search
```

**Index Schema**:
```json
{
  "name": "deployment-knowledge",
  "fields": [
    {
      "name": "id",
      "type": "Edm.String",
      "key": true,
      "searchable": false
    },
    {
      "name": "content",
      "type": "Edm.String",
      "searchable": true,
      "analyzer": "en.microsoft"
    },
    {
      "name": "contentVector",
      "type": "Collection(Edm.Single)",
      "searchable": true,
      "vectorSearchDimensions": 3072,
      "vectorSearchProfileName": "myHnswProfile"
    },
    {
      "name": "patternType",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true
    },
    {
      "name": "cloudProvider",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true
    },
    {
      "name": "dataVolumeMin",
      "type": "Edm.Int32",
      "filterable": true,
      "sortable": true
    },
    {
      "name": "dataVolumeMax",
      "type": "Edm.Int32",
      "filterable": true,
      "sortable": true
    },
    {
      "name": "concurrentUsersMin",
      "type": "Edm.Int32",
      "filterable": true
    },
    {
      "name": "concurrentUsersMax",
      "type": "Edm.Int32",
      "filterable": true
    },
    {
      "name": "successRate",
      "type": "Edm.Double",
      "filterable": true,
      "sortable": true
    },
    {
      "name": "deploymentCount",
      "type": "Edm.Int32",
      "filterable": true,
      "sortable": true
    },
    {
      "name": "configuration",
      "type": "Edm.String",
      "searchable": false
    },
    {
      "name": "humanApproved",
      "type": "Edm.Boolean",
      "filterable": true
    },
    {
      "name": "approvedBy",
      "type": "Edm.String",
      "filterable": true
    },
    {
      "name": "approvedDate",
      "type": "Edm.DateTimeOffset",
      "filterable": true,
      "sortable": true
    },
    {
      "name": "version",
      "type": "Edm.Int32",
      "filterable": true,
      "sortable": true
    }
  ],
  "vectorSearch": {
    "profiles": [
      {
        "name": "myHnswProfile",
        "algorithm": "myHnsw",
        "vectorizer": "myOpenAI"
      }
    ],
    "algorithms": [
      {
        "name": "myHnsw",
        "kind": "hnsw",
        "hnswParameters": {
          "m": 4,
          "efConstruction": 400,
          "efSearch": 500,
          "metric": "cosine"
        }
      }
    ],
    "vectorizers": [
      {
        "name": "myOpenAI",
        "kind": "azureOpenAI",
        "azureOpenAIParameters": {
          "resourceUri": "https://honua-openai.openai.azure.com",
          "deploymentId": "text-embedding-3-large",
          "apiKey": "${AZURE_OPENAI_KEY}"
        }
      }
    ]
  },
  "semantic": {
    "configurations": [
      {
        "name": "my-semantic-config",
        "prioritizedFields": {
          "titleField": {
            "fieldName": "content"
          },
          "contentFields": [
            {
              "fieldName": "content"
            }
          ]
        }
      }
    ]
  }
}
```

**Indexing Code**:
```csharp
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;

public sealed class AzureAISearchKnowledgeStore
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly AzureOpenAIProvider _openAIProvider;

    public AzureAISearchKnowledgeStore(
        IConfiguration config,
        AzureOpenAIProvider openAIProvider)
    {
        var endpoint = new Uri(config["AzureAISearch:Endpoint"]);
        var credential = new AzureKeyCredential(config["AzureAISearch:ApiKey"]);

        _indexClient = new SearchIndexClient(endpoint, credential);
        _searchClient = _indexClient.GetSearchClient("deployment-knowledge");
        _openAIProvider = openAIProvider;
    }

    public async Task IndexApprovedPatternAsync(ApprovedPattern pattern)
    {
        // Generate embedding
        var embeddingText = $"""
            Deployment pattern for {pattern.CloudProvider} cloud.
            Data volume: {pattern.Applicability.DataVolumeMin}-{pattern.Applicability.DataVolumeMax}GB.
            Concurrent users: {pattern.Applicability.ConcurrentUsersMin}-{pattern.Applicability.ConcurrentUsersMax}.
            Configuration: {pattern.Configuration.InstanceType}, {pattern.Configuration.DatabaseInstanceType}.
            Success rate: {pattern.Evidence.SuccessRate * 100}% over {pattern.Evidence.DeploymentCount} deployments.
            Cost: ${pattern.Evidence.AvgCost}/month.
            Performance: {pattern.Evidence.AvgPerformance}ms P95.
            """;

        var embedding = await _openAIProvider.GetEmbeddingAsync(embeddingText);

        var document = new SearchDocument
        {
            ["id"] = pattern.Id,
            ["content"] = embeddingText,
            ["contentVector"] = embedding,
            ["patternType"] = "architecture",
            ["cloudProvider"] = pattern.CloudProvider,
            ["dataVolumeMin"] = pattern.Applicability.DataVolumeMin,
            ["dataVolumeMax"] = pattern.Applicability.DataVolumeMax,
            ["concurrentUsersMin"] = pattern.Applicability.ConcurrentUsersMin,
            ["concurrentUsersMax"] = pattern.Applicability.ConcurrentUsersMax,
            ["successRate"] = pattern.Evidence.SuccessRate,
            ["deploymentCount"] = pattern.Evidence.DeploymentCount,
            ["configuration"] = JsonSerializer.Serialize(pattern.Configuration),
            ["humanApproved"] = true,
            ["approvedBy"] = pattern.ApprovedBy,
            ["approvedDate"] = pattern.ApprovedDate,
            ["version"] = pattern.Version
        };

        await _searchClient.UploadDocumentsAsync(new[] { document });
    }

    public async Task<List<PatternMatch>> SearchPatternsAsync(CustomerRequirements requirements)
    {
        // Generate query embedding
        var queryText = $"""
            Need deployment for {requirements.DataVolumeGb}GB data,
            {requirements.ConcurrentUsers} concurrent users,
            on {requirements.CloudProvider},
            budget ${requirements.BudgetMonthly}/month
            """;

        var queryEmbedding = await _openAIProvider.GetEmbeddingAsync(queryText);

        var searchOptions = new SearchOptions
        {
            // Hybrid search: vector + semantic + filters
            VectorSearch = new()
            {
                Queries =
                {
                    new VectorizedQuery(queryEmbedding)
                    {
                        KNearestNeighborsCount = 5,
                        Fields = { "contentVector" }
                    }
                }
            },

            // Semantic ranking (Microsoft's proprietary reranking)
            SemanticSearch = new()
            {
                SemanticConfigurationName = "my-semantic-config",
                QueryCaption = new(QueryCaptionType.Extractive),
                QueryAnswer = new(QueryAnswerType.Extractive)
            },

            // Metadata filters
            Filter = $"""
                humanApproved eq true
                and cloudProvider eq '{requirements.CloudProvider}'
                and dataVolumeMin le {requirements.DataVolumeGb}
                and dataVolumeMax ge {requirements.DataVolumeGb}
                and concurrentUsersMin le {requirements.ConcurrentUsers}
                and concurrentUsersMax ge {requirements.ConcurrentUsers}
                """,

            // Ranking
            OrderBy = { "successRate desc", "deploymentCount desc" },

            Size = 3,
            IncludeTotalCount = true
        };

        var response = await _searchClient.SearchAsync<SearchDocument>(queryText, searchOptions);

        var matches = new List<PatternMatch>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            matches.Add(new PatternMatch
            {
                Id = result.Document["id"].ToString(),
                Content = result.Document["content"].ToString(),
                Configuration = JsonSerializer.Deserialize<DeploymentConfig>(
                    result.Document["configuration"].ToString()),
                SuccessRate = (double)result.Document["successRate"],
                DeploymentCount = (int)result.Document["deploymentCount"],
                VectorScore = result.Score ?? 0,
                SemanticScore = result.SemanticSearch?.RerankerScore ?? 0,
                Captions = result.SemanticSearch?.Captions.Select(c => c.Text).ToList()
            });
        }

        return matches;
    }
}
```

**Cost**:
- Standard tier: ~$250/month (100 indexes, 25GB storage)
- Includes 1M queries/month
- Embeddings auto-generated by Azure OpenAI vectorizer

---

### 3. Azure Monitor + Application Insights

**Why?**
- ✅ Track agent performance (latency, errors, tokens used)
- ✅ Distributed tracing across agents
- ✅ Cost monitoring (OpenAI API usage)
- ✅ Custom metrics (deployment success rate, prediction accuracy)
- ✅ Alerts (high error rate, cost overruns)

**Setup**:
```csharp
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

public sealed class AzureMonitorTelemetry
{
    private readonly TelemetryClient _telemetry;

    public void TrackDeployment(DeploymentTelemetry deployment)
    {
        var properties = new Dictionary<string, string>
        {
            ["deploymentId"] = deployment.DeploymentId,
            ["cloudProvider"] = deployment.Requirements.CloudProvider,
            ["dataVolumeGb"] = deployment.Requirements.DataVolumeGb.ToString(),
            ["success"] = deployment.Outcome.Success.ToString()
        };

        var metrics = new Dictionary<string, double>
        {
            ["costAccuracy"] = deployment.Actuals.CostAccuracyPercent,
            ["performanceAccuracy"] = deployment.Actuals.PerformanceAccuracyPercent,
            ["durationSeconds"] = deployment.Outcome.Duration.TotalSeconds,
            ["customerSatisfaction"] = deployment.Outcome.CustomerSatisfied ? 5.0 : 0.0
        };

        _telemetry.TrackEvent("DeploymentCompleted", properties, metrics);
    }

    public void TrackAgentExecution(AgentStepResult result)
    {
        var dependency = new DependencyTelemetry
        {
            Name = $"Agent:{result.AgentName}",
            Type = "AI Agent",
            Data = result.Action,
            Duration = result.Duration,
            Success = result.Success
        };

        _telemetry.TrackDependency(dependency);
    }

    public void TrackOpenAICall(string model, int tokensUsed, TimeSpan duration, decimal cost)
    {
        _telemetry.TrackMetric("OpenAI.TokensUsed", tokensUsed, new Dictionary<string, string>
        {
            ["model"] = model
        });

        _telemetry.TrackMetric("OpenAI.Cost", (double)cost, new Dictionary<string, string>
        {
            ["model"] = model
        });

        _telemetry.TrackDependency(new DependencyTelemetry
        {
            Name = "Azure OpenAI",
            Type = "LLM",
            Data = model,
            Duration = duration,
            Success = true
        });
    }
}
```

**Kusto Queries**:
```kusto
// Deployment success rate by cloud provider
customEvents
| where name == "DeploymentCompleted"
| summarize
    SuccessRate = avg(todouble(customMeasurements.success)),
    Count = count()
    by cloudProvider = tostring(customDimensions.cloudProvider)
| order by SuccessRate desc

// Cost accuracy trend
customEvents
| where name == "DeploymentCompleted"
| summarize
    AvgCostAccuracy = avg(customMeasurements.costAccuracy)
    by bin(timestamp, 7d)
| render timechart

// OpenAI cost by model
customMetrics
| where name == "OpenAI.Cost"
| summarize TotalCost = sum(value) by model = tostring(customDimensions.model)

// Agent performance (P95 latency)
dependencies
| where type == "AI Agent"
| summarize P95_ms = percentile(duration, 95) by name
| order by P95_ms desc
```

**Cost**:
- ~$50/month for typical usage (5GB logs, 1B data points)

---

### 4. Azure Key Vault

**Why?**
- ✅ Secure storage for API keys, connection strings
- ✅ Automatic rotation
- ✅ Audit logging (who accessed what secret when)
- ✅ Managed Identity integration (no secrets in code)

**Setup**:
```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

public sealed class AzureKeyVaultConfiguration
{
    private readonly SecretClient _secretClient;

    public AzureKeyVaultConfiguration(IConfiguration config)
    {
        var vaultUri = new Uri(config["KeyVault:VaultUri"]);
        var credential = new DefaultAzureCredential();
        _secretClient = new SecretClient(vaultUri, credential);
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        var secret = await _secretClient.GetSecretAsync(secretName);
        return secret.Value.Value;
    }
}
```

**Configuration**:
```json
{
  "KeyVault": {
    "VaultUri": "https://honua-vault.vault.azure.net/"
  },
  "AzureOpenAI": {
    "Endpoint": "https://honua-openai.openai.azure.com/",
    "ApiKey": "from-key-vault:openai-api-key"
  },
  "AzureAISearch": {
    "Endpoint": "https://honua-search.search.windows.net",
    "ApiKey": "from-key-vault:search-admin-key"
  }
}
```

---

### 5. Azure Functions (Pattern Analysis)

**Why Azure Functions for nightly pattern analysis?**
- ✅ Serverless, pay only for execution time
- ✅ Timer trigger (run at 2am daily)
- ✅ Access to PostgreSQL on Azure
- ✅ Integrated with Application Insights
- ✅ ~$0.20/month for nightly job

**Function App**:
```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public sealed class PatternAnalysisFunction
{
    private readonly DeploymentPatternAnalyzer _analyzer;
    private readonly AzureAISearchKnowledgeStore _knowledgeStore;
    private readonly ILogger<PatternAnalysisFunction> _logger;

    [Function("AnalyzeDeploymentPatterns")]
    public async Task RunAsync(
        [TimerTrigger("0 0 2 * * *")] TimerInfo timer) // 2am daily
    {
        _logger.LogInformation("Starting pattern analysis at {Time}", DateTime.UtcNow);

        // 1. Analyze deployment history (SQL queries)
        var report = await _analyzer.AnalyzePatternsAsync();

        // 2. Send report for human review (email/Teams)
        await SendReportForReviewAsync(report);

        _logger.LogInformation("Pattern analysis complete. Found {Count} patterns",
            report.TopPatterns.Count);
    }

    [Function("ApprovePattern")]
    public async Task<HttpResponseData> ApprovePatternAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [FromBody] PatternApprovalRequest approval)
    {
        // Human approved via email link or admin UI
        var pattern = await _analyzer.GetPatternByIdAsync(approval.PatternId);

        if (approval.Approved)
        {
            pattern.HumanApproved = true;
            pattern.ApprovedBy = approval.ApprovedBy;
            pattern.ApprovedDate = DateTime.UtcNow;

            // Index in Azure AI Search
            await _knowledgeStore.IndexApprovedPatternAsync(pattern);

            _logger.LogInformation("Pattern {PatternId} approved and indexed", pattern.Id);
        }

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
```

---

### 6. Azure Database for PostgreSQL

**Why Azure PostgreSQL vs self-hosted?**
- ✅ Managed service (automatic backups, patching)
- ✅ 99.99% SLA
- ✅ Built-in HA (zone redundant)
- ✅ Automatic scaling
- ✅ ~$100/month for Burstable B2s (2 vCore, 4GB RAM)

**Setup**:
```bash
# Create PostgreSQL flexible server
az postgres flexible-server create \
  --name honua-db \
  --resource-group honua-rg \
  --location westus2 \
  --sku-name Standard_B2s \
  --tier Burstable \
  --storage-size 32 \
  --version 16 \
  --admin-user honuaadmin \
  --admin-password <strong-password> \
  --public-access 0.0.0.0 # Or use VNet integration

# Enable pgvector extension (if needed as fallback)
az postgres flexible-server parameter set \
  --resource-group honua-rg \
  --server-name honua-db \
  --name azure.extensions \
  --value vector
```

---

## Cost Summary

| Service | Tier | Monthly Cost | Notes |
|---------|------|--------------|-------|
| Azure OpenAI | Pay-as-you-go | $150-200 | 10K deployments/month |
| Azure AI Search | Standard | $250 | 100 indexes, 25GB |
| Azure Monitor | Pay-as-you-go | $50 | 5GB logs/month |
| Azure PostgreSQL | Burstable B2s | $100 | 2 vCore, 4GB RAM |
| Azure Functions | Consumption | $1 | Nightly job |
| Azure Key Vault | Standard | $3 | Secret storage |
| **Total** | | **~$554/month** | |
| **With Free Credits** | | **$354/month** | $200/month credit for 12 months |

---

## Deployment Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                  Azure Resource Group: honua-rg              │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────────┐      ┌──────────────────┐            │
│  │ Azure OpenAI     │      │ Azure AI Search  │            │
│  │ - GPT-4 Turbo    │◄────►│ - Vector Store   │            │
│  │ - Embeddings     │      │ - Hybrid Search  │            │
│  └──────────────────┘      └──────────────────┘            │
│           │                         │                        │
│           └────────┬────────────────┘                        │
│                    │                                         │
│                    ▼                                         │
│  ┌────────────────────────────────────────────┐            │
│  │  App Service / Container Apps               │            │
│  │  - Honua.Cli.AI                             │            │
│  │  - Semantic Kernel Agents                   │            │
│  │  - Deployment Orchestration                 │            │
│  └────────────────────────────────────────────┘            │
│           │                    │                             │
│           │                    │                             │
│           ▼                    ▼                             │
│  ┌──────────────────┐  ┌──────────────────┐               │
│  │ Azure PostgreSQL │  │ Azure Functions  │               │
│  │ - Telemetry DB   │  │ - Pattern        │               │
│  │ - Deployment     │  │   Analysis       │               │
│  │   History        │  │ - Nightly Jobs   │               │
│  └──────────────────┘  └──────────────────┘               │
│                                                              │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Azure Monitor + Application Insights                 │  │
│  │ - Logs, Metrics, Traces                              │  │
│  │ - Cost Tracking, Alerts                              │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Azure Key Vault                                      │  │
│  │ - API Keys, Connection Strings                       │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## Infrastructure as Code (Bicep)

```bicep
@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment (dev, staging, prod)')
param environment string = 'dev'

// Azure OpenAI
resource openai 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: 'honua-openai-${environment}'
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: 'honua-openai-${environment}'
    publicNetworkAccess: 'Enabled'
  }
}

// Deploy GPT-4 Turbo
resource gpt4Deployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openai
  name: 'gpt-4-turbo'
  sku: {
    name: 'Standard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4'
      version: '0125-preview'
    }
  }
}

// Deploy Embeddings
resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openai
  name: 'text-embedding-3-large'
  sku: {
    name: 'Standard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-large'
      version: '1'
    }
  }
  dependsOn: [gpt4Deployment]
}

// Azure AI Search
resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: 'honua-search-${environment}'
  location: location
  sku: {
    name: 'standard'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    semanticSearch: 'standard' // Enable semantic ranking
  }
}

// PostgreSQL Flexible Server
resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-03-01-preview' = {
  name: 'honua-db-${environment}'
  location: location
  sku: {
    name: 'Standard_B2s'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: 'honuaadmin'
    administratorLoginPassword: '<from-key-vault>'
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'honua-insights-${environment}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: 'honua-vault-${environment}'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    enableRbacAuthorization: true
  }
}

// Store secrets in Key Vault
resource openaiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'openai-api-key'
  properties: {
    value: openai.listKeys().key1
  }
}

resource searchKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'search-admin-key'
  properties: {
    value: search.listAdminKeys().primaryKey
  }
}

// Outputs
output openaiEndpoint string = openai.properties.endpoint
output searchEndpoint string = 'https://${search.name}.search.windows.net'
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output keyVaultUri string = keyVault.properties.vaultUri
```

**Deploy**:
```bash
az deployment group create \
  --resource-group honua-rg \
  --template-file azure-infrastructure.bicep \
  --parameters environment=dev
```

---

## Managed Identity Setup

**Why Managed Identity?**
- ✅ No secrets in code or config
- ✅ Automatic credential rotation
- ✅ RBAC-based access control

**Setup**:
```bash
# Create managed identity
az identity create \
  --name honua-identity \
  --resource-group honua-rg

# Grant access to Azure OpenAI
az role assignment create \
  --assignee <managed-identity-client-id> \
  --role "Cognitive Services OpenAI User" \
  --scope /subscriptions/<sub-id>/resourceGroups/honua-rg/providers/Microsoft.CognitiveServices/accounts/honua-openai-dev

# Grant access to Azure AI Search
az role assignment create \
  --assignee <managed-identity-client-id> \
  --role "Search Index Data Contributor" \
  --scope /subscriptions/<sub-id>/resourceGroups/honua-rg/providers/Microsoft.Search/searchServices/honua-search-dev

# Grant access to Key Vault
az role assignment create \
  --assignee <managed-identity-client-id> \
  --role "Key Vault Secrets User" \
  --scope /subscriptions/<sub-id>/resourceGroups/honua-rg/providers/Microsoft.KeyVault/vaults/honua-vault-dev
```

**Use in Code**:
```csharp
using Azure.Identity;

// DefaultAzureCredential automatically uses Managed Identity in production
var credential = new DefaultAzureCredential();

var openAIClient = new OpenAIClient(
    new Uri("https://honua-openai.openai.azure.com/"),
    credential);

var searchClient = new SearchClient(
    new Uri("https://honua-search.search.windows.net"),
    "deployment-knowledge",
    credential);
```

---

## Monitoring Dashboard

**Azure Dashboard** (JSON export):
```json
{
  "lenses": {
    "0": {
      "order": 0,
      "parts": {
        "0": {
          "position": { "x": 0, "y": 0, "colSpan": 6, "rowSpan": 4 },
          "metadata": {
            "type": "Extension/HubsExtension/PartType/MonitorChartPart",
            "settings": {
              "content": {
                "options": {
                  "chart": {
                    "metrics": [
                      {
                        "resourceMetadata": { "id": "/subscriptions/.../honua-insights" },
                        "name": "customMetrics/OpenAI.TokensUsed",
                        "aggregationType": 7
                      }
                    ],
                    "title": "OpenAI Token Usage",
                    "titleKind": 1
                  }
                }
              }
            }
          }
        },
        "1": {
          "position": { "x": 6, "y": 0, "colSpan": 6, "rowSpan": 4 },
          "metadata": {
            "type": "Extension/HubsExtension/PartType/MonitorChartPart",
            "settings": {
              "content": {
                "options": {
                  "chart": {
                    "metrics": [
                      {
                        "resourceMetadata": { "id": "/subscriptions/.../honua-insights" },
                        "name": "customEvents/DeploymentCompleted",
                        "aggregationType": 7
                      }
                    ],
                    "title": "Deployments per Day"
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}
```

---

## Azure for Startups Credit Tiers

### Tier 1: Founders Hub (Standard)
- **Credits**: $200/month for 12 months
- **Total**: $2,400 over 12 months
- **Requirements**: Early-stage startup, any funding level

### Tier 2: Microsoft for Startups (Recommended for You)
- **Credits**: $1,000-5,000/month for 12 months
- **Total**: $12,000-60,000 over 12 months
- **Requirements**:
  - Pre-seed to Series A
  - Building B2B SaaS
  - Third-party validation (accelerator, VC, etc.)
- **Apply**: https://www.microsoft.com/en-us/startups

### Tier 3: Microsoft for Startups (Enterprise)
- **Credits**: $25,000-150,000/year
- **Total**: Up to $150,000
- **Requirements**:
  - Series A+ funding
  - Significant revenue or rapid growth
  - Strategic partnership potential

### Your Positioning (Tier 2 Application Strategy)

**Why you qualify for Tier 2 ($1K-5K/month)**:

1. **B2B SaaS**: Enterprise geospatial infrastructure
2. **Competitive Advantage**: Disrupting $100K+ consulting market with AI
3. **Technical Depth**: 155K+ lines of production code, OGC-compliant
4. **Microsoft Stack**: Committed to Azure AI, .NET, C#
5. **Market Validation**: Targeting Esri's enterprise customers

**Application Tips**:

**Elevator Pitch** (use in application):
> "HonuaIO replaces $50-150K geospatial deployment consultants with an AI agent
> that designs, deploys, and optimizes infrastructure in hours instead of weeks.
> We're disrupting the $1B+ geospatial consulting market by democratizing
> enterprise GIS infrastructure for mid-market organizations. Built on Azure AI
> (OpenAI, AI Search), targeting $1M ARR in 18 months."

**Emphasize Azure Commitment**:
- 100% Azure AI stack (OpenAI, AI Search, Monitor)
- .NET/C# codebase (Microsoft's ecosystem)
- Plan to scale to 1,000+ deployments/month
- Each deployment generates ~$50-100 in Azure consumption

**Third-Party Validation** (if you have any):
- Accelerator participation
- Angel investors
- Enterprise customer LOIs
- GitHub stars/community traction

**Technical Architecture** (attach AZURE_AI_ARCHITECTURE.md):
- Shows sophisticated use of Azure services
- Demonstrates you'll be a high-value customer
- Proves technical capability to execute

---

## Projected Azure Spend with Credits

### Scenario 1: $1,000/month credits (12 months = $12K)

**Months 1-12**: Fully covered + $400-500/month surplus
- Development, testing, initial customers
- Can experiment freely with GPT-4, Azure AI Search
- No out-of-pocket costs

**Months 13+**: $554/month out-of-pocket
- By then, you should have revenue to cover costs

### Scenario 2: $5,000/month credits (12 months = $60K)

**What $5K/month buys**:
- Base infrastructure: $554/month
- **Surplus for growth**: $4,446/month

**Scaling headroom**:
- 10x more deployments (100K/month)
- Premium tier Azure AI Search ($2,500/month)
- Multi-region deployment
- Development + staging + production environments
- **Estimated**: Support 50-100 paying customers

**Break-even**:
- At $20K MRR (10 customers × $2K), you cover costs after credits expire

### Scenario 3: $25,000/year credits (aggressive tier)

**What $25K/year buys**:
- Full production infrastructure
- Multi-region HA deployment
- Dedicated support
- Can run 12-18 months without paying Azure bills
- Gives you runway to reach $50K+ MRR before credits expire

---

## Cost Optimization Strategy

### Phase 1: Development (Months 1-3)
**Monthly Cost**: ~$200
- Dev tier OpenAI (lower capacity)
- Basic tier AI Search ($75/month)
- Burstable PostgreSQL
- Single region

### Phase 2: Beta Testing (Months 4-6)
**Monthly Cost**: ~$400
- Standard OpenAI deployment
- Standard AI Search
- 10-20 beta customers

### Phase 3: Production (Months 7-12)
**Monthly Cost**: ~$554 (from original estimate)
- Full production stack
- 50-100 customers
- Multi-AZ (not multi-region yet)

### Phase 4: Scale (Months 13-18)
**Monthly Cost**: ~$2,000-5,000
- Multi-region deployment
- Premium AI Search tier
- Reserved instances (40% discount)
- 500+ customers

**Revenue Required to Cover**:
- 10 customers at $200/month = $2,000 MRR (break-even)
- 50 customers at $100/month = $5,000 MRR (profitable)

---

## Application Strategy for Tier 2

### 1. Quantify Azure Consumption Potential

**In Application, State**:
> "We estimate each customer deployment will generate $30-50 in Azure consumption
> (OpenAI API calls, AI Search queries, storage). At our target of 1,000
> deployments/month by Month 18, we project $30-50K/month in Azure consumption,
> with potential to grow to $200K+/month as we scale to enterprise customers."

**Math**:
- 1 deployment = 100K tokens (GPT-4) + 50 embedding calls + 1,000 search queries
- GPT-4: $1.50/deployment
- Embeddings: $0.10/deployment
- AI Search: $0.05/deployment
- Total: ~$1.65/deployment in marginal costs
- At 1,000 deployments/month = $1,650/month marginal
- Plus fixed infrastructure = ~$2,200/month total

**But customers also deploy infrastructure**:
- If 50% choose Azure (vs AWS): 500 customers × $2,500/month avg = **$1.25M/month Azure consumption** through your platform

**This is the key**: You're not just using Azure AI, you're driving Azure infrastructure adoption.

### 2. Highlight Microsoft Alignment

**Strategic Alignment**:
- You're competing with Esri (Oracle-based)
- You're pushing customers to Azure instead of AWS
- You're a .NET shop (Microsoft's ecosystem)
- You're using cutting-edge Azure AI (OpenAI, AI Search)

**In Application**:
> "HonuaIO is a force multiplier for Azure adoption. We're targeting Esri
> customers who currently deploy on-premises or AWS. By making Azure-based
> geospatial infrastructure accessible through AI automation, we expect to
> drive $1M+ in monthly Azure consumption within 18 months, primarily through
> customer infrastructure deployments (VM, Database, Storage)."

### 3. Growth Projections

**18-Month Roadmap** (attach to application):

| Quarter | Customers | Deployments/Month | Azure Consumption | Revenue |
|---------|-----------|-------------------|-------------------|---------|
| Q1 (1-3 months) | 5 beta | 10 | $2K/month | $0 (beta) |
| Q2 (4-6 months) | 25 | 100 | $10K/month | $2.5K MRR |
| Q3 (7-9 months) | 100 | 500 | $50K/month | $10K MRR |
| Q4 (10-12 months) | 250 | 1,500 | $150K/month | $25K MRR |
| Q5 (13-15 months) | 500 | 3,000 | $300K/month | $50K MRR |
| Q6 (16-18 months) | 1,000 | 5,000 | $500K/month | $100K MRR |

**Note**: Azure consumption = customer infrastructure spend, not just your AI costs

### 4. Attach Supporting Materials

**What to Include**:
1. **AZURE_AI_ARCHITECTURE.md** - Shows technical depth
2. **AI_CONSULTANT_MVP.md** - Shows product roadmap
3. **GitHub repo** (if public) - Shows real traction
4. **Demo video** - Shows working product
5. **Customer testimonials** - Even from beta users

---

## Expected Outcome

**Most Likely**: $1,000-2,000/month credits
- Covers all costs for 12 months
- Gives you runway to reach $10-20K MRR
- No out-of-pocket Azure spend during critical growth phase

**Best Case**: $5,000/month credits
- Covers all costs + scaling headroom
- Can support 100+ customers on free credits
- Reach profitability before credits expire

**Stretch**: $25K/year or higher
- Basically free Azure for 12-18 months
- Can focus 100% on sales/product, zero infra costs
- Ideal scenario

---

## What to Do After Approval

### Month 1: Infrastructure Setup
```bash
# Deploy full production stack
az deployment group create \
  --resource-group honua-prod \
  --template-file azure-infrastructure.bicep \
  --parameters environment=prod

# Set up cost alerts
az monitor metrics alert create \
  --name "Azure-Cost-Alert" \
  --resource-group honua-prod \
  --condition "total AzureConsumption > $4000" \
  --description "Alert when monthly spend exceeds 80% of credits"
```

### Month 1-3: Optimize for Credits
- Use dev/test pricing where available
- Enable auto-shutdown for dev resources
- Use spot instances for non-critical workloads
- Monitor daily spend in Cost Management

### Month 6: Mid-Point Review
- Analyze actual vs projected consumption
- Identify cost optimization opportunities
- Plan for post-credit sustainability
- Consider reserved instances if exceeding credits

### Month 10: Transition Planning
- Calculate actual cost per customer
- Ensure MRR covers Azure costs + margin
- Lock in reserved instance pricing (40% off)
- Plan for credit expiration

---

## Next Steps

1. **Apply for Microsoft for Startups** (Tier 2)
   - https://www.microsoft.com/en-us/startups
   - Use elevator pitch above
   - Attach AZURE_AI_ARCHITECTURE.md
   - Emphasize $500K+ Azure consumption potential

2. **Deploy Infrastructure**:
   ```bash
   az deployment group create \
     --resource-group honua-rg \
     --template-file azure-infrastructure.bicep
   ```

3. **Configure Honua.Cli.AI**:
   - Update appsettings.json with Azure endpoints
   - Test Azure OpenAI connection
   - Create Azure AI Search index
   - Run first deployment with telemetry

4. **Set up Monitoring**:
   - Create Application Insights dashboard
   - Configure alerts (cost, errors, latency)
   - Set up budget alerts

5. **Cost Optimization**:
   - Use reserved instances for PostgreSQL (-40%)
   - Use spot instances for development
   - Enable auto-shutdown for dev resources

---

## References

- [Azure OpenAI Service](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Azure AI Search Vector Search](https://learn.microsoft.com/en-us/azure/search/vector-search-overview)
- [Semantic Kernel with Azure](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
- [Azure Monitor Cost Optimization](https://learn.microsoft.com/en-us/azure/azure-monitor/best-practices-cost)
