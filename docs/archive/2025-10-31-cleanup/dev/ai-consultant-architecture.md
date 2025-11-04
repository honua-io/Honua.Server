# Honua AI Consultant - Technical Architecture

## Overview

The Honua AI Consultant is a separate service that helps users configure and manage their Honua GIS servers through natural language conversation.

**Key Principle:** The AI Consultant generates static configuration (YAML/JSON). The Honua server reads and executes this configuration. The AI never runs in the request path.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         USER                                    │
│  CLI / Web UI / API                                             │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│              Honua AI Consultant Service                        │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │  Conversation Manager                                     │ │
│  │  - Session management                                      │ │
│  │  - Context tracking                                        │ │
│  │  - Multi-turn conversation                                 │ │
│  └───────────────────────────────────────────────────────────┘ │
│                            │                                    │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │  Intent Recognition                                        │ │
│  │  - Classify user intent                                    │ │
│  │  - Extract parameters                                      │ │
│  │  - Validate requests                                       │ │
│  └───────────────────────────────────────────────────────────┘ │
│                            │                                    │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │  Action Executors                                          │ │
│  │  ┌─────────────┐ ┌─────────────┐ ┌──────────────────┐    │ │
│  │  │  Database   │ │  Metadata   │ │  Git Operations  │    │ │
│  │  │  Inspector  │ │  Generator  │ │  (PR creation)   │    │ │
│  │  └─────────────┘ └─────────────┘ └──────────────────┘    │ │
│  └───────────────────────────────────────────────────────────┘ │
│                            │                                    │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │  LLM Integration (Claude/GPT-4)                           │ │
│  │  - Generate YAML/JSON                                      │ │
│  │  - Explain configurations                                  │ │
│  │  - Answer questions                                        │ │
│  └───────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                    External Integrations                        │
│                                                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────┐    │
│  │   Database   │  │      Git     │  │   Honua Server    │    │
│  │  (for schema │  │  (create PR) │  │  (validate conf)  │    │
│  │  inspection) │  │              │  │                   │    │
│  └──────────────┘  └──────────────┘  └───────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

## Core Components

### 1. Conversation Manager

Handles multi-turn conversations with context:

```typescript
interface ConversationSession {
  id: string;
  userId: string;
  environment: string;  // dev, staging, production
  context: ConversationContext;
  history: Message[];
  createdAt: Date;
  lastActivityAt: Date;
}

interface ConversationContext {
  // Current working scope
  focusedService?: string;
  focusedLayer?: string;

  // Database connections (if provided)
  databaseConnections: Record<string, DatabaseConnection>;

  // Git repository (if configured)
  gitRepository?: GitRepositoryConfig;

  // Recent actions
  recentActions: Action[];

  // Pending approvals
  pendingApprovals: PendingApproval[];
}

interface Message {
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
  metadata?: Record<string, any>;
}
```

**Responsibilities:**
- Track conversation state
- Maintain context across turns
- Detect when user switches topics
- Remember previous decisions

### 2. Intent Recognition

Classifies user requests into actionable intents:

```typescript
enum Intent {
  // Read-only intents (Free tier)
  EXPLAIN_CONFIGURATION = 'explain_configuration',
  ANSWER_QUESTION = 'answer_question',
  SHOW_EXAMPLES = 'show_examples',
  VALIDATE_YAML = 'validate_yaml',
  EXPLAIN_CONCEPT = 'explain_concept',

  // Write intents (Pro tier)
  CREATE_LAYER = 'create_layer',
  MODIFY_LAYER = 'modify_layer',
  DELETE_LAYER = 'delete_layer',
  INSPECT_DATABASE = 'inspect_database',
  GENERATE_METADATA = 'generate_metadata',
  CREATE_PR = 'create_pr',
  OPTIMIZE_CONFIGURATION = 'optimize_configuration',

  // Admin intents (Enterprise tier)
  ANALYZE_TOPOLOGY = 'analyze_topology',
  RECOMMEND_INDEXES = 'recommend_indexes',
  DETECT_ISSUES = 'detect_issues',
}

interface IntentClassification {
  intent: Intent;
  confidence: number;
  parameters: Record<string, any>;
  requiresApproval: boolean;
  tierRequired: 'free' | 'pro' | 'enterprise';
}
```

**Implementation:**
```typescript
class IntentRecognizer {
  async classifyIntent(
    message: string,
    context: ConversationContext
  ): Promise<IntentClassification> {
    // Use LLM to classify intent
    const prompt = `
Classify the user's intent from this message:
"${message}"

Context:
- Current service: ${context.focusedService}
- Available databases: ${Object.keys(context.databaseConnections).join(', ')}

Available intents:
${Object.values(Intent).join('\n')}

Return JSON with: intent, confidence, parameters
`;

    const response = await this.llm.complete(prompt);
    return JSON.parse(response);
  }
}
```

### 3. Database Inspector

Connects to user databases to introspect schema:

```typescript
interface DatabaseInspector {
  // Connect to database
  connect(connection: DatabaseConnection): Promise<void>;

  // List all tables/views
  getTables(): Promise<TableInfo[]>;

  // Get table schema
  getTableSchema(tableName: string): Promise<TableSchema>;

  // Detect geometry columns
  getGeometryColumns(tableName: string): Promise<GeometryColumn[]>;

  // Analyze data for recommendations
  analyzeTable(tableName: string): Promise<TableAnalysis>;
}

interface TableSchema {
  tableName: string;
  columns: ColumnInfo[];
  primaryKey?: string[];
  indexes: IndexInfo[];
  estimatedRowCount: number;
}

interface TableAnalysis {
  // Data characteristics
  geometryType: 'Point' | 'LineString' | 'Polygon' | 'MultiPolygon' | 'Mixed';
  srid: number;
  extent: BoundingBox;

  // Performance hints
  hasSpatialIndex: boolean;
  recommendedIndexes: string[];

  // Data quality
  hasNullGeometries: boolean;
  hasInvalidGeometries: boolean;

  // Suggested configuration
  suggestedCacheTTL: number;
  suggestedMaxFeatures: number;
}
```

**Example Usage:**
```typescript
// User: "Add a layer for the parcels table"

// 1. Inspector connects to database
const inspector = new DatabaseInspector();
await inspector.connect(context.databaseConnections['postgis-primary']);

// 2. Get table schema
const schema = await inspector.getTableSchema('parcels');

// 3. Analyze table
const analysis = await inspector.analyzeTable('parcels');

// 4. Generate metadata based on analysis
const metadata = {
  name: 'parcels',
  datasource: 'postgis-primary',
  table: 'parcels',
  geometryColumn: analysis.geometryColumn,
  idColumn: schema.primaryKey[0],
  caching: {
    enabled: true,
    ttl: analysis.suggestedCacheTTL
  },
  properties: schema.columns.map(col => ({
    name: col.name,
    type: mapPostgresTypeToOGC(col.type)
  }))
};
```

### 4. Metadata Generator

Generates Honua configuration from templates and analysis:

```typescript
class MetadataGenerator {
  async generateLayerMetadata(
    tableName: string,
    schema: TableSchema,
    analysis: TableAnalysis,
    userPreferences?: Partial<LayerConfig>
  ): Promise<string> {
    // Build prompt for LLM
    const prompt = `
Generate Honua layer metadata YAML for a PostGIS table.

Table: ${tableName}
Columns: ${JSON.stringify(schema.columns, null, 2)}
Geometry Type: ${analysis.geometryType}
SRID: ${analysis.srid}
Spatial Index: ${analysis.hasSpatialIndex ? 'Yes' : 'No'}

Requirements:
- Use OGC API - Features standard
- Enable caching with TTL ${analysis.suggestedCacheTTL} seconds
- Include all non-geometry columns as properties
${userPreferences ? `- User preferences: ${JSON.stringify(userPreferences)}` : ''}

Generate YAML following the Honua schema:
apiVersion: honua.io/v1
kind: ServiceCollection
...
`;

    const yaml = await this.llm.complete(prompt);

    // Validate generated YAML
    await this.validateMetadata(yaml);

    return yaml;
  }

  async validateMetadata(yaml: string): Promise<ValidationResult> {
    // Parse YAML
    const parsed = YAML.parse(yaml);

    // Validate against schema
    const errors: string[] = [];

    if (!parsed.apiVersion) {
      errors.push('Missing apiVersion');
    }

    if (!parsed.kind) {
      errors.push('Missing kind');
    }

    // ... more validation

    return {
      valid: errors.length === 0,
      errors
    };
  }
}
```

### 5. Git Operations

Creates pull requests with generated configuration:

```typescript
interface GitOperator {
  // Create a new branch
  createBranch(baseBranch: string, newBranchName: string): Promise<void>;

  // Commit files
  commitFiles(files: FileChange[], message: string): Promise<string>;

  // Create pull request
  createPullRequest(params: PullRequestParams): Promise<PullRequest>;
}

interface FileChange {
  path: string;
  content: string;
  operation: 'create' | 'modify' | 'delete';
}

interface PullRequestParams {
  title: string;
  description: string;
  baseBranch: string;
  headBranch: string;
  labels?: string[];
}

// Example usage
async function createLayerPR(
  layerName: string,
  metadata: string,
  environment: string
) {
  const gitOps = new GitOperator(repositoryConfig);

  // 1. Create feature branch
  const branchName = `feature/add-${layerName}-layer`;
  await gitOps.createBranch('main', branchName);

  // 2. Commit metadata file
  const files: FileChange[] = [{
    path: `environments/${environment}/layers/${layerName}.yaml`,
    content: metadata,
    operation: 'create'
  }];

  const commitSha = await gitOps.commitFiles(
    files,
    `Add ${layerName} layer configuration

Generated by Honua AI Consultant

🤖 Generated with Claude Code
Co-Authored-By: Claude <noreply@anthropic.com>`
  );

  // 3. Create PR
  const pr = await gitOps.createPullRequest({
    title: `Add ${layerName} layer`,
    description: `
## Summary
Adds configuration for ${layerName} layer

## Changes
- Added \`environments/${environment}/layers/${layerName}.yaml\`

## AI-Generated
This PR was generated by Honua AI Consultant based on database schema analysis.

## Test Plan
- [ ] Deploy to ${environment}
- [ ] Verify layer appears in OGC API
- [ ] Test feature queries
- [ ] Check caching behavior

🤖 Generated with Claude Code`,
    baseBranch: 'main',
    headBranch: branchName,
    labels: ['ai-generated', environment]
  });

  return pr;
}
```

## Conversation Flow Examples

### Example 1: Add New Layer

```
User: "Add a layer for the bike_lanes table"