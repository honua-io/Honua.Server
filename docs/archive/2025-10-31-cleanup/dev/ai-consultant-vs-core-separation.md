# AI Consultant vs Core Server - Clear Separation

## The Principle

**Core Server: Zero AI Dependencies**
- Fast, predictable, standards-compliant
- No machine learning
- No LLM calls
- Simple deployment
- Works offline

**AI Consultant: All Intelligence**
- Analyzes your setup
- Makes recommendations
- Generates configuration
- Proposes optimizations
- Requires API connection

## Complete Feature Separation

### Honua Core Server (Open Source)

**What it does:**
- ✅ Serve OGC API - Features
- ✅ Serve OGC API - Tiles
- ✅ Serve Esri Geoservices REST API
- ✅ Connect to databases (PostGIS, SQLite, SQL Server)
- ✅ Read metadata (YAML, JSON, Git)
- ✅ Authenticate users (JWT, OAuth, API keys)
- ✅ Serve raster tiles (S3, Azure, filesystem)
- ✅ Export data (GeoJSON, Shapefile, GeoPackage, CSV)
- ✅ Cache responses
- ✅ Log requests

**What it does NOT do:**
- ❌ No AI
- ❌ No ML models
- ❌ No LLM calls
- ❌ No "smart" features
- ❌ No learning from usage

**Configuration:**
```yaml
# metadata.yaml - Written by humans or AI Consultant
apiVersion: honua.io/v1
kind: Service
metadata:
  name: parcels
spec:
  title: Property Parcels
  layers:
    - name: parcels
      datasource: postgis-primary
      table: parcels
      geometryColumn: geom
```

---

### AI Consultant (Commercial SaaS)

**What it does:**
- ✅ Chat interface for configuration
- ✅ Analyze database schemas
- ✅ Generate metadata YAML
- ✅ Detect performance issues
- ✅ Recommend indexes
- ✅ Suggest caching strategies
- ✅ Identify security risks
- ✅ Create Git PRs
- ✅ Review configurations
- ✅ Answer questions
- ✅ Explain concepts
- ✅ Generate migrations
- ✅ Optimize queries (by changing config)
- ✅ Detect breaking changes
- ✅ Suggest best practices

**What it does NOT do:**
- ❌ Does not run IN the Honua server
- ❌ Does not intercept requests
- ❌ Does not modify runtime behavior
- ❌ Does not cache or store data

**Architecture:**
```
User → AI Consultant → Generates YAML → Git PR → Honua reads YAML
```

---

## Examples: What Goes Where

### Example 1: Slow Queries

**❌ Wrong Approach (AI in Core):**
```csharp
// In Honua server runtime
public async Task<FeatureCollection> GetFeaturesAsync(FeatureQuery query)
{
    // Call AI to optimize query
    query = await _aiService.OptimizeQueryAsync(query); // ❌ NO!

    return await _dataStore.QueryAsync(query);
}
```

**✅ Right Approach (AI in Consultant):**
```bash
# User asks AI Consultant
User: "Parcel queries are slow"

AI Consultant:
  1. Analyzes query logs (if shared)
  2. Inspects database schema
  3. Detects missing spatial index
  4. Generates migration SQL
  5. Creates PR with:
     - Migration to add index
     - Updated metadata with index hint

# User merges PR
# Honua reloads metadata
# Queries are now fast (because of index, not AI)
```

### Example 2: Smart Caching

**❌ Wrong Approach (AI in Core):**
```csharp
// In Honua server runtime
public async Task<TimeSpan> GetCacheDurationAsync(string layerId)
{
    // Call AI to predict cache duration
    return await _aiService.PredictCacheDurationAsync(layerId); // ❌ NO!
}
```

**✅ Right Approach (AI in Consultant):**
```bash
# User asks AI Consultant
User: "How should I configure caching?"

AI Consultant:
  1. Asks about data update frequency
  2. Analyzes typical query patterns (if shared)
  3. Recommends caching strategy
  4. Generates configuration:

# Generated metadata.yaml
caching:
  parcels:
    ttl: 3600      # AI determined: data changes daily
  vehicles:
    ttl: 30        # AI determined: real-time data

# Honua uses these fixed values (no runtime AI needed)
```

### Example 3: Index Recommendations

**❌ Wrong Approach (AI in Core):**
```csharp
// In Honua server runtime
public async Task<List<string>> GetIndexRecommendationsAsync()
{
    // Analyze queries and recommend indexes
    return await _aiService.RecommendIndexesAsync(); // ❌ NO!
}
```

**✅ Right Approach (AI in Consultant):**
```bash
# User asks AI Consultant
User: "What indexes should I create?"

AI Consultant:
  1. Connects to database
  2. Analyzes existing indexes
  3. Reviews common query patterns
  4. Generates recommendations:

Recommended indexes:
1. CREATE INDEX idx_parcels_zoning ON parcels(zoning_type);
   Reason: Used in 45% of queries
   Expected speedup: 10-20x

2. CREATE INDEX idx_parcels_owner ON parcels(owner_name);
   Reason: Used in 30% of queries
   Expected speedup: 5-10x

Would you like me to create a PR with these migrations?

# User approves
# AI creates migration PR
# User reviews and merges
# Indexes are created
# Queries get faster (because of indexes, not AI)
```

### Example 4: Data Validation

**❌ Wrong Approach (AI in Core):**
```csharp
// In Honua server runtime
public async Task<bool> ValidateFeatureAsync(Feature feature)
{
    // Use AI to detect anomalies
    var anomalies = await _aiService.DetectAnomaliesAsync(feature); // ❌ NO!
    return !anomalies.Any();
}
```

**✅ Right Approach (AI in Consultant):**
```bash
# User asks AI Consultant
User: "Help me validate incoming parcel data"

AI Consultant:
  1. Analyzes existing data patterns
  2. Identifies validation rules
  3. Generates validation configuration:

# Generated validation.yaml
validation:
  parcels:
    rules:
      - field: area
        min: 500
        max: 1000000
        reason: "Area must be between 500 and 1M sq ft"

      - field: geometry
        type: polygon
        check: is_valid

      - field: zoning_type
        allowed_values: [R1, R2, C1, C2, I1]

# Honua enforces these rules (simple validation, no AI)
```

---

## Key Insight

**AI Consultant generates STATIC CONFIGURATION**

The AI doesn't need to run at request time. Instead:

1. AI analyzes once
2. AI generates config
3. Honua uses config (no AI needed)
4. If situation changes, ask AI again

**Benefits:**
- ✅ Honua stays fast and simple
- ✅ Predictable behavior
- ✅ Works offline
- ✅ No AI API costs per request
- ✅ Easy to debug
- ✅ Config can be reviewed by humans

---

## User Experience Comparison

### Without AI Consultant

```bash
# User manually writes YAML
vim metadata.yaml

# User manually creates indexes
psql -c "CREATE INDEX idx_parcels_zoning ON parcels(zoning_type)"

# User manually tunes caching
vim metadata.yaml  # Add caching config

# User deploys
git commit -m "Add parcels layer"
git push
```

**Time:** 2-3 hours for experienced user

### With AI Consultant

```bash
# User chats with AI
honua ai "Add parcels layer from postgis.parcels table"

AI:
  [Analyzes table schema]
  [Generates metadata.yaml]
  [Detects missing indexes]
  [Recommends caching strategy]
  [Creates PR with everything]

# User reviews PR
gh pr view 42

# Looks good, merge
gh pr merge 42
```

**Time:** 5-10 minutes

**AI generates the config, Honua executes the config. Clean separation!**

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        USER                                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
                 ┌────────────────────────┐
                 │   AI Consultant        │
                 │   (Commercial SaaS)    │
                 │                        │
                 │  - Analyzes            │
                 │  - Recommends          │
                 │  - Generates config    │
                 │  - Creates PRs         │
                 └────────────────────────┘
                              │
                              │ Generates
                              ▼
                 ┌────────────────────────┐
                 │   Git Repository       │
                 │   honua-config/        │
                 │                        │
                 │  - metadata.yaml       │
                 │  - caching.yaml        │
                 │  - migrations/         │
                 └────────────────────────┘
                              │
                              │ Reads
                              ▼
                 ┌────────────────────────┐
                 │   Honua Server         │
                 │   (Open Source)        │
                 │                        │
                 │  - Reads YAML          │
                 │  - Serves data         │
                 │  - NO AI               │
                 └────────────────────────┘
                              │
                              │ Queries
                              ▼
                 ┌────────────────────────┐
                 │   PostGIS Database     │
                 └────────────────────────┘
```

**AI is completely outside the request path!**

---

## What This Means for Development

### Honua Core Development

**Focus on:**
- Fast query execution
- Standards compliance
- Reliability
- Simplicity
- Performance

**NO need to worry about:**
- AI/ML models
- LLM integration
- Learning algorithms
- Training data

### AI Consultant Development

**Focus on:**
- Database introspection
- YAML generation
- Best practice detection
- Performance analysis
- Security scanning
- Migration generation

**Built with:**
- Claude/GPT-4 API
- Database analyzers
- Configuration generators
- Git integration

---

## Pricing Implications

**Honua Core (Free & Open Source):**
- Zero AI costs
- Zero API calls
- Runs anywhere
- No usage limits

**AI Consultant (Paid):**
- Pay per query
- Or flat monthly fee
- Requires internet connection
- Usage-based pricing makes sense

**This makes the free tier actually free!**

Users can run Honua forever without paying anything if they write YAML by hand.

---

## Summary

**Honua Core Server:**
- Configuration-driven
- No runtime AI
- Fast and simple
- Reads YAML, serves data
- Works offline

**AI Consultant:**
- Generates configuration
- Analyzes and recommends
- Creates PRs
- Runs separately
- Requires API connection

**Clean separation. Simple architecture. Best of both worlds!**
