# Azure AI Integration Example

This example demonstrates how to use the Honua Azure AI integration for deployment pattern search and LLM-powered recommendations.

## Prerequisites

1. **Azure Resources** (deployed via Terraform):
   - Azure OpenAI Service with GPT-4 deployment
   - Azure OpenAI Service with text-embedding-3-large deployment
   - Azure AI Search service with deployment-knowledge index
   - PostgreSQL database

2. **Configuration**:
   - Copy `appsettings.json` and update with your Azure credentials
   - Or set environment variables:
     ```bash
     export LlmProvider__Azure__EndpointUrl="https://YOUR-RESOURCE.openai.azure.com/"
     export LlmProvider__Azure__ApiKey="YOUR-API-KEY"
     export AzureAISearch__Endpoint="https://YOUR-SEARCH.search.windows.net"
     export AzureAISearch__ApiKey="YOUR-SEARCH-KEY"
     ```

## Running the Example

### Option 1: Using dotnet run

```bash
cd examples/AzureAI
dotnet run
```

### Option 2: Build and run

```bash
cd examples/AzureAI
dotnet build
dotnet bin/Debug/net9.0/AzureAI.dll
```

## What This Example Does

### 1. Pattern Search Flow

The example demonstrates the complete pattern search workflow:

```
User Requirements (500GB, 1000 users, AWS, us-west-2)
    ↓
Generate embedding for requirements
    ↓
Hybrid search in Azure AI Search
    ↓
Top 3 matching patterns (ranked by success rate)
    ↓
LLM explains why the top pattern is a good match
```

### 2. Health Checks

Verifies connectivity to all Azure services:

- **Azure OpenAI** - Chat completions endpoint
- **Azure Embeddings** - Vector embedding endpoint
- **Azure AI Search** - Knowledge base index

Each health check reports:
- Status (Healthy/Degraded/Unhealthy)
- Response time
- Configuration details
- Error messages (if any)

## Expected Output

### When Knowledge Base is Empty

```
=== Pattern Search Example ===

Searching for patterns matching:
  Data Volume: 500GB
  Concurrent Users: 1000
  Cloud: aws
  Region: us-west-2

No matching patterns found.
This is expected if the knowledge base is empty.
Patterns are added through the approval workflow.

=== Health Check Example ===

Overall Status: Degraded
Total Duration: 1234ms

azure_openai:
  Status: Healthy
  Description: Azure OpenAI is available
  Data:
    provider: AzureOpenAI
    model: gpt-4o

azure_embeddings:
  Status: Healthy
  Description: Azure embedding service is available
  Data:
    provider: AzureOpenAI
    model: text-embedding-3-large
    dimensions: 3072

azure_ai_search:
  Status: Degraded
  Description: Azure AI Search index does not exist
  Data:
    error: Index not found
    status: 404
```

### When Patterns Exist

```
=== Pattern Search Example ===

Searching for patterns matching:
  Data Volume: 500GB
  Concurrent Users: 1000
  Cloud: aws
  Region: us-west-2

Found 3 matching pattern(s):

Pattern: AWS High-Volume Standard
  Success Rate: 95.0%
  Deployments: 23
  Cloud: aws
  Match Score: 0.92
  Configuration: {"instanceType":"t3.xlarge","storageType":"gp3","cacheEnabled":true}

Pattern: AWS Medium-Scale Production
  Success Rate: 91.0%
  Deployments: 18
  Cloud: aws
  Match Score: 0.87
  Configuration: {"instanceType":"t3.large","storageType":"gp3","cacheEnabled":false}

LLM Explanation:
This AWS High-Volume Standard pattern is an excellent match for your requirements
because it's been successfully deployed 23 times with a 95% success rate in similar
scenarios. The t3.xlarge instance type provides sufficient compute capacity for
1000 concurrent users, while gp3 storage offers the performance needed for 500GB
of imagery data. The enabled caching layer significantly improves response times
for frequently accessed imagery, which is critical for user experience at this scale.

(Used 156 tokens)
```

## Code Walkthrough

### Service Registration

```csharp
// Register all Azure AI services with one line
builder.Services.AddAzureAI(builder.Configuration);

// Or register individually for fine-grained control
builder.Services
    .AddAzureOpenAI(builder.Configuration)      // LLM provider only
    .AddAzureEmbeddings(builder.Configuration)  // Embedding provider only
    .AddAzureKnowledgeStore()                    // Search functionality
    .AddPatternApprovalService();                // Approval workflow

// Add health checks
builder.Services.AddHealthChecks()
    .AddAzureAIHealthChecks();
```

### Pattern Search

```csharp
var knowledgeStore = services.GetRequiredService<AzureAISearchKnowledgeStore>();

var requirements = new DeploymentRequirements
{
    DataVolumeGb = 500,
    ConcurrentUsers = 1000,
    CloudProvider = "aws",
    Region = "us-west-2"
};

var matches = await knowledgeStore.SearchPatternsAsync(requirements);
// Returns top 3 patterns ranked by success rate
```

### LLM Chat

```csharp
var llmFactory = services.GetRequiredService<ILlmProviderFactory>();
var provider = llmFactory.CreateProvider("azure");

var request = new LlmRequest
{
    SystemPrompt = "You are a helpful deployment consultant.",
    UserPrompt = "Explain this deployment pattern...",
    Temperature = 0.2,
    MaxTokens = 500
};

var response = await provider.CompleteAsync(request);
Console.WriteLine(response.Content);
```

## Next Steps

1. **Deploy Infrastructure**: Use Terraform configs in `infrastructure/terraform/azure/`
2. **Create Index**: Run `src/Honua.Cli.AI/Database/create-search-index.sh`
3. **Seed Patterns**: Approve patterns via `PatternApprovalService`
4. **Monitor Health**: Integrate health checks into your monitoring

## Troubleshooting

### "Azure OpenAI endpoint not configured"

Update `appsettings.json` or set environment variable:
```bash
export LlmProvider__Azure__EndpointUrl="https://YOUR-RESOURCE.openai.azure.com/"
```

### "Azure AI Search index not found"

Create the index using:
```bash
cd src/Honua.Cli.AI/Database
./create-search-index.sh
```

### "No matching patterns found"

The knowledge base is empty. Patterns are added through the approval workflow:

1. Deploy services → Telemetry collected
2. SQL analyzes patterns → Creates recommendations
3. Human reviews → Approves patterns
4. Patterns indexed → Available for search

## Architecture

```
┌─────────────────────────────────────────┐
│           User Application              │
│  (This Example or Your Application)     │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│      Dependency Injection Container     │
│                                         │
│  • ILlmProviderFactory                 │
│  • IEmbeddingProvider                  │
│  • AzureAISearchKnowledgeStore         │
│  • PatternApprovalService              │
│  • Health Checks                       │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│          Azure Services                 │
│                                         │
│  Azure OpenAI (Chat + Embeddings)      │
│  Azure AI Search (Knowledge Base)      │
│  PostgreSQL (Telemetry + Approvals)    │
└─────────────────────────────────────────┘
```

## Learn More

- [Azure AI Implementation Guide](../../docs/AZURE_AI_IMPLEMENTATION.md)
- [Architecture Overview](../../docs/AZURE_AI_ARCHITECTURE.md)
- [MVP Specification](../../docs/AI_CONSULTANT_MVP.md)
- [Terraform Configs](../../infrastructure/terraform/azure/)
