# Knowledge Base Architecture

Complete architecture for the AI deployment consultant's knowledge base, using **PostgreSQL for approval workflow** and **Azure AI Search for the RAG knowledge base**.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     Deployment Telemetry                        │
│  (PostgreSQL: deployment_history table)                         │
│  - Every deployment tracked (success/failure)                   │
│  - Facts only: cost, performance, duration, errors              │
│  - No LLM involvement → no hallucinations                       │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 │ Nightly analysis (2 AM UTC)
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│              Pattern Analysis Function                          │
│  (Azure Function: PatternAnalysisFunction.cs)                   │
│  - SQL-based statistical analysis (NOT LLM)                     │
│  - Groups deployments by configuration                          │
│  - Calculates success rate, cost accuracy, satisfaction         │
│  - Generates recommendations with evidence                      │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 │ Writes recommendations
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│           Pattern Recommendations (PostgreSQL)                  │
│  (Staging table: pattern_recommendations)                       │
│  - Status: pending_review                                       │
│  - Awaits human approval                                        │
│  - NOT searchable by AI (not in RAG yet)                        │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 │ Human reviews via CLI
                 │ honua-cli review-patterns
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│              Human Approval Decision                            │
│  (PatternApprovalService.cs)                                    │
│  - Approve → index in Azure AI Search                           │
│  - Reject → mark as rejected, do not index                      │
│  - Optional review notes                                        │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 │ If approved
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│              Azure AI Search Knowledge Base                     │
│  (Vector database: deployment-knowledge index)                  │
│  - Only human-approved patterns                                 │
│  - Hybrid search (vector + keyword + filters)                   │
│  - Semantic ranking for relevance                               │
│  - THIS is the source of truth for AI consultant                │
└─────────────────────────────────────────────────────────────────┘
                 │
                 │ AI consultant searches
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│           AI Deployment Consultant                              │
│  (DeploymentConfigurationAgent.cs)                              │
│  - Searches Azure AI Search for matching patterns               │
│  - LLM explains recommendations to customer                     │
│  - Generates deployment configuration                           │
└─────────────────────────────────────────────────────────────────┘
```

## Data Flow Example

### 1. Initial Deployments (Weeks 1-4)

Customer deploys 15 times with similar requirements (500GB data, 1000 users, AWS):

```sql
-- PostgreSQL: deployment_history table
INSERT INTO deployment_history VALUES
  ('deploy-001', 'customer-1', 'aws', 'us-west-2', 500, 1000, ...),
  ('deploy-002', 'customer-2', 'aws', 'us-west-2', 480, 950, ...),
  ...
  ('deploy-015', 'customer-15', 'aws', 'us-west-2', 520, 1100, ...);
```

**No pattern yet** - AI consultant has no knowledge base, uses defaults.

### 2. Nightly Pattern Analysis (Week 5)

Pattern Analysis Function runs at 2 AM:

```sql
-- SQL analysis finds pattern (10+ deployments with m6i.2xlarge)
SELECT
  cloud_provider,
  instance_type,
  COUNT(*) as deployment_count,
  AVG(CASE WHEN success THEN 1.0 ELSE 0.0 END) as success_rate
FROM deployment_history
WHERE timestamp >= NOW() - INTERVAL '90 days'
GROUP BY cloud_provider, instance_type
HAVING COUNT(*) >= 10;

-- Result: 15 deployments, 93% success rate → recommendation generated
```

Recommendation written to `pattern_recommendations` table:

```sql
INSERT INTO pattern_recommendations VALUES (
  id: 'abc-123',
  pattern_name: 'AWS Medium Dataset Standard Workload (us-west-2)',
  cloud_provider: 'aws',
  status: 'pending_review',  -- NOT yet in knowledge base
  evidence_json: {
    "successRate": 0.93,
    "deploymentCount": 15,
    "avgCostAccuracy": 95.2,
    "avgCustomerSatisfaction": 4.6
  }
);
```

### 3. Human Review (Week 5)

Engineer reviews via CLI:

```bash
$ honua-cli review-patterns

Found 1 pending pattern recommendation:

ID: abc-123
Name: AWS Medium Dataset Standard Workload (us-west-2)
Cloud Provider: aws
Region: us-west-2

Applicability:
  Data Volume: 450-550 GB
  Concurrent Users: 900-1200
  Budget: $2500-$3200/month

Evidence:
  Success Rate: 93.3%
  Deployments: 15
  Cost Accuracy: 95.2%
  Customer Satisfaction: 4.6/5.0

Configuration: {"instanceType": "m6i.2xlarge", "instanceCount": 2, ...}

$ honua-cli review-patterns --approve abc-123 --reviewer "Mike"
```

### 4. Indexing in Azure AI Search

`PatternApprovalService.ApprovePatternAsync()` called:

1. **Update PostgreSQL**: Set status = 'approved'
2. **Generate embedding**: OpenAI text-embedding-3-large
3. **Index in Azure AI Search**:

```json
POST https://search-honua-abc123.search.windows.net/indexes/deployment-knowledge/docs/index

{
  "value": [{
    "id": "abc-123",
    "content": "Deployment pattern for aws cloud. Data volume: 450-550GB. Concurrent users: 900-1200. Success rate: 93% over 15 deployments.",
    "contentVector": [0.023, -0.041, ...],  // 3072 dimensions
    "patternType": "architecture",
    "patternName": "AWS Medium Dataset Standard Workload (us-west-2)",
    "cloudProvider": "aws",
    "region": "us-west-2",
    "dataVolumeMin": 450,
    "dataVolumeMax": 550,
    "concurrentUsersMin": 900,
    "concurrentUsersMax": 1200,
    "successRate": 0.93,
    "deploymentCount": 15,
    "humanApproved": true,
    "approvedBy": "Mike",
    "configuration": "{...}"
  }]
}
```

### 5. AI Consultant Uses Pattern (Week 6+)

New customer asks: "I need to deploy 500GB imagery with 1000 users on AWS"

AI consultant searches Azure AI Search:

```csharp
var matches = await _knowledgeStore.SearchPatternsAsync(new CustomerRequirements
{
    DataVolumeGb = 500,
    ConcurrentUsers = 1000,
    CloudProvider = "aws",
    Region = "us-west-2"
});

// Returns top 3 matches with hybrid search:
// 1. Vector similarity (semantic: "500GB" ~ "450-550GB")
// 2. Metadata filters (cloudProvider eq 'aws', dataVolumeMin le 500, etc)
// 3. Semantic ranking (Microsoft's reranking algorithm)
// 4. Scoring profile (boost high success rate)

// Result: Pattern abc-123 matches with 0.95 vector score
```

LLM generates response:

```
Based on 15 successful deployments with similar requirements (93% success rate),
I recommend the following architecture:

AWS Cloud - us-west-2 region
- 2x m6i.2xlarge EC2 instances
- RDS PostgreSQL db.r6g.xlarge
- ElastiCache Redis cache.r6g.large

Estimated cost: $2,847/month
Expected performance: 150ms P95 tile response
Supports: 1,500 concurrent users

This configuration has demonstrated 95.2% cost prediction accuracy and
4.6/5.0 customer satisfaction across 15 real-world deployments.
```

## Database Schema

### PostgreSQL Tables

#### `deployment_history`
**Purpose**: Raw telemetry from every deployment (facts only, no LLM)

```sql
CREATE TABLE deployment_history (
    id UUID PRIMARY KEY,
    deployment_id VARCHAR(100) UNIQUE,
    customer_id VARCHAR(100),
    cloud_provider VARCHAR(50),
    data_volume_gb INTEGER,
    predicted_monthly_cost DECIMAL,
    actual_monthly_cost DECIMAL,
    cost_accuracy_percent DECIMAL,
    success BOOLEAN,
    configuration JSONB,
    -- 20+ more columns
);
```

#### `pattern_recommendations`
**Purpose**: Staging table for human approval (NOT searchable by AI yet)

```sql
CREATE TABLE pattern_recommendations (
    id UUID PRIMARY KEY,
    pattern_name VARCHAR(200),
    cloud_provider VARCHAR(50),
    configuration_json JSONB,
    applicability_json JSONB,
    evidence_json JSONB,
    status VARCHAR(50) DEFAULT 'pending_review',  -- pending_review | approved | rejected
    analyzed_at TIMESTAMP,
    reviewed_by VARCHAR(100),
    reviewed_at TIMESTAMP
);
```

#### `known_issues`
**Purpose**: Known deployment issues (also indexed in Azure AI Search after approval)

```sql
CREATE TABLE known_issues (
    id UUID PRIMARY KEY,
    description TEXT,
    affected_regions VARCHAR(100)[],
    trigger_conditions TEXT,
    solution TEXT,
    severity VARCHAR(20),
    implemented BOOLEAN DEFAULT FALSE
);
```

### Azure AI Search Index

**Name**: `deployment-knowledge`

**Key Fields**:
- `contentVector` (3072 dims) - Semantic search
- `cloudProvider` - Filter
- `dataVolumeMin/Max` - Range filter
- `successRate` - Ranking boost
- `humanApproved` - Always true (only approved patterns)
- `configuration` - JSON with exact deployment config

**Search Capabilities**:
1. **Vector search**: Semantic similarity (HNSW algorithm)
2. **Keyword search**: Traditional full-text (BM25)
3. **Filters**: Metadata (cloud, region, data volume range)
4. **Semantic ranking**: Microsoft's proprietary reranking
5. **Scoring profiles**: Boost high success rate, recent approvals

## Files

| File | Purpose |
|------|---------|
| `schema.sql` | PostgreSQL schema (telemetry, staging, approval) |
| `search-index-schema.json` | Azure AI Search index definition |
| `create-search-index.sh` | Script to create Azure AI Search index |
| `AzureAISearchKnowledgeStore.cs` | C# service for indexing/searching |
| `PatternAnalysisFunction.cs` | Azure Function for nightly analysis |
| `PatternApprovalService.cs` | Approval workflow service |
| `ReviewPatternsCommand.cs` | CLI for human review |

## Deployment Steps

### 1. Deploy Azure Infrastructure

```bash
cd infrastructure/terraform/azure
terraform init
terraform apply
```

This creates:
- Azure OpenAI (GPT-4 Turbo + embeddings)
- Azure AI Search (Basic tier with vector search)
- PostgreSQL Flexible Server
- Application Insights
- Azure Function App

### 2. Create PostgreSQL Schema

```bash
# Get connection string from Terraform output
POSTGRES_HOST=$(terraform output -raw postgres_host)

# Run schema migration
psql "host=$POSTGRES_HOST dbname=honua user=honuaadmin sslmode=require" < schema.sql
```

### 3. Create Azure AI Search Index

```bash
cd ../../../src/Honua.Cli.AI/Database
./create-search-index.sh
```

This creates the `deployment-knowledge` index with:
- 29 fields (metadata, vectors, evidence)
- HNSW vector search (cosine similarity)
- Semantic search configuration
- Scoring profiles for ranking

### 4. Deploy Azure Function

```bash
cd ..
func azure functionapp publish $(terraform output -raw function_app_name)
```

The function runs nightly at 2 AM UTC to analyze patterns.

### 5. Start Collecting Telemetry

After each deployment, write telemetry to `deployment_history`:

```csharp
await connection.ExecuteAsync(@"
    INSERT INTO deployment_history
    (deployment_id, customer_id, cloud_provider, data_volume_gb,
     predicted_monthly_cost, actual_monthly_cost, success, configuration)
    VALUES (@DeploymentId, @CustomerId, @CloudProvider, @DataVolumeGb,
            @PredictedCost, @ActualCost, @Success, @Configuration::jsonb)",
    deployment);
```

### 6. Wait for Pattern Analysis (or trigger manually)

After 10+ deployments with similar config, the nightly function generates recommendations.

### 7. Review and Approve Patterns

```bash
# List pending recommendations
honua-cli review-patterns

# Approve a pattern
honua-cli review-patterns --approve <id> --reviewer "Your Name"
```

### 8. AI Consultant Uses Patterns

The AI consultant automatically searches Azure AI Search for matching patterns:

```csharp
var matches = await _knowledgeStore.SearchPatternsAsync(requirements);
// Returns only human-approved patterns with high success rates
```

## Key Design Decisions

### Why PostgreSQL for Staging?

1. **ACID transactions**: Approval workflow needs consistency
2. **SQL analytics**: Pattern analysis is SQL-based (not LLM)
3. **Audit trail**: Complete history of reviews (who, when, why)
4. **Cost**: Free tier sufficient for MVP

### Why Azure AI Search for Knowledge Base?

1. **Hybrid search**: Vector + keyword + filters in one query
2. **No hallucination**: Returns only indexed documents (no generation)
3. **Semantic ranking**: Microsoft's reranking improves relevance
4. **Managed service**: No infrastructure to manage
5. **Scoring profiles**: Custom ranking (boost success rate, recency)

### Why Separate Staging and Knowledge Base?

1. **Human-in-the-loop**: Prevents bad patterns from polluting RAG
2. **Traceability**: Every pattern has approval audit trail
3. **Versioning**: Can update patterns without losing history
4. **Quality control**: Only 80%+ success rate patterns get approved

### Why SQL Analysis (Not LLM)?

1. **Reproducibility**: Same data → same recommendations
2. **No hallucination**: Pure statistics, no invented facts
3. **Explainability**: "93% success over 15 deployments" vs "I think this works"
4. **Cost**: Free (vs $0.03 per analysis with LLM)

## Monitoring

### Query Azure AI Search Usage

```bash
# Get search metrics
az monitor metrics list \
  --resource /subscriptions/.../providers/Microsoft.Search/searchServices/search-honua-abc123 \
  --metric SearchQueriesPerSecond,SearchLatency
```

### Query PostgreSQL Analytics

```sql
-- Pattern performance summary
SELECT * FROM pattern_performance
WHERE deployment_count >= 10
ORDER BY success_rate DESC;

-- OpenAI cost trend
SELECT * FROM openai_cost_summary
WHERE date >= CURRENT_DATE - INTERVAL '30 days';
```

### Application Insights Queries (Kusto)

```kusto
// Deployment success rate trend
customEvents
| where name == "DeploymentCompleted"
| summarize SuccessRate = avg(todouble(customMeasurements.success))
    by bin(timestamp, 1d)
| render timechart

// Cost accuracy improvement over time
customEvents
| where name == "DeploymentCompleted"
| summarize AvgCostAccuracy = avg(customMeasurements.costAccuracy)
    by bin(timestamp, 7d)
| render timechart
```

## Cost Estimate

| Component | Cost |
|-----------|------|
| Azure AI Search (Basic) | $75/month |
| PostgreSQL (B1ms) | $14/month |
| Azure Function (Consumption) | ~$0.20/month |
| OpenAI Embeddings | ~$5/month (500 patterns) |
| Total | **$94/month** |

With Azure for Startups Tier 1 ($200/month credits): **Free** in Year 1

## Future Enhancements

1. **Web UI for approvals**: React app instead of CLI
2. **A/B testing**: Deploy pattern A vs B, measure which performs better
3. **Automated approval**: Auto-approve patterns with 95%+ success rate
4. **Pattern versioning**: Track pattern evolution over time
5. **Multi-region patterns**: Different configs per AWS region
6. **Cost optimization**: Proactive recommendations when cheaper alternatives exist
