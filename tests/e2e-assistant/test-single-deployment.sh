#!/bin/bash
# Quick test script for single deployment with real AI

set -e

if [ -z "$OPENAI_API_KEY" ]; then
    echo "ERROR: OPENAI_API_KEY environment variable required"
    echo "Usage: export OPENAI_API_KEY=sk-your-key && ./test-single-deployment.sh"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"

# Get prompt from user or use default
if [ -z "$1" ]; then
    PROMPT="Deploy Honua with PostGIS database and Redis caching using Docker Compose for development"
    echo "Using default prompt: $PROMPT"
else
    PROMPT="$1"
    echo "Using custom prompt: $PROMPT"
fi

# Create workspace
WORKSPACE="/tmp/honua-test-$(date +%s)"
mkdir -p "$WORKSPACE"

echo ""
echo "=== Step 1: AI Generating Configuration ==="
echo ""

# Run consultant
cd "$PROJECT_ROOT"
dotnet run --project src/Honua.Cli -- consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-log

echo ""
echo "=== Step 2: Reviewing Generated Files ==="
echo ""

ls -lah "$WORKSPACE"

if [ -f "$WORKSPACE/docker-compose.yml" ]; then
    echo ""
    echo "Generated docker-compose.yml:"
    echo "─────────────────────────────"
    cat "$WORKSPACE/docker-compose.yml"
    echo ""

    # Ask if user wants to deploy
    read -p "Deploy this configuration? (y/N) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo ""
        echo "=== Step 3: Deploying with Docker Compose ==="
        echo ""

        PROJECT_NAME="honua-quicktest-$(date +%s)"
        cd "$WORKSPACE"

        docker-compose -f docker-compose.yml -p "$PROJECT_NAME" up -d

        echo ""
        echo "Waiting for services to be ready..."
        sleep 10

        # Find Honua port
        HONUA_PORT=$(grep -A 5 "honua:" docker-compose.yml | grep -E "^\s*-\s*\"?[0-9]+" | head -1 | sed -E 's/.*"?([0-9]+):.*/\1/' || echo "5000")

        echo ""
        echo "=== Step 4: Testing Endpoints ==="
        echo ""

        # Test OGC landing page
        if curl -s -f "http://localhost:$HONUA_PORT/ogc" > /dev/null; then
            echo "✓ OGC landing page: http://localhost:$HONUA_PORT/ogc"
        else
            echo "✗ OGC landing page failed"
        fi

        # Test collections
        if curl -s "http://localhost:$HONUA_PORT/ogc/collections" | jq -e '.collections' > /dev/null 2>&1; then
            echo "✓ Collections endpoint working"
        else
            echo "⚠ Collections endpoint returned unexpected format"
        fi

        echo ""
        echo "Services running. Access Honua at: http://localhost:$HONUA_PORT"
        echo ""
        echo "To stop and clean up:"
        echo "  docker-compose -p $PROJECT_NAME down -v"
        echo ""
        echo "To view logs:"
        echo "  docker-compose -p $PROJECT_NAME logs -f"
    else
        echo "Deployment skipped."
        echo "To deploy manually:"
        echo "  cd $WORKSPACE"
        echo "  docker-compose up -d"
    fi
else
    echo "No docker-compose.yml generated. Check workspace: $WORKSPACE"
fi

echo ""
echo "Workspace: $WORKSPACE"
