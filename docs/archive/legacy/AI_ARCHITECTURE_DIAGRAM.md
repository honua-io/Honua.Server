# Honua AI Architecture Diagram

## High-Level System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              USER REQUEST                                        │
│                    "Deploy production Honua to AWS with security"                │
└───────────────────────────────────────┬─────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                       SEMANTIC AGENT COORDINATOR                                 │
│  ┌────────────────────────────────────────────────────────────────────────┐    │
│  │  Multi-Provider LLM Router                                              │    │
│  │  ┌──────────────┐         ┌──────────────┐         ┌──────────────┐   │    │
│  │  │   OpenAI     │         │  Anthropic   │         │    Azure     │   │    │
│  │  │   (Fast)     │◄────────┤   (Smart)    │────────►│   OpenAI     │   │    │
│  │  └──────────────┘         └──────────────┘         └──────────────┘   │    │
│  │         │                         │                         │          │    │
│  │         └────────────┬────────────┴─────────────────────────┘          │    │
│  │                      ▼                                                  │    │
│  │          Smart Routing by Task Type                                    │    │
│  │          • Intent → OpenAI (fast)                                      │    │
│  │          • Security → Anthropic (deep)                                 │    │
│  │          • Architecture → Both (swarm)                                 │    │
│  └────────────────────────────────────────────────────────────────────────┘    │
│                                        │                                         │
│                                        ▼                                         │
│  ┌────────────────────────────────────────────────────────────────────────┐    │
│  │  PHASE 1: Intent Analysis (OpenAI - Fast)                              │    │
│  │  ┌──────────────────────────────────────────────────────────────────┐ │    │
│  │  │ Input: User request                                               │ │    │
│  │  │ Output: {                                                         │ │    │
│  │  │   primaryIntent: "deployment",                                    │ │    │
│  │  │   requiredAgents: ["Architecture", "Deployment", "Security"],    │ │    │
│  │  │   requiresMultipleAgents: true                                   │ │    │
│  │  │ }                                                                 │ │    │
│  │  └──────────────────────────────────────────────────────────────────┘ │    │
│  └────────────────────────────────────────────────────────────────────────┘    │
│                                        │                                         │
│                                        ▼                                         │
│  ┌────────────────────────────────────────────────────────────────────────┐    │
│  │  PHASE 2: Complexity Analysis                                          │    │
│  │  Hierarchical Task Decomposer (Anthropic)                              │    │
│  │  ┌──────────────────────────────────────────────────────────────────┐ │    │
│  │  │ Checks:                                                           │ │    │
│  │  │ • Multi-cloud? (AWS + Azure + GCP)                               │ │    │
│  │  │ • 4+ agents needed?                                               │ │    │
│  │  │ • High complexity keywords?                                       │ │    │
│  │  │                                                                   │ │    │
│  │  │ Decision: ShouldDecompose = true                                 │ │    │
│  │  │ Strategy: SequentialWithSubtasks                                 │ │    │
│  │  └──────────────────────────────────────────────────────────────────┘ │    │
│  └────────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────┬───────────────────────────────────────┘
                                          │
                    ┌─────────────────────┴─────────────────────┐
                    │                                             │
                    ▼                                             ▼
┌─────────────────────────────────────┐       ┌─────────────────────────────────────┐
│   SIMPLE PATH                        │       │   COMPLEX PATH                       │
│   (Single agent, no decomposition)   │       │   (Decomposed into phases)           │
│                                      │       │                                      │
│   Execute Agent                      │       │   See "Decomposed Workflow" below    │
│   ↓                                  │       │                                      │
│   Review (Security + Cost)           │       │                                      │
│   ↓                                  │       │                                      │
│   Return Result                      │       │                                      │
└──────────────────────────────────────┘       └──────────────────────────────────────┘
```

## Decomposed Workflow (Complex Path)

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                         HIERARCHICAL EXECUTION                                    │
└───────────────────────────────────────┬──────────────────────────────────────────┘
                                        │
                                        ▼
            ┌───────────────────────────────────────────────────────┐
            │  PHASE 1: Architecture Design                         │
            │  ┌─────────────────────────────────────────────────┐ │
            │  │   ARCHITECTURE SWARM (Parallel)                 │ │
            │  │                                                 │ │
            │  │   ┌──────────────┐  ┌──────────────┐          │ │
            │  │   │Cost Optimizer│  │Performance   │          │ │
            │  │   │  (OpenAI)    │  │Optimizer     │          │ │
            │  │   │              │  │(Anthropic)   │          │ │
            │  │   │"Serverless"  │  │"Dedicated"   │          │ │
            │  │   └──────┬───────┘  └──────┬───────┘          │ │
            │  │          │                  │                  │ │
            │  │   ┌──────┴──────────────────┴───────┐         │ │
            │  │   │                                  │         │ │
            │  │   ┌──────────────┐  ┌──────────────┐│         │ │
            │  │   │Simplicity    │  │Scalability   ││         │ │
            │  │   │Advocate      │  │Architect     ││         │ │
            │  │   │(OpenAI)      │  │(Anthropic)   ││         │ │
            │  │   │              │  │              ││         │ │
            │  │   │"Managed Svcs"│  │"Kubernetes"  ││         │ │
            │  │   └──────┬───────┘  └──────┬───────┘│         │ │
            │  │          │                  │        │         │ │
            │  │          └──────────┬───────┘        │         │ │
            │  │                     ▼                │         │ │
            │  │          ┌─────────────────────┐    │         │ │
            │  │          │ Round 1: Proposals  │    │         │ │
            │  │          │ Round 2: Critiques  │    │         │ │
            │  │          │ Round 3: Consensus  │    │         │ │
            │  │          └──────────┬──────────┘    │         │ │
            │  │                     ▼                │         │ │
            │  │          ┌─────────────────────┐    │         │ │
            │  │          │ 3 Options + Tradeoffs│   │         │ │
            │  │          │ User selects → Track │   │         │ │
            │  │          └─────────────────────┘    │         │ │
            │  └─────────────────────────────────────────────┘ │
            └───────────────────────┬───────────────────────────┘
                                    │
                                    ▼
            ┌───────────────────────────────────────────────────────┐
            │  PHASE 2: Configuration Generation (Parallel)         │
            │                                                        │
            │  ┌────────────────────────┐  ┌───────────────────┐   │
            │  │ DeploymentConfig Agent │  │ Security Agent    │   │
            │  │    (Anthropic)         │  │  (Anthropic)      │   │
            │  │                        │  │                   │   │
            │  │ Generates:             │  │ Generates:        │   │
            │  │ • Terraform            │  │ • IAM policies    │   │
            │  │ • docker-compose       │  │ • Security groups │   │
            │  │ • K8s manifests        │  │ • Secrets config  │   │
            │  └──────────┬─────────────┘  └──────────┬────────┘   │
            │             │                           │             │
            │             └───────────┬───────────────┘             │
            │                         ▼                             │
            │          ┌──────────────────────────┐                │
            │          │  Generated Artifacts     │                │
            │          │  • main.tf               │                │
            │          │  • iam-policies.json     │                │
            │          └──────────┬───────────────┘                │
            └─────────────────────┼──────────────────────────────┘
                                  │
                                  ▼
            ┌───────────────────────────────────────────────────────┐
            │  PHASE 3: Review & Critique (Parallel)                │
            │                                                        │
            │  ┌────────────────────────┐  ┌───────────────────┐   │
            │  │ Security Review Agent  │  │ Cost Review Agent │   │
            │  │    (Anthropic)         │  │   (OpenAI)        │   │
            │  │                        │  │                   │   │
            │  │ Heuristic Checks:      │  │ Heuristic Checks: │   │
            │  │ • Hardcoded creds      │  │ • Oversized DB    │   │
            │  │ • Public access        │  │ • No auto-scaling │   │
            │  │ • Missing encryption   │  │ • Expensive NAT   │   │
            │  │                        │  │ • Premium storage │   │
            │  │ LLM Analysis:          │  │                   │   │
            │  │ • Complex patterns     │  │ LLM Analysis:     │   │
            │  │ • Policy violations    │  │ • Right-sizing    │   │
            │  │ • Best practices       │  │ • Savings opps    │   │
            │  └──────────┬─────────────┘  └──────────┬────────┘   │
            │             │                           │             │
            │             └───────────┬───────────────┘             │
            │                         ▼                             │
            │          ┌──────────────────────────┐                │
            │          │  Issues Found?           │                │
            │          │  Yes: Regenerate         │                │
            │          │  No: Proceed ────────────┼───────────┐    │
            │          └──────────────────────────┘           │    │
            └────────────────────────────────────────────────┼────┘
                                                              │
                       ┌──────────────────────────────────────┘
                       │
                       ▼
            ┌───────────────────────────────────────────────────────┐
            │  PHASE 4: Validation Loop (if executing)              │
            │                                                        │
            │  ┌─────────────────────────────────────────────────┐ │
            │  │  Iteration 1                                    │ │
            │  │  ┌──────────┐   ┌──────────┐   ┌──────────┐   │ │
            │  │  │ Execute  │──▶│ Validate │──▶│ Success? │   │ │
            │  │  └──────────┘   └──────────┘   └─────┬────┘   │ │
            │  │                                       │         │ │
            │  │                           ┌───────────┤         │ │
            │  │                           │ No        │ Yes     │ │
            │  │                           ▼           │         │ │
            │  │                  ┌──────────────┐    │         │ │
            │  │                  │  Remediate   │    │         │ │
            │  │                  │  • Fix issue │    │         │ │
            │  │                  │  • Retry     │    │         │ │
            │  │                  └──────┬───────┘    │         │ │
            │  │                         │            │         │ │
            │  │  ┌──────────────────────┘            │         │ │
            │  │  │                                   │         │ │
            │  │  │  Iteration 2                      │         │ │
            │  │  │  ┌──────────┐   ┌──────────┐     │         │ │
            │  │  └─▶│ Execute  │──▶│ Validate │─────┘         │ │
            │  │     └──────────┘   └──────────┘               │ │
            │  │                                                │ │
            │  │  Max 3 iterations                             │ │
            │  │  Track all for learning                       │ │
            │  └─────────────────────────────────────────────────┘ │
            └───────────────────────────┬───────────────────────────┘
                                        │
                                        ▼
            ┌───────────────────────────────────────────────────────┐
            │  RESULT + TELEMETRY                                   │
            │                                                        │
            │  Success/Failure                                      │
            │  Agents Used                                          │
            │  Response                                             │
            │                                                        │
            │  Tracked for Learning:                                │
            │  • User architecture selection                        │
            │  • Security/cost issues found                         │
            │  • Validation loop iterations                         │
            │  • Success/failure patterns                           │
            └────────────────────────────────────────────────────────┘
```

## Multi-Provider Routing Logic

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    SMART LLM PROVIDER ROUTER                             │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
                  ┌──────────────────┴──────────────────┐
                  │                                     │
                  ▼                                     ▼
    ┌──────────────────────────┐         ┌──────────────────────────┐
    │   Single Provider Mode    │         │  Multi-Provider Mode     │
    │                           │         │                          │
    │  Only 1 API key configured│         │  Both keys configured    │
    │                           │         │                          │
    │  ┌─────────────────────┐ │         │  ┌────────────────────┐ │
    │  │  All Tasks          │ │         │  │  Task Analysis     │ │
    │  │         ↓           │ │         │  │                    │ │
    │  │  Same Provider      │ │         │  │  • Type            │ │
    │  │  (Anthropic/OpenAI) │ │         │  │  • Criticality     │ │
    │  └─────────────────────┘ │         │  │  • Latency needs   │ │
    │                           │         │  │  • Cost budget     │ │
    │  No routing overhead      │         │  └─────────┬──────────┘ │
    └───────────────────────────┘         │            │            │
                                          │            ▼            │
                                          │  ┌─────────────────────┐│
                                          │  │ Routing Decision    ││
                                          │  │                     ││
                                          │  │ Intent → OpenAI     ││
                                          │  │ Security → Anthropic││
                                          │  │ Cost → OpenAI       ││
                                          │  │ Critical → Anthropic││
                                          │  └─────────────────────┘│
                                          └──────────────────────────┘
```

## Provider Selection Matrix

```
┌──────────────────────────────────────────────────────────────────────────┐
│                     TASK TYPE → PROVIDER MAPPING                          │
├────────────────────────────┬─────────────────┬─────────────────────────┤
│ Task Type                  │ Provider        │ Reason                   │
├────────────────────────────┼─────────────────┼─────────────────────────┤
│ Intent Classification      │ OpenAI          │ Fast, cheap, consistent  │
│ Security Review            │ Anthropic       │ Deep analysis, reasoning │
│ Cost Review                │ OpenAI          │ Structured output, fast  │
│ Architecture Swarm         │ Both (parallel) │ Diverse perspectives     │
│ Code Generation            │ Anthropic       │ Complex Terraform/K8s    │
│ Troubleshooting            │ Anthropic       │ Root cause analysis      │
│ Summarization              │ OpenAI          │ Creative, fast           │
│ Validation                 │ Anthropic       │ Detailed verification    │
│ Critical Decisions         │ Anthropic       │ Superior reasoning       │
├────────────────────────────┼─────────────────┼─────────────────────────┤
│ CRITICALITY OVERRIDE       │                 │                          │
│ • Critical tasks           │ Anthropic       │ Always (if available)    │
│ • Latency < 2s             │ OpenAI          │ Faster response          │
│ • Cost < $0.01             │ OpenAI          │ Cheaper                  │
└────────────────────────────┴─────────────────┴─────────────────────────┘
```

## Learning Loop Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          LEARNING LOOP                                   │
│                                                                          │
│  Every deployment feeds back into knowledge base                        │
└──────────────────────────────────────┬──────────────────────────────────┘
                                       │
                                       ▼
        ┌──────────────────────────────────────────────────────┐
        │  TELEMETRY COLLECTION                                │
        │                                                       │
        │  ┌─────────────────────────────────────────────────┐ │
        │  │ Architecture Swarm Tracking                     │ │
        │  │ • Options presented                             │ │
        │  │ • User selection                                │ │
        │  │ • Requirements context                          │ │
        │  └────────────────────┬────────────────────────────┘ │
        │                       │                              │
        │  ┌────────────────────▼─────────────────────────────┐│
        │  │ Review Outcome Tracking                          ││
        │  │ • Pattern ID                                     ││
        │  │ • Security issues found                          ││
        │  │ • Cost issues found                              ││
        │  │ • Approved/rejected                              ││
        │  └────────────────────┬─────────────────────────────┘│
        │                       │                               │
        │  ┌────────────────────▼─────────────────────────────┐│
        │  │ Validation Loop Tracking                         ││
        │  │ • Iterations needed                              ││
        │  │ • Failure reasons                                ││
        │  │ • Ultimate success/failure                       ││
        │  └────────────────────┬─────────────────────────────┘│
        │                       │                               │
        │  ┌────────────────────▼─────────────────────────────┐│
        │  │ Deployment Outcome Tracking                      ││
        │  │ • Success/failure                                ││
        │  │ • User feedback                                  ││
        │  │ • Pattern used                                   ││
        │  └────────────────────┬─────────────────────────────┘│
        └────────────────────────┼─────────────────────────────┘
                                 │
                                 ▼
        ┌──────────────────────────────────────────────────────┐
        │  PATTERN SCORING ENGINE                              │
        │                                                       │
        │  For each deployment pattern:                        │
        │  ┌─────────────────────────────────────────────────┐ │
        │  │ Success Rate = successful / total               │ │
        │  │ Acceptance Rate = accepted / recommended        │ │
        │  │ Review Pass Rate = passed / generated           │ │
        │  │ First-Try Rate = validated_first / deployed     │ │
        │  │                                                 │ │
        │  │ Confidence Score = weighted average             │ │
        │  └─────────────────────────────────────────────────┘ │
        └─────────────────────────┬───────────────────────────┘
                                  │
                                  ▼
        ┌──────────────────────────────────────────────────────┐
        │  PATTERN KNOWLEDGE BASE                              │
        │                                                       │
        │  ┌─────────────────────────────────────────────────┐ │
        │  │ Pattern: aws-ecs-fargate-aurora                 │ │
        │  │ • Success Rate: 95%                             │ │
        │  │ • Acceptance Rate: 78%                          │ │
        │  │ • Review Pass Rate: 60%                         │ │
        │  │ • Common Issues:                                │ │
        │  │   - Hardcoded RDS password (40%)                │ │
        │  │   - Oversized instance (30%)                    │ │
        │  │ • User Preferences:                             │ │
        │  │   - Selected for cost-focused deployments       │ │
        │  │ • Confidence: HIGH                              │ │
        │  └─────────────────────────────────────────────────┘ │
        └─────────────────────────┬───────────────────────────┘
                                  │
                                  ▼
        ┌──────────────────────────────────────────────────────┐
        │  FUTURE RECOMMENDATIONS                              │
        │                                                       │
        │  When similar request comes in:                      │
        │  1. Search patterns by requirements                  │
        │  2. Rank by confidence score                         │
        │  3. Filter out low-success patterns                  │
        │  4. Proactively avoid common issues                  │
        │  5. Reflect user preferences                         │
        └──────────────────────────────────────────────────────┘
```

## Data Flow Example

```
Request: "Deploy production Honua to AWS"
    │
    ├─▶ Intent Analysis (OpenAI)
    │   └─▶ Result: {deployment, [Architecture, Deployment, Security]}
    │
    ├─▶ Decomposition Check (Anthropic)
    │   └─▶ Result: Not complex enough, skip decomposition
    │
    ├─▶ Architecture Swarm (Both providers)
    │   ├─▶ CostOptimizer (OpenAI): "ECS Fargate + Aurora Serverless"
    │   ├─▶ PerformanceOptimizer (Anthropic): "ECS EC2 + Aurora Provisioned"
    │   ├─▶ SimplicityAdvocate (OpenAI): "Fargate + RDS Managed"
    │   ├─▶ ScalabilityArchitect (Anthropic): "EKS + Aurora Global"
    │   └─▶ Consensus: 3 options presented
    │       └─▶ User selects: "CostOptimizer" ─┐
    │                                           │
    ├─▶ Generate Configuration (Anthropic)      │
    │   └─▶ Terraform for ECS Fargate + Aurora  │
    │                                           │
    ├─▶ Security Review (Anthropic)             │
    │   └─▶ Found: Hardcoded RDS password ──────┼─▶ Track for learning
    │       └─▶ Regenerate with Secrets Manager │
    │                                           │
    ├─▶ Cost Review (OpenAI)                    │
    │   └─▶ Found: db.r5.4xlarge oversized ─────┼─▶ Track for learning
    │       └─▶ Suggest: db.t3.large            │
    │                                           │
    ├─▶ Validation Loop (Anthropic)             │
    │   ├─▶ Deploy → Health check fail ─────────┼─▶ Track for learning
    │   ├─▶ Remediate: Add env var             │
    │   └─▶ Deploy → Success                    │
    │                                           │
    └─▶ Learning Loop ◀────────────────────────┘
        ├─▶ Pattern "ecs-fargate-aurora" used successfully
        ├─▶ Security issue "hardcoded-password" recorded
        ├─▶ Cost issue "oversized-rds" recorded
        ├─▶ User preferred "CostOptimizer" architecture
        └─▶ Future deployments improved!
```

## Component Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     COMPONENT RELATIONSHIPS                              │
└──────────────────────────────────────┬──────────────────────────────────┘
                                       │
        ┌──────────────────────────────┴──────────────────────────────┐
        │                                                              │
        ▼                                                              ▼
┌──────────────────┐                                      ┌──────────────────┐
│ LlmProviderFactory│                                     │ ILlmProvider     │
│                   │                                     │   (Primary)      │
│ • GetAvailable()  │────────creates────────────────────▶│                  │
│ • GetProvider()   │                                     │ • Anthropic      │
└────────┬──────────┘                                     │ • OpenAI         │
         │                                                │ • Azure          │
         │                                                └──────────────────┘
         │
         │                    ┌──────────────────┐
         └───────provides────▶│ SmartLlmRouter   │
                              │                  │
                              │ • RouteRequest() │
                              │ • SecondOpinion()│
                              │ • Consensus()    │
                              └────────┬─────────┘
                                       │
                                       │
                     ┌─────────────────┴─────────────────┐
                     │                                   │
                     ▼                                   ▼
        ┌───────────────────────┐          ┌───────────────────────┐
        │ SemanticAgent         │          │ Specialized Agents    │
        │ Coordinator           │          │                       │
        │                       │          │ • Architecture        │
        │ • ProcessRequest()    │──uses───▶│ • Deployment          │
        │ • AnalyzeIntent()     │          │ • Security            │
        │ • OrchestrateAgents() │          │ • Performance         │
        └───────────┬───────────┘          │ • etc.                │
                    │                      └───────────┬───────────┘
                    │                                  │
                    │                                  │
                    ├──────uses──────────────────┐    │
                    │                             │    │
                    ▼                             ▼    ▼
        ┌─────────────────────┐      ┌─────────────────────────┐
        │ HierarchicalTask    │      │ ValidationLoop          │
        │ Decomposer          │      │ Executor                │
        │                     │      │                         │
        │ • ShouldDecompose() │      │ • ExecuteWithValidation │
        │ • DecomposeAsync()  │      │ • ExecuteWithRetry      │
        └─────────────────────┘      └─────────────────────────┘
                    │                             │
                    │                             │
                    └──────────┬──────────────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │ Review Agents       │
                    │                     │
                    │ • SecurityReview    │
                    │ • CostReview        │
                    └──────────┬──────────┘
                               │
                               │
                               ▼
                    ┌─────────────────────┐
                    │ ArchitectureSwarm   │
                    │ Coordinator         │
                    │                     │
                    │ • 4 Agents (Swarm)  │
                    │ • Consensus         │
                    │ • Track Selection   │
                    └──────────┬──────────┘
                               │
                               │
                               ▼
                    ┌─────────────────────┐
                    │ IPatternUsage       │
                    │ Telemetry           │
                    │                     │
                    │ • TrackSwarm()      │
                    │ • TrackReview()     │
                    │ • TrackLoop()       │
                    │ • TrackOutcome()    │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │ Pattern Knowledge   │
                    │ Base                │
                    │                     │
                    │ • Search()          │
                    │ • Update Scores()   │
                    │ • Learn from Data   │
                    └─────────────────────┘
```

## File Structure

```
src/Honua.Cli.AI/
├── Services/
│   ├── AI/
│   │   ├── ILlmProvider.cs
│   │   ├── ILlmProviderFactory.cs
│   │   ├── LlmProviderFactory.cs ...................... Factory + availability
│   │   ├── ILlmProviderRouter.cs ....................... Router interface
│   │   ├── SmartLlmProviderRouter.cs ................... Smart routing logic
│   │   ├── LlmProviderOptions.cs ....................... Config (EnableSmartRouting)
│   │   └── Providers/
│   │       ├── AnthropicLlmProvider.cs
│   │       ├── OpenAILlmProvider.cs
│   │       ├── AzureOpenAILlmProvider.cs
│   │       └── MockLlmProvider.cs
│   │
│   ├── Agents/
│   │   ├── IAgentCoordinator.cs
│   │   ├── SemanticAgentCoordinator.cs ................. Main coordinator
│   │   ├── HierarchicalTaskDecomposer.cs ............... Decomposition logic
│   │   ├── ValidationLoopExecutor.cs ................... Retry with validation
│   │   ├── ArchitectureSwarmCoordinator.cs ............. Swarm pattern
│   │   └── Specialized/
│   │       ├── SecurityReviewAgent.cs .................. Security review
│   │       ├── CostReviewAgent.cs ...................... Cost review
│   │       ├── ArchitectureConsultingAgent.cs
│   │       ├── DeploymentConfigurationAgent.cs
│   │       ├── DeploymentExecutionAgent.cs
│   │       └── ... (other specialized agents)
│   │
│   └── VectorSearch/
│       ├── IPatternUsageTelemetry.cs ................... Telemetry interface
│       ├── IDeploymentPatternKnowledgeStore.cs
│       └── ... (vector search implementations)
│
docs/
├── AI_ARCHITECTURE_DIAGRAM.md .......................... This file
├── AI_MULTIAGENT_ENHANCEMENTS.md ....................... Full implementation guide
├── AI_MULTI_PROVIDER_SETUP.md .......................... Configuration guide
└── AI_IMPLEMENTATION_SUMMARY.md ........................ Summary
```
