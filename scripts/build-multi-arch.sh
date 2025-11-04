#!/bin/bash
set -euo pipefail

# Multi-Architecture Docker Build Script for Honua
# Supports building for AMD64 and ARM64 architectures

# Configuration
IMAGE_NAME="${IMAGE_NAME:-honua-server}"
VERSION="${VERSION:-latest}"
PLATFORMS="${PLATFORMS:-linux/amd64,linux/arm64}"
DOCKERFILE="${DOCKERFILE:-Dockerfile}"
CACHE_FROM="${CACHE_FROM:-}"
CACHE_TO="${CACHE_TO:-}"
PUSH="${PUSH:-false}"

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

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    # Check if Docker is installed
    if ! command -v docker &> /dev/null; then
        log_error "Docker is not installed"
        exit 1
    fi

    # Check if buildx is available
    if ! docker buildx version &> /dev/null; then
        log_error "Docker buildx is not available"
        exit 1
    fi

    log_info "Prerequisites check passed"
}

# Setup buildx builder
setup_builder() {
    local builder_name="honua-multi-arch-builder"

    log_info "Setting up buildx builder..."

    # Check if builder already exists
    if docker buildx inspect "$builder_name" &> /dev/null; then
        log_info "Builder '$builder_name' already exists, using it"
        docker buildx use "$builder_name"
    else
        log_info "Creating new builder '$builder_name'"
        docker buildx create \
            --name "$builder_name" \
            --driver docker-container \
            --use
    fi

    # Bootstrap the builder
    docker buildx inspect --bootstrap

    log_info "Builder setup complete"
}

# Build and optionally push the image
build_image() {
    log_info "Building image: $IMAGE_NAME:$VERSION"
    log_info "Platforms: $PLATFORMS"

    local build_args=()
    build_args+=(--platform "$PLATFORMS")
    build_args+=(--tag "$IMAGE_NAME:$VERSION")
    build_args+=(--tag "$IMAGE_NAME:latest")
    build_args+=(--file "$DOCKERFILE")

    # Add cache configuration
    if [ -n "$CACHE_FROM" ]; then
        build_args+=(--cache-from "$CACHE_FROM")
    fi

    if [ -n "$CACHE_TO" ]; then
        build_args+=(--cache-to "$CACHE_TO")
    fi

    # Add build arguments
    build_args+=(--build-arg "VERSION=$VERSION")
    build_args+=(--build-arg "BUILD_DATE=$(date -u +'%Y-%m-%dT%H:%M:%SZ')")
    build_args+=(--build-arg "VCS_REF=$(git rev-parse --short HEAD 2>/dev/null || echo 'unknown')")

    # Add metadata labels
    build_args+=(--label "org.opencontainers.image.created=$(date -u +'%Y-%m-%dT%H:%M:%SZ')")
    build_args+=(--label "org.opencontainers.image.version=$VERSION")
    build_args+=(--label "org.opencontainers.image.revision=$(git rev-parse HEAD 2>/dev/null || echo 'unknown')")

    # Push or load
    if [ "$PUSH" = "true" ]; then
        log_info "Pushing image to registry"
        build_args+=(--push)
    else
        log_info "Building image locally (not pushing)"
        build_args+=(--load)
    fi

    # Build the image
    docker buildx build "${build_args[@]}" .

    if [ $? -eq 0 ]; then
        log_info "Build successful!"
    else
        log_error "Build failed!"
        exit 1
    fi
}

# Generate SBOM
generate_sbom() {
    log_info "Generating SBOM..."

    if command -v syft &> /dev/null; then
        syft "$IMAGE_NAME:$VERSION" -o spdx-json > "sbom-$VERSION.spdx.json"
        log_info "SBOM saved to sbom-$VERSION.spdx.json"
    else
        log_warn "Syft not installed, skipping SBOM generation"
    fi
}

# Scan image for vulnerabilities
scan_image() {
    log_info "Scanning image for vulnerabilities..."

    if command -v trivy &> /dev/null; then
        trivy image \
            --severity HIGH,CRITICAL \
            --exit-code 0 \
            "$IMAGE_NAME:$VERSION"

        log_info "Vulnerability scan complete"
    else
        log_warn "Trivy not installed, skipping vulnerability scan"
    fi
}

# Display image information
display_info() {
    log_info "Image Information:"
    echo "  Name: $IMAGE_NAME"
    echo "  Version: $VERSION"
    echo "  Platforms: $PLATFORMS"

    if [ "$PUSH" = "true" ]; then
        echo "  Registry: $(echo "$IMAGE_NAME" | cut -d/ -f1)"
    fi

    # Display image size for each platform
    if [ "$PUSH" = "false" ]; then
        log_info "Image sizes:"
        docker images "$IMAGE_NAME:$VERSION" --format "  {{.Repository}}:{{.Tag}} - {{.Size}}"
    fi
}

# Main execution
main() {
    log_info "Starting multi-architecture build..."

    check_prerequisites
    setup_builder
    build_image

    if [ "$PUSH" = "false" ]; then
        generate_sbom
        scan_image
    fi

    display_info

    log_info "Multi-architecture build complete!"
}

# Show usage
usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Build multi-architecture Docker images for Honua

OPTIONS:
    -n, --name NAME         Image name (default: honua-server)
    -v, --version VERSION   Image version (default: latest)
    -p, --platforms PLATFORMS
                           Target platforms (default: linux/amd64,linux/arm64)
    -f, --file DOCKERFILE   Dockerfile path (default: Dockerfile)
    --push                  Push image to registry
    --cache-from CACHE      Cache source
    --cache-to CACHE        Cache destination
    -h, --help             Show this help message

EXAMPLES:
    # Build for local use
    $0 --name myapp --version 1.0.0

    # Build and push to registry
    $0 --name ghcr.io/org/app --version 1.0.0 --push

    # Build with caching
    $0 --cache-from type=registry,ref=myapp:buildcache \\
       --cache-to type=registry,ref=myapp:buildcache,mode=max \\
       --push

EOF
}

# Parse command line arguments
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
        -p|--platforms)
            PLATFORMS="$2"
            shift 2
            ;;
        -f|--file)
            DOCKERFILE="$2"
            shift 2
            ;;
        --push)
            PUSH="true"
            shift
            ;;
        --cache-from)
            CACHE_FROM="$2"
            shift 2
            ;;
        --cache-to)
            CACHE_TO="$2"
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

# Run main function
main
