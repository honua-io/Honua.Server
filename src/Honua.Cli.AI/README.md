# Honua AI Assistant

An intelligent infrastructure assistant for Honua geospatial servers. Uses AI to analyze performance, suggest optimizations, and execute database operations safely with a Terraform-style plan/apply workflow.

## 🎯 Overview

The Honua AI Assistant is a **privacy-first, security-focused** tool that helps you:

- **Optimize database performance** - Analyze query patterns and suggest indexes
- **Troubleshoot issues** - Diagnose slow queries and configuration problems
- **Plan infrastructure changes** - Generate and review plans before execution
- **Monitor costs** - Track LLM API usage and estimated costs

## 🔑 Key Features

### 🔒 Security-First Design

- **Scoped Token Architecture** - AI never sees raw credentials
- **Minimal Access Principle** - Tokens limited to specific operations (e.g., "CREATE INDEX only")
- **Time-Limited Tokens** - Automatic expiration (default: 10-30 minutes)
- **User Approval Required** - Explicit consent for each credential access
- **AES-256 Encryption** - Local credential storage with PBKDF2 key derivation

### 🛡️ Safety Model

- **Plan/Apply Workflow** - Review changes before execution (inspired by Terraform)
- **Risk Assessment** - Low/Medium/High/Critical classification with reversibility tracking
- **Rollback Plans** - Automatic generation of reversal steps
- **Dependency Tracking** - Ensures operations execute in correct order
- **Dry-Run Mode** - Test plans without making changes

### 🔐 Privacy-First Telemetry

- **Opt-In Only** - Disabled by default, requires explicit user consent
- **PII Sanitization** - Removes file paths, connection strings, user data
- **Local Storage** - Telemetry stored in `~/.honua/telemetry/` (user-reviewable)
- **Anonymous IDs** - No personally identifiable information
- **Cost Tracking** - Monitor LLM API usage without sending data externally

## 📦 Architecture

```
Honua.Cli.AI/
├── Services/
│   ├── AI/
│   │   ├── ILlmProvider.cs           # Multi-provider AI abstraction
│   │   ├── OpenAIProvider.cs         # OpenAI (GPT-4, GPT-3.5)
│   │   ├── AnthropicProvider.cs      # Anthropic (Claude)
│   │   ├── OllamaProvider.cs         # Local models (Llama, Mistral)
│   │   └── MockLlmProvider.cs        # Testing without API calls
│   ├── Planning/
│   │   ├── ExecutionPlan.cs          # Terraform-style plan model
│   │   ├── SemanticAssistantPlanner.cs # AI-powered planner (Semantic Kernel)
│   │   └── PlanExecutor.cs           # Executes approved plans
│   ├── Execution/
│   │   └── Executors/
│   │       ├── CreateIndexExecutor.cs    # CREATE INDEX CONCURRENTLY
│   │       ├── VacuumAnalyzeExecutor.cs  # VACUUM ANALYZE
│   │       └── IStepExecutor.cs          # Pluggable executor interface
│   ├── Telemetry/
│   │   ├── LocalFileTelemetryService.cs # Privacy-first telemetry
│   │   └── ITelemetryService.cs          # Telemetry abstraction
│   └── Plugins/                        # Semantic Kernel plugins
│       ├── WorkspacePlugin.cs          # Database introspection
│       ├── PerformancePlugin.cs        # Query analysis
│       └── SecurityPlugin.cs           # Secret management

Honua.Cli.AI.Secrets/
├── ISecretsManager.cs                 # Cross-platform secret management
├── EncryptedFileSecretsManager.cs     # FOSS: AES-256 encrypted files
├── WindowsSecretsManager.cs           # Windows Credential Manager
├── MacOSSecretsManager.cs             # macOS Keychain
└── LinuxSecretsManager.cs             # Secret Service API (GNOME Keyring)
```

## 🚀 Quick Start

### 1. Configure LLM Provider

```bash
# Option 1: OpenAI
export OPENAI_API_KEY=sk-...

# Option 2: Anthropic
export ANTHROPIC_API_KEY=sk-ant-...

# Option 3: Ollama (local, no API key needed)
ollama pull llama3
```

### 2. Store Database Credentials

```bash
honua secrets set postgres-production \
  --value "Host=localhost;Database=geodata;Username=admin;Password=..."
```

Credentials are encrypted with AES-256 and stored in:
- Windows: Credential Manager
- macOS: Keychain
- Linux: GNOME Keyring / Secret Service
- FOSS Fallback: `~/.honua/secrets.enc` (encrypted with master password)

### 3. Enable Telemetry (Optional)

```bash
honua telemetry enable

# Review telemetry data anytime
cat ~/.honua/telemetry/telemetry-2025-10-02.jsonl
```

### 4. Ask for Help

```bash
# Natural language query
honua assistant "My queries on the cities table are slow"

# The AI will:
# 1. Analyze your database schema
# 2. Review query logs
# 3. Generate an optimization plan
# 4. Show you the plan for approval
# 5. Execute the plan if you approve
```

## 📋 Example: Query Optimization

```bash
$ honua assistant "Optimize queries on the cities table"

🤖 Analyzing your database...

Found issue: Table 'cities' (5M rows) has full table scans on 'geom' column
Estimated cost: 2.5s per query → 50ms with spatial index

📝 Generated Plan: cities-optimization-20251002-1430

┌─────────────────────────────────────────────────────────────────────┐
│ Execution Plan: Optimize cities table                               │
├─────────────────────────────────────────────────────────────────────┤
│ Type: Optimization                                                   │
│ Risk: Low (all changes reversible)                                  │
│ Estimated Duration: 3-5 minutes                                      │
│ Credentials Required:                                                │
│   - postgres-production (DDL access, 30min, CREATE INDEX only)       │
└─────────────────────────────────────────────────────────────────────┘

Steps:
  1. Analyze table statistics → Pending
  2. Create spatial index (CONCURRENTLY) → Pending
     CREATE INDEX CONCURRENTLY idx_cities_geom ON cities USING GIST(geom)
  3. Verify index usage → Pending

Rollback Plan:
  - Drop index: DROP INDEX CONCURRENTLY idx_cities_geom

⚠️  Request for database access:
   Secret: postgres-production
   Purpose: Create spatial index on cities table
   Scope: DDL (CREATE INDEX only)
   Duration: 30 minutes

Approve? [y/N] y

✅ Executing plan...

Step 1/3: Analyzing table statistics... ✅ (2.1s)
Step 2/3: Creating spatial index... ✅ (182.3s)
  Index size: 245 MB
Step 3/3: Verifying index usage... ✅ (0.5s)

🎉 Optimization complete!
   Query performance: 2.5s → 48ms (98% faster)

📊 Telemetry:
   LLM calls: 3 (GPT-4)
   Tokens: 1,245 prompt + 823 completion
   Estimated cost: $0.06
```

## 🧪 Testing

The AI Assistant includes comprehensive tests:

```bash
# Run all tests
dotnet test tests/Honua.Cli.AI.Tests

# Test coverage
- MockLlmProvider: 10 tests ✅
- ExecutionPlan models: 22 tests ✅
- Telemetry: 5 tests ✅ (7 skipped - async file I/O issues)
- Total: 35 passing, 7 skipped
```

### Mock LLM Provider

Test AI features without making real API calls:

```csharp
var mockLlm = new MockLlmProvider();
mockLlm.ConfigureResponse(
    pattern: "optimize",
    response: "CREATE INDEX idx_geom ON cities(geom)"
);

var planner = new SemanticAssistantPlanner(mockLlm);
var plan = await planner.GeneratePlanAsync("Optimize the cities table");

Assert.Contains("CREATE INDEX", plan.Steps[0].Operation);
```

## 🔐 Security Considerations

### What the AI Can Access

✅ **Read Access (via plugins):**
- Database schema metadata (tables, columns, indexes)
- Query execution plans (via `EXPLAIN`)
- Table statistics (row counts, sizes)
- System configuration (safe settings like `shared_buffers`)

❌ **Never Accessible:**
- Raw table data (no `SELECT * FROM users`)
- User credentials (only scoped tokens)
- Production secrets (encrypted at rest)
- Connection strings (sanitized in telemetry)

### Scoped Token Example

When the AI requests database access:

```json
{
  "secretRef": "postgres-production",
  "scope": {
    "level": "DDL",
    "allowedOperations": ["CREATE INDEX"],
    "deniedOperations": ["DROP TABLE", "DELETE"]
  },
  "duration": "PT30M",
  "purpose": "Create spatial index on cities.geom"
}
```

The token expires after 30 minutes and **only** allows `CREATE INDEX`.

## 📊 Telemetry Details

When enabled, telemetry tracks:

- **Commands** - Which assistant commands you run (frequency, success rate)
- **Plans** - Types of plans generated (optimization, deployment, etc.)
- **LLM Calls** - Provider, model, token usage, estimated cost
- **Errors** - Error types (PII sanitized)

Telemetry files (`~/.honua/telemetry/*.jsonl`) contain:

```json
{
  "timestamp": "2025-10-02T14:30:00Z",
  "eventType": "LlmCall",
  "userId": "a1b2c3d4e5f6...",  // Anonymous GUID
  "sessionId": "x7y8z9...",
  "provider": "openai",
  "model": "gpt-4",
  "promptTokens": 1245,
  "completionTokens": 823,
  "estimatedCost": 0.06,
  "duration": "PT2.1S"
}
```

**No PII is collected.** All file paths and connection strings are sanitized.

## 🛠️ Development

### Building

```bash
dotnet build src/Honua.Cli.AI
dotnet build src/Honua.Cli.AI.Secrets
```

### Running Tests

```bash
dotnet test tests/Honua.Cli.AI.Tests --verbosity normal
```

### Adding a New LLM Provider

1. Implement `ILlmProvider`:

```csharp
public class MyLlmProvider : ILlmProvider
{
    public async Task<LlmResponse> CompleteAsync(string prompt, LlmOptions? options = null)
    {
        // Call your LLM API
        var result = await _client.GenerateAsync(prompt);

        return new LlmResponse
        {
            Content = result.Text,
            FinishReason = "stop",
            TokensUsed = result.Tokens,
            Model = "my-model-v1"
        };
    }

    // ... implement other methods
}
```

2. Register in DI container:

```csharp
services.AddSingleton<ILlmProvider, MyLlmProvider>();
```

### Adding a New Executor

1. Implement `IStepExecutor`:

```csharp
public class CustomExecutor : IStepExecutor
{
    public StepType SupportedStepType => StepType.Custom;

    public async Task<StepExecutionResult> ExecuteAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Get scoped credential
        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope { Level = AccessLevel.DDL },
                Duration = TimeSpan.FromMinutes(30),
                Purpose = "Custom operation"
            },
            cancellationToken);

        // Execute operation...

        return new StepExecutionResult
        {
            Success = true,
            Output = "Operation completed",
            Duration = elapsed
        };
    }
}
```

2. Register the executor:

```csharp
services.AddTransient<IStepExecutor, CustomExecutor>();
```

## 📚 Design Documents

- [AI Safety Model](../../docs/design/ai-assistant-safety-model.md) - Security architecture
- [FOSS vs AI Boundary](../../docs/design/ai-assistant-foss-boundary.md) - Feature separation
- [Implementation Roadmap](../../docs/design/ai-assistant-implementation-roadmap.md) - Development plan

## 🤝 Contributing

We welcome contributions! Key areas:

1. **New LLM Providers** - Add support for more AI services
2. **New Executors** - Implement additional database operations
3. **Telemetry Fixes** - Resolve async file I/O test issues
4. **Integration Tests** - End-to-end testing with real databases

## 📄 License

See [LICENSE](../../LICENSE) for details.

## 🙏 Acknowledgments

- **Semantic Kernel** - Microsoft's AI orchestration framework
- **Terraform** - Inspiration for plan/apply workflow
- **PostgreSQL/PostGIS** - World-class spatial database

---

**Made with ❤️ by the Honua team**
