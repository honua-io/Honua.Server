#!/bin/bash

# Script to initialize SQL Server database for Honua integration tests

echo "Starting SQL Server container for tests..."

# Start SQL Server container
docker-compose -f docker-compose-test.yml up -d

echo "Waiting for SQL Server to be ready..."
sleep 30

# Wait for SQL Server to be healthy
echo "Checking SQL Server health..."
for i in {1..30}; do
    if docker exec honua-sqlserver-test /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P TestPassword123! -Q "SELECT 1" -C -No > /dev/null 2>&1; then
        echo "SQL Server is ready!"
        break
    fi
    echo "Waiting for SQL Server... ($i/30)"
    sleep 2
done

# Create the test database
echo "Creating HonuaTest database..."
docker exec honua-sqlserver-test /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P TestPassword123! -C -No -Q "
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'HonuaTest')
BEGIN
    CREATE DATABASE HonuaTest;
    PRINT 'HonuaTest database created successfully.';
END
ELSE
BEGIN
    PRINT 'HonuaTest database already exists.';
END
" -C

echo "Test database setup complete!"
echo "Connection string: Server=localhost,1433;Database=HonuaTest;User Id=sa;Password=TestPassword123!;MultipleActiveResultSets=True;Encrypt=False;TrustServerCertificate=True;"
