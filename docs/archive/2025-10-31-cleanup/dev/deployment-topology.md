# Honua Deployment Topology Management

## The Problem

Honua deployments can be incredibly complex with many permutations:

```
User Request
    ↓
[WAF] → [CDN] → [Load Balancer] → [Reverse Proxy] → [Honua Servers (N)]
                      ↓                                      ↓
                 [Tile Cache]                          [Metadata Cache]
                      ↓                                      ↓
                 [S3/Azure/GCS]                        [File System/Git]
                      ↓                                      ↓
              [Raster Tiles]                    [PostgreSQL/PostGIS/SQLite]
                                                             ↓
                                                    [Spatial Indexes]
```

Each component needs different handling during deployment:
- CDN cache flush
- Load balancer health checks
- Tile cache invalidation
- Metadata reload
- Spatial index rebuild
- SSL certificate updates
- DNS propagation
- etc.

## Solution: Topology Definition Language

### Topology Definition File

```yaml
# topology.yaml - Declarative deployment topology
apiVersion: honua.io/v1
kind: Topology
metadata:
  name: production-us-west
  environment: production
  region: us-west-2
  version: "1.0.0"

spec:
  # DNS Configuration
  dns:
    provider: cloudflare
    zone: gis.example.com
    records:
      - type: A
        name: api.gis.example.com
        target: ${load_balancer.public_ip}
        ttl: 300
      - type: CNAME
        name: tiles.gis.example.com
        target: ${cdn.cname}
        ttl: 3600

  # SSL/TLS Configuration
  ssl:
    provider: letsencrypt
    domain: "*.gis.example.com"
    autoRenew: true
    certPath: /etc/ssl/honua
    keyPath: /etc/ssl/honua
    email: ops@example.com

  # WAF (Web Application Firewall)
  waf:
    provider: cloudflare
    enabled: true
    rulesets:
      - owasp-core
      - rate-limiting
    customRules:
      - name: block-bad-bots
        action: block
        expression: "(cf.client.bot)"

  # CDN Configuration
  cdn:
    provider: cloudflare
    enabled: true
    caching:
      rules:
        - path: "/ogc/*/tiles/*"
          ttl: 86400  # 24 hours
          cacheLevel: aggressive
        - path: "/ogc/collections"
          ttl: 300    # 5 minutes
          cacheLevel: standard
        - path: "/ogc/*/items"
          ttl: 60     # 1 minute
          cacheLevel: standard
    purgeStrategy:
      onDeploy: selective  # selective, full, or none
      patterns:
        - "/ogc/collections*"
        - "/metadata/*"

  # Load Balancer
  loadBalancer:
    provider: aws-alb
    type: application
    scheme: internet-facing
    listeners:
      - port: 443
        protocol: HTTPS
        sslPolicy: ELBSecurityPolicy-TLS-1-2-2017-01
        certificates:
          - ${ssl.arn}
        defaultActions:
          - type: forward
            targetGroup: honua-servers
    healthCheck:
      path: /health
      interval: 30
      timeout: 5
      healthyThreshold: 2
      unhealthyThreshold: 3
    deploymentStrategy:
      type: rolling
      maxUnavailable: 25%

  # Reverse Proxy (Optional)
  reverseProxy:
    provider: nginx
    enabled: true
    config:
      clientMaxBodySize: 100M
      proxyTimeout: 300s
      caching:
        enabled: true
        path: /var/cache/nginx
        size: 10g
      headers:
        - "X-Forwarded-For $proxy_add_x_forwarded_for"
        - "X-Real-IP $remote_addr"
    deploymentActions:
      preDeploy:
        - action: config-test
          command: "nginx -t"
      postDeploy:
        - action: reload
          command: "nginx -s reload"

  # Honua Application Servers
  applicationServers:
    type: container  # container, vm, or kubernetes
    provider: aws-ecs
    cluster: honua-production
    count: 3
    autoScaling:
      enabled: true
      minCapacity: 2
      maxCapacity: 10
      targetCPU: 70
      targetMemory: 80

    instances:
      - name: honua-server-1
        host: 10.0.1.10
        port: 5000
        zone: us-west-2a
      - name: honua-server-2
        host: 10.0.1.11
        port: 5000
        zone: us-west-2b
      - name: honua-server-3
        host: 10.0.1.12
        port: 5000
        zone: us-west-2c

    deploymentStrategy:
      type: rolling
      maxSurge: 1
      maxUnavailable: 0
      healthCheckGracePeriod: 60s

    configuration:
      metadata:
        provider: git
        repository: git@github.com:example/honua-config.git
        branch: production
        path: environments/production/metadata.yaml
        syncInterval: 60s

      cache:
        metadata:
          enabled: true
          provider: redis
          ttl: 300

      authentication:
        mode: JWT
        provider: auth0

      features:
        ogcApi: true
        geoservices: true
        rasterTiles: true

  # Tile Cache Layer
  tileCache:
    provider: s3
    type: cloud-storage
    config:
      bucket: honua-tiles-production
      region: us-west-2
      cdn:
        enabled: true
        distribution: ${cdn.id}
      lifecycle:
        enabled: true
        rules:
          - expireAfterDays: 90
            prefix: "temp/"

    invalidationStrategy:
      onDeploy: selective
      patterns:
        - layer: "*"
          zoomLevels: [0, 1, 2]  # Invalidate overview tiles

    pregeneration:
      enabled: true
      layers:
        - parcels
        - streets
      zoomLevels: [0, 1, 2, 3, 4]
      trigger: postDeploy

  # Metadata Storage
  metadataStore:
    provider: git
    repository: git@github.com:example/honua-config.git
    branch: production
    path: environments/production

    cache:
      enabled: true
      provider: filesystem
      path: /var/honua/metadata-cache
      ttl: 300

    validation:
      enabled: true
      strict: true

  # Primary Database
  database:
    provider: postgresql
    type: aws-rds
    instance: honua-production-db
    host: honua-db.cluster-abc123.us-west-2.rds.amazonaws.com
    port: 5432
    database: gis_production
    ssl: required

    extensions:
      - postgis
      - pg_stat_statements

    backup:
      enabled: true
      schedule: "0 2 * * *"  # Daily at 2 AM
      retention: 30
      pointInTimeRecovery: true

    maintenanceWindow:
      enabled: true
      day: Sunday
      hour: 3
      duration: 2

    deploymentActions:
      preDeploy:
        - action: backup
          type: snapshot
        - action: validate
          query: "SELECT PostGIS_Version()"

      postDeploy:
        - action: analyze
          tables: all
        - action: reindex
          concurrent: true
          condition: spatial_indexes_modified

  # Spatial Indexes
  spatialIndexes:
    strategy: concurrent
    rebuild:
      trigger: schema-change
      concurrency: true
      vacuum: analyze
    monitoring:
      enabled: true
      slowQueryThreshold: 1000ms

  # Monitoring & Observability
  monitoring:
    provider: datadog
    enabled: true
    metrics:
      - requests_per_second
      - response_time_p95
      - error_rate
      - cache_hit_ratio

    alerts:
      - name: high-error-rate
        condition: error_rate > 5%
        duration: 5m
        action: notify-oncall

      - name: deployment-validation-failed
        condition: health_check_failed
        action: auto-rollback

    dashboards:
      - deployment-overview
      - ogc-performance
      - tile-cache-stats

  # Deployment Coordination
  deploymentCoordination:
    phases:
      - name: pre-flight
        steps:
          - validate-topology
          - check-dependencies
          - verify-backups
          - test-connectivity

      - name: database
        steps:
          - create-backup
          - run-migrations
          - rebuild-indexes
          - analyze-tables

      - name: application
        steps:
          - update-metadata
          - rolling-restart
          - health-check
          - warmup-cache

      - name: edge
        steps:
          - update-load-balancer
          - flush-cdn-cache
          - update-dns
          - verify-ssl

      - name: validation
        steps:
          - ogc-conformance-test
          - performance-benchmark
          - smoke-tests
          - monitor-errors

      - name: post-deployment
        steps:
          - regenerate-tiles
          - warm-metadata-cache
          - notify-stakeholders
          - update-documentation

    rollbackStrategy:
      automatic: true
      triggers:
        - health-check-failure
        - error-rate-threshold
        - performance-degradation
      steps:
        - drain-load-balancer
        - restore-previous-version
        - restore-database-backup
        - invalidate-caches
        - verify-rollback

  # Access & Permissions
  access:
    deployment:
      approvers:
        - team: gis-ops
        - role: admin

      serviceAccount:
        name: honua-deployer
        permissions:
          - database:migrate
          - servers:deploy
          - cdn:purge
          - loadbalancer:modify

    runtime:
      serviceAccount:
        name: honua-app
        permissions:
          - database:read
          - database:write
          - s3:tiles:readwrite
          - secrets:read

  # Integration Points
  integrations:
    - name: github
      type: git-repository
      config:
        repository: example/honua-config
        webhook: ${webhook_url}

    - name: slack
      type: notifications
      config:
        channel: "#gis-deployments"
        events:
          - deployment-started
          - deployment-completed
          - deployment-failed
          - rollback-triggered

    - name: pagerduty
      type: incidents
      config:
        serviceKey: ${pagerduty_key}
        severity: high
```

## Topology Discovery

The AI agent can automatically discover and map your topology:

```bash
# Auto-discover topology
$ honua-ai discover topology --environment production

Discovering topology for production environment...

Scanning infrastructure...
✓ Found DNS provider: Cloudflare
✓ Found CDN: Cloudflare CDN
✓ Found Load Balancer: AWS ALB (honua-prod-alb)
✓ Found Application Servers: 3 ECS tasks
✓ Found Database: PostgreSQL RDS (honua-production-db)
✓ Found Tile Cache: S3 (honua-tiles-production)
✓ Found Monitoring: Datadog

Mapping dependencies...
✓ DNS → CDN → Load Balancer → App Servers → Database
✓ CDN → Tile Cache (S3)
✓ App Servers → Metadata (Git)

Analyzing configuration...
✓ SSL/TLS: Let's Encrypt (auto-renew enabled)
✓ Health Checks: Configured (interval: 30s)
✓ Auto-scaling: Enabled (2-10 instances)
✓ Backups: Daily snapshots (30-day retention)

Generated topology map: topology-production.yaml
Would you like to review and commit? (yes/no): yes
```

## Deployment Orchestration

The agent coordinates changes across all topology components:

```bash
$ honua-ai deploy --environment production

Loading topology: production-us-west
Analyzing deployment plan...

Topology Impact Analysis:
─────────────────────────────────────────────
Component              Action Needed
─────────────────────────────────────────────
DNS                    None
WAF                    None
CDN                    Selective cache flush
Load Balancer          Health check update
Reverse Proxy          Config reload
App Servers (3)        Rolling update
Tile Cache             Invalidate + regen
Metadata Cache         Flush
Database               Migration + reindex
Spatial Indexes        Rebuild (concurrent)
Monitoring             Update dashboards
─────────────────────────────────────────────

Estimated Duration: 15 minutes
Downtime: None (rolling deployment)
Risk Level: MEDIUM

Deployment Sequence:
  1. Pre-flight checks (2m)
  2. Database backup & migration (5m)
  3. Application rolling update (5m)
  4. Edge layer updates (2m)
  5. Post-deployment validation (1m)

Proceed? (yes/no): yes

[Phase 1: Pre-flight] ─────────────────────
✓ Topology validated
✓ Dependencies checked
✓ Database backup verified
✓ Connectivity tested

[Phase 2: Database] ───────────────────────
✓ Snapshot created: snap-abc123
✓ Migration 005 applied (2.3s)
✓ Spatial indexes rebuilt (45s)
✓ Tables analyzed

[Phase 3: Application] ────────────────────
✓ Metadata updated (Git sync)
✓ Rolling update started
  ✓ honua-server-1 updated (health: OK)
  ✓ honua-server-2 updated (health: OK)
  ✓ honua-server-3 updated (health: OK)
✓ Metadata cache warmed

[Phase 4: Edge] ───────────────────────────
✓ Load balancer health check updated
✓ CDN cache purged (273 objects)
✓ SSL certificates verified
✓ DNS propagation checked

[Phase 5: Validation] ─────────────────────
✓ OGC conformance tests passed
✓ Performance benchmark: 98ms avg (✓ within SLA)
✓ Error rate: 0.02% (✓ normal)
✓ Tile generation queued (background)

Deployment completed successfully! (12m 34s)

Post-deployment tasks in progress:
  ⏳ Regenerating overview tiles (layers: parcels, streets)
  ⏳ Warming metadata cache
  ✓ Stakeholders notified
```

## Topology-Aware Changes

### Example: Adding a New Layer

```bash
$ honua-ai add layer bike-lanes

Analyzing topology for layer addition...

Topology Impact:
  Database:
    ✓ Table exists: bike_lanes
    ✓ Spatial index: Required (will create)

  Application Servers:
    ✓ Metadata sync: Required
    ✓ Service restart: Rolling (no downtime)

  Tile Cache:
    ⚠ New tile generation required
    ⏱ Estimated: 2.5 hours for zoom 0-14
    💡 Suggestion: Pre-generate in background

  CDN:
    ℹ No cache flush needed (new endpoint)

  Load Balancer:
    ℹ No changes needed

  Monitoring:
    ✓ Dashboard update: Required
    ✓ New metrics: layer_bike_lanes_requests

Deployment Plan:
  1. Create spatial index (concurrent)
  2. Update metadata (Git commit)
  3. Sync to app servers
  4. Queue tile pre-generation
  5. Update monitoring dashboard

Proceed? (yes/no): yes
```

## Topology Validation

```bash
$ honua-ai validate topology

Validating production topology...

Connectivity Tests:
  ✓ DNS resolves correctly
  ✓ SSL certificate valid (expires: 2025-12-15)
  ✓ CDN responds (cache hit ratio: 94%)
  ✓ Load balancer healthy
  ✓ All app servers responding
  ✓ Database connection pool healthy
  ✓ Tile cache accessible
  ✓ Metadata repository accessible

Configuration Tests:
  ✓ Health check endpoints configured
  ✓ Auto-scaling policies active
  ✓ Backup schedule verified
  ✓ Monitoring alerts configured
  ✓ SSL auto-renewal enabled

Security Tests:
  ✓ WAF rules active
  ✓ Database SSL enforced
  ✓ API authentication required
  ✓ Secrets properly secured

Performance Tests:
  ✓ Response time p95: 145ms (target: <200ms)
  ✓ Cache hit ratio: 94% (target: >90%)
  ✓ Database connections: 12/100 (healthy)
  ✓ Error rate: 0.01% (target: <1%)

All topology validations passed!
```

This topology system gives your AI agent:

1. **Complete visibility** into your deployment architecture
2. **Coordinated deployments** across all components
3. **Automatic discovery** of infrastructure
4. **Impact analysis** before changes
5. **Safe rollbacks** across the entire stack
6. **Validation** at every level

Would you like me to start implementing the topology discovery system?
