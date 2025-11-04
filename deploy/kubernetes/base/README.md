# Honua Kubernetes NetworkPolicy Configuration

This directory contains Kubernetes NetworkPolicy manifests for securing pod-to-pod communication in the Honua deployment.

## Overview

Honua implements a **zero-trust network security model** using Kubernetes NetworkPolicies:

- **Default Deny**: All traffic is denied by default
- **Explicit Allow**: Only explicitly permitted traffic flows are allowed
- **Namespace Isolation**: Cross-namespace traffic is restricted
- **Least Privilege**: Each component has minimal required network access

## Files

| File | Description |
|------|-------------|
| `00-namespace.yaml` | Namespace definition with security labels |
| `01-networkpolicy-default-deny.yaml` | Default deny all ingress/egress traffic (baseline) |
| `02-networkpolicy-honua-server.yaml` | Network policy for Honua application server |
| `03-networkpolicy-postgresql.yaml` | Network policy for PostgreSQL/PostGIS database |
| `04-networkpolicy-redis.yaml` | Network policy for Redis cache |
| `05-networkpolicy-dns.yaml` | Allow DNS access for all pods |
| `06-networkpolicy-namespace-isolation.yaml` | Cross-namespace traffic restrictions |
| `NETWORK_ARCHITECTURE.md` | Detailed network architecture documentation |
| `test-network-policies.sh` | Automated test suite for NetworkPolicies |

## Quick Start

### Prerequisites

1. **CNI Plugin with NetworkPolicy Support**: Ensure your Kubernetes cluster uses a CNI plugin that supports NetworkPolicies:
   - ✓ Calico
   - ✓ Cilium
   - ✓ Weave Net
   - ✓ Antrea
   - ✗ Flannel (requires Flannel + Calico)

2. **Label Required Namespaces**:
   ```bash
   # Label ingress controller namespace
   kubectl label namespace ingress-nginx name=ingress-nginx

   # Label monitoring namespace
   kubectl label namespace monitoring name=monitoring monitoring=true

   # Label observability namespace
   kubectl label namespace observability name=observability observability=true

   # Label kube-system for DNS
   kubectl label namespace kube-system name=kube-system
   ```

3. **Ensure Correct Pod Labels**: Verify your deployments have the correct labels:
   ```bash
   kubectl get pods -n honua --show-labels
   ```

### Deployment

Deploy NetworkPolicies in the following order:

```bash
# 1. Create namespace with security labels
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
```

### Verification

Run the automated test suite to verify NetworkPolicies are working correctly:

```bash
./test-network-policies.sh --verbose
```

Expected output:
```
========================================
Test Summary
========================================
Total tests: 15
Passed: 15
Failed: 0
Skipped: 0

✓ All tests passed!
```

## Traffic Flow Summary

### Allowed Traffic

| Source | Destination | Port | Purpose |
|--------|------------|------|---------|
| Ingress Controller → Honua Server | 8080 | External HTTP/HTTPS requests |
| Honua Server → PostgreSQL | 5432 | Database queries |
| Honua Server → Redis | 6379 | Caching operations |
| Honua Server → Internet | 443 | Cloud storage, APIs, OIDC |
| Honua Server → Internet | 80 | External data sources |
| Monitoring → All Services | Various | Metrics collection |
| All Pods → DNS | 53 | DNS resolution |

### Blocked Traffic

- All ingress traffic except from allowed sources
- All egress traffic except to allowed destinations
- All cross-namespace traffic except from monitoring/ingress
- All traffic to databases/caches except from application pods

## Configuration Details

### Honua Server Policy

**Ingress allowed from:**
- Ingress controller (port 8080)
- Other Honua Server pods (port 8080)
- Monitoring namespace (port 8080)

**Egress allowed to:**
- PostgreSQL (port 5432)
- Redis (port 6379)
- DNS (port 53)
- External HTTPS (port 443)
- External HTTP (port 80)
- OTLP Collector (ports 4317, 4318)

### PostgreSQL Policy

**Ingress allowed from:**
- Honua Server pods (port 5432)
- Database backup pods (port 5432)
- Database migration jobs (port 5432)
- Monitoring namespace (port 5432)

**Egress allowed to:**
- DNS (port 53)
- Other PostgreSQL pods (port 5432) - for replication

### Redis Policy

**Ingress allowed from:**
- Honua Server pods (port 6379)
- Monitoring namespace (port 6379)
- Other Redis pods (ports 6379, 16379, 26379)

**Egress allowed to:**
- DNS (port 53)
- Other Redis pods (ports 6379, 16379, 26379) - for clustering

## Troubleshooting

### Common Issues

#### 1. Pods cannot connect to services

**Symptoms:**
- Application errors about connection failures
- Timeouts when connecting to databases or caches

**Solutions:**
```bash
# Check if NetworkPolicies exist
kubectl get networkpolicies -n honua

# Verify pod labels match policy selectors
kubectl get pods -n honua --show-labels

# Describe the policy to see selectors
kubectl describe networkpolicy honua-server -n honua

# Check CNI plugin supports NetworkPolicies
kubectl get nodes -o wide
```

#### 2. External API calls failing

**Symptoms:**
- Cannot access cloud storage (S3, Azure Blob, GCS)
- Cannot reach weather APIs or external data sources
- OIDC authentication failures

**Solutions:**
```bash
# Verify egress rules allow HTTPS (443) and HTTP (80)
kubectl describe networkpolicy honua-server -n honua | grep -A 20 "Egress"

# Test connectivity from a pod
kubectl exec -it <honua-pod> -n honua -- curl -v https://www.google.com

# Check DNS resolution
kubectl exec -it <honua-pod> -n honua -- nslookup google.com
```

#### 3. Monitoring not working

**Symptoms:**
- Prometheus cannot scrape metrics
- No metrics visible in Grafana
- Health checks failing

**Solutions:**
```bash
# Check if monitoring namespace has correct labels
kubectl get namespace monitoring --show-labels

# Verify ingress rules allow monitoring namespace
kubectl describe networkpolicy honua-server -n honua | grep -A 10 "Ingress"

# Check if metrics port is accessible
kubectl exec -it <monitoring-pod> -n monitoring -- curl http://honua-service.honua.svc.cluster.local/metrics
```

#### 4. Database connection issues

**Symptoms:**
- Application cannot connect to PostgreSQL
- Connection timeouts to database

**Solutions:**
```bash
# Verify Honua Server pods have correct labels
kubectl get pods -n honua -l app=honua-server

# Check PostgreSQL NetworkPolicy
kubectl describe networkpolicy postgis-database -n honua

# Test database connectivity
kubectl exec -it <honua-pod> -n honua -- nc -zv postgis-service 5432
```

### Debugging Commands

```bash
# List all NetworkPolicies
kubectl get networkpolicies -n honua

# Describe a specific policy
kubectl describe networkpolicy <policy-name> -n honua

# Show all policy events
kubectl get events -n honua --field-selector involvedObject.kind=NetworkPolicy

# Check pod labels
kubectl get pods -n honua --show-labels

# Get pod network information
kubectl exec -it <pod-name> -n honua -- ip addr
kubectl exec -it <pod-name> -n honua -- ip route

# Test DNS resolution
kubectl exec -it <pod-name> -n honua -- nslookup kubernetes.default

# Test service connectivity
kubectl exec -it <pod-name> -n honua -- nc -zv <service-name> <port>

# Check CNI plugin logs
kubectl logs -n kube-system -l k8s-app=calico-node
```

## Customization

### Adding New Allowed Traffic

To allow new traffic flows, edit the appropriate NetworkPolicy file:

1. **Allow new external service** (add to `02-networkpolicy-honua-server.yaml`):
   ```yaml
   egress:
   - to:
     - namespaceSelector: {}
     ports:
     - protocol: TCP
       port: 8443  # New port
   ```

2. **Allow new internal service** (create new NetworkPolicy):
   ```yaml
   apiVersion: networking.k8s.io/v1
   kind: NetworkPolicy
   metadata:
     name: new-service
     namespace: honua
   spec:
     podSelector:
       matchLabels:
         app: new-service
     ingress:
     - from:
       - podSelector:
           matchLabels:
             app: honua-server
       ports:
       - protocol: TCP
         port: 8080
   ```

3. **Allow new namespace** (add to `06-networkpolicy-namespace-isolation.yaml`):
   ```yaml
   ingress:
   - from:
     - namespaceSelector:
         matchLabels:
           name: new-namespace
   ```

### Restricting External Access

To restrict external access to specific IP ranges, use `ipBlock`:

```yaml
egress:
- to:
  - ipBlock:
      cidr: 52.0.0.0/8  # AWS IP range
      except:
      - 52.0.0.0/24     # Blocked subnet
  ports:
  - protocol: TCP
    port: 443
```

### Disabling HTTP Access

For production environments, consider removing HTTP (port 80) egress:

```bash
# Edit the Honua Server policy
kubectl edit networkpolicy honua-server -n honua

# Remove or comment out the HTTP egress rule:
# - to:
#   - namespaceSelector: {}
#   ports:
#   - protocol: TCP
#     port: 80
```

## Security Best Practices

1. **Always test in staging first**: Deploy NetworkPolicies to a staging environment before production

2. **Monitor connectivity**: Watch logs and metrics after deployment for connection failures

3. **Use specific labels**: Use specific pod/namespace labels instead of wildcards

4. **Restrict egress**: Limit external access to only required services

5. **Regular audits**: Periodically review and audit NetworkPolicy configurations

6. **Version control**: Track all NetworkPolicy changes in Git

7. **Combine with Pod Security**: Use Pod Security Standards/Policies in addition to NetworkPolicies

8. **Enable audit logging**: Track all NetworkPolicy changes in Kubernetes audit logs

9. **Use CIDR blocks**: Restrict external access to known IP ranges when possible

10. **Implement service mesh**: Consider Istio/Linkerd for mTLS and advanced traffic control

## Rollback

If issues occur after deploying NetworkPolicies, rollback in reverse order:

```bash
# 1. Remove default deny first (restores connectivity)
kubectl delete networkpolicy default-deny-all -n honua

# 2. Remove other policies if needed
kubectl delete networkpolicy honua-server -n honua
kubectl delete networkpolicy postgis-database -n honua
kubectl delete networkpolicy redis-cache -n honua
kubectl delete networkpolicy namespace-isolation -n honua
kubectl delete networkpolicy allow-dns-access -n honua

# 3. Verify connectivity is restored
kubectl exec -it <pod-name> -n honua -- curl http://postgis-service:5432

# 4. Fix issues and redeploy
```

## Production Hardening

For production deployments, consider these additional measures:

1. **Enable Pod Security Standards**:
   ```yaml
   apiVersion: v1
   kind: Namespace
   metadata:
     name: honua
     labels:
       pod-security.kubernetes.io/enforce: restricted
   ```

2. **Use Admission Controllers**: Enable PodSecurityPolicy or OPA Gatekeeper

3. **Implement mTLS**: Use a service mesh (Istio/Linkerd) for encrypted pod-to-pod communication

4. **Restrict IP ranges**: Use CIDR blocks to limit external access

5. **Enable network logging**: Use CNI plugins with flow logging (Calico, Cilium)

6. **Regular scanning**: Use tools like Falco, Trivy, or Snyk for runtime security

7. **Secrets management**: Use External Secrets Operator or HashiCorp Vault

8. **Certificate management**: Use cert-manager for TLS certificates

## Testing

### Automated Testing

Run the full test suite:

```bash
./test-network-policies.sh --verbose
```

### Manual Testing

Test specific connectivity:

```bash
# Create a test pod
kubectl run test-pod -n honua --image=nicolaka/netshoot -- sleep 3600

# Test database connectivity
kubectl exec -it test-pod -n honua -- nc -zv postgis-service 5432

# Test Redis connectivity
kubectl exec -it test-pod -n honua -- nc -zv redis-service 6379

# Test external HTTPS
kubectl exec -it test-pod -n honua -- curl -v https://www.google.com

# Cleanup
kubectl delete pod test-pod -n honua
```

## Support and Documentation

- **Network Architecture**: See [NETWORK_ARCHITECTURE.md](./NETWORK_ARCHITECTURE.md) for detailed architecture
- **Kubernetes NetworkPolicy Docs**: https://kubernetes.io/docs/concepts/services-networking/network-policies/
- **NetworkPolicy Recipes**: https://github.com/ahmetb/kubernetes-network-policy-recipes
- **Security Best Practices**: https://kubernetes.io/docs/concepts/security/

## License

This configuration is part of the Honua project. See the main project LICENSE file for details.
