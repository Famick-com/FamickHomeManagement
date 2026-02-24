#!/bin/bash
# Build and publish the self-hosted Docker image to Docker Hub
# Usage: ./publish-selfhosted-dockerhub.sh <version>
# Example: ./publish-selfhosted-dockerhub.sh 1.0.0         (tags: 1.0.0 + latest)
# Example: ./publish-selfhosted-dockerhub.sh 1.0.0-beta.1  (tags: 1.0.0-beta.1 only)
#
# Prerequisites:
#   You must be logged in to Docker Hub before running this script.
#   Run one of the following:
#
#     # Interactive login (prompts for password/PAT):
#     docker login -u mtherienfamick
#
#     # Non-interactive (e.g. CI) using a Personal Access Token:
#     echo "$DOCKERHUB_TOKEN" | docker login -u mtherienfamick --password-stdin
#
#   To create a Personal Access Token:
#     1. Go to https://hub.docker.com/settings/security
#     2. Click "New Access Token"
#     3. Give it a description (e.g. "Famick CI") and Read/Write permissions
#     4. Store the token securely (it's only shown once)

set -e

# Configuration
DOCKERHUB_USER="mtherienfamick"
DOCKERHUB_REPO="mtherienfamick/famick"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DOCKERFILE="Dockerfile"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

VERSION="${1:-}"
if [ -z "$VERSION" ]; then
    log_error "Version parameter is required."
    echo "Usage: $0 <version>"
    echo "  e.g. $0 1.0.0         (release — tags: 1.0.0 + latest)"
    echo "  e.g. $0 1.0.0-beta.1  (pre-release — tags: 1.0.0-beta.1 only)"
    exit 1
fi

# Determine if this is a release version (no hyphen suffix = release)
IS_RELEASE=false
if [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    IS_RELEASE=true
fi

# Pull latest code
log_info "Pulling latest code..."
cd "$PROJECT_ROOT" && git pull origin main
cd "$PROJECT_ROOT"

# Verify Docker is running
if ! docker info > /dev/null 2>&1; then
    log_error "Docker is not running. Please start Docker and try again."
    exit 1
fi

# Check Docker Hub authentication
log_info "Checking Docker Hub authentication..."
if ! docker pull "$DOCKERHUB_REPO:__auth_check__" 2>&1 | grep -q "denied\|unauthorized" && \
   ! docker pull "$DOCKERHUB_REPO:__auth_check__" 2>&1 | grep -q "not found"; then
    : # authenticated or repo accessible
fi

# A more reliable check: try a no-op login to see if credentials are stored
if ! grep -q "hub.docker.com\|docker.io\|index.docker.io" ~/.docker/config.json 2>/dev/null; then
    log_warn "No Docker Hub credentials found."
    log_info "Please run: docker login -u $DOCKERHUB_USER"
    log_info "Or set DOCKERHUB_TOKEN and run: echo \"\$DOCKERHUB_TOKEN\" | docker login -u $DOCKERHUB_USER --password-stdin"
    exit 1
fi
log_info "Docker Hub credentials found."

# Verify Dockerfile exists
if [ ! -f "$PROJECT_ROOT/$DOCKERFILE" ]; then
    log_error "Dockerfile not found at $PROJECT_ROOT/$DOCKERFILE"
    exit 1
fi

cd "$PROJECT_ROOT"

log_info "Building self-hosted image from $DOCKERFILE"
log_info "Docker Hub repo: $DOCKERHUB_REPO"

# Build for both amd64 and arm64 (common self-hosting platforms)
PLATFORMS="linux/amd64,linux/arm64"

# Check if buildx is available for multi-platform builds
if docker buildx version > /dev/null 2>&1; then
    BUILDER_NAME="famick-multiplatform"

    # Create builder if it doesn't exist
    if ! docker buildx inspect "$BUILDER_NAME" > /dev/null 2>&1; then
        log_info "Creating buildx builder for multi-platform builds..."
        docker buildx create --name "$BUILDER_NAME" --use
    else
        docker buildx use "$BUILDER_NAME"
    fi

    TAGS=(-t "$DOCKERHUB_REPO:$VERSION")
    if [ "$IS_RELEASE" = true ]; then
        TAGS+=(-t "$DOCKERHUB_REPO:latest")
    fi

    log_info "Building and pushing version $VERSION (multi-platform: $PLATFORMS)..."
    docker buildx build \
        --platform "$PLATFORMS" \
        -f "$DOCKERFILE" \
        "${TAGS[@]}" \
        --push \
        .
    log_info "Pushed $DOCKERHUB_REPO:$VERSION"
    if [ "$IS_RELEASE" = true ]; then
        log_info "Pushed $DOCKERHUB_REPO:latest"
    fi
else
    log_warn "Docker buildx not available. Building for local platform only."

    log_info "Building image..."
    docker build -f "$DOCKERFILE" -t "$DOCKERHUB_REPO:$VERSION" .

    log_info "Pushing $DOCKERHUB_REPO:$VERSION..."
    docker push "$DOCKERHUB_REPO:$VERSION"

    if [ "$IS_RELEASE" = true ]; then
        docker tag "$DOCKERHUB_REPO:$VERSION" "$DOCKERHUB_REPO:latest"
        log_info "Pushing $DOCKERHUB_REPO:latest..."
        docker push "$DOCKERHUB_REPO:latest"
    fi
fi

echo ""
log_info "Publish complete!"
log_info "Image available at: https://hub.docker.com/r/$DOCKERHUB_REPO"
if [ "$IS_RELEASE" = true ]; then
    log_info "Tags: $VERSION, latest"
else
    log_info "Tags: $VERSION (pre-release, no latest tag)"
fi
