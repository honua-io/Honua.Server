#!/bin/bash
set -euo pipefail

# Push Docker Images to Multiple Container Registries
# Supports: GHCR, AWS ECR, Azure ACR, GCP GCR, Docker Hub

# Configuration
IMAGE_NAME="${IMAGE_NAME:-honua-server}"
VERSION="${VERSION:-latest}"
SOURCE_IMAGE="${SOURCE_IMAGE:-}"

# Registry configurations
GHCR_ENABLED="${GHCR_ENABLED:-false}"
GHCR_REGISTRY="${GHCR_REGISTRY:-ghcr.io}"
GHCR_ORG="${GHCR_ORG:-}"

AWS_ECR_ENABLED="${AWS_ECR_ENABLED:-false}"
AWS_ECR_REGISTRY="${AWS_ECR_REGISTRY:-}"
AWS_REGION="${AWS_REGION:-us-east-1}"

AZURE_ACR_ENABLED="${AZURE_ACR_ENABLED:-false}"
AZURE_ACR_REGISTRY="${AZURE_ACR_REGISTRY:-}"

GCP_GCR_ENABLED="${GCP_GCR_ENABLED:-false}"
GCP_GCR_REGISTRY="${GCP_GCR_REGISTRY:-gcr.io}"
GCP_PROJECT="${GCP_PROJECT:-}"

DOCKERHUB_ENABLED="${DOCKERHUB_ENABLED:-false}"
DOCKERHUB_ORG="${DOCKERHUB_ORG:-}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

# Login to GitHub Container Registry
login_ghcr() {
    log_step "Logging in to GitHub Container Registry..."

    if [ -z "$GHCR_TOKEN" ]; then
        log_error "GHCR_TOKEN environment variable not set"
        return 1
    fi

    echo "$GHCR_TOKEN" | docker login ghcr.io -u "$GHCR_USERNAME" --password-stdin

    log_info "Successfully logged in to GHCR"
}

# Login to AWS ECR
login_aws_ecr() {
    log_step "Logging in to AWS ECR..."

    if ! command -v aws &> /dev/null; then
        log_error "AWS CLI not installed"
        return 1
    fi

    aws ecr get-login-password --region "$AWS_REGION" | \
        docker login --username AWS --password-stdin "$AWS_ECR_REGISTRY"

    log_info "Successfully logged in to AWS ECR"
}

# Login to Azure ACR
login_azure_acr() {
    log_step "Logging in to Azure Container Registry..."

    if ! command -v az &> /dev/null; then
        log_error "Azure CLI not installed"
        return 1
    fi

    # Login to Azure (assumes service principal or managed identity)
    if [ -n "$AZURE_CLIENT_ID" ] && [ -n "$AZURE_CLIENT_SECRET" ] && [ -n "$AZURE_TENANT_ID" ]; then
        az login --service-principal \
            -u "$AZURE_CLIENT_ID" \
            -p "$AZURE_CLIENT_SECRET" \
            --tenant "$AZURE_TENANT_ID" > /dev/null
    fi

    # Get ACR credentials
    az acr login --name "$(echo "$AZURE_ACR_REGISTRY" | cut -d. -f1)"

    log_info "Successfully logged in to Azure ACR"
}

# Login to GCP GCR
login_gcp_gcr() {
    log_step "Logging in to Google Container Registry..."

    if ! command -v gcloud &> /dev/null; then
        log_error "Google Cloud SDK not installed"
        return 1
    fi

    # Authenticate using service account key
    if [ -n "$GCP_SERVICE_ACCOUNT_KEY" ]; then
        echo "$GCP_SERVICE_ACCOUNT_KEY" | gcloud auth activate-service-account --key-file=-
    fi

    # Configure Docker to use gcloud as credential helper
    gcloud auth configure-docker "$GCP_GCR_REGISTRY" --quiet

    log_info "Successfully logged in to GCP GCR"
}

# Login to Docker Hub
login_dockerhub() {
    log_step "Logging in to Docker Hub..."

    if [ -z "$DOCKERHUB_TOKEN" ]; then
        log_error "DOCKERHUB_TOKEN environment variable not set"
        return 1
    fi

    echo "$DOCKERHUB_TOKEN" | docker login -u "$DOCKERHUB_USERNAME" --password-stdin

    log_info "Successfully logged in to Docker Hub"
}

# Tag and push image to a registry
push_to_registry() {
    local source_image="$1"
    local target_image="$2"
    local registry_name="$3"

    log_step "Pushing to $registry_name..."

    # Tag image
    docker tag "$source_image" "$target_image"

    # Push image
    if docker push "$target_image"; then
        log_info "Successfully pushed to $registry_name: $target_image"
        return 0
    else
        log_error "Failed to push to $registry_name"
        return 1
    fi
}

# Push multi-arch manifest
push_manifest() {
    local image="$1"
    local platforms="${2:-linux/amd64,linux/arm64}"

    log_step "Creating multi-arch manifest for $image..."

    local manifest_images=()

    # Parse platforms
    IFS=',' read -ra PLATFORM_ARRAY <<< "$platforms"

    for platform in "${PLATFORM_ARRAY[@]}"; do
        local arch=$(echo "$platform" | cut -d/ -f2)
        manifest_images+=("$image-$arch")
    done

    # Create and push manifest
    docker manifest create "$image" "${manifest_images[@]}"
    docker manifest push "$image"

    log_info "Manifest pushed successfully"
}

# Main push logic
push_images() {
    local success_count=0
    local fail_count=0

    # Determine source image
    if [ -z "$SOURCE_IMAGE" ]; then
        SOURCE_IMAGE="$IMAGE_NAME:$VERSION"
    fi

    log_info "Source image: $SOURCE_IMAGE"
    log_info "Version: $VERSION"
    echo ""

    # Push to GHCR
    if [ "$GHCR_ENABLED" = "true" ]; then
        if login_ghcr; then
            local target="${GHCR_REGISTRY}/${GHCR_ORG}/${IMAGE_NAME}:${VERSION}"
            if push_to_registry "$SOURCE_IMAGE" "$target" "GHCR"; then
                ((success_count++))

                # Also tag as latest if not a pre-release
                if [[ ! "$VERSION" =~ (alpha|beta|rc) ]]; then
                    docker tag "$SOURCE_IMAGE" "${GHCR_REGISTRY}/${GHCR_ORG}/${IMAGE_NAME}:latest"
                    docker push "${GHCR_REGISTRY}/${GHCR_ORG}/${IMAGE_NAME}:latest"
                fi
            else
                ((fail_count++))
            fi
        else
            ((fail_count++))
        fi
        echo ""
    fi

    # Push to AWS ECR
    if [ "$AWS_ECR_ENABLED" = "true" ]; then
        if login_aws_ecr; then
            local target="${AWS_ECR_REGISTRY}/${IMAGE_NAME}:${VERSION}"
            if push_to_registry "$SOURCE_IMAGE" "$target" "AWS ECR"; then
                ((success_count++))
            else
                ((fail_count++))
            fi
        else
            ((fail_count++))
        fi
        echo ""
    fi

    # Push to Azure ACR
    if [ "$AZURE_ACR_ENABLED" = "true" ]; then
        if login_azure_acr; then
            local target="${AZURE_ACR_REGISTRY}/${IMAGE_NAME}:${VERSION}"
            if push_to_registry "$SOURCE_IMAGE" "$target" "Azure ACR"; then
                ((success_count++))
            else
                ((fail_count++))
            fi
        else
            ((fail_count++))
        fi
        echo ""
    fi

    # Push to GCP GCR
    if [ "$GCP_GCR_ENABLED" = "true" ]; then
        if login_gcp_gcr; then
            local target="${GCP_GCR_REGISTRY}/${GCP_PROJECT}/${IMAGE_NAME}:${VERSION}"
            if push_to_registry "$SOURCE_IMAGE" "$target" "GCP GCR"; then
                ((success_count++))
            else
                ((fail_count++))
            fi
        else
            ((fail_count++))
        fi
        echo ""
    fi

    # Push to Docker Hub
    if [ "$DOCKERHUB_ENABLED" = "true" ]; then
        if login_dockerhub; then
            local target="${DOCKERHUB_ORG}/${IMAGE_NAME}:${VERSION}"
            if push_to_registry "$SOURCE_IMAGE" "$target" "Docker Hub"; then
                ((success_count++))
            else
                ((fail_count++))
            fi
        else
            ((fail_count++))
        fi
        echo ""
    fi

    # Summary
    log_info "Push Summary:"
    echo "  Successful: $success_count"
    echo "  Failed: $fail_count"

    if [ $fail_count -gt 0 ]; then
        log_warn "Some pushes failed!"
        return 1
    else
        log_info "All pushes successful!"
        return 0
    fi
}

# Usage
usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Push Docker images to multiple container registries

OPTIONS:
    -n, --name NAME         Image name (default: honua-server)
    -v, --version VERSION   Image version (default: latest)
    -s, --source IMAGE      Source image to push (default: IMAGE_NAME:VERSION)
    -h, --help             Show this help message

REGISTRY CONFIGURATION (via environment variables):
    GHCR:
        GHCR_ENABLED=true
        GHCR_USERNAME=username
        GHCR_TOKEN=token
        GHCR_ORG=organization

    AWS ECR:
        AWS_ECR_ENABLED=true
        AWS_ECR_REGISTRY=123456789.dkr.ecr.region.amazonaws.com
        AWS_REGION=us-east-1
        AWS_ACCESS_KEY_ID=key
        AWS_SECRET_ACCESS_KEY=secret

    Azure ACR:
        AZURE_ACR_ENABLED=true
        AZURE_ACR_REGISTRY=myregistry.azurecr.io
        AZURE_CLIENT_ID=client-id
        AZURE_CLIENT_SECRET=secret
        AZURE_TENANT_ID=tenant-id

    GCP GCR:
        GCP_GCR_ENABLED=true
        GCP_GCR_REGISTRY=gcr.io
        GCP_PROJECT=project-id
        GCP_SERVICE_ACCOUNT_KEY=service-account-key-json

    Docker Hub:
        DOCKERHUB_ENABLED=true
        DOCKERHUB_USERNAME=username
        DOCKERHUB_TOKEN=token
        DOCKERHUB_ORG=organization

EXAMPLE:
    $0 --name honua-server --version 1.0.0

EOF
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -n|--name)
            IMAGE_NAME="$2"
            shift 2
            ;;
        -v|--version)
            VERSION="$2"
            shift 2
            ;;
        -s|--source)
            SOURCE_IMAGE="$2"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            usage
            exit 1
            ;;
    esac
done

# Main execution
main() {
    log_info "Starting push to registries..."
    push_images
    log_info "Push complete!"
}

main
