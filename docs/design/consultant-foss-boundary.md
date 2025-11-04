# FOSS vs. AI-Powered CLI Feature Boundary

**Status**: Design Document
**Last Updated**: 2025-10-02
**Purpose**: Define clear boundary between open source CLI and AI-powered features

---

## Executive Summary

Honua follows a **FOSS-first philosophy** where all core administration capabilities are open source. The Consultant is a **separate, optional enhancement** that adds natural language interaction and intelligent automation on top of the FOSS foundation.

**Key Principle**: Users should be able to do everything manually with the FOSS CLI that the AI can do automatically.

---

## Feature Matrix

| Capability | FOSS CLI | Consultant | Notes |
|------------|----------|--------------|-------|
| **Configuration** |
| Initialize workspace | ✅ `honua init` | ✅ `honua assistant setup` | FOSS: Manual config; AI: Interactive wizard |
| Edit metadata | ✅ Manual YAML/JSON editing | ✅ Natural language updates | AI parses intent → generates config |
| Validate config | ✅ `honua validate` | ✅ Auto-validates during setup | Same validation, different interface |
| **Deployment** |
| Local Docker | ✅ `honua docker up` | ✅ `honua assistant deploy --local` | FOSS: Docker Compose; AI: Guided setup |
| Cloud deployment | ✅ Manual (Terraform/scripts) | ✅ `honua assistant deploy --cloud aws` | AI generates Terraform, FOSS uses it |
| TLS/SSL setup | ✅ `honua certs` commands | ✅ Auto-configured during wizard | FOSS: Manual certbot; AI: Integrated |
| **Database** |
| Create indexes | ✅ `honua db create-index` | ✅ "Optimize my database" | AI analyzes workload → suggests indexes |
| Run VACUUM | ✅ `honua db vacuum` | ✅ Auto-scheduled in plans | Same operation, AI schedules it |
| Manage schemas | ✅ `honua db migrate` | ✅ Schema evolution assistance | FOSS: Manual migrations; AI: Guided |
| **Performance** |
| View metrics | ✅ `honua metrics` | ✅ `honua assistant analyze` | FOSS: Raw metrics; AI: Insights |
| Optimize queries | ✅ Manual query tuning | ✅ "Why is this query slow?" | AI uses EXPLAIN, suggests fixes |
| Cache config | ✅ Edit `cache.json` | ✅ "Set up caching for X layer" | AI calculates optimal settings |
| **Troubleshooting** |
| View logs | ✅ `honua logs` | ✅ "What's wrong with my service?" | FOSS: Raw logs; AI: Root cause |
| Health checks | ✅ `honua health` | ✅ Proactive alerts | Same checks, AI monitors |
| Connection test | ✅ `honua db test-connection` | ✅ Auto-diagnosed in wizard | Same test, different UX |
| **Data Management** |
| Import data | ✅ `honua import shapefile` | ✅ "Load cities.shp into PostGIS" | Same importer, AI parses intent |
| Export data | ✅ `honua export --format csv` | ✅ "Export as CSV" | Same exporter |
| Schema inference | ✅ `honua schema analyze` | ✅ Auto-detected during import | Same analysis |
| **Migrations** |
| ArcGIS → Honua | ✅ Manual conversion scripts | ✅ "Migrate from ArcGIS Server" | AI orchestrates scripts |
| GeoServer → Honua | ✅ Manual config mapping | ✅ Guided migration | AI maps configs |
| **Monitoring** |
| Performance stats | ✅ `honua stats` | ✅ AI-powered insights | FOSS: Numbers; AI: Trends |
| Alert rules | ✅ Config files | ✅ Natural language rules | AI generates alert configs |
| Dashboards | ✅ Prometheus/Grafana | ✅ Auto-configured | AI generates dashboard JSON |

---

## Design Philosophy

### 1. FOSS Foundation (Core CLI)

**Goal**: Empower users with full manual control

**Characteristics**:
- **Explicit Commands**: Every operation has a clear CLI command
- **Documentation-Driven**: Comprehensive man pages and guides
- **Scriptable**: All commands work in automation/CI/CD
- **No Magic**: Users understand what's happening
- **No External Dependencies**: Works offline, no API keys needed

**Example FOSS Workflow** (Production deployment):
```bash
# 1. Initialize workspace
honua init --provider postgres --connection "postgresql://..."

# 2. Import data
honua import shapefile cities.shp --table cities --srid 4326

# 3. Create indexes (manual optimization)
honua db create-index cities geom --type gist
honua db create-index cities name --type btree

# 4. Generate metadata
honua metadata generate --format yaml

# 5. Validate configuration
honua validate

# 6. Start service
honua serve --port 8080

# 7. Set up TLS (manual)
honua certs request gis.example.com --provider letsencrypt
honua certs install gis.example.com

# 8. Monitor
honua metrics --prometheus
```

**Users who prefer FOSS approach**:
- DevOps engineers (want scriptable commands)
- Security-conscious orgs (no AI, no external APIs)
- Offline/air-gapped deployments
- Users who want to learn deeply

---

### 2. Consultant (Enhanced UX)

**Goal**: Lower barrier to entry, automate complex workflows

**Characteristics**:
- **Natural Language**: "Set up PostGIS for my cities data"
- **Intelligent Planning**: Analyzes workspace → suggests optimizations
- **Contextual Help**: Understands user's environment
- **Proactive**: Monitors and suggests improvements
- **Orchestration**: Chains multiple FOSS commands together

**Example AI Workflow** (Same production deployment):
```bash
# Single command - AI does everything above
$ honua assistant setup --production

👋 I'll set up a production Honua service.

📊 Analyzing your environment...
   ✓ Found: cities.shp (125,000 features)
   ✓ Detected: PostgreSQL 16 with PostGIS
   ✓ Available: Docker, Let's Encrypt

📋 Execution Plan (15 min):

  Step 1: Import cities.shp → PostgreSQL
  Step 2: Create spatial index on geometry (est. 30s)
  Step 3: Create B-tree index on 'name' (frequently queried)
  Step 4: Generate OGC API metadata
  Step 5: Request TLS certificate for gis.example.com
  Step 6: Configure HTTPS with HSTS
  Step 7: Start service on port 443

💰 Estimated cost: $0 (local deployment)
⏱  Estimated time: 15 minutes
🔒 Risk: Low (all changes reversible)

Apply this plan? [y/N]: y

[... executes all 7 steps ...]

✅ Service live at https://gis.example.com/ogc
```

**Users who prefer AI approach**:
- First-time users (low barrier to entry)
- Time-constrained teams (automation)
- Users migrating from commercial platforms
- Those who want "best practices by default"

---

## Technical Architecture

### FOSS CLI Implementation

**Location**: `src/Honua.Cli/` (existing project)

**Commands**:
```
honua init          # Initialize workspace
honua import        # Import data
honua export        # Export data
honua serve         # Start server
honua db            # Database commands
honua certs         # Certificate management
honua validate      # Configuration validation
honua logs          # View logs
honua metrics       # Performance metrics
honua health        # Health checks
honua schema        # Schema analysis
honua migrate       # Data migrations
```

**Key Files**:
- `src/Honua.Cli/Commands/*.cs` - All CLI commands
- `src/Honua.Cli/Services/*.cs` - Business logic services
- `docs/cli/*.md` - Command documentation

**No Dependencies On**:
- ❌ LLM providers (OpenAI, Anthropic, etc.)
- ❌ Cloud services (except target deployment)
- ❌ Semantic Kernel or AI frameworks
- ❌ Internet connectivity (for core features)

---

### Consultant Implementation

**Location**: `src/Honua.Cli.AI/` (separate project)

**How It Works**:
1. **User Input**: Natural language prompt
2. **Planning**: LLM analyzes intent → generates ExecutionPlan
3. **Execution**: Calls FOSS CLI commands under the hood
4. **Monitoring**: Tracks progress, reports errors

**Example Under the Hood**:
```
User: "Optimize my database"

Consultant:
  1. Calls: honua db analyze (FOSS command)
  2. Reads: Query statistics
  3. LLM: Analyzes slow queries → recommends indexes
  4. Generates ExecutionPlan:
     - Step 1: honua db create-index cities geom --type gist
     - Step 2: honua db vacuum --analyze
     - Step 3: honua db update-statistics
  5. User approves
  6. Executes each FOSS command
  7. Reports results
```

**Key Principle**: **Consultant = Intelligent wrapper around FOSS CLI**

The AI never does anything the FOSS CLI can't do manually. It just makes it easier.

---

## Licensing Model

### FOSS CLI (Apache 2.0 / MIT)

**What's Included**:
- All CLI commands
- All core services
- All database providers
- All import/export tools
- Certificate management
- Performance monitoring
- Documentation

**User Obligations**:
- None (open source)
- Can use commercially
- Can modify and redistribute

---

### Consultant (TBD - Separate License)

**Options Under Consideration**:

**Option 1: FOSS with BYOK (Bring Your Own Key)**
- Code is open source
- Users provide their own OpenAI/Anthropic API key
- No license restrictions
- **Pros**: Maximum openness, user controls costs
- **Cons**: Users must pay for LLM API calls

**Option 2: Freemium**
- Free tier: 10 AI operations/month
- Paid tier: Unlimited + premium models
- Code is proprietary
- **Pros**: Easy onboarding, monetization
- **Cons**: Less community contribution

**Option 3: Commercial Only**
- Consultant is closed-source commercial product
- Sold as plugin to FOSS CLI
- **Pros**: Clear business model
- **Cons**: May limit adoption

**Recommended**: **Option 1 (FOSS with BYOK)**
- Aligns with Honua's open philosophy
- Users in full control
- Community can contribute improvements
- Can still offer hosted version as SaaS

---

## User Scenarios

### Scenario 1: Local Development (FOSS Only)

**User**: Developer testing locally

**Workflow**:
```bash
# No AI needed - simple setup
honua init --provider sqlite --path ./data.gpkg
honua import geojson cities.json
honua serve --port 8080
```

**Why FOSS**: Simple task, no need for AI complexity

---

### Scenario 2: First Production Deploy (Consultant)

**User**: Small GIS team, first time using Honua

**Workflow**:
```bash
# AI guides through complex setup
honua assistant setup --production --cloud aws

# AI asks questions:
# - What AWS region?
# - What database size? (small/medium/large)
# - Enable CDN? (y/n)
# - Custom domain?

# AI generates Terraform, deploys everything
```

**Why AI**: Complex task, many decisions, best practices needed

---

### Scenario 3: Performance Troubleshooting (Hybrid)

**User**: Service is slow, needs optimization

**Option A: FOSS Approach**:
```bash
# Manual investigation
honua metrics --slow-queries
honua db analyze cities
honua logs --level warn --since 1h

# Read output, make decisions
honua db create-index cities name
honua db vacuum --analyze
honua cache configure --ttl 3600
```

**Option B: AI Approach**:
```bash
# Let AI diagnose
honua assistant diagnose "Service is slow"

# AI analyzes:
# - Slow query logs
# - Missing indexes
# - Cache hit rate
# - Database statistics

# Presents ExecutionPlan with fixes
# User approves, AI applies
```

**Why Both**: FOSS for learning/control, AI for speed

---

### Scenario 4: Migration from ArcGIS (AI Recommended)

**User**: Enterprise migrating from ArcGIS Server

**Why AI**:
- Complex mapping of ArcGIS concepts → Honua
- Need to preserve layer names, symbology, permissions
- Risk of data loss if done manually
- AI can validate each step

**Workflow**:
```bash
honua assistant migrate \
  --from arcgis \
  --url https://arcgis.example.com/rest \
  --layers "Cities,Roads,Parcels"

# AI:
# - Introspects ArcGIS REST API
# - Maps to Honua metadata
# - Generates migration plan
# - User reviews before applying
```

**FOSS Alternative**:
```bash
# Manual migration (possible but tedious)
honua migrate arcgis-to-metadata \
  --url https://arcgis.example.com/rest \
  --output metadata.yaml

# Edit metadata.yaml manually
# Fix any mapping errors

honua import arcgis-layer \
  --url https://arcgis.example.com/rest/services/Cities \
  --table cities

# Repeat for each layer...
```

---

## Security & Privacy Considerations

### FOSS CLI
- ✅ **No telemetry** (unless explicitly enabled)
- ✅ **No external API calls**
- ✅ **Full control over credentials**
- ✅ **Auditable code** (open source)
- ✅ **Works offline**

### Consultant
- ⚠️ **Requires LLM API access** (OpenAI/Anthropic/Ollama)
- ⚠️ **Sends metadata to LLM** (never data, only schema/config)
- ⚠️ **Credential handling**: Uses scoped tokens (see safety model)
- ✅ **Opt-in telemetry** (can be disabled)
- ✅ **Local LLM option** (Ollama for air-gapped)

**For Security-Conscious Users**:
- Use FOSS CLI only (no AI)
- OR use AI with local Ollama (no cloud APIs)
- OR use AI with organization's Azure OpenAI (data stays in tenant)

---

## Documentation Strategy

### FOSS CLI Documentation
- **Man Pages**: `man honua-init`, `man honua-db`, etc.
- **Online Docs**: Comprehensive CLI reference
- **Tutorials**: Step-by-step guides for common tasks
- **Examples**: Sample scripts for automation

### Consultant Documentation
- **Conversational Guide**: How to talk to the AI
- **Example Prompts**: Common tasks in natural language
- **Safety Model**: How plan/apply works
- **Troubleshooting**: What to do if AI misunderstands

---

## Migration Path

### For Existing FOSS Users
1. Continue using FOSS CLI (nothing changes)
2. Optionally try AI for complex tasks
3. AI can learn from your existing scripts

### For New Users
1. Start with Consultant (fast onboarding)
2. Graduate to FOSS CLI as you learn
3. Use hybrid approach (AI for new tasks, FOSS for automation)

---

## Summary

| Aspect | FOSS CLI | Consultant |
|--------|----------|--------------|
| **Philosophy** | Manual control, explicit commands | Natural language, intelligent automation |
| **Audience** | DevOps, power users, learners | Beginners, time-constrained teams |
| **Dependencies** | None (offline capable) | LLM API (OpenAI/Ollama/etc.) |
| **Licensing** | Apache 2.0 / MIT | TBD (recommend FOSS with BYOK) |
| **Privacy** | No telemetry, no external calls | Metadata sent to LLM (configurable) |
| **Learning Curve** | Steeper (must learn commands) | Gentle (conversational) |
| **Automation** | Perfect for scripts/CI/CD | Great for ad-hoc tasks |
| **Cost** | Free | LLM API costs (if using cloud) |

**Key Insight**: The Consultant is not a replacement for the FOSS CLI—it's a **higher-level interface** that calls FOSS commands under the hood. Users who want to understand what's happening can always drop down to the FOSS level.

This approach ensures:
1. ✅ FOSS users can do everything manually
2. ✅ AI users get intelligent assistance
3. ✅ No vendor lock-in (AI calls open source tools)
4. ✅ Transparency (AI plans show exact commands)
5. ✅ Trust (users approve before execution)

---

## Next Steps

1. **Document FOSS CLI commands** - Ensure all planned AI features have FOSS equivalents
2. **Decide licensing** for Consultant (recommend FOSS with BYOK)
3. **Build both in parallel** - FOSS commands first, then AI wrapper
4. **Test migration path** - Ensure users can move between FOSS and AI seamlessly
