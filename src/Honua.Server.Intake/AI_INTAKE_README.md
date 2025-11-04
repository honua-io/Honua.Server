# AI-Powered Intake System

## Overview

The AI-powered intake system guides customers through configuring their custom Honua server build using conversational AI (OpenAI GPT-4 or Anthropic Claude). It automatically:

1. Asks customers about their requirements (protocols, databases, cloud platform)
2. Recommends optimal configurations (suggests ARM64 for cost savings)
3. Generates build manifests automatically
4. Provisions registry credentials
5. Triggers builds

## Architecture

### Components

```
┌─────────────────────────────────────────────────────────────┐
│                     IntakeController                         │
│                   (REST API Endpoints)                       │
└──────────────┬──────────────────────────────────────────────┘
               │
               ├─── POST /api/intake/start
               ├─── POST /api/intake/message
               ├─── POST /api/intake/build
               └─── GET  /api/intake/builds/{buildId}/status
               │
       ┌───────┴────────┐
       │                │
┌──────▼──────┐  ┌─────▼──────────┐
│ IntakeAgent │  │ ManifestGen.   │
│   (AI)      │  │                │
└──────┬──────┘  └────────────────┘
       │
       ├─── OpenAI GPT-4
       ├─── Anthropic Claude
       │
┌──────▼──────────┐
│ ConversationStore│
│   (PostgreSQL)   │
└──────────────────┘
```

### Services

- **IntakeAgent**: Main AI conversation agent that manages customer interactions
- **ManifestGenerator**: Converts AI-extracted requirements into build manifests
- **ConversationStore**: PostgreSQL storage for conversation history
- **IntakeController**: REST API for client applications

## Getting Started

### 1. Prerequisites

- PostgreSQL database
- OpenAI API key OR Anthropic API key
- .NET 9.0 SDK

### 2. Database Setup

Create the conversation storage table:

```sql
CREATE TABLE intake_conversations (
    conversation_id TEXT PRIMARY KEY,
    customer_id TEXT,
    messages_json JSONB NOT NULL,
    status TEXT NOT NULL,
    requirements_json JSONB,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ
);

CREATE INDEX idx_intake_conversations_customer_id
    ON intake_conversations(customer_id)
    WHERE customer_id IS NOT NULL;

CREATE INDEX idx_intake_conversations_status
    ON intake_conversations(status);

CREATE INDEX idx_intake_conversations_created_at
    ON intake_conversations(created_at DESC);
```

Or use the automatic initialization:

```csharp
var conversationStore = serviceProvider.GetRequiredService<IConversationStore>() as ConversationStore;
await conversationStore?.InitializeDatabaseAsync();
```

### 3. Configuration

Add to your `appsettings.json`:

```json
{
  "IntakeAgent": {
    "Provider": "openai",
    "OpenAIApiKey": "sk-your-api-key",
    "OpenAIModel": "gpt-4-turbo-preview",
    "MaxTokens": 2000,
    "Temperature": 0.7,
    "ConnectionString": "Host=localhost;Database=honua;Username=postgres;Password=postgres"
  }
}
```

**For Anthropic Claude:**

```json
{
  "IntakeAgent": {
    "Provider": "anthropic",
    "AnthropicApiKey": "sk-ant-your-api-key",
    "AnthropicModel": "claude-3-opus-20240229",
    "MaxTokens": 2000,
    "Temperature": 0.7,
    "ConnectionString": "Host=localhost;Database=honua;Username=postgres;Password=postgres"
  }
}
```

### 4. Dependency Injection

Register services in your `Program.cs` or `Startup.cs`:

```csharp
using Honua.Server.Intake;

// Option 1: Use configuration
services.AddAIIntakeAgent(configuration);

// Option 2: Configure explicitly
services.AddAIIntakeAgent(options =>
{
    options.Provider = "openai";
    options.OpenAIApiKey = "sk-your-key";
    options.ConnectionString = "Host=localhost;Database=honua;...";
});

// Option 3: Add complete intake system
services.AddCompleteIntakeSystem(configuration);
```

## API Usage

### Starting a Conversation

```http
POST /api/intake/start
Content-Type: application/json

{
  "customerId": "customer-123"
}
```

**Response:**

```json
{
  "conversationId": "conv-abc123",
  "initialMessage": "Hello! I'm here to help you configure your custom Honua Server build...",
  "startedAt": "2024-10-29T18:00:00Z",
  "customerId": "customer-123"
}
```

### Sending Messages

```http
POST /api/intake/message
Content-Type: application/json

{
  "conversationId": "conv-abc123",
  "message": "We need to publish GIS data from PostgreSQL using OGC APIs"
}
```

**Response (ongoing):**

```json
{
  "conversationId": "conv-abc123",
  "message": "Great! OGC APIs are perfect for modern GIS applications. A few questions to help me recommend the right configuration:\n\n1. What cloud provider will you be deploying to (AWS, Azure, GCP, or on-premises)?\n2. Approximately how many concurrent users do you expect?\n3. Do you need any additional protocols like WFS, WMS, or ESRI REST API?",
  "intakeComplete": false,
  "timestamp": "2024-10-29T18:01:00Z"
}
```

**Response (complete):**

```json
{
  "conversationId": "conv-abc123",
  "message": "Perfect! I have all the information I need. Here's my recommendation:\n\n**Recommended Tier**: Pro ($499/month)\n**Architecture**: ARM64 (Graviton)\n**Cloud**: AWS us-east-1\n**Instance**: t4g.medium ($24/month)\n**Estimated Total**: $523/month\n\nWith ARM64, you'll save about 40% compared to x64 instances. Ready to proceed with the build?",
  "intakeComplete": true,
  "requirements": {
    "protocols": ["ogc-api", "wfs"],
    "databases": ["postgresql"],
    "cloudProvider": "aws",
    "architecture": "linux-arm64",
    "tier": "pro",
    "load": {
      "concurrentUsers": 20,
      "requestsPerSecond": 100,
      "classification": "light"
    }
  },
  "estimatedMonthlyCost": 523,
  "costBreakdown": {
    "license": 499,
    "infrastructure": 24,
    "storage": 10
  },
  "timestamp": "2024-10-29T18:05:00Z"
}
```

### Triggering a Build

```http
POST /api/intake/build
Content-Type: application/json

{
  "conversationId": "conv-abc123",
  "customerId": "customer-123",
  "buildName": "acme-corp-gis-server"
}
```

**Response:**

```json
{
  "success": true,
  "buildId": "build-xyz789",
  "manifest": {
    "version": "1.0",
    "name": "acme-corp-gis-server",
    "architecture": "linux-arm64",
    "modules": ["Core", "Api", "OgcApi", "Wfs"],
    "databaseConnectors": ["PostgreSQL"],
    "cloudTargets": [{
      "provider": "aws",
      "region": "us-east-1",
      "instanceType": "t4g.medium",
      "registryUrl": "ecr.aws"
    }],
    "resources": {
      "minCpu": 1.0,
      "minMemoryGb": 2.0,
      "recommendedCpu": 2.0,
      "recommendedMemoryGb": 4.0,
      "storageGb": 50
    },
    "tier": "pro",
    "generatedAt": "2024-10-29T18:06:00Z"
  },
  "registryResult": {
    "success": true,
    "registryType": "AwsEcr",
    "customerId": "customer-123",
    "namespace": "honua/customer-123",
    "credential": {
      "registryUrl": "123456789012.dkr.ecr.us-east-1.amazonaws.com",
      "username": "AWS",
      "password": "eyJw...",
      "expiresAt": "2024-10-30T18:06:00Z"
    }
  },
  "triggeredAt": "2024-10-29T18:06:00Z"
}
```

### Checking Build Status

```http
GET /api/intake/builds/build-xyz789/status
```

**Response:**

```json
{
  "buildId": "build-xyz789",
  "status": "building",
  "progress": 45,
  "currentStage": "Building container image",
  "startedAt": "2024-10-29T18:06:00Z"
}
```

## AI Conversation Flow

### System Prompt

The AI agent uses a comprehensive system prompt that defines:

1. **Role**: Technical consultant for Honua Server
2. **Information to gather**:
   - Protocols/APIs needed
   - Data sources (databases)
   - Cloud provider
   - Architecture (ARM64 vs x64)
   - Expected load
   - Advanced features
3. **Tier recommendations**:
   - Core (Free)
   - Pro ($499/month)
   - Enterprise ($2,500/month)
   - Enterprise ASP (Custom)
4. **Cost optimization guidance**: Always recommend ARM64 unless x64 is required

### Example Conversation

**User**: "We need to publish GIS data from PostgreSQL"

**AI**: "Great! I can help you set up a Honua server for PostgreSQL. A few questions:

1. What APIs do your clients need? (OGC API, ESRI REST, WFS, WMS, etc.)
2. Where will you deploy? (AWS, Azure, GCP, on-premises)
3. How many concurrent users do you expect?"

**User**: "OGC API and WFS. We'll use AWS. About 20 concurrent users."

**AI**: "Perfect! Based on your needs, I recommend:

- **Tier**: Pro ($499/month) - includes OGC API, WFS, and commercial support
- **Architecture**: ARM64 (Graviton) - saves 40% on compute costs
- **Instance**: t4g.medium ($24/month)
- **Total**: $523/month

With ARM64 Graviton instances, you'll get excellent performance at a fraction of the cost. Sound good?"

**User**: "Yes, let's do it"

**AI**: *[Calls complete_intake function with extracted requirements]*

"Excellent! I've captured all your requirements. Click 'Trigger Build' to start provisioning your custom Honua server."

## Cost Optimization

The AI agent automatically recommends ARM64 architecture for cost savings:

### AWS Savings (ARM64 vs x64)

| Load      | ARM64 Instance | x64 Instance | Monthly Savings |
|-----------|----------------|--------------|-----------------|
| Light     | t4g.medium ($24) | t3.medium ($40) | $16 (40%) |
| Moderate  | c7g.large ($62) | c6i.large ($96) | $34 (35%) |
| Heavy     | c7g.xlarge ($124) | c6i.xlarge ($192) | $68 (35%) |

### Azure Savings (ARM64 vs x64)

| Load      | ARM64 Instance | x64 Instance | Monthly Savings |
|-----------|----------------|--------------|-----------------|
| Light     | D2ps_v5 ($30) | D2s_v5 ($45) | $15 (33%) |
| Moderate  | D4ps_v5 ($60) | D4s_v5 ($90) | $30 (33%) |

### GCP Savings (ARM64 vs x64)

| Load      | ARM64 Instance | x64 Instance | Monthly Savings |
|-----------|----------------|--------------|-----------------|
| Light     | t2a-standard-2 ($26) | e2-standard-2 ($42) | $16 (38%) |
| Moderate  | t2a-standard-4 ($52) | e2-standard-4 ($84) | $32 (38%) |

## Manifest Generation

The ManifestGenerator converts AI-extracted requirements into structured build manifests:

### Protocol → Module Mapping

```csharp
ogc-api → OgcApi
esri-rest → GeoservicesREST
wfs → Wfs
wms → Wms
stac → Stac
vector-tiles → VectorTiles
```

### Database → Connector Mapping

```csharp
postgresql → PostgreSQL
sqlserver → SqlServer
bigquery → BigQuery
snowflake → Snowflake
s3 → S3
azure-blob → AzureBlob
```

### Resource Sizing

| Load Class | Min CPU | Min Memory | Recommended CPU | Recommended Memory |
|------------|---------|------------|-----------------|-------------------|
| Light      | 1 core  | 2 GB       | 2 cores         | 4 GB              |
| Moderate   | 2 cores | 4 GB       | 4 cores         | 8 GB              |
| Heavy      | 4 cores | 8 GB       | 8 cores         | 16 GB             |

## Advanced Usage

### Custom AI Provider

Implement your own AI provider by extending the IntakeAgent:

```csharp
private async Task<(ConversationMessage, FunctionCall?)> CallCustomAIAsync(
    List<ConversationMessage> messages,
    CancellationToken cancellationToken)
{
    // Your custom AI integration
}
```

### Custom Manifest Generation

Override manifest generation logic:

```csharp
public class CustomManifestGenerator : IManifestGenerator
{
    public async Task<BuildManifest> GenerateAsync(
        BuildRequirements requirements,
        string? buildName = null,
        CancellationToken cancellationToken = default)
    {
        // Your custom logic
    }
}

// Register
services.AddSingleton<IManifestGenerator, CustomManifestGenerator>();
```

## Error Handling

All endpoints return appropriate HTTP status codes:

- **200 OK**: Success
- **400 Bad Request**: Invalid request parameters
- **404 Not Found**: Conversation or build not found
- **500 Internal Server Error**: System error

Error responses include descriptive messages:

```json
{
  "error": "Conversation conv-123 not found"
}
```

## Logging

The system uses structured logging throughout:

```csharp
_logger.LogInformation("Starting new conversation {ConversationId} for customer {CustomerId}",
    conversationId, customerId);

_logger.LogWarning("Conversation {ConversationId} not found", conversationId);

_logger.LogError(ex, "Failed to process message for conversation {ConversationId}", conversationId);
```

## Security Considerations

1. **API Key Storage**: Store OpenAI/Anthropic API keys in Azure Key Vault or AWS Secrets Manager
2. **Database Encryption**: Enable encryption at rest for PostgreSQL
3. **TLS**: Always use HTTPS for API endpoints
4. **Authentication**: Implement authentication for API endpoints (not included in base implementation)
5. **Rate Limiting**: Add rate limiting to prevent API abuse

## Testing

Example test for the IntakeAgent:

```csharp
[Fact]
public async Task ProcessMessage_ShouldExtractRequirements_WhenIntakeComplete()
{
    // Arrange
    var conversationId = await _intakeAgent.StartConversationAsync("test-customer");

    // Act
    var response = await _intakeAgent.ProcessMessageAsync(
        conversationId,
        "We need OGC API, PostgreSQL, AWS, ARM64, 20 users");

    // Assert
    Assert.True(response.IntakeComplete);
    Assert.NotNull(response.Requirements);
    Assert.Contains("ogc-api", response.Requirements.Protocols);
    Assert.Equal("aws", response.Requirements.CloudProvider);
}
```

## Future Enhancements

- [ ] Multi-language support
- [ ] Voice input/output
- [ ] Integration with billing systems
- [ ] Automated testing of generated manifests
- [ ] Machine learning to improve recommendations
- [ ] Real-time build status streaming via WebSockets
- [ ] Integration with CI/CD pipelines (GitHub Actions, GitLab CI)

## License

Copyright (c) 2024 HonuaIO. All rights reserved.
