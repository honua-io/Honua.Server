#!/bin/bash

echo "🚀 Setting up Honua development environment..."

# Restore .NET dependencies
echo "📦 Restoring .NET packages..."
dotnet restore BSCore.sln

# Build the solution (non-critical if it fails due to remaining issues)
echo "🔨 Attempting to build solution..."
dotnet build BSCore.sln --no-restore || echo "⚠️  Build has issues - will continue with test setup"

# Run basic tests to verify environment
echo "🧪 Running basic spatial tests..."
cd BSCoreTest/Honua.BasicTests
dotnet test --verbosity normal || echo "⚠️  Some tests failed - checking GDAL setup"

# Verify GDAL/PROJ setup
echo "🗺️  Verifying GDAL/PROJ installation..."
echo "GDAL Version: $(gdalinfo --version)"
echo "PROJ Version: $(proj --version)"
echo "GDAL Data: $GDAL_DATA"
echo "PROJ Data: $PROJ_LIB"

# List available GDAL drivers
echo "📋 Available GDAL drivers:"
ogrinfo --formats | head -10

# Test database connections
echo "🗄️  Testing database connections..."

# Wait for databases to be ready
echo "⏳ Waiting for databases to start..."
sleep 10

# Test SQL Server connection
echo "Testing SQL Server..."
sqlcmd -S sqlserver -U sa -P 'Honua123!' -Q "SELECT @@VERSION" || echo "⚠️  SQL Server not ready"

# Test PostgreSQL connection
echo "Testing PostgreSQL..."
PGPASSWORD='Honua123!' psql -h postgres -U postgres -d honua -c "SELECT version();" || echo "⚠️  PostgreSQL not ready"

# Test MySQL connection
echo "Testing MySQL..."
mysql -h mysql -u root -p'Honua123!' -e "SELECT VERSION();" || echo "⚠️  MySQL not ready"

# Test Redis connection
echo "Testing Redis..."
redis-cli -h redis ping || echo "⚠️  Redis not ready"

echo "✅ Development environment setup complete!"
echo ""
echo "🔗 Available services:"
echo "  - SQL Server: localhost:1433 (sa/Honua123!)"
echo "  - PostgreSQL: localhost:5432 (postgres/Honua123!)"
echo "  - MySQL: localhost:3306 (root/Honua123!)"
echo "  - Redis: localhost:6379"
echo ""
echo "🧪 To run tests:"
echo "  cd BSCoreTest/Honua.BasicTests && dotnet test"
echo ""
echo "🔨 To build:"
echo "  dotnet build BSCore.sln"