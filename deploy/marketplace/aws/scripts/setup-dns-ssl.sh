#!/bin/bash
set -euo pipefail

# Honua IO - DNS and SSL/TLS Setup Script for AWS EKS
# This script configures DNS and SSL/TLS for your Honua IO deployment
#
# Prerequisites:
#   - CloudFormation stack deployed
#   - kubectl configured to access EKS cluster
#   - jq installed

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATES_DIR="$(dirname "$SCRIPT_DIR")/templates"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check dependencies
check_dependencies() {
    local missing_deps=()

    command -v aws >/dev/null 2>&1 || missing_deps+=("aws-cli")
    command -v kubectl >/dev/null 2>&1 || missing_deps+=("kubectl")
    command -v jq >/dev/null 2>&1 || missing_deps+=("jq")
    command -v helm >/dev/null 2>&1 || missing_deps+=("helm")

    if [ ${#missing_deps[@]} -ne 0 ]; then
        log_error "Missing required dependencies: ${missing_deps[*]}"
        echo "Please install them before continuing."
        exit 1
    fi
}

# Get CloudFormation stack outputs
get_stack_output() {
    local stack_name=$1
    local output_key=$2

    aws cloudformation describe-stacks \
        --stack-name "$stack_name" \
        --query "Stacks[0].Outputs[?OutputKey=='$output_key'].OutputValue" \
        --output text 2>/dev/null || echo ""
}

# Main function
main() {
    log_info "Honua IO DNS and SSL/TLS Setup"
    echo ""

    check_dependencies

    # Get stack name
    read -p "Enter CloudFormation stack name: " STACK_NAME

    if [ -z "$STACK_NAME" ]; then
        log_error "Stack name cannot be empty"
        exit 1
    fi

    log_info "Fetching stack outputs..."

    CLUSTER_NAME=$(get_stack_output "$STACK_NAME" "ClusterName")
    AWS_REGION=$(aws configure get region)
    DOMAIN_NAME=$(get_stack_output "$STACK_NAME" "DomainURL" | sed 's|https://||')
    HOSTED_ZONE_ID=$(get_stack_output "$STACK_NAME" "Route53HostedZoneId")
    ACM_CERT_ARN=$(get_stack_output "$STACK_NAME" "ACMCertificateArn")
    CERT_MANAGER_ROLE_ARN=$(get_stack_output "$STACK_NAME" "CertManagerRoleArn")
    EXTERNAL_DNS_ROLE_ARN=$(get_stack_output "$STACK_NAME" "ExternalDnsRoleArn")

    if [ -z "$CLUSTER_NAME" ]; then
        log_error "Could not find CloudFormation stack: $STACK_NAME"
        exit 1
    fi

    log_info "Stack Name: $STACK_NAME"
    log_info "Cluster Name: $CLUSTER_NAME"
    log_info "AWS Region: $AWS_REGION"
    log_info "Domain: ${DOMAIN_NAME:-N/A (using LoadBalancer DNS)}"
    echo ""

    # Configure kubectl
    log_info "Configuring kubectl..."
    aws eks update-kubeconfig --name "$CLUSTER_NAME" --region "$AWS_REGION"

    # Check if domain is configured
    if [ -z "$DOMAIN_NAME" ] || [ "$DOMAIN_NAME" = "N/A" ]; then
        log_warn "No custom domain configured. Skipping DNS/SSL setup."
        log_info "You can access Honua IO via the LoadBalancer URL:"
        kubectl get svc honua-gateway -n honua-system -o jsonpath='{.status.loadBalancer.ingress[0].hostname}' 2>/dev/null || echo "Service not yet deployed"
        exit 0
    fi

    # Ask for Let's Encrypt email if needed
    if [ -z "$ACM_CERT_ARN" ] || [ "$ACM_CERT_ARN" = "N/A - Using Let's Encrypt" ]; then
        read -p "Enter email for Let's Encrypt notifications: " LETSENCRYPT_EMAIL

        if [ -z "$LETSENCRYPT_EMAIL" ]; then
            log_error "Email is required for Let's Encrypt"
            exit 1
        fi
    else
        LETSENCRYPT_EMAIL="noreply@example.com"  # Not used when ACM cert exists
    fi

    # Install cert-manager
    log_info "Installing cert-manager..."

    # Add Jetstack Helm repository
    helm repo add jetstack https://charts.jetstack.io 2>/dev/null || true
    helm repo update

    # Install cert-manager with CRDs
    helm upgrade --install cert-manager jetstack/cert-manager \
        --namespace cert-manager \
        --create-namespace \
        --version v1.13.3 \
        --set installCRDs=true \
        --set serviceAccount.annotations."eks\.amazonaws\.com/role-arn"="$CERT_MANAGER_ROLE_ARN" \
        --wait

    log_info "cert-manager installed successfully"

    # Create temporary file for infrastructure manifest
    INFRA_MANIFEST=$(mktemp)

    # Substitute variables in infrastructure manifest
    envsubst < "$TEMPLATES_DIR/kubernetes-infrastructure.yaml" > "$INFRA_MANIFEST" <<EOF
export CLUSTER_NAME="$CLUSTER_NAME"
export AWS_REGION="$AWS_REGION"
export HOSTED_ZONE_ID="$HOSTED_ZONE_ID"
export DOMAIN_NAME="$DOMAIN_NAME"
export LETSENCRYPT_EMAIL="$LETSENCRYPT_EMAIL"
export CERT_MANAGER_IRSA_ROLE_ARN="$CERT_MANAGER_ROLE_ARN"
export EXTERNAL_DNS_IRSA_ROLE_ARN="$EXTERNAL_DNS_ROLE_ARN"
EOF

    # Apply infrastructure manifest
    log_info "Deploying external-dns and SSL certificates..."
    sed -e "s|\${CLUSTER_NAME}|$CLUSTER_NAME|g" \
        -e "s|\${AWS_REGION}|$AWS_REGION|g" \
        -e "s|\${HOSTED_ZONE_ID}|$HOSTED_ZONE_ID|g" \
        -e "s|\${DOMAIN_NAME}|$DOMAIN_NAME|g" \
        -e "s|\${LETSENCRYPT_EMAIL}|$LETSENCRYPT_EMAIL|g" \
        -e "s|\${CERT_MANAGER_IRSA_ROLE_ARN}|$CERT_MANAGER_ROLE_ARN|g" \
        -e "s|\${EXTERNAL_DNS_IRSA_ROLE_ARN}|$EXTERNAL_DNS_ROLE_ARN|g" \
        "$TEMPLATES_DIR/kubernetes-infrastructure.yaml" | kubectl apply -f -

    rm -f "$INFRA_MANIFEST"

    # Wait for cert-manager to be ready
    log_info "Waiting for cert-manager to be ready..."
    kubectl wait --for=condition=Available --timeout=300s -n cert-manager deployment/cert-manager
    kubectl wait --for=condition=Available --timeout=300s -n cert-manager deployment/cert-manager-webhook
    kubectl wait --for=condition=Available --timeout=300s -n cert-manager deployment/cert-manager-cainjector

    # Wait for external-dns to be ready
    log_info "Waiting for external-dns to be ready..."
    kubectl wait --for=condition=Available --timeout=300s -n external-dns deployment/external-dns

    # Check certificate status
    log_info "Checking certificate status..."
    sleep 10  # Give cert-manager a moment to process

    kubectl get certificate -n honua-system honua-tls -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null || echo "Certificate not yet created"

    echo ""
    log_info "DNS and SSL/TLS setup complete!"
    echo ""
    echo "Next steps:"
    echo "1. If you created a new hosted zone, update your domain registrar with these nameservers:"

    if [ -n "$(get_stack_output "$STACK_NAME" "Route53Nameservers")" ]; then
        echo "   $(get_stack_output "$STACK_NAME" "Route53Nameservers")"
    fi

    echo ""
    echo "2. Check external-dns logs to verify DNS record creation:"
    echo "   kubectl logs -n external-dns -l app=external-dns"
    echo ""
    echo "3. Check certificate status:"
    echo "   kubectl describe certificate honua-tls -n honua-system"
    echo ""
    echo "4. Once DNS propagates and certificate is issued, access Honua IO at:"
    echo "   https://$DOMAIN_NAME"
    echo ""
}

main "$@"
