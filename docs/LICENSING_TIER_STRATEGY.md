# HonuaIO Licensing Tier Strategy

## Executive Summary

This document defines the **Free**, **Professional**, and **Enterprise** tiers for HonuaIO, based on customer segmentation, feature value, and competitive positioning.

## Customer Segmentation

### Free Tier → Hobbyists, Students, Startups
**Profile:**
- Individual developers or small teams (1-3 people)
- Prototyping or personal projects
- Limited budget, not mission-critical
- Self-service support via docs/community

**Goal:** Get users hooked, build community, create evangelists

### Professional Tier → Growing Companies, Consultancies
**Profile:**
- Small-medium teams (5-25 people)
- Production applications with moderate scale
- Budget for tools, need reliability
- Email/ticket support acceptable

**Goal:** Monetize companies that have budget but don't need enterprise features

### Enterprise Tier → Large Organizations, Data-Intensive
**Profile:**
- Large teams, multiple departments
- Mission-critical applications
- Complex infrastructure (multi-cloud, data warehouses)
- Need SLA, priority support, compliance features

**Goal:** Capture high-value customers willing to pay premium for scale/support

---

## Recommended Tiering Strategy

### Tier Comparison Table

| Feature Category | Free | Professional | Enterprise |
|-----------------|------|--------------|------------|
| **Price** | $0 | $99-299/month | $999-2,999/month |
| **Users** | 1 | 10 | Unlimited |
| **Collections/Layers** | 10 | 100 | Unlimited |
| **API Requests/Day** | 10,000 | 100,000 | Unlimited |
| **Storage** | 5GB | 100GB | Unlimited |

### Data Providers

| Provider | Free | Pro | Enterprise |
|----------|------|-----|------------|
| **PostgreSQL/PostGIS** | ✅ | ✅ | ✅ |
| **MySQL (Spatial)** | ✅ | ✅ | ✅ |
| **SQLite/SpatiaLite** | ✅ | ✅ | ✅ |
| **SQL Server** | ❌ | ✅ | ✅ |
| **Oracle Spatial** | ❌ | ❌ | ✅ |
| **Snowflake** | ❌ | ❌ | ✅ |
| **BigQuery** | ❌ | ❌ | ✅ |
| **Redshift** | ❌ | ❌ | ✅ |
| **MongoDB** | ❌ | ❌ | ✅ |
| **Cosmos DB** | ❌ | ❌ | ✅ |
| **Elasticsearch** | ❌ | ❌ | ✅ |

**Rationale:**
- **Free:** Open source databases only (most accessible)
- **Pro:** Add SQL Server (common in enterprises, mid-tier licensing)
- **Enterprise:** Cloud data warehouses + NoSQL (high-value, analytics use cases)

### Authentication & Security

| Feature | Free | Pro | Enterprise |
|---------|------|-----|------------|
| **Local Users/Passwords** | ✅ | ✅ | ✅ |
| **JWT/API Keys** | ✅ | ✅ | ✅ |
| **OIDC/OAuth2** | ❌ | ✅ | ✅ |
| **SAML/SSO** | ❌ | ❌ | ✅ |
| **Advanced Audit Logging** | ❌ | ❌ | ✅ |
| **Role-Based Access (RBAC)** | Basic | Advanced | Advanced |

**Rationale:**
- **Free:** Basic auth for getting started
- **Pro:** OIDC for modern app integration (Auth0, Okta)
- **Enterprise:** SAML for corporate SSO (Microsoft AD, Google Workspace)

### APIs & Standards

| API/Protocol | Free | Pro | Enterprise |
|--------------|------|-----|------------|
| **OGC API Features** | ✅ | ✅ | ✅ |
| **OGC API Tiles** | ✅ | ✅ | ✅ |
| **WMS 1.3** | ✅ | ✅ | ✅ |
| **WFS 2.0** | ✅ | ✅ | ✅ |
| **WCS 2.0** | ✅ | ✅ | ✅ |
| **STAC 1.0** | ❌ | ✅ | ✅ |
| **Esri REST (FeatureServer)** | ✅ | ✅ | ✅ |
| **Esri REST (MapServer)** | ✅ | ✅ | ✅ |
| **OData v4** | ❌ | ✅ | ✅ |

**Rationale:**
- **Free:** Core OGC APIs (standard, widely needed)
- **Pro:** Add STAC (cloud-native geospatial) and OData (BI tools)
- **Enterprise:** Everything (no feature restrictions)

### Processing & Analysis

| Feature | Free | Pro | Enterprise |
|---------|------|-----|------------|
| **Basic Queries (bbox, filter)** | ✅ | ✅ | ✅ |
| **CQL2 Filtering** | ✅ | ✅ | ✅ |
| **Vector Tiles (MVT)** | ✅ | ✅ | ✅ |
| **Raster Tiles (XYZ)** | ✅ | ✅ | ✅ |
| **Simple Geoprocessing** | ❌ | ✅ | ✅ |
| **Cloud Batch Processing** | ❌ | ❌ | ✅ |
| **Advanced Analytics** | ❌ | ❌ | ✅ |

**Rationale:**
- **Free:** Read-only queries and tile generation
- **Pro:** Add processing (buffer, intersect, union) - valuable for apps
- **Enterprise:** Massive scale batch processing (AWS Batch, etc.)

### Data Management

| Feature | Free | Pro | Enterprise |
|---------|------|-----|------------|
| **Transactional Editing** | ✅ | ✅ | ✅ |
| **Attachments** | ✅ | ✅ | ✅ |
| **Versioning/History** | ❌ | ❌ | ✅ |
| **Branching/Merging** | ❌ | ❌ | ✅ |
| **Multi-tenancy** | ❌ | ❌ | ✅ |
| **GitOps Integration** | ❌ | ❌ | ✅ |

**Rationale:**
- **Free:** Basic editing workflows
- **Pro:** Same as Free (keep simple)
- **Enterprise:** Advanced data governance (versioning, multi-tenant isolation)

### Integration & BI

| Feature | Free | Pro | Enterprise |
|---------|------|-----|------------|
| **REST API** | ✅ | ✅ | ✅ |
| **OpenAPI/Swagger** | ✅ | ✅ | ✅ |
| **Power BI Connector** | ❌ | ✅ | ✅ |
| **Tableau Connector** | ❌ | ✅ | ✅ |
| **Webhooks** | ❌ | ✅ | ✅ |

**Rationale:**
- **Free:** API access only
- **Pro:** BI integrations (key differentiator for business users)
- **Enterprise:** Full integration suite

### Export Formats

| Format | Free | Pro | Enterprise |
|--------|------|-----|------------|
| **GeoJSON** | ✅ | ✅ | ✅ |
| **CSV** | ✅ | ✅ | ✅ |
| **Shapefile** | ✅ | ✅ | ✅ |
| **GeoPackage** | ✅ | ✅ | ✅ |
| **KML/KMZ** | ✅ | ✅ | ✅ |
| **GML** | ❌ | ✅ | ✅ |
| **TopoJSON** | ❌ | ✅ | ✅ |
| **Parquet** | ❌ | ❌ | ✅ |

**Rationale:**
- **Free:** Common formats (covers 95% of use cases)
- **Pro:** Add advanced formats
- **Enterprise:** Cloud-optimized formats (Parquet for analytics)

### Observability & Operations

| Feature | Free | Pro | Enterprise |
|---------|------|-----|------------|
| **Basic Logs** | ✅ | ✅ | ✅ |
| **Metrics (Prometheus)** | ✅ | ✅ | ✅ |
| **Health Checks** | ✅ | ✅ | ✅ |
| **OpenTelemetry** | ❌ | ✅ | ✅ |
| **Advanced Dashboards** | ❌ | ✅ | ✅ |
| **Custom Alerting** | ❌ | ❌ | ✅ |
| **Performance Profiling** | ❌ | ❌ | ✅ |

**Rationale:**
- **Free:** Basic visibility
- **Pro:** Production-ready monitoring
- **Enterprise:** Advanced ops features

### Support & SLA

| Support Type | Free | Pro | Enterprise |
|--------------|------|-----|------------|
| **Community Forum** | ✅ | ✅ | ✅ |
| **Documentation** | ✅ | ✅ | ✅ |
| **Email Support** | ❌ | ✅ | ✅ |
| **Response Time** | - | 2 business days | 4 hours |
| **Priority Bug Fixes** | ❌ | ❌ | ✅ |
| **Dedicated Engineer** | ❌ | ❌ | ✅ (add-on) |
| **Uptime SLA** | None | None | 99.9% |
| **Professional Services** | ❌ | ❌ | Available |

**Rationale:**
- **Free:** Self-service only
- **Pro:** Standard support for businesses
- **Enterprise:** Mission-critical support + SLA

---

## Alternative: Simpler Two-Tier Model

If you want to start simpler, consider **Free + Enterprise only**:

### Free (Hobbyists + Startups)
- All core features
- PostgreSQL/MySQL/SQLite only
- Local auth only
- Community support
- 10K requests/day, 5GB storage

### Enterprise (Everything Else)
- Everything in Free +
- All data providers
- SAML/SSO
- BI connectors
- Cloud batch processing
- Versioning/multi-tenancy
- Priority support + SLA
- Unlimited usage

**Pricing:** $0 → $1,499/month (simpler sales motion)

---

## Pricing Recommendations

### Monthly Pricing

| Tier | Monthly | Annual (save 20%) |
|------|---------|-------------------|
| **Free** | $0 | $0 |
| **Professional** | $299 | $2,388 ($199/mo) |
| **Enterprise** | $1,499 | $11,988 ($999/mo) |

### Per-Seat Pricing (Alternative)

| Tier | Per User/Month | Min Seats |
|------|----------------|-----------|
| **Free** | $0 | 1 |
| **Professional** | $49/user | 5 ($245/mo min) |
| **Enterprise** | Custom | 25+ |

### Usage-Based Add-Ons

- Extra storage: $10/100GB/month
- Extra API requests: $5/100K requests/month
- Additional users: $39/user/month

---

## Implementation Priority

### Phase 1: MVP Tiers (Now)
1. Lock enterprise data providers behind license
2. Lock SAML/SSO behind license
3. Lock cloud batch processing behind license
4. Implement basic usage limits (requests/day, storage)

### Phase 2: Value Add (Q2)
5. Add BI connectors to Pro tier
6. Add versioning to Enterprise tier
7. Implement user seat limits

### Phase 3: Polish (Q3)
8. Add telemetry for usage tracking
9. Implement soft limits with upgrade prompts
10. Build customer license portal

---

## Competitive Positioning

### vs PostGIS/GeoServer (Free/Open Source)
- **Compete:** Make Free tier very generous
- **Differentiate:** Modern APIs (STAC, OGC API), better DX, cloud-native

### vs Mapbox/Carto (SaaS)
- **Compete:** Self-hosted option, transparent pricing
- **Differentiate:** Standards compliance, no vendor lock-in

### vs Esri ArcGIS (Enterprise)
- **Compete:** Lower price point, modern architecture
- **Differentiate:** Open standards, simpler deployment

---

## Key Principles

1. **Free tier should be genuinely useful** - Not a demo, but production-ready for small projects
2. **Pro tier solves business pain** - BI integrations, better auth, support
3. **Enterprise tier enables scale** - Cloud providers, governance, SLA
4. **Clear upgrade path** - Each tier removes specific pain points
5. **No artificial limits** - Tier restrictions should feel natural, not arbitrary

---

## Recommended Decision: Three-Tier

**Why:**
- Pro tier captures mid-market (biggest opportunity)
- Clear value prop at each tier
- Room for upsell/cross-sell
- Standard SaaS pricing model

**Next Steps:**
1. Validate pricing with 5-10 potential customers
2. Implement license gates for Phase 1 features
3. Create comparison page for website
4. Test upgrade prompts in UI

---

## Questions to Answer

Before finalizing, clarify:

1. **Who is your ideal customer?**
   - Startups building geo apps? → Optimize Free tier
   - Enterprises with data warehouses? → Optimize Enterprise tier
   - Consultancies building client projects? → Optimize Pro tier

2. **What's your sales motion?**
   - Self-service only? → Focus on Pro tier (credit card, quick purchase)
   - Enterprise sales team? → Focus on Enterprise tier (custom deals)

3. **What's most valuable?**
   - Data provider access? → Gate aggressively
   - Processing power? → Gate compute-heavy features
   - Support? → Make support the differentiator

4. **What drives costs?**
   - API requests → Limit by usage
   - Storage → Limit by GB
   - Compute → Limit by job count

Let me know your thoughts on these questions and I can refine the recommendation!
