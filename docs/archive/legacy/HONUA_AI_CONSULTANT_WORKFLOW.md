# Honua AI Consultant Workflow

## Complete Workflow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                         HONUA AI CONSULTANT WORKFLOW                            │
│                              (2025 Architecture)                                │
└─────────────────────────────────────────────────────────────────────────────────┘
                                       │
                                       ▼
                        ┌──────────────────────────┐
                        │   User Request Received  │
                        │  (ConsultantWorkflow)    │
                        └────────────┬─────────────┘
                                     │
                                     ▼
                        ┌──────────────────────────┐
                        │  Environment Initialized │
                        │  Workspace Resolved      │
                        └────────────┬─────────────┘
                                     │
                                     ▼
                        ┌──────────────────────────┐
                        │  Context Builder Phase   │
                        │  • Scan workspace        │
                        │  • Detect metadata.json  │
                        │  • Detect infra files    │
                        │  • Generate observations │
                        └────────────┬─────────────┘
                                     │
                                     ▼
                    ┌────────────────────────────────┐
                    │   Execution Mode Decision      │
                    └────┬───────────────────────┬───┘
                         │                       │
            ┌────────────┴──────────┐           │
            │                       │           │
            ▼                       ▼           ▼
    ┌──────────────┐      ┌──────────────┐   ┌──────────────┐
    │ Multi-Agent  │      │     Auto     │   │  Plan Mode   │
    │     Mode     │      │     Mode     │   │ (Traditional)│
    └──────┬───────┘      └──────┬───────┘   └──────┬───────┘
           │                     │                   │
           ▼                     ▼                   │
    ┌───────────────────────────────────────┐       │
    │  SemanticAgentCoordinator             │       │
    │  ┌─────────────────────────────────┐  │       │
    │  │  Phase 1: Intent Analysis       │  │       │
    │  │  • LLM analyzes user request    │  │       │
    │  │  • Identifies primary intent    │  │       │
    │  │  • Determines required agents   │  │       │
    │  │  • Multi-agent check            │  │       │
    │  └────────────┬────────────────────┘  │       │
    │               ▼                        │       │
    │  ┌─────────────────────────────────┐  │       │
    │  │  Phase 2: Agent Routing         │  │       │
    │  │  ┌────────────┐  ┌────────────┐ │  │       │
    │  │  │  Multiple  │  │   Single   │ │  │       │
    │  │  │   Agents   │  │   Agent    │ │  │       │
    │  │  └─────┬──────┘  └─────┬──────┘ │  │       │
    │  │        │               │        │  │       │
    │  │        ▼               ▼        │  │       │
    │  │  ┌─────────────────────────┐   │  │       │
    │  │  │ IntelligentAgentSelector│   │  │       │
    │  │  │ • VectorDB similarity   │   │  │       │
    │  │  │ • Pattern telemetry     │   │  │       │
    │  │  │ • Confidence scoring    │   │  │       │
    │  │  └───────────┬─────────────┘   │  │       │
    │  └──────────────┼─────────────────┘  │       │
    │                 ▼                     │       │
    │  ┌─────────────────────────────────┐ │       │
    │  │  Specialized Agent Execution    │ │       │
    │  │  ┌────────────────────────────┐ │ │       │
    │  │  │ ArchitectureConsulting     │ │ │       │
    │  │  │ • Requirements extraction  │ │ │       │
    │  │  │ • Architecture selection   │ │ │       │
    │  │  │ • Cost estimation         │ │ │       │
    │  │  └────────────────────────────┘ │ │       │
    │  │  ┌────────────────────────────┐ │ │       │
    │  │  │ DeploymentConfiguration    │ │ │       │
    │  │  │ • Terraform generation     │ │ │       │
    │  │  │ • Kubernetes manifests     │ │ │       │
    │  │  │ • Docker Compose           │ │ │       │
    │  │  └────────────────────────────┘ │ │       │
    │  │  ┌────────────────────────────┐ │ │       │
    │  │  │ DataIngestion              │ │ │       │
    │  │  │ • YAML metadata generation │ │ │       │
    │  │  │ • Schema detection         │ │ │       │
    │  │  │ • Data source config       │ │ │       │
    │  │  └────────────────────────────┘ │ │       │
    │  │  ┌────────────────────────────┐ │ │       │
    │  │  │ CertificateManagement      │ │ │       │
    │  │  │ • Let's Encrypt config     │ │ │       │
    │  │  │ • DNS-01 / HTTP-01         │ │ │       │
    │  │  │ • Auto-renewal             │ │ │       │
    │  │  └────────────────────────────┘ │ │       │
    │  │  ┌────────────────────────────┐ │ │       │
    │  │  │ CloudPermissionGenerator   │ │ │       │
    │  │  │ • IAM policies (AWS)       │ │ │       │
    │  │  │ • RBAC (Azure)             │ │ │       │
    │  │  │ • IAM bindings (GCP)       │ │ │       │
    │  │  └────────────────────────────┘ │ │       │
    │  │  ┌────────────────────────────┐ │ │       │
    │  │  │ ObservabilityConfiguration │ │ │       │
    │  │  │ • Prometheus config        │ │ │       │
    │  │  │ • Grafana dashboards       │ │ │       │
    │  │  │ • OpenTelemetry            │ │ │       │
    │  │  └────────────────────────────┘ │ │       │
    │  │  ┌────────────────────────────┐ │ │       │
    │  │  │ + 10 more specialized      │ │ │       │
    │  │  │   agents available...      │ │ │       │
    │  │  └────────────────────────────┘ │ │       │
    │  └─────────────────────────────────┘ │       │
    │                 │                     │       │
    │                 ▼                     │       │
    │  ┌─────────────────────────────────┐ │       │
    │  │  Phase 3: Response Synthesis    │ │       │
    │  │  • Aggregate agent results      │ │       │
    │  │  • Format for user display      │ │       │
    │  └────────────┬────────────────────┘ │       │
    │               ▼                       │       │
    │  ┌─────────────────────────────────┐ │       │
    │  │  Phase 4: Next Steps            │ │       │
    │  │  • Determine follow-up actions  │ │       │
    │  │  • Generate recommendations     │ │       │
    │  └────────────┬────────────────────┘ │       │
    └───────────────┼──────────────────────┘       │
                    │                              │
                    └──────────────┬───────────────┘
                                   │
                                   ▼
                      ┌──────────────────────────┐
                      │  Agent Critic Review     │
                      │  • Security validation   │
                      │  • Cost analysis         │
                      │  • Best practice checks  │
                      └────────────┬─────────────┘
                                   │
                    ┌──────────────┴──────────────┐
                    │                             │
                    ▼                             ▼
         ┌────────────────────┐        ┌────────────────────┐
         │  Multi-Agent Mode  │        │   Plan-Based Mode  │
         │  Result Display    │        │ (Traditional Flow) │
         └──────────┬─────────┘        └──────────┬─────────┘
                    │                             │
                    │                             ▼
                    │              ┌──────────────────────────┐
                    │              │  ConsultantPlanner       │
                    │              │  • LLM generates plan    │
                    │              │  • Step-by-step actions  │
                    │              │  • Pattern matching      │
                    │              └────────────┬─────────────┘
                    │                           │
                    │                           ▼
                    │              ┌──────────────────────────┐
                    │              │  Plan Formatter          │
                    │              │  • Render plan to console│
                    │              │  • Show confidence       │
                    │              │  • Highlight risks       │
                    │              └────────────┬─────────────┘
                    │                           │
                    └───────────────┬───────────┘
                                    │
                                    ▼
                       ┌──────────────────────────┐
                       │  Enhancement Phase       │
                       │  (If not dry-run)        │
                       └────────────┬─────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    ▼               ▼               ▼
         ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
         │ Architecture │  │ Architecture │  │   Metadata   │
         │   Diagram    │  │Documentation │  │Configuration │
         │  Generator   │  │   Generator  │  │   Display    │
         └──────┬───────┘  └──────┬───────┘  └──────┬───────┘
                │                 │                  │
                └─────────────────┼──────────────────┘
                                  │
                                  ▼
                     ┌──────────────────────────┐
                     │   Approval Phase         │
                     │  ┌────────────────────┐  │
                     │  │ Auto-approve?      │  │
                     │  ├────────────────────┤  │
                     │  │ Trust high         │  │
                     │  │ confidence?        │  │
                     │  ├────────────────────┤  │
                     │  │ Manual approval    │  │
                     │  └──────────┬─────────┘  │
                     └─────────────┼────────────┘
                                   │
                    ┌──────────────┴──────────────┐
                    │                             │
                    ▼                             ▼
         ┌────────────────────┐        ┌────────────────────┐
         │   Plan Approved    │        │   Plan Declined    │
         └──────────┬─────────┘        └──────────┬─────────┘
                    │                             │
                    ▼                             ▼
         ┌────────────────────┐        ┌────────────────────┐
         │ Pattern Acceptance │        │  Log Plan Only     │
         │    Telemetry       │        │  No Execution      │
         └──────────┬─────────┘        └────────────────────┘
                    │
                    ▼
         ┌────────────────────┐
         │   Save Session     │
         │  (for refinement)  │
         └──────────┬─────────┘
                    │
                    ▼
         ┌────────────────────┐
         │ ConsultantExecutor │
         │  • Execute steps   │
         │  • Skill plugins   │
         │  • Error handling  │
         └──────────┬─────────┘
                    │
                    ▼
         ┌────────────────────┐
         │  Deployment Outcome│
         │     Telemetry      │
         │  • Success/failure │
         │  • Pattern learning│
         └──────────┬─────────┘
                    │
                    ▼
         ┌────────────────────┐
         │   Session Logging  │
         │  • Console logs    │
         │  • Transcript JSON │
         │  • Detailed report │
         └──────────┬─────────┘
                    │
                    ▼
         ┌────────────────────┐
         │  Final Result      │
         │  Return to User    │
         └────────────────────┘
```

## Key Components

### 1. **Execution Modes**
- **Multi-Agent Mode**: Uses SemanticAgentCoordinator with intelligent agent selection and specialized agents
- **Auto Mode**: Tries multi-agent first, falls back to plan-based if needed (recommended default)
- **Plan Mode**: Traditional LLM-based plan generation and execution with skill plugins

### 2. **SemanticAgentCoordinator**
The new multi-agent orchestration system with 4 phases:

1. **Intent Analysis**: LLM analyzes user request to determine intent and required agents
2. **Agent Routing**:
   - Single agent: Uses IntelligentAgentSelector with vector similarity and telemetry
   - Multiple agents: Orchestrates sequential or parallel agent execution
3. **Response Synthesis**: Aggregates agent results into coherent user response
4. **Next Steps**: Determines follow-up recommendations

### 3. **Specialized Agents**
Currently 15+ specialized agents available:
- ArchitectureConsultingAgent
- DeploymentConfigurationAgent
- DataIngestionAgent
- CertificateManagementAgent
- CloudPermissionGeneratorAgent
- ObservabilityConfigurationAgent
- DnsConfigurationAgent
- GitOpsConfigurationAgent
- BlueGreenDeploymentAgent
- SpaDeploymentAgent
- DiagramGeneratorAgent
- ArchitectureDocumentationAgent
- And more...

### 4. **Intelligence Features**

#### IntelligentAgentSelector
- Vector database similarity search for historical patterns
- Pattern telemetry analysis for success rates
- Confidence scoring (High ≥80%, Medium 50-79%, Low <50%)
- Automatic routing to best-fit agent

#### Pattern Telemetry
- Tracks pattern recommendations and acceptance
- Records deployment outcomes (success/failure)
- Learns from user feedback
- Improves future recommendations

#### Agent Critics
- Security validation
- Cost analysis
- Best practice compliance checks
- Warning generation

### 5. **Enhancement Features**

When not in dry-run mode:

1. **Architecture Diagram Generator**: ASCII art diagrams for terminal display
2. **Architecture Documentation Generator**: Comprehensive markdown documentation
3. **Metadata Configuration Display**: Shows Honua metadata.json structure
4. **Terraform Graph Support**: DOT format graph generation

### 6. **Approval Mechanisms**

Three ways to approve plans:
1. **Auto-approve flag**: `--auto-approve`
2. **High-confidence auto-approval**: `--trust-high-confidence` (≥80% confidence)
3. **Manual approval**: Interactive prompt

### 7. **Session Management**

- Session history stored in-memory during execution
- Persistent storage via IConsultantSessionStore
- Session refinement: `honua consultant refine --session {sessionId}`
- Multi-agent transcripts saved as JSON
- Plan-based logs saved as Markdown

## Data Flow

```
User Input
    ↓
Context Building (workspace scan, metadata detection, infrastructure detection)
    ↓
Mode Selection (multi-agent vs plan-based)
    ↓
[Multi-Agent Path]                    [Plan-Based Path]
    ↓                                      ↓
Intent Analysis                       LLM Plan Generation
    ↓                                      ↓
Agent Selection (intelligent)         Plan Formatting
    ↓                                      ↓
Agent Execution                       Enhancement Generation
    ↓                                      ↓
Response Synthesis                    User Approval
    ↓                                      ↓
    └──────────────┬───────────────────────┘
                   ↓
         Enhancement Display
                   ↓
            User Approval
                   ↓
         Telemetry Tracking
                   ↓
         Plan Execution
                   ↓
         Outcome Tracking
                   ↓
         Session Logging
                   ↓
            Final Result
```

## Telemetry & Learning

The system learns from every interaction:

1. **Pattern Recommendations**: Tracks which patterns are recommended
2. **Pattern Acceptance**: Tracks which patterns users approve
3. **Deployment Outcomes**: Tracks success/failure of deployments
4. **Agent Performance**: Tracks which agents successfully handle requests
5. **Vector Embeddings**: Stores request embeddings for similarity matching

This creates a feedback loop that improves recommendations over time.

## Configuration

The workflow is configured through:
- `appsettings.json`: LLM provider settings, telemetry options
- Environment variables: API keys, connection strings
- `~/.honua/`: User workspace, logs, session store
- Command-line flags: Mode selection, dry-run, auto-approve

## File Outputs

During execution, the workflow creates:

1. **Session Logs**: `~/.honua/logs/consultant-YYYYMMDD.md`
2. **Multi-Agent Transcripts**: `~/.honua/logs/consultant-YYYYMMDD-multi-{guid}.json`
3. **Architecture Documentation**: `~/.honua/logs/architecture-YYYYMMDD-HHmmss.md`
4. **Terraform Graph**: `terraform-graph.dot` (if applicable)
5. **Generated Configurations**: Terraform, Kubernetes, Docker Compose, etc.

---

*Last Updated: 2025-10-14*
*Based on: HonuaIO commit 3bff2b85*
