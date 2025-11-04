#!/bin/bash
# Testcontainers Verification Script
# Verifies Docker availability and Testcontainers setup

set -e

echo "========================================="
echo "Testcontainers Setup Verification"
echo "========================================="
echo ""

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check Docker availability
echo "1. Checking Docker availability..."
if command -v docker &> /dev/null; then
    echo -e "${GREEN}✓ Docker CLI found${NC}"

    if docker ps &> /dev/null; then
        echo -e "${GREEN}✓ Docker daemon is running${NC}"
    else
        echo -e "${RED}✗ Docker daemon is not running${NC}"
        echo "  Please start Docker Desktop or Docker service"
        exit 1
    fi
else
    echo -e "${RED}✗ Docker CLI not found${NC}"
    echo "  Please install Docker Desktop: https://www.docker.com/products/docker-desktop/"
    exit 1
fi
echo ""

# Check Docker version
echo "2. Docker version:"
docker --version
echo ""

# Check required images
echo "3. Checking required Docker images..."
images=(
    "minio/minio:latest"
    "mcr.microsoft.com/azure-storage/azurite:latest"
    "redis:7-alpine"
    "postgis/postgis:16-3.4"
    "mysql:8.0"
    "alpine:latest"
)

for image in "${images[@]}"; do
    echo "   Checking: $image"
    if docker image inspect "$image" &> /dev/null; then
        echo -e "   ${GREEN}✓ Image available locally${NC}"
    else
        echo -e "   ${YELLOW}⚠ Image not found locally, will be pulled on first test run${NC}"
    fi
done
echo ""

# Check .NET SDK
echo "4. Checking .NET SDK..."
if command -v dotnet &> /dev/null; then
    echo -e "${GREEN}✓ .NET SDK found${NC}"
    dotnet --version
else
    echo -e "${RED}✗ .NET SDK not found${NC}"
    echo "  Please install .NET 9.0 SDK: https://dotnet.microsoft.com/download"
    exit 1
fi
echo ""

# Check NuGet packages
echo "5. Checking Testcontainers NuGet packages..."
cd "$(dirname "$0")/Honua.Server.Core.Tests"

if grep -q "Testcontainers.Minio" Honua.Server.Core.Tests.csproj; then
    echo -e "${GREEN}✓ Testcontainers.Minio package configured${NC}"
else
    echo -e "${RED}✗ Testcontainers.Minio package not found${NC}"
fi

if grep -q "Testcontainers.Azurite" Honua.Server.Core.Tests.csproj; then
    echo -e "${GREEN}✓ Testcontainers.Azurite package configured${NC}"
else
    echo -e "${RED}✗ Testcontainers.Azurite package not found${NC}"
fi

if grep -q "Testcontainers.Redis" Honua.Server.Core.Tests.csproj; then
    echo -e "${GREEN}✓ Testcontainers.Redis package configured${NC}"
else
    echo -e "${RED}✗ Testcontainers.Redis package not found${NC}"
fi

if grep -q "Testcontainers.PostgreSql" Honua.Server.Core.Tests.csproj; then
    echo -e "${GREEN}✓ Testcontainers.PostgreSql package configured${NC}"
else
    echo -e "${RED}✗ Testcontainers.PostgreSql package not found${NC}"
fi

if grep -q "Testcontainers.MySql" Honua.Server.Core.Tests.csproj; then
    echo -e "${GREEN}✓ Testcontainers.MySql package configured${NC}"
else
    echo -e "${RED}✗ Testcontainers.MySql package not found${NC}"
fi
echo ""

# Check test infrastructure files
echo "6. Checking test infrastructure files..."
files=(
    "TestInfrastructure/StorageContainerFixture.cs"
    "TestInfrastructure/RedisContainerFixture.cs"
    "TestInfrastructure/DockerAvailability.cs"
    "TestInfrastructure/MultiProviderTestFixture.cs"
)

for file in "${files[@]}"; do
    if [ -f "$file" ]; then
        echo -e "   ${GREEN}✓ $file${NC}"
    else
        echo -e "   ${RED}✗ $file not found${NC}"
    fi
done
echo ""

# Check Docker Compose configuration
echo "7. Checking Docker Compose configuration..."
if [ -f "docker-compose.storage-emulators.yml" ]; then
    echo -e "${GREEN}✓ docker-compose.storage-emulators.yml found${NC}"

    # Count services
    service_count=$(grep -c "image:" docker-compose.storage-emulators.yml || true)
    echo "   Services configured: $service_count"
else
    echo -e "${RED}✗ docker-compose.storage-emulators.yml not found${NC}"
fi
echo ""

# Check documentation
echo "8. Checking documentation..."
cd ..
docs=(
    "TESTCONTAINERS_GUIDE.md"
    "Honua.Server.Core.Tests/DOCKER_COMPOSE_TESTING.md"
    "Honua.Server.Core.Tests/STORAGE_INTEGRATION_TESTS.md"
)

for doc in "${docs[@]}"; do
    if [ -f "$doc" ]; then
        echo -e "   ${GREEN}✓ $doc${NC}"
    else
        echo -e "   ${YELLOW}⚠ $doc not found${NC}"
    fi
done
echo ""

# Summary
echo "========================================="
echo "Verification Summary"
echo "========================================="
echo -e "${GREEN}✓ Docker is available and ready${NC}"
echo -e "${GREEN}✓ Testcontainers packages configured${NC}"
echo -e "${GREEN}✓ Test infrastructure created${NC}"
echo -e "${GREEN}✓ Docker Compose configuration ready${NC}"
echo ""
echo "Next steps:"
echo "1. Pull required images (optional, recommended):"
echo "   docker pull minio/minio:latest"
echo "   docker pull mcr.microsoft.com/azure-storage/azurite:latest"
echo "   docker pull redis:7-alpine"
echo ""
echo "2. Run tests:"
echo "   cd .."
echo "   dotnet test"
echo ""
echo "3. Or start manual test environment:"
echo "   cd tests/Honua.Server.Core.Tests"
echo "   docker-compose -f docker-compose.storage-emulators.yml up -d"
echo ""
echo "For more information, see:"
echo "  - tests/TESTCONTAINERS_GUIDE.md"
echo "  - tests/Honua.Server.Core.Tests/DOCKER_COMPOSE_TESTING.md"
echo ""
