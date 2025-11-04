#!/bin/bash
# Docker build script for Honua Server
# Supports building Full and Lite variants for multiple architectures
#
# Usage:
#   ./scripts/docker-build.sh full amd64       # Build Full image for x64
#   ./scripts/docker-build.sh lite arm64       # Build Lite image for ARM64
#   ./scripts/docker-build.sh full multi       # Build Full multi-arch with buildx
#   ./scripts/docker-build.sh all multi        # Build all variants multi-arch

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
VARIANT="${1:-full}"
ARCHITECTURE="${2:-amd64}"
REGISTRY="${REGISTRY:-ghcr.io/honuaio}"
VERSION="${VERSION:-dev}"
PUSH="${PUSH:-false}"

# Functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

show_usage() {
    cat <<EOF
Docker Build Script for Honua Server

Usage:
  $0 <variant> <architecture> [options]

Variants:
  full        Full-featured image (vector + raster + cloud)
  lite        Lightweight image (vector-only, serverless-optimized)
  all         Build both variants

Architectures:
  amd64       x86_64 (Intel/AMD)
  arm64       aarch64 (ARM64, Graviton)
  multi       Multi-architecture build (requires buildx)

Environment Variables:
  REGISTRY    Container registry (default: ghcr.io/honuaio)
  VERSION     Image version tag (default: dev)
  PUSH        Push to registry after build (default: false)

Examples:
  # Build Full image for x64
  $0 full amd64

  # Build Lite image for ARM64
  $0 lite arm64

  # Build and push Full multi-arch image
  VERSION=1.0.0 PUSH=true $0 full multi

  # Build all variants for multi-arch
  $0 all multi

EOF
}

build_image() {
    local variant=$1
    local arch=$2
    local dockerfile="Dockerfile"
    local image_name="honua-server"

    if [ "$variant" = "lite" ]; then
        dockerfile="Dockerfile.lite"
    fi

    local platform="linux/${arch}"
    local tag="${REGISTRY}/${image_name}:${VERSION}"

    if [ "$variant" = "lite" ]; then
        tag="${tag}-lite"
    fi

    if [ "$arch" != "multi" ]; then
        tag="${tag}-${arch}"
    fi

    log_info "Building ${variant} variant for ${arch}..."
    log_info "Dockerfile: ${dockerfile}"
    log_info "Tag: ${tag}"

    if [ "$arch" = "multi" ]; then
        # Multi-architecture build with buildx
        local platforms="linux/amd64,linux/arm64"

        # Ensure buildx is set up
        if ! docker buildx inspect multiarch >/dev/null 2>&1; then
            log_warning "Buildx builder 'multiarch' not found. Creating..."
            docker buildx create --name multiarch --driver docker-container --bootstrap
        fi

        docker buildx use multiarch

        local buildx_args=(
            "buildx" "build"
            "--platform" "${platforms}"
            "-f" "${dockerfile}"
            "-t" "${tag}"
            "--build-arg" "VERSION=${VERSION}"
            "--build-arg" "BUILD_DATE=$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
            "--build-arg" "VCS_REF=$(git rev-parse --short HEAD 2>/dev/null || echo 'unknown')"
        )

        if [ "$PUSH" = "true" ]; then
            buildx_args+=("--push")
        else
            buildx_args+=("--load")
        fi

        buildx_args+=(".")

        docker "${buildx_args[@]}"
    else
        # Single architecture build
        local build_args=(
            "build"
            "--platform" "${platform}"
            "-f" "${dockerfile}"
            "-t" "${tag}"
            "--build-arg" "VERSION=${VERSION}"
            "--build-arg" "BUILD_DATE=$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
            "--build-arg" "VCS_REF=$(git rev-parse --short HEAD 2>/dev/null || echo 'unknown')"
            "."
        )

        docker "${build_args[@]}"

        if [ "$PUSH" = "true" ]; then
            log_info "Pushing ${tag}..."
            docker push "${tag}"
        fi
    fi

    log_success "Built ${tag}"
}

# Main script
main() {
    if [ "$#" -eq 0 ] || [ "$1" = "-h" ] || [ "$1" = "--help" ]; then
        show_usage
        exit 0
    fi

    # Change to repository root
    cd "$(dirname "$0")/.."

    log_info "Starting Honua Server Docker build"
    log_info "Variant: ${VARIANT}"
    log_info "Architecture: ${ARCHITECTURE}"
    log_info "Version: ${VERSION}"
    log_info "Registry: ${REGISTRY}"
    log_info "Push: ${PUSH}"
    echo ""

    if [ "$VARIANT" = "all" ]; then
        log_info "Building all variants..."
        build_image "full" "${ARCHITECTURE}"
        build_image "lite" "${ARCHITECTURE}"
    else
        build_image "${VARIANT}" "${ARCHITECTURE}"
    fi

    echo ""
    log_success "Build completed successfully!"

    if [ "$PUSH" = "false" ]; then
        log_info "Images were not pushed. To push, set PUSH=true"
    fi

    echo ""
    log_info "Available images:"
    docker images | grep "${REGISTRY}/honua-server" | grep "${VERSION}" || true
}

main "$@"
