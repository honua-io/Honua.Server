#!/bin/bash
# Helper script to create Kubernetes secrets for Honua Server

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

print_success() { echo -e "${GREEN}✓${NC} $1"; }
print_error() { echo -e "${RED}✗${NC} $1"; }
print_info() { echo -e "${BLUE}ℹ${NC} $1"; }

# Configuration
NAMESPACE="${NAMESPACE:-honua}"
SECRET_NAME="${SECRET_NAME:-honua-secrets}"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -n|--namespace)
            NAMESPACE="$2"
            shift 2
            ;;
        --secret-name)
            SECRET_NAME="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -n, --namespace NAMESPACE    Target namespace (default: honua)"
            echo "  --secret-name NAME           Secret name (default: honua-secrets)"
            echo "  -h, --help                   Show this help message"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

print_info "Creating secrets for Honua Server in namespace: $NAMESPACE"
echo ""

# Create namespace if it doesn't exist
if ! kubectl get namespace "$NAMESPACE" &> /dev/null; then
    print_info "Creating namespace: $NAMESPACE"
    kubectl create namespace "$NAMESPACE"
    print_success "Namespace created"
else
    print_info "Namespace already exists"
fi
echo ""

# Database configuration
print_info "Database Configuration"
read -p "Database host: " DB_HOST
read -p "Database port [5432]: " DB_PORT
DB_PORT=${DB_PORT:-5432}
read -p "Database name: " DB_NAME
read -p "Database username: " DB_USER
read -sp "Database password: " DB_PASSWORD
echo ""

# Create database secret
print_info "Creating database secret..."
kubectl create secret generic "${SECRET_NAME}-db" \
    --from-literal=host="$DB_HOST" \
    --from-literal=port="$DB_PORT" \
    --from-literal=database="$DB_NAME" \
    --from-literal=username="$DB_USER" \
    --from-literal=password="$DB_PASSWORD" \
    --namespace="$NAMESPACE" \
    --dry-run=client -o yaml | kubectl apply -f -

print_success "Database secret created"
echo ""

# Redis configuration
read -p "Configure Redis? (y/N): " CONFIGURE_REDIS
if [[ "$CONFIGURE_REDIS" =~ ^[Yy]$ ]]; then
    print_info "Redis Configuration"
    read -p "Redis host: " REDIS_HOST
    read -p "Redis port [6379]: " REDIS_PORT
    REDIS_PORT=${REDIS_PORT:-6379}
    read -sp "Redis password (optional): " REDIS_PASSWORD
    echo ""

    print_info "Creating Redis secret..."
    kubectl create secret generic "${SECRET_NAME}-redis" \
        --from-literal=host="$REDIS_HOST" \
        --from-literal=port="$REDIS_PORT" \
        --from-literal=password="$REDIS_PASSWORD" \
        --namespace="$NAMESPACE" \
        --dry-run=client -o yaml | kubectl apply -f -

    print_success "Redis secret created"
    echo ""
fi

# TLS certificate
read -p "Create TLS secret? (y/N): " CREATE_TLS
if [[ "$CREATE_TLS" =~ ^[Yy]$ ]]; then
    print_info "TLS Configuration"
    read -p "Path to TLS certificate file: " TLS_CERT
    read -p "Path to TLS key file: " TLS_KEY

    if [ -f "$TLS_CERT" ] && [ -f "$TLS_KEY" ]; then
        print_info "Creating TLS secret..."
        kubectl create secret tls "${SECRET_NAME}-tls" \
            --cert="$TLS_CERT" \
            --key="$TLS_KEY" \
            --namespace="$NAMESPACE" \
            --dry-run=client -o yaml | kubectl apply -f -

        print_success "TLS secret created"
    else
        print_error "Certificate or key file not found"
    fi
    echo ""
fi

# Image pull secret
read -p "Create image pull secret? (y/N): " CREATE_PULL_SECRET
if [[ "$CREATE_PULL_SECRET" =~ ^[Yy]$ ]]; then
    print_info "Image Pull Secret Configuration"
    read -p "Registry server: " REGISTRY_SERVER
    read -p "Username: " REGISTRY_USER
    read -sp "Password: " REGISTRY_PASSWORD
    echo ""
    read -p "Email: " REGISTRY_EMAIL

    print_info "Creating image pull secret..."
    kubectl create secret docker-registry "${SECRET_NAME}-pull" \
        --docker-server="$REGISTRY_SERVER" \
        --docker-username="$REGISTRY_USER" \
        --docker-password="$REGISTRY_PASSWORD" \
        --docker-email="$REGISTRY_EMAIL" \
        --namespace="$NAMESPACE" \
        --dry-run=client -o yaml | kubectl apply -f -

    print_success "Image pull secret created"
    echo ""
fi

# Summary
print_success "All secrets created successfully!"
echo ""
print_info "To view secrets:"
echo "  kubectl get secrets -n $NAMESPACE"
echo ""
print_info "To delete secrets:"
echo "  kubectl delete secret ${SECRET_NAME}-db -n $NAMESPACE"
echo "  kubectl delete secret ${SECRET_NAME}-redis -n $NAMESPACE"
echo "  kubectl delete secret ${SECRET_NAME}-tls -n $NAMESPACE"
echo "  kubectl delete secret ${SECRET_NAME}-pull -n $NAMESPACE"
