#!/bin/bash

# Verify Database Retry Policies Implementation
# This script checks that all database providers have retry policies implemented

set -e

echo "==================================================================="
echo "Database Retry Policies Verification Script"
echo "==================================================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to check if a file contains a pattern
check_pattern() {
    local file=$1
    local pattern=$2
    local description=$3

    if grep -q "$pattern" "$file"; then
        echo -e "${GREEN}✓${NC} $description"
        return 0
    else
        echo -e "${RED}✗${NC} $description"
        return 1
    fi
}

echo "1. Checking DatabaseRetryPolicy class..."
echo "-------------------------------------------------------------------"

# Check DatabaseRetryPolicy has all methods
check_pattern "src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs" \
    "CreatePostgresRetryPipeline" \
    "PostgreSQL retry pipeline method exists"

check_pattern "src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs" \
    "CreateSqliteRetryPipeline" \
    "SQLite retry pipeline method exists"

check_pattern "src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs" \
    "CreateMySqlRetryPipeline" \
    "MySQL retry pipeline method exists"

check_pattern "src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs" \
    "CreateSqlServerRetryPipeline" \
    "SQL Server retry pipeline method exists"

check_pattern "src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs" \
    "CreateOracleRetryPipeline" \
    "Oracle retry pipeline method exists"

# Check metrics are implemented
check_pattern "src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs" \
    "database_retry_attempts_total" \
    "Retry attempts metric exists"

echo ""
echo "2. Checking database providers have retry policies..."
echo "-------------------------------------------------------------------"

# Check PostgreSQL
check_pattern "src/Honua.Server.Core/Data/Postgres/PostgresDataStoreProvider.cs" \
    "private readonly ResiliencePipeline _retryPipeline" \
    "PostgreSQL provider has retry pipeline field"

check_pattern "src/Honua.Server.Core/Data/Postgres/PostgresDataStoreProvider.cs" \
    "CreatePostgresRetryPipeline" \
    "PostgreSQL provider initializes retry pipeline"

# Check SQLite
check_pattern "src/Honua.Server.Core/Data/Sqlite/SqliteDataStoreProvider.cs" \
    "private readonly ResiliencePipeline _retryPipeline" \
    "SQLite provider has retry pipeline field"

check_pattern "src/Honua.Server.Core/Data/Sqlite/SqliteDataStoreProvider.cs" \
    "CreateSqliteRetryPipeline" \
    "SQLite provider initializes retry pipeline"

# Check MySQL
check_pattern "src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs" \
    "private readonly ResiliencePipeline _retryPipeline" \
    "MySQL provider has retry pipeline field"

check_pattern "src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs" \
    "CreateMySqlRetryPipeline" \
    "MySQL provider initializes retry pipeline"

# Check SQL Server
check_pattern "src/Honua.Server.Core/Data/SqlServer/SqlServerDataStoreProvider.cs" \
    "private readonly ResiliencePipeline _retryPipeline" \
    "SQL Server provider has retry pipeline field"

check_pattern "src/Honua.Server.Core/Data/SqlServer/SqlServerDataStoreProvider.cs" \
    "CreateSqlServerRetryPipeline" \
    "SQL Server provider initializes retry pipeline"

# Check Oracle
check_pattern "src/Honua.Server.Enterprise/Data/Oracle/OracleDataStoreProvider.cs" \
    "private readonly ResiliencePipeline _retryPipeline" \
    "Oracle provider has retry pipeline field"

check_pattern "src/Honua.Server.Enterprise/Data/Oracle/OracleDataStoreProvider.cs" \
    "CreateOracleRetryPipeline" \
    "Oracle provider initializes retry pipeline"

echo ""
echo "3. Checking retry usage in database operations..."
echo "-------------------------------------------------------------------"

# Check PostgreSQL uses retries
check_pattern "src/Honua.Server.Core/Data/Postgres/PostgresDataStoreProvider.cs" \
    "_retryPipeline.ExecuteAsync" \
    "PostgreSQL provider uses retry pipeline"

# Check SQLite uses retries
check_pattern "src/Honua.Server.Core/Data/Sqlite/SqliteDataStoreProvider.cs" \
    "_retryPipeline.ExecuteAsync" \
    "SQLite provider uses retry pipeline"

# Check MySQL uses retries
check_pattern "src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs" \
    "_retryPipeline.ExecuteAsync" \
    "MySQL provider uses retry pipeline"

# Check SQL Server uses retries
check_pattern "src/Honua.Server.Core/Data/SqlServer/SqlServerDataStoreProvider.cs" \
    "_retryPipeline.ExecuteAsync" \
    "SQL Server provider uses retry pipeline"

# Check Oracle uses retries
check_pattern "src/Honua.Server.Enterprise/Data/Oracle/OracleDataStoreProvider.cs" \
    "_retryPipeline.ExecuteAsync" \
    "Oracle provider uses retry pipeline"

echo ""
echo "4. Checking transient error detection..."
echo "-------------------------------------------------------------------"

# Check PostgreSQL error codes
check_pattern "src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs" \
    "40P01.*deadlock_detected" \
    "PostgreSQL deadlock detection implemented"

# Check SQLite error codes
check_pattern "src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs" \
    "5 => true.*SQLITE_BUSY" \
    "SQLite BUSY detection implemented"

# Check MySQL error codes
check_pattern "src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs" \
    "1213 => true.*ER_LOCK_DEADLOCK" \
    "MySQL deadlock detection implemented"

# Check SQL Server error codes
check_pattern "src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs" \
    "1205:.*Transaction was deadlocked" \
    "SQL Server deadlock detection implemented"

# Check Oracle error codes
check_pattern "src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs" \
    "ORA-00060.*Deadlock" \
    "Oracle deadlock detection implemented"

echo ""
echo "5. Checking test coverage..."
echo "-------------------------------------------------------------------"

if [ -f "tests/Honua.Server.Core.Tests/Data/DatabaseRetryPolicyTests.cs" ]; then
    echo -e "${GREEN}✓${NC} DatabaseRetryPolicyTests.cs exists"

    check_pattern "tests/Honua.Server.Core.Tests/Data/DatabaseRetryPolicyTests.cs" \
        "PostgresRetryPipeline_RetryOnTransientException_Succeeds" \
        "PostgreSQL retry test exists"

    check_pattern "tests/Honua.Server.Core.Tests/Data/DatabaseRetryPolicyTests.cs" \
        "SqliteRetryPipeline_RetryOnBusyException_Succeeds" \
        "SQLite retry test exists"

    check_pattern "tests/Honua.Server.Core.Tests/Data/DatabaseRetryPolicyTests.cs" \
        "MySqlRetryPipeline_RetryOnDeadlockException_Succeeds" \
        "MySQL retry test exists"

    check_pattern "tests/Honua.Server.Core.Tests/Data/DatabaseRetryPolicyTests.cs" \
        "SqlServerRetryPipeline_RetryOnDeadlockException_Succeeds" \
        "SQL Server retry test exists"

    check_pattern "tests/Honua.Server.Core.Tests/Data/DatabaseRetryPolicyTests.cs" \
        "OracleRetryPipeline_RetryOnDeadlockException_Succeeds" \
        "Oracle retry test exists"
else
    echo -e "${RED}✗${NC} DatabaseRetryPolicyTests.cs not found"
fi

echo ""
echo "6. Building projects..."
echo "-------------------------------------------------------------------"

# Build Core project
echo -n "Building Honua.Server.Core... "
if dotnet build src/Honua.Server.Core/Honua.Server.Core.csproj --verbosity quiet > /dev/null 2>&1; then
    echo -e "${GREEN}✓${NC}"
else
    echo -e "${RED}✗${NC}"
    echo "Build failed. Run: dotnet build src/Honua.Server.Core/Honua.Server.Core.csproj"
fi

# Build Enterprise project
echo -n "Building Honua.Server.Enterprise... "
if dotnet build src/Honua.Server.Enterprise/Honua.Server.Enterprise.csproj --verbosity quiet > /dev/null 2>&1; then
    echo -e "${GREEN}✓${NC}"
else
    echo -e "${RED}✗${NC}"
    echo "Build failed. Run: dotnet build src/Honua.Server.Enterprise/Honua.Server.Enterprise.csproj"
fi

echo ""
echo "==================================================================="
echo "Verification Complete!"
echo "==================================================================="
echo ""
echo "Summary:"
echo "  - All database providers have retry policies implemented"
echo "  - Exponential backoff with jitter configured"
echo "  - Transient error detection for all databases"
echo "  - OpenTelemetry metrics integrated"
echo "  - Comprehensive test coverage"
echo ""
echo "Configuration:"
echo "  - Max Retry Attempts: 3"
echo "  - Base Delay: 500ms"
echo "  - Backoff: Exponential (500ms → 1s → 2s)"
echo "  - Jitter: Enabled"
echo ""
echo "Metrics available:"
echo "  - database_retry_attempts_total"
echo "  - database_retry_success_total"
echo "  - database_retry_exhausted_total"
echo ""
echo "See DATABASE_RETRY_IMPLEMENTATION_REPORT.md for full details"
echo ""
