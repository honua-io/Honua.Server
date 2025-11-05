# Honua GeoEvent MVP - Realistic Roadmap

**Version:** 3.0 (Pragmatic MVP)
**Date:** November 2025
**Status:** Design Phase

---

## Executive Summary

**Approach:** Build a **minimum viable geofencing service** that integrates with Azure Stream Analytics. Focus on core value (geofencing + OGC standards) with realistic timelines and clear MVP vs future roadmap.

### MVP Scope (6 Months)

**What We're Building:**
1. Basic geofencing service (point-in-polygon, enter/exit events)
2. Azure Stream Analytics integration (webhook receiver)
3. SensorThings API integration (write observations)
4. Simple Blazor admin UI (forms-based, not visual designer)
5. Basic monitoring and operations

**What We're NOT Building (Future Phases):**
- ❌ Visual workflow designer (Phase 2, +6 months)
- ❌ 3D geofencing (Phase 3, +12 months)
- ❌ ML/AI features (Phase 3, +12 months)
- ❌ AWS/GCP support (Phase 2, +6 months)
- ❌ Dynamic geofences (Phase 2, +6 months)
- ❌ Dwell detection (Phase 2, +6 months)
- ❌ Advanced spatial analytics (Phase 3, +12 months)

---

## Table of Contents

1. [MVP Architecture](#1-mvp-architecture)
2. [Realistic Timelines](#2-realistic-timelines)
3. [Core Features](#3-core-features)
4. [Performance Targets](#4-performance-targets)
5. [Operations Plan](#5-operations-plan)
6. [Phase 2+ Roadmap](#6-phase-2-roadmap)

---

## 1. MVP Architecture

### 1.1 MVP Data Flow

```
User's Azure Environment:
├─ IoT Devices → Azure Event Hub
├─ Azure Stream Analytics (filter/aggregate)
└─ Output: HTTP Webhook → Honua API

Honua GeoEvent MVP:
├─ Webhook Receiver (ASP.NET Core)
├─ Geofencing Service (NetTopologySuite)
│  └─ PostgreSQL/PostGIS (geofence storage)
├─ SensorThings Writer
│  └─ PostgreSQL/PostGIS (sta_observations)
└─ Blazor Admin UI (CRUD forms)

Outputs:
├─ SensorThings Observations
├─ PostgreSQL database
└─ Webhook notifications (optional)
```

**Key Decision:** Azure Stream Analytics ONLY for MVP. AWS/GCP in Phase 2.

### 1.2 MVP Components

**Component 1: Geofencing Service (Months 1-3)**
- PostgreSQL table for geofences
- Basic CRUD API (create, read, update, delete)
- Point-in-polygon evaluation (ST_Within)
- Enter/exit event detection (no dwell in MVP)
- **Target: 1,000 geofences, 100ms P95 latency**

**Component 2: Azure SA Integration (Month 4)**
- Webhook receiver endpoint
- Schema validation
- Error handling and retry
- Basic rate limiting

**Component 3: SensorThings Integration (Month 4)**
- Write observations for geofence events
- Link to existing Things/Datastreams
- Basic metadata

**Component 4: Admin UI (Months 5-6)**
- Forms-based geofence CRUD
- Event log viewer (table)
- Basic statistics dashboard
- No visual workflow designer in MVP

**Component 5: Operations (Months 5-6)**
- OpenTelemetry monitoring
- Health checks
- Basic alerting
- Load testing and capacity planning

---

## 2. Realistic Timelines

### Month 1-2: Core Geofencing (Foundation)

**Goal:** Basic geofencing with simple point-in-polygon

**Week 1-2:**
- Database schema (geofences table)
- CRUD API endpoints (REST)
- Unit tests for API

**Week 3-4:**
- Point-in-polygon evaluation (ST_Within)
- Basic spatial index (GIST)
- Integration tests

**Week 5-6:**
- Enter/exit detection (track last state per entity)
- Event generation (JSON payload)
- Performance testing (100 geofences)

**Week 7-8:**
- Scale testing (1,000 geofences)
- Query optimization
- Documentation (API docs)

**Deliverable:**
- Geofencing API functional
- 1,000 geofences supported
- 100ms P95 latency for single evaluation
- 90% test coverage

**Known Limitations:**
- No dwell detection
- No dynamic geofences
- No 3D support
- Simple state tracking (no complex transitions)

---

### Month 3-4: Integration & Enrichment

**Goal:** Integrate with Azure SA and SensorThings

**Week 1-2 (Month 3):**
- Webhook receiver endpoint
- Azure SA output configuration
- Schema validation
- Error handling

**Week 3-4 (Month 3):**
- Basic spatial enrichment (nearest feature)
- PostGIS query optimization
- Enrichment API tests

**Week 5-6 (Month 4):**
- SensorThings observation creation
- Link events to Things/Datastreams
- Batch write optimization

**Week 7-8 (Month 4):**
- Output connectors (webhook, email via SendGrid)
- End-to-end testing
- Performance testing (100 events/sec)

**Deliverable:**
- Azure SA → Honua integration working
- SensorThings observations created
- 100 events/sec throughput
- End-to-end workflow functional

**Known Limitations:**
- Single-threaded processing (no horizontal scaling yet)
- No advanced enrichment (just nearest)
- No workflow designer (manual Azure SA query writing)

---

### Month 5-6: Admin UI & Operations

**Goal:** Basic management UI and production readiness

**Week 1-2 (Month 5):**
- Blazor project setup
- Geofence CRUD forms
- List/detail views
- Basic validation

**Week 3-4 (Month 5):**
- Event log viewer (table with pagination)
- Basic filtering (by geofence, entity, date)
- Export to CSV

**Week 5-6 (Month 6):**
- Statistics dashboard
  - Total geofences
  - Events per day (chart)
  - Top active geofences
  - Error rate
- Health check page

**Week 7-8 (Month 6):**
- Operations work:
  - OpenTelemetry instrumentation
  - Application Insights integration
  - Alerting rules (error rate, latency)
  - Load testing (1,000 events/sec target)
  - Capacity planning document
  - Deployment runbook

**Deliverable:**
- Admin UI deployed
- Basic forms-based management
- Event monitoring
- Production-ready deployment
- Beta customer ready

**Known Limitations:**
- No visual workflow designer
- Forms-based UI (not drag-and-drop)
- Basic charts (not real-time streaming)

---

## 3. Core Features

### 3.1 Geofence Management

**MVP Features:**
- ✓ Create geofence (polygon only, 2D)
- ✓ Update geofence
- ✓ Delete geofence
- ✓ List geofences (paginated)
- ✓ Get geofence by ID

**API Example:**
```http
POST /api/v1/geofences
{
  "name": "School Zone - Lincoln Elementary",
  "geometry": {
    "type": "Polygon",
    "coordinates": [[
      [-122.4200, 37.7750],
      [-122.4190, 37.7750],
      [-122.4190, 37.7740],
      [-122.4200, 37.7740],
      [-122.4200, 37.7750]
    ]]
  },
  "properties": {
    "zone_type": "school",
    "speed_limit": 25
  }
}

Response 201:
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "School Zone - Lincoln Elementary",
  "created_at": "2025-11-05T14:30:00Z"
}
```

**Future (Not MVP):**
- Multi-polygon support
- Circles (buffer from point)
- Time-based activation
- Conditional rules
- 3D volumes

### 3.2 Event Detection

**MVP Events:**
- ✓ **Enter:** First time entity enters geofence
- ✓ **Exit:** Entity leaves geofence

**Event Payload:**
```json
{
  "event_type": "geofence.enter",
  "event_id": "uuid",
  "event_time": "2025-11-05T14:30:00Z",
  "geofence_id": "550e8400-...",
  "geofence_name": "School Zone - Lincoln Elementary",
  "entity_id": "vehicle-1247",
  "location": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  },
  "properties": {
    "speed": 68.5,
    "heading": 185
  }
}
```

**Future (Not MVP):**
- Dwell (requires time-series tracking)
- Approach (requires buffer zones)
- Loiter (requires pattern detection)
- Depart (requires velocity tracking)

### 3.3 Azure Stream Analytics Integration

**MVP Integration:**
- ✓ Webhook receiver (POST /api/v1/events)
- ✓ JSON schema validation
- ✓ Error responses (400, 500)
- ✓ Basic retry handling

**Azure SA Configuration:**
```sql
-- User writes this query manually (no visual designer in MVP)
SELECT
    vehicleId,
    location.lat as latitude,
    location.lon as longitude,
    speed,
    System.Timestamp() as eventTime
INTO [honua-webhook]
FROM [vehicle-stream] TIMESTAMP BY eventTime
WHERE speed > 65
```

**Azure SA Output Configuration:**
```json
{
  "type": "webhook",
  "url": "https://honua.example.com/api/v1/events",
  "headers": {
    "Content-Type": "application/json",
    "X-API-Key": "secret"
  }
}
```

**Future (Not MVP):**
- Visual workflow designer (generates Azure SA queries)
- Multiple output formats
- Batch processing
- AWS Kinesis support
- GCP Dataflow support

---

## 4. Performance Targets

### 4.1 MVP Targets (Achievable)

| Metric | MVP Target | Measurement Method |
|--------|------------|-------------------|
| **Geofence Capacity** | 1,000 | Load test with 1k polygons |
| **Evaluation Latency** | 100ms (P95) | API latency monitoring |
| **Throughput** | 100 events/sec | Load test with k6 |
| **Uptime** | 99% | Monthly average (allows ~7 hours downtime) |
| **API Availability** | 99.5% | Health check monitoring |

**Notes:**
- Single Azure App Service instance
- Standard tier PostgreSQL
- No caching layer in MVP
- No horizontal scaling in MVP

### 4.2 Load Testing Plan

**Week 6 (Month 6):**

**Test 1: Geofence Capacity**
```bash
# Insert 1,000 geofences
# Measure: Insert time, database size

# Evaluate 1,000 points against all geofences
# Measure: Query time, memory usage

# Target: < 100ms P95 for single point evaluation
```

**Test 2: Throughput**
```bash
# Use k6 or JMeter
# Ramp up: 0 → 100 requests/sec over 5 minutes
# Sustain: 100 req/sec for 30 minutes
# Measure: Latency, error rate, resource usage

# Target: < 1% error rate, < 100ms P95 latency
```

**Test 3: Stress Test**
```bash
# Ramp up: 0 → 200 requests/sec
# Find breaking point
# Measure: When does error rate exceed 5%?

# Document: Maximum throughput before degradation
```

### 4.3 Capacity Planning

**Azure Resources (MVP):**

| Resource | SKU | Cost/Month | Purpose |
|----------|-----|------------|---------|
| **App Service** | P1v3 (2 cores, 8GB) | ~$146 | Honua API |
| **PostgreSQL** | General Purpose 2 vCore | ~$150 | Database |
| **Application Insights** | Pay-as-you-go | ~$20 | Monitoring |
| **Total** | | **~$316/month** | |

**Scaling Path:**
- **1k events/sec:** Upgrade to P2v3 + 4 vCore DB (~$450/month)
- **10k events/sec:** Horizontal scaling + Redis cache (~$1,500/month)

**Document:** `docs/GEOEVENT_CAPACITY_PLANNING.md` (to be created in Month 6)

---

## 5. Operations Plan

### 5.1 Observability

**Metrics (OpenTelemetry):**
- `geoevent.api.requests` (counter, by endpoint)
- `geoevent.api.latency` (histogram, P50/P95/P99)
- `geoevent.api.errors` (counter, by status code)
- `geoevent.geofence.evaluations` (counter)
- `geoevent.geofence.evaluation_time` (histogram)
- `geoevent.events.processed` (counter, by event type)

**Logs (Structured JSON):**
```json
{
  "timestamp": "2025-11-05T14:30:00Z",
  "level": "info",
  "message": "Geofence evaluation completed",
  "entity_id": "vehicle-1247",
  "geofence_id": "550e8400-...",
  "evaluation_time_ms": 23,
  "events_generated": 1
}
```

**Traces (Distributed):**
- Trace webhook request → geofence evaluation → SensorThings write
- Visualize in Application Insights

### 5.2 Health Checks

**Endpoints:**
- `/health` - Overall health (200 OK or 503 Service Unavailable)
- `/health/ready` - Readiness (can accept traffic)
- `/health/live` - Liveness (process is running)

**Checks:**
- Database connectivity (PostgreSQL)
- Disk space (> 10% free)
- Memory usage (< 90%)
- Recent error rate (< 5% in last 5 minutes)

### 5.3 Alerting

**Critical Alerts (PagerDuty):**
- API error rate > 5% for 5 minutes
- Database connection failure
- Health check failing for 2 minutes
- Disk space < 5%

**Warning Alerts (Email):**
- API latency P95 > 200ms for 10 minutes
- Throughput drops 50% below baseline
- Memory usage > 80%

### 5.4 Deployment

**CI/CD Pipeline:**
```
Git Push → GitHub Actions
  ↓
Build + Test (unit tests, coverage check)
  ↓
Docker Build
  ↓
Push to ACR (Azure Container Registry)
  ↓
Deploy to Staging (Azure App Service)
  ↓
Smoke Tests
  ↓
Manual Approval
  ↓
Deploy to Production
```

**Rollback Plan:**
- Keep last 3 versions in ACR
- One-click rollback in Azure Portal
- Database migrations: Use Flyway with rollback scripts
- Target: Rollback in < 5 minutes

---

## 6. Phase 2+ Roadmap

### Phase 2 (Months 7-12): Scale & Workflow

**Goals:**
- Horizontal scaling (multiple instances)
- Visual workflow designer (Blazor)
- AWS Kinesis support
- Dwell detection
- Advanced spatial enrichment

**Deliverables:**
- 1,000 events/sec throughput
- 10k geofences supported
- Visual workflow designer (no-code)
- Multi-region deployment

**Performance Targets:**
- 10ms P95 latency (with caching)
- 99.9% uptime SLA
- Auto-scaling (1-10 instances)

### Phase 3 (Months 13-18): Advanced Features

**Goals:**
- 3D geofencing
- ML/AI anomaly detection
- GCP Dataflow support
- Advanced analytics (hot spots, trajectories)
- Real-time dashboard (SignalR)

**Deliverables:**
- 3D geofence support
- ML-powered alerts
- Predictive geofencing
- Enterprise features (SSO, audit logs)

**Performance Targets:**
- 10k events/sec throughput
- 100k geofences supported
- < 5ms P95 latency

---

## 7. MVP Success Criteria

### 7.1 Technical Success (Measurable)

**Must Have (MVP Launch Blockers):**
- [ ] 1,000 geofences functional
- [ ] 100 events/sec throughput sustained
- [ ] 100ms P95 latency
- [ ] 99% uptime over 1 month
- [ ] 0 critical bugs in production
- [ ] < 1% error rate
- [ ] Load tests passing
- [ ] Health checks implemented
- [ ] Monitoring dashboard live

**Nice to Have (Stretch Goals):**
- [ ] 2,000 geofences
- [ ] 200 events/sec throughput
- [ ] 50ms P95 latency
- [ ] 99.5% uptime

### 7.2 Business Success

**Must Have:**
- [ ] 5 beta customers signed up (LOI)
- [ ] 2 beta customers actively testing
- [ ] 1 paying customer (proof of concept)
- [ ] Documentation complete
- [ ] Pricing finalized
- [ ] Sales collateral ready

**Nice to Have:**
- [ ] 10 beta customers
- [ ] 3 paying customers
- [ ] $1,500/month MRR
- [ ] 2 case studies published

### 7.3 Quality Gates

**Before Beta Release:**
- [ ] Unit test coverage > 80%
- [ ] Integration tests passing
- [ ] Load tests passing
- [ ] Security review completed
- [ ] OWASP top 10 mitigated
- [ ] Penetration test passed
- [ ] Disaster recovery plan documented
- [ ] Runbook completed

**Before General Availability:**
- [ ] 1 month beta with no critical issues
- [ ] Performance SLAs met
- [ ] 99% uptime demonstrated
- [ ] Customer feedback incorporated
- [ ] Support process established
- [ ] Pricing validated

---

## 8. Risk Mitigation

### 8.1 Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Timeline slips beyond 6 months** | High | High | Build MVP first, defer visual designer to Phase 2 |
| **Performance below 100 events/sec** | Medium | High | Load test in Month 4, not Month 6. Early optimization |
| **Spatial query performance** | Medium | Medium | PostgreSQL tuning, spatial indexes, query optimization |
| **Integration complexity with Azure SA** | Low | Medium | Simple webhook, well-documented Azure SA outputs |
| **Scope creep (team adds features)** | High | High | Strict MVP scope enforcement, defer features to Phase 2 |

### 8.2 Operational Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **No customers sign up for beta** | Medium | High | Start recruiting in Month 1, not Month 5 |
| **Azure costs exceed budget** | Low | Medium | Cost monitoring, alerts at $500/month threshold |
| **Support burden too high** | Medium | Medium | Self-service docs, limited beta size (5-10 customers) |
| **Team capacity insufficient** | Medium | High | Hire 1-2 engineers in Month 1, not Month 3 |

### 8.3 Market Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Customers want AWS, not Azure** | Medium | Medium | Phase 2 roadmap shows AWS support, collect feedback |
| **Visual designer is must-have** | Low | High | Position as Phase 2 feature, validate with customers |
| **Pricing too high/low** | Medium | Medium | Beta pricing feedback, adjust before GA |
| **Esri releases competing feature** | Low | Medium | Focus on OGC standards differentiation |

---

## 9. Explicit Commitments vs Roadmap

### Committed for MVP (6 Months)

**Guaranteed Deliverables:**
- ✓ Basic geofencing service (2D polygons, enter/exit)
- ✓ Azure Stream Analytics webhook integration
- ✓ SensorThings API observation creation
- ✓ Simple Blazor admin UI (forms-based)
- ✓ 1,000 geofences supported
- ✓ 100 events/sec throughput
- ✓ 100ms P95 latency
- ✓ 99% uptime
- ✓ Production deployment
- ✓ API documentation
- ✓ 5 beta customers

### Roadmap Items (Phase 2+)

**Not Guaranteed, Timeline TBD:**
- Visual workflow designer (Blazor)
- Dwell detection
- 3D geofencing
- AWS Kinesis support
- GCP Dataflow support
- ML/AI features
- Advanced spatial analytics
- 10k+ geofences
- < 10ms latency
- 1k+ events/sec

**Positioning:**
- Sales: "MVP launches in 6 months with core geofencing. Visual designer in Phase 2."
- Marketing: "Azure-first, multi-cloud roadmap"
- Customers: "MVP feature set is X, roadmap features are Y. We'll prioritize based on feedback."

---

## 10. Pricing (Revised)

### MVP Pricing

**Startup (Free Beta):**
- 100 geofences
- 10k events/month
- Community support
- Azure only
- Beta期間限定 (limited beta period)

**Professional ($99/month):**
- 1,000 geofences
- 100k events/month
- Email support (48-hour response)
- Azure only
- Forms-based admin UI

**Enterprise ($399/month):**
- Unlimited geofences
- Unlimited events
- Priority support (8-hour response)
- Azure only (AWS/GCP in Phase 2)
- Dedicated Slack channel

**Plus Azure Costs:**
- Azure Stream Analytics: ~$80/month
- Azure Event Hub: ~$15/month
- **Total: $194-574/month** (vs Esri $1,667/month)

---

## Conclusion

This **pragmatic MVP** focuses on:

1. **Achievable goals** - 6 months to working geofencing service
2. **Realistic performance** - 100 events/sec, 100ms latency, 1k geofences
3. **Clear scope** - No visual designer, no 3D, no multi-cloud in MVP
4. **Operations focus** - Load testing, monitoring, capacity planning from Day 1
5. **Risk mitigation** - Early beta recruitment, performance testing in Month 4

**Key Changes from Previous Design:**
- ❌ Removed 4-month timeline (now 6 months)
- ❌ Removed visual workflow designer from MVP (Phase 2)
- ❌ Removed ✅ markers (these were confusing)
- ❌ Removed 10k geofences, <10ms latency (Phase 2 goals)
- ❌ Removed multi-cloud support from MVP (Azure only)
- ✅ Added detailed load testing plan
- ✅ Added capacity planning
- ✅ Added operations runbook requirements
- ✅ Added clear MVP vs roadmap distinction

**Expected Outcome:**
- Functional MVP in 6 months
- 5 beta customers testing
- 1-2 paying customers
- Solid foundation for Phase 2 scaling

---

**Document Status:** ✅ Ready for Review
**Next Action:** Team review and commitment
**Approval Required:** CTO, Engineering Lead, Product Manager
