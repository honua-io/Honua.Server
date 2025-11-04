# Honua Kubernetes Deployment Guide with NetworkPolicies

This guide provides step-by-step instructions for deploying Honua on Kubernetes with comprehensive network security using NetworkPolicies.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Architecture Overview](#architecture-overview)
- [Pre-Deployment Steps](#pre-deployment-steps)
- [Deployment](#deployment)
- [Verification](#verification)
- [Troubleshooting](#troubleshooting)
- [Rollback](#rollback)
- [Production Hardening](#production-hardening)

## Prerequisites

### 1. Kubernetes Cluster

- Kubernetes version: 1.22+ (NetworkPolicy v1 API)
- CNI plugin with NetworkPolicy support:
  - ✓ Calico (recommended)
  - ✓ Cilium (recommended)
  - ✓ Weave Net
  - ✓ Antrea
  - ⚠ Flannel (requires Calico for NetworkPolicy support)

Verify your CNI plugin:
```bash
kubectl get nodes -o wide
kubectl get pods -n kube-system | grep -E "calico|cilium|weave|antrea"
```

### 2. Required Tools

```bash
# kubectl
kubectl version --client

# kustomize (optional, for overlay deployments)
kustomize version

# nc (netcat) for testing
nc -h
```

### 3. Cluster Access

Ensure you have cluster-admin privileges:
```bash
kubectl auth can-i create networkpolicies --all-namespaces
```

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    EXTERNAL INTERNET                         │
│  • Cloud Storage (S3, Azure, GCS)                           │
│  • External APIs (Weather, AI/LLM, OIDC)                    │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       │ HTTPS (443), HTTP (80)
                       │
┌──────────────────────▼───────────────────────────────────────┐
│              INGRESS CONTROLLER NAMESPACE                     │
│  • NGINX Ingress Controller                                  │
│  • TLS Termination                                           │
└──────────────────────┬───────────────────────────────────────┘
                       │
                       │ HTTP (8080)
                       │
┌──────────────────────▼───────────────────────────────────────┐
│                 HONUA NAMESPACE                              │
│                                                              │
│  ┌────────────────────────────────────────────────┐         │
│  │   Honua Server (2-10 replicas)                 │         │
│  │   • API Server (port 8080)                     │         │
│  │   • Egress: PostgreSQL, Redis, Internet        │         │
│  └────────┬──────────────────────┬──────────────────┘         │
│           │                      │                           │
│  ┌────────▼──────────┐  ┌────────▼──────────┐               │
│  │  PostgreSQL/PostGIS│  │   Redis Cache     │               │
│  │  • Port: 5432      │  │   • Port: 6379    │               │
│  │  • No Internet     │  │   • No Internet   │               │
│  └────────────────────┘  └───────────────────┘               │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

**Security Model:**
- **Default Deny**: All traffic blocked by default
- **Explicit Allow**: Only permitted traffic flows allowed
- **Namespace Isolation**: Cross-namespace traffic restricted
- **Least Privilege**: Minimal network access per component

## Pre-Deployment Steps

### Step 1: Label Namespaces

NetworkPolicies use namespace labels to allow cross-namespace traffic. Label your namespaces:

```bash
# Create and label the Honua namespace (if using base manifests directly)
kubectl create namespace honua
kubectl label namespace honua name=honua environment=production

# Label ingress controller namespace
kubectl label namespace ingress-nginx name=ingress-nginx ingress-controller=true

# Label monitoring namespace (if exists)
kubectl label namespace monitoring name=monitoring monitoring=true

# Label observability namespace (if exists)
kubectl label namespace observability name=observability observability=true

# Label kube-system (should already exist)
kubectl label namespace kube-system name=kube-system --overwrite
```

Verify labels:
```bash
kubectl get namespaces --show-labels | grep -E "honua|ingress|monitoring|observability"
```

### Step 2: Verify Pod Labels

Ensure your application pods have the correct labels:

```bash
# Honua Server pods must have: app=honua-server
# PostgreSQL pods must have: app=postgis
# Redis pods must have: app=redis
```

If deploying new resources, ensure your Deployment/StatefulSet manifests include these labels:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  namespace: honua
spec:
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
    spec:
      containers:
      - name: honua-server
        image: honuaio/honua-server:latest
        # ... rest of spec
```

### Step 3: Backup Current Configuration

Before applying NetworkPolicies, backup your current configuration:

```bash
# Backup all resources in honua namespace
kubectl get all -n honua -o yaml > honua-backup-$(date +%Y%m%d).yaml

# Backup existing NetworkPolicies (if any)
kubectl get networkpolicies -n honua -o yaml > networkpolicies-backup-$(date +%Y%m%d).yaml
```

### Step 4: Prepare Rollback Plan

Document current services and endpoints:

```bash
# Test connectivity before applying policies
kubectl run test-connectivity -n honua --image=nicolaka/netshoot --rm -it -- /bin/bash

# Inside the pod, test:
nc -zv postgis-service 5432
nc -zv redis-service 6379
curl -v http://honua-service/health
curl -v https://www.google.com

exit
```

## Deployment

### Option 1: Deploy with Kustomize (Recommended)

Deploy all NetworkPolicies at once using Kustomize:

```bash
# Deploy base configuration
kubectl apply -k deploy/kubernetes/base/

# OR deploy production overlay
kubectl apply -k deploy/kubernetes/overlays/production/
```

### Option 2: Deploy Manually (Step-by-Step)

For more control, deploy manifests individually in this order:

```bash
cd deploy/kubernetes/base

# Step 1: Create namespace with security labels
kubectl apply -f 00-namespace.yaml

# Step 2: Apply DNS policy FIRST (critical for all pods)
kubectl apply -f 05-networkpolicy-dns.yaml

# Verify DNS policy is active
kubectl get networkpolicy allow-dns-access -n honua

# Step 3: Apply namespace isolation
kubectl apply -f 06-networkpolicy-namespace-isolation.yaml

# Step 4: Apply service-specific policies
kubectl apply -f 02-networkpolicy-honua-server.yaml
kubectl apply -f 03-networkpolicy-postgresql.yaml
kubectl apply -f 04-networkpolicy-redis.yaml

# Verify all policies are applied
kubectl get networkpolicies -n honua

# Step 5: Test connectivity BEFORE applying default deny
# Run test script (see Verification section below)

# Step 6: Apply default deny LAST (after verifying all allow rules work)
kubectl apply -f 01-networkpolicy-default-deny.yaml

# Verify default deny is active
kubectl get networkpolicy default-deny-all -n honua
```

### Option 3: Gradual Rollout (Safest)

For production environments, deploy incrementally:

```bash
# Week 1: Deploy DNS and namespace isolation only
kubectl apply -f 00-namespace.yaml
kubectl apply -f 05-networkpolicy-dns.yaml
kubectl apply -f 06-networkpolicy-namespace-isolation.yaml

# Monitor for 1 week, verify no issues

# Week 2: Deploy service-specific policies
kubectl apply -f 02-networkpolicy-honua-server.yaml
kubectl apply -f 03-networkpolicy-postgresql.yaml
kubectl apply -f 04-networkpolicy-redis.yaml

# Monitor for 1 week, verify no connectivity issues

# Week 3: Deploy default deny (after confirming all allow rules work)
kubectl apply -f 01-networkpolicy-default-deny.yaml

# Monitor closely for 1 week
```

## Verification

### Step 1: Check NetworkPolicy Status

```bash
# List all NetworkPolicies
kubectl get networkpolicies -n honua

# Expected output:
# NAME                        POD-SELECTOR   AGE
# allow-dns-access            <none>         1m
# default-deny-all            <none>         1m
# honua-server                app=honua-server   1m
# namespace-isolation         <none>         1m
# postgis-database            app=postgis    1m
# redis-cache                 app=redis      1m

# Describe a policy to see details
kubectl describe networkpolicy honua-server -n honua
```

### Step 2: Run Automated Tests

Run the comprehensive test suite:

```bash
cd deploy/kubernetes/base

# Run with verbose output
./test-network-policies.sh --verbose

# Expected output:
# ========================================
# Test Summary
# ========================================
# Total tests: 15
# Passed: 15
# Failed: 0
# Skipped: 0
#
# ✓ All tests passed!
```

### Step 3: Manual Connectivity Tests

Test specific connectivity scenarios:

```bash
# Create a test pod with honua-server label
kubectl run test-honua-server -n honua \
  --image=nicolaka/netshoot \
  --labels="app=honua-server" \
  --rm -it -- /bin/bash

# Inside the pod, test allowed connections:
nc -zv postgis-service 5432          # Should succeed
nc -zv redis-service 6379            # Should succeed
nc -zv kube-dns.kube-system 53       # Should succeed
curl -v https://www.google.com       # Should succeed

# Test blocked connections:
# (None expected from honua-server pod)

exit

# Create a test pod WITHOUT app label (should be blocked)
kubectl run test-generic -n honua \
  --image=nicolaka/netshoot \
  --rm -it -- /bin/bash

# Inside the pod, test blocked connections:
nc -zv postgis-service 5432          # Should FAIL (blocked)
nc -zv redis-service 6379            # Should FAIL (blocked)
curl -v https://www.google.com       # Should FAIL (blocked)

# DNS should still work:
nc -zv kube-dns.kube-system 53       # Should succeed

exit
```

### Step 4: Check Application Health

```bash
# Check if Honua Server pods are running
kubectl get pods -n honua -l app=honua-server

# Check logs for connection errors
kubectl logs -n honua -l app=honua-server --tail=50

# Test API endpoint
kubectl port-forward -n honua svc/honua-service 8080:80
curl http://localhost:8080/health

# Check database connectivity
kubectl logs -n honua -l app=honua-server | grep -i database

# Check Redis connectivity
kubectl logs -n honua -l app=honua-server | grep -i redis
```

### Step 5: Monitor Metrics

```bash
# Check Prometheus metrics (if monitoring is enabled)
kubectl port-forward -n monitoring svc/prometheus 9090:9090

# Open browser: http://localhost:9090
# Query: sum(rate(container_network_receive_bytes_total{namespace="honua"}[5m]))

# Check for connection errors in logs
kubectl logs -n honua -l app=honua-server | grep -i "connection refused\|timeout\|failed to connect"
```

## Troubleshooting

### Issue 1: Pods Cannot Connect to Database

**Symptoms:**
- Application logs show database connection errors
- `nc -zv postgis-service 5432` fails from Honua Server pod

**Solutions:**

```bash
# 1. Check if PostgreSQL service exists
kubectl get service postgis-service -n honua

# 2. Verify pod labels match NetworkPolicy selector
kubectl get pods -n honua -l app=honua-server --show-labels
kubectl describe networkpolicy postgis-database -n honua | grep -A 10 "Ingress"

# 3. Check if DNS is working
kubectl exec -it <honua-pod> -n honua -- nslookup postgis-service

# 4. Test connectivity with correct labels
kubectl run test-with-label -n honua \
  --image=nicolaka/netshoot \
  --labels="app=honua-server" \
  --rm -it -- nc -zv postgis-service 5432
```

### Issue 2: External API Calls Failing

**Symptoms:**
- Cannot access cloud storage (S3, Azure, GCS)
- OIDC authentication failures
- Weather API calls failing

**Solutions:**

```bash
# 1. Check egress rules for Honua Server
kubectl describe networkpolicy honua-server -n honua | grep -A 20 "Egress"

# 2. Test DNS resolution
kubectl exec -it <honua-pod> -n honua -- nslookup google.com

# 3. Test HTTPS connectivity
kubectl exec -it <honua-pod> -n honua -- curl -v https://www.google.com

# 4. Check if egress to port 443 is allowed
# Look for this rule in honua-server NetworkPolicy:
#   ports:
#   - protocol: TCP
#     port: 443

# 5. If using IP restrictions, verify IP ranges
kubectl describe networkpolicy honua-server -n honua | grep -A 5 "ipBlock"
```

### Issue 3: Monitoring Not Working

**Symptoms:**
- Prometheus cannot scrape metrics
- No metrics in Grafana
- Health checks failing

**Solutions:**

```bash
# 1. Check monitoring namespace labels
kubectl get namespace monitoring --show-labels

# Required label: name=monitoring OR monitoring=true

# 2. Add missing label
kubectl label namespace monitoring monitoring=true

# 3. Verify ingress rules allow monitoring
kubectl describe networkpolicy honua-server -n honua | grep -A 10 "Ingress"

# 4. Test from monitoring namespace
kubectl run test-monitoring -n monitoring \
  --image=nicolaka/netshoot \
  --rm -it -- curl -v http://honua-service.honua.svc.cluster.local/metrics
```

### Issue 4: Ingress Not Working

**Symptoms:**
- Cannot access application from outside cluster
- 502/503 errors from ingress controller

**Solutions:**

```bash
# 1. Check ingress controller namespace labels
kubectl get namespace ingress-nginx --show-labels

# Required label: name=ingress-nginx OR ingress-controller=true

# 2. Add missing label
kubectl label namespace ingress-nginx ingress-controller=true

# 3. Verify ingress rules
kubectl describe networkpolicy honua-server -n honua | grep -A 10 "Ingress"

# 4. Test from ingress namespace
kubectl get pods -n ingress-nginx
kubectl exec -it <ingress-pod> -n ingress-nginx -- curl -v http://honua-service.honua.svc.cluster.local:80
```

### Issue 5: CNI Plugin Not Supporting NetworkPolicies

**Symptoms:**
- NetworkPolicies deployed but not enforced
- All traffic flows regardless of policies

**Solutions:**

```bash
# 1. Check CNI plugin
kubectl get pods -n kube-system | grep -E "calico|cilium|weave|antrea|flannel"

# 2. If using Flannel, install Calico for NetworkPolicy support
kubectl apply -f https://raw.githubusercontent.com/projectcalico/calico/v3.26.1/manifests/canal.yaml

# 3. Verify NetworkPolicy support
kubectl api-resources | grep networkpolicies

# 4. Check CNI logs for errors
kubectl logs -n kube-system -l k8s-app=calico-node | grep -i error
```

## Rollback

If you need to rollback NetworkPolicies:

### Quick Rollback (Restore Connectivity)

```bash
# Remove default deny policy immediately (restores most connectivity)
kubectl delete networkpolicy default-deny-all -n honua

# Wait 30 seconds and test connectivity
sleep 30
kubectl exec -it <honua-pod> -n honua -- nc -zv postgis-service 5432
```

### Full Rollback

```bash
# Remove all NetworkPolicies
kubectl delete networkpolicies -n honua --all

# Verify policies are removed
kubectl get networkpolicies -n honua

# Restore from backup (if needed)
kubectl apply -f networkpolicies-backup-<date>.yaml
```

### Rollback Individual Policies

```bash
# Remove specific policy
kubectl delete networkpolicy <policy-name> -n honua

# Example: Remove Honua Server policy
kubectl delete networkpolicy honua-server -n honua

# Verify
kubectl get networkpolicies -n honua
```

## Production Hardening

### 1. Restrict External Access by IP Range

For production, restrict external access to known IP ranges:

```bash
# Edit honua-server NetworkPolicy
kubectl edit networkpolicy honua-server -n honua
```

Add CIDR blocks:

```yaml
egress:
- to:
  - ipBlock:
      cidr: 52.0.0.0/8  # AWS S3 IP range
      except:
      - 52.0.0.0/24     # Block specific subnet
  ports:
  - protocol: TCP
    port: 443
```

### 2. Enable Pod Security Standards

```bash
# Apply restricted Pod Security Standard
kubectl label namespace honua \
  pod-security.kubernetes.io/enforce=restricted \
  pod-security.kubernetes.io/audit=restricted \
  pod-security.kubernetes.io/warn=restricted
```

### 3. Implement Service Mesh

For advanced security, consider a service mesh:

```bash
# Istio
istioctl install --set profile=default

# Linkerd
linkerd install | kubectl apply -f -

# Enable mTLS for namespace
kubectl label namespace honua istio-injection=enabled
```

### 4. Enable Network Logging

With Calico:

```bash
# Enable flow logs
kubectl apply -f - <<EOF
apiVersion: projectcalico.org/v3
kind: GlobalNetworkPolicy
metadata:
  name: network-logging
spec:
  selector: all()
  types:
  - Ingress
  - Egress
  ingress:
  - action: Log
  egress:
  - action: Log
EOF
```

### 5. Regular Security Audits

```bash
# Check for overly permissive policies
kubectl get networkpolicies -n honua -o yaml | grep -A 10 "podSelector: {}"

# Audit NetworkPolicy changes
kubectl get events -n honua --field-selector involvedObject.kind=NetworkPolicy

# Review external egress
kubectl describe networkpolicy honua-server -n honua | grep -A 30 "Egress"
```

## Best Practices

1. **Test in staging first**: Always deploy to staging before production
2. **Gradual rollout**: Deploy incrementally with monitoring
3. **Monitor logs**: Watch application logs for connection errors
4. **Use specific labels**: Avoid wildcards in pod/namespace selectors
5. **Document changes**: Track all NetworkPolicy changes in Git
6. **Regular audits**: Review policies quarterly
7. **Least privilege**: Grant minimal required network access
8. **Version control**: Keep NetworkPolicies in version control
9. **Automated testing**: Run test suite regularly
10. **Emergency rollback**: Have rollback plan ready

## Additional Resources

- [Network Architecture Documentation](./base/NETWORK_ARCHITECTURE.md)
- [NetworkPolicy README](./base/README.md)
- [Kubernetes NetworkPolicy Guide](https://kubernetes.io/docs/concepts/services-networking/network-policies/)
- [NetworkPolicy Recipes](https://github.com/ahmetb/kubernetes-network-policy-recipes)
- [Calico NetworkPolicy Tutorial](https://docs.projectcalico.org/security/tutorials/kubernetes-policy-basic)

## Support

For issues or questions:
1. Check [NETWORK_ARCHITECTURE.md](./base/NETWORK_ARCHITECTURE.md) for detailed documentation
2. Run the test suite: `./test-network-policies.sh --verbose`
3. Check troubleshooting section above
4. Review Kubernetes NetworkPolicy documentation

---

**Last Updated**: 2025-10-18
**Document Version**: 1.0
**Honua Version**: Latest
