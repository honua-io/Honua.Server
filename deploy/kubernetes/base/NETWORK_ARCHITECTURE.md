# Honua Kubernetes Network Architecture

This document describes the network security architecture for Honua deployed on Kubernetes with NetworkPolicies.

## Network Security Model

Honua implements a **zero-trust network security model** using Kubernetes NetworkPolicies:

- **Default Deny**: All traffic is denied by default
- **Explicit Allow**: Only explicitly permitted traffic is allowed
- **Namespace Isolation**: Cross-namespace traffic is restricted
- **Least Privilege**: Each component has minimal required network access

## Network Topology

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         EXTERNAL INTERNET                                │
│                                                                           │
│  • Cloud Storage (S3, Azure Blob, GCS)                                  │
│  • Weather/Raster Data APIs                                             │
│  • AI/LLM APIs (OpenAI, Azure OpenAI, Anthropic)                        │
│  • OIDC Providers (Auth0, Azure AD, Google)                             │
│  • External Data Sources (HTTP/HTTPS)                                   │
└───────────────────────────────┬───────────────────────────────────────────┘
                                │
                                │ HTTPS (443), HTTP (80)
                                │
┌───────────────────────────────▼───────────────────────────────────────────┐
│                    INGRESS CONTROLLER NAMESPACE                           │
│                       (ingress-nginx)                                     │
│                                                                           │
│  ┌─────────────────────────────────────────────────────┐                │
│  │  Ingress Controller (NGINX/Traefik/etc.)            │                │
│  │  • TLS Termination                                  │                │
│  │  • Rate Limiting                                    │                │
│  │  • Request Routing                                  │                │
│  └─────────────────────┬───────────────────────────────┘                │
└────────────────────────┼───────────────────────────────────────────────────┘
                         │
                         │ HTTP (8080)
                         │
┌────────────────────────▼───────────────────────────────────────────────────┐
│                         HONUA NAMESPACE                                    │
│                                                                            │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │                    Honua Server Pods                              │    │
│  │                    (app: honua-server)                            │    │
│  │                                                                   │    │
│  │  • Port: 8080 (HTTP API)                                         │    │
│  │  • Replicas: 2-10 (HPA enabled)                                  │    │
│  │                                                                   │    │
│  │  Ingress Allowed From:                                           │    │
│  │    ✓ Ingress Controller (port 8080)                              │    │
│  │    ✓ Same pod selector (health checks)                           │    │
│  │    ✓ Monitoring namespace (metrics)                              │    │
│  │                                                                   │    │
│  │  Egress Allowed To:                                              │    │
│  │    ✓ PostgreSQL (port 5432)                                      │    │
│  │    ✓ Redis (port 6379)                                           │    │
│  │    ✓ DNS (port 53 UDP/TCP)                                       │    │
│  │    ✓ External HTTPS (port 443)                                   │    │
│  │    ✓ External HTTP (port 80)                                     │    │
│  │    ✓ OTLP Collector (ports 4317, 4318)                           │    │
│  │    ✓ Other Honua pods (health checks)                            │    │
│  └───────────────┬────────────────────┬──────────────────────────────┘    │
│                  │                    │                                   │
│                  │                    │                                   │
│         Port 5432│                    │Port 6379                          │
│                  │                    │                                   │
│  ┌───────────────▼──────────┐  ┌──────▼──────────────────┐              │
│  │   PostgreSQL/PostGIS     │  │    Redis Cache          │              │
│  │   StatefulSet            │  │    Deployment           │              │
│  │   (app: postgis)         │  │    (app: redis)         │              │
│  │                          │  │                         │              │
│  │  • Port: 5432            │  │  • Port: 6379           │              │
│  │  • Persistent Storage    │  │  • Cluster Bus: 16379   │              │
│  │                          │  │  • Sentinel: 26379      │              │
│  │  Ingress Allowed From:   │  │                         │              │
│  │    ✓ honua-server pods   │  │  Ingress Allowed From:  │              │
│  │    ✓ backup pods         │  │    ✓ honua-server pods  │              │
│  │    ✓ migration jobs      │  │    ✓ monitoring pods    │              │
│  │    ✓ monitoring pods     │  │    ✓ other redis pods   │              │
│  │    ✓ same pods           │  │                         │              │
│  │      (replication)       │  │  Egress Allowed To:     │              │
│  │                          │  │    ✓ DNS (port 53)      │              │
│  │  Egress Allowed To:      │  │    ✓ other redis pods   │              │
│  │    ✓ DNS (port 53)       │  │      (cluster mode)     │              │
│  │    ✓ same pods           │  │                         │              │
│  │      (replication)       │  │                         │              │
│  └──────────────────────────┘  └─────────────────────────┘              │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
                                 │
                                 │ Metrics/Traces
                                 │
┌────────────────────────────────▼────────────────────────────────────────┐
│                    MONITORING NAMESPACE                                  │
│                                                                          │
│  • Prometheus (metrics scraping)                                        │
│  • Grafana (dashboards)                                                 │
│  • postgres-exporter (database metrics)                                 │
│  • redis-exporter (cache metrics)                                       │
└──────────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────────────┐
│                  OBSERVABILITY NAMESPACE                                   │
│                                                                            │
│  • OpenTelemetry Collector (OTLP endpoints 4317/4318)                     │
│  • Jaeger (distributed tracing)                                           │
│  • Loki (log aggregation)                                                 │
└────────────────────────────────────────────────────────────────────────────┘
```

## Traffic Flow Matrix

### Allowed Traffic Flows

| Source | Destination | Port | Protocol | Purpose |
|--------|------------|------|----------|---------|
| **Ingress Controller** | Honua Server | 8080 | TCP | External HTTP/HTTPS requests |
| **Honua Server** | PostgreSQL | 5432 | TCP | Database queries |
| **Honua Server** | Redis | 6379 | TCP | Caching operations |
| **Honua Server** | Internet | 443 | TCP | Cloud storage, APIs, OIDC |
| **Honua Server** | Internet | 80 | TCP | External data sources |
| **Honua Server** | OTLP Collector | 4317, 4318 | TCP | Distributed tracing |
| **Honua Server** | Honua Server | 8080 | TCP | Health checks |
| **Monitoring** | Honua Server | 8080 | TCP | Metrics scraping |
| **Monitoring** | PostgreSQL | 5432 | TCP | Database metrics |
| **Monitoring** | Redis | 6379 | TCP | Cache metrics |
| **Backup Jobs** | PostgreSQL | 5432 | TCP | Database backups |
| **Migration Jobs** | PostgreSQL | 5432 | TCP | Schema migrations |
| **PostgreSQL** | PostgreSQL | 5432 | TCP | Replication (HA) |
| **Redis** | Redis | 6379, 16379, 26379 | TCP | Cluster/Sentinel |
| **All Pods** | kube-dns | 53 | UDP/TCP | DNS resolution |

### Blocked Traffic (Default Deny)

- **All other ingress traffic** is denied by default
- **All other egress traffic** is denied by default
- **Cross-namespace traffic** is denied except for explicitly allowed namespaces (ingress, monitoring, observability)
- **Lateral movement** between unrelated pods is prevented

## External Dependencies

The Honua Server requires egress access to the following external services:

### Cloud Storage Providers
- **AWS S3**: HTTPS (443) for raster tile caching and data storage
- **Azure Blob Storage**: HTTPS (443) for raster tile caching and data storage
- **Google Cloud Storage**: HTTPS (443) for raster tile caching and data storage

### Data Sources
- **Weather APIs**: HTTP/HTTPS (80/443) for meteorological data
- **External Raster Sources**: HTTP/HTTPS (80/443) for remote COG/Zarr datasets
- **Geospatial Data Services**: HTTP/HTTPS (80/443) for WMS/WFS/WMTS

### Authentication
- **OIDC Providers**: HTTPS (443) for user authentication
  - Azure AD / Microsoft Entra ID
  - Auth0
  - Google OAuth
  - Custom OIDC providers

### AI/LLM Services (if enabled)
- **OpenAI API**: HTTPS (443) for GPT models
- **Azure OpenAI**: HTTPS (443) for GPT models
- **Anthropic API**: HTTPS (443) for Claude models

### Observability
- **OTLP Endpoints**: TCP (4317/4318) for distributed tracing
- **Prometheus Remote Write**: HTTPS (443) for metrics forwarding
- **Cloud logging services**: HTTPS (443) for log forwarding

## NetworkPolicy Files

The following NetworkPolicy manifests implement this architecture:

1. **00-namespace.yaml**: Namespace definition with security labels
2. **01-networkpolicy-default-deny.yaml**: Default deny all traffic (baseline)
3. **02-networkpolicy-honua-server.yaml**: Honua application server policies
4. **03-networkpolicy-postgresql.yaml**: PostgreSQL database policies
5. **04-networkpolicy-redis.yaml**: Redis cache policies
6. **05-networkpolicy-dns.yaml**: DNS access for all pods
7. **06-networkpolicy-namespace-isolation.yaml**: Cross-namespace restrictions

## Security Principles

### 1. Zero Trust Network Model
- No implicit trust between any network entities
- All traffic must be explicitly allowed
- Default deny policy as baseline

### 2. Defense in Depth
- Multiple layers of network security:
  - Namespace isolation
  - Pod-level NetworkPolicies
  - Service-level access controls
  - Application-level authentication/authorization

### 3. Least Privilege Access
- Each component has minimal required network access
- Databases have no internet access
- Caches have no internet access
- Application servers have restricted egress

### 4. Network Segmentation
- Different trust zones (frontend, backend, data layer)
- Namespace-level isolation
- Pod-level isolation within namespace

### 5. Secure by Default
- Default deny all policy
- Explicit allow rules required
- No overly permissive rules

## Monitoring and Compliance

### Network Policy Enforcement
- Ensure CNI plugin supports NetworkPolicies (Calico, Cilium, Weave Net, etc.)
- Verify policies are being enforced with connectivity tests
- Monitor NetworkPolicy events in Kubernetes audit logs

### Security Auditing
- Regular review of NetworkPolicy configurations
- Audit allowed traffic flows
- Monitor for policy violations
- Track changes to network policies via GitOps

### Compliance
- Supports PCI-DSS network segmentation requirements
- Implements NIST zero trust principles
- Aligns with CIS Kubernetes benchmarks for network security

## Troubleshooting

### Common Issues

**Pods cannot connect to services:**
- Check if NetworkPolicy exists for the pod
- Verify pod labels match policy selectors
- Ensure CNI plugin supports NetworkPolicies
- Check if DNS policy allows kube-dns access

**External API calls failing:**
- Verify egress rules allow HTTPS (443) or HTTP (80)
- Check if DNS resolution is working
- Verify external endpoints are reachable from cluster

**Monitoring not working:**
- Check if monitoring namespace has correct labels
- Verify ingress rules allow monitoring namespace
- Ensure metrics ports are exposed in policies

### Debugging Commands

```bash
# Check if NetworkPolicies are applied
kubectl get networkpolicies -n honua

# Describe a specific policy
kubectl describe networkpolicy honua-server -n honua

# Check pod labels
kubectl get pods -n honua --show-labels

# Test connectivity from a pod
kubectl exec -it <pod-name> -n honua -- curl -v http://redis-service:6379

# Check NetworkPolicy events
kubectl get events -n honua --field-selector involvedObject.kind=NetworkPolicy

# Verify CNI plugin supports NetworkPolicies
kubectl get nodes -o wide
kubectl describe node <node-name> | grep -i network
```

## Migration from No NetworkPolicies

### Pre-Deployment Checklist

1. **Verify CNI Plugin**: Ensure your Kubernetes cluster uses a CNI plugin that supports NetworkPolicies
   - Calico: ✓ Supported
   - Cilium: ✓ Supported
   - Weave Net: ✓ Supported
   - Flannel: ✗ Not supported (use Flannel + Calico)
   - AWS VPC CNI: ⚠ Requires additional setup

2. **Label Resources**: Ensure all pods, namespaces, and services have correct labels

3. **Test in Staging**: Deploy NetworkPolicies to staging environment first

4. **Monitor Connectivity**: Watch for connection failures after deployment

### Deployment Steps

```bash
# 1. Apply namespace with security labels
kubectl apply -f 00-namespace.yaml

# 2. Apply DNS policy first (critical for all pods)
kubectl apply -f 05-networkpolicy-dns.yaml

# 3. Apply namespace isolation
kubectl apply -f 06-networkpolicy-namespace-isolation.yaml

# 4. Apply service-specific policies
kubectl apply -f 02-networkpolicy-honua-server.yaml
kubectl apply -f 03-networkpolicy-postgresql.yaml
kubectl apply -f 04-networkpolicy-redis.yaml

# 5. Apply default deny LAST (after all allow rules are in place)
kubectl apply -f 01-networkpolicy-default-deny.yaml

# 6. Verify all policies are active
kubectl get networkpolicies -n honua

# 7. Test connectivity
./test-network-policies.sh
```

### Rollback Plan

If issues occur, remove the default deny policy first:

```bash
# Remove default deny to restore connectivity
kubectl delete networkpolicy default-deny-all -n honua

# Remove other policies if needed
kubectl delete networkpolicies -n honua --all
```

## Production Hardening

### Additional Security Measures

1. **Enable Pod Security Standards**: Use `restricted` pod security standard
2. **Implement Service Mesh**: Consider Istio/Linkerd for mTLS between services
3. **Use Network Policies with CIDR Blocks**: Restrict egress to specific IP ranges
4. **Enable Audit Logging**: Track all NetworkPolicy changes
5. **Regular Security Scans**: Use tools like Falco, Trivy, or Snyk
6. **Secrets Management**: Use external secrets operator (e.g., External Secrets Operator)
7. **TLS Everywhere**: Enforce TLS for all internal communications

### Advanced NetworkPolicy Features

For production environments, consider:

- **CIDR-based egress rules**: Restrict external access to specific IP ranges
- **Named ports**: Use named ports in policies for better maintainability
- **Multiple policies**: Combine multiple policies for granular control
- **Policy ordering**: Use annotations to indicate policy priority

Example with CIDR restrictions:

```yaml
# Restrict egress to specific cloud provider IP ranges
egress:
- to:
  - ipBlock:
      cidr: 52.0.0.0/8  # AWS S3 IP range (example)
  ports:
  - protocol: TCP
    port: 443
```

## References

- [Kubernetes NetworkPolicy Documentation](https://kubernetes.io/docs/concepts/services-networking/network-policies/)
- [Kubernetes Network Policy Recipes](https://github.com/ahmetb/kubernetes-network-policy-recipes)
- [NIST Zero Trust Architecture](https://www.nist.gov/publications/zero-trust-architecture)
- [CIS Kubernetes Benchmark](https://www.cisecurity.org/benchmark/kubernetes)
