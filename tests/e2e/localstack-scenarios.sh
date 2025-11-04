#!/usr/bin/env bash
#
# LocalStack AWS E2E Test Suite for Honua
# Tests AWS service integrations using LocalStack
#

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test results
TESTS_PASSED=0
TESTS_FAILED=0
TESTS_TOTAL=0

# LocalStack configuration
LOCALSTACK_ENDPOINT="http://localhost:4566"
AWS_DEFAULT_REGION="us-east-1"
AWS_ACCESS_KEY_ID="test"
AWS_SECRET_ACCESS_KEY="test"

# Cleanup function
cleanup() {
    echo -e "${YELLOW}Cleaning up LocalStack resources...${NC}"
    docker rm -f honua-localstack-test 2>/dev/null || true

    # Clean up test files
    rm -rf /tmp/honua-localstack-test 2>/dev/null || true
}

trap cleanup EXIT

# Test helper functions
test_start() {
    local test_name="$1"
    TESTS_TOTAL=$((TESTS_TOTAL + 1))
    echo -e "\n${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}Test $TESTS_TOTAL: $test_name${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

test_pass() {
    TESTS_PASSED=$((TESTS_PASSED + 1))
    echo -e "${GREEN}✓ PASSED${NC}"
}

test_fail() {
    local reason="$1"
    TESTS_FAILED=$((TESTS_FAILED + 1))
    echo -e "${RED}✗ FAILED: $reason${NC}"
}

# Start LocalStack
start_localstack() {
    echo "Starting LocalStack..."
    docker run -d \
        --name honua-localstack-test \
        -p 4566:4566 \
        -e SERVICES=s3,dynamodb,lambda,ecs,ecr,secretsmanager \
        -e DEBUG=1 \
        -e DOCKER_HOST=unix:///var/run/docker.sock \
        -v /var/run/docker.sock:/var/run/docker.sock \
        localstack/localstack:latest

    # Wait for LocalStack to be ready
    echo "Waiting for LocalStack to be ready..."
    for i in {1..30}; do
        if curl -s "$LOCALSTACK_ENDPOINT/health" | grep -q "\"s3\": \"available\""; then
            echo "LocalStack is ready!"
            return 0
        fi
        sleep 2
    done

    echo -e "${RED}ERROR: LocalStack failed to start${NC}"
    return 1
}

# Test 1: S3 Bucket Operations
test_s3_operations() {
    test_start "S3 bucket creation and object operations"

    # Create bucket
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 mb s3://honua-test-bucket 2>/dev/null

    # Create test file
    echo "test geospatial data" > /tmp/test-geodata.txt

    # Upload file
    if aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 cp /tmp/test-geodata.txt s3://honua-test-bucket/ > /dev/null 2>&1; then
        # Download file
        if aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 cp s3://honua-test-bucket/test-geodata.txt /tmp/test-geodata-download.txt > /dev/null 2>&1; then
            # Verify content
            if diff /tmp/test-geodata.txt /tmp/test-geodata-download.txt > /dev/null 2>&1; then
                test_pass
            else
                test_fail "Downloaded file content mismatch"
            fi
        else
            test_fail "Failed to download file from S3"
        fi
    else
        test_fail "Failed to upload file to S3"
    fi

    # Cleanup
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 rb s3://honua-test-bucket --force 2>/dev/null || true
    rm -f /tmp/test-geodata.txt /tmp/test-geodata-download.txt
}

# Test 2: S3 Bucket Versioning
test_s3_versioning() {
    test_start "S3 bucket versioning for GeoJSON files"

    # Create bucket with versioning
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 mb s3://honua-versioned-bucket 2>/dev/null
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3api put-bucket-versioning \
        --bucket honua-versioned-bucket \
        --versioning-configuration Status=Enabled

    # Upload version 1
    echo '{"type":"Feature","geometry":{"type":"Point","coordinates":[0,0]}}' > /tmp/test.geojson
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 cp /tmp/test.geojson s3://honua-versioned-bucket/ > /dev/null 2>&1

    # Upload version 2
    echo '{"type":"Feature","geometry":{"type":"Point","coordinates":[1,1]}}' > /tmp/test.geojson
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 cp /tmp/test.geojson s3://honua-versioned-bucket/ > /dev/null 2>&1

    # List versions
    version_count=$(aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3api list-object-versions \
        --bucket honua-versioned-bucket | grep -c "VersionId" || echo "0")

    if [ "$version_count" -ge "2" ]; then
        test_pass
    else
        test_fail "Versioning not working (found $version_count versions)"
    fi

    # Cleanup
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 rb s3://honua-versioned-bucket --force 2>/dev/null || true
    rm -f /tmp/test.geojson
}

# Test 3: DynamoDB Table Operations
test_dynamodb_operations() {
    test_start "DynamoDB table creation and item operations"

    # Create table
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" dynamodb create-table \
        --table-name HonuaMetadata \
        --attribute-definitions \
            AttributeName=CollectionId,AttributeType=S \
            AttributeName=Timestamp,AttributeType=N \
        --key-schema \
            AttributeName=CollectionId,KeyType=HASH \
            AttributeName=Timestamp,KeyType=RANGE \
        --billing-mode PAY_PER_REQUEST > /dev/null 2>&1

    # Put item
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" dynamodb put-item \
        --table-name HonuaMetadata \
        --item '{
            "CollectionId": {"S": "test-collection"},
            "Timestamp": {"N": "1234567890"},
            "Metadata": {"S": "test metadata"}
        }' > /dev/null 2>&1

    # Get item
    result=$(aws --endpoint-url="$LOCALSTACK_ENDPOINT" dynamodb get-item \
        --table-name HonuaMetadata \
        --key '{"CollectionId": {"S": "test-collection"}, "Timestamp": {"N": "1234567890"}}' \
        --query 'Item.Metadata.S' --output text 2>/dev/null)

    if [ "$result" = "test metadata" ]; then
        test_pass
    else
        test_fail "Failed to retrieve item from DynamoDB"
    fi

    # Cleanup
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" dynamodb delete-table --table-name HonuaMetadata > /dev/null 2>&1 || true
}

# Test 4: Secrets Manager
test_secrets_manager() {
    test_start "AWS Secrets Manager operations"

    # Create secret
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" secretsmanager create-secret \
        --name honua/db/credentials \
        --secret-string '{"username":"dbuser","password":"dbpass123"}' > /dev/null 2>&1

    # Retrieve secret
    secret=$(aws --endpoint-url="$LOCALSTACK_ENDPOINT" secretsmanager get-secret-value \
        --secret-id honua/db/credentials \
        --query 'SecretString' --output text 2>/dev/null)

    if echo "$secret" | grep -q "dbuser"; then
        test_pass
    else
        test_fail "Failed to retrieve secret"
    fi

    # Cleanup
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" secretsmanager delete-secret \
        --secret-id honua/db/credentials --force-delete-without-recovery > /dev/null 2>&1 || true
}

# Test 5: S3 with CloudFront-like behavior
test_s3_public_access() {
    test_start "S3 bucket with public access configuration"

    # Create bucket
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 mb s3://honua-public-tiles 2>/dev/null

    # Disable block public access
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3api put-public-access-block \
        --bucket honua-public-tiles \
        --public-access-block-configuration \
            "BlockPublicAcls=false,IgnorePublicAcls=false,BlockPublicPolicy=false,RestrictPublicBuckets=false" 2>/dev/null

    # Set bucket policy
    cat > /tmp/bucket-policy.json <<EOF
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Principal": "*",
            "Action": "s3:GetObject",
            "Resource": "arn:aws:s3:::honua-public-tiles/*"
        }
    ]
}
EOF

    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3api put-bucket-policy \
        --bucket honua-public-tiles \
        --policy file:///tmp/bucket-policy.json 2>/dev/null

    # Upload a tile
    echo "tile data" > /tmp/tile.mvt
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 cp /tmp/tile.mvt s3://honua-public-tiles/tiles/14/8192/5461.mvt > /dev/null 2>&1

    # Verify policy is set
    if aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3api get-bucket-policy \
        --bucket honua-public-tiles > /dev/null 2>&1; then
        test_pass
    else
        test_fail "Failed to set bucket policy"
    fi

    # Cleanup
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 rb s3://honua-public-tiles --force 2>/dev/null || true
    rm -f /tmp/bucket-policy.json /tmp/tile.mvt
}

# Test 6: Multi-bucket scenario (data lake)
test_multi_bucket_datalake() {
    test_start "Multi-bucket data lake architecture"

    # Create buckets for different data zones
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 mb s3://honua-raw-data 2>/dev/null
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 mb s3://honua-processed-data 2>/dev/null
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 mb s3://honua-analytics 2>/dev/null

    # Upload to raw
    echo "raw geojson" > /tmp/raw.geojson
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 cp /tmp/raw.geojson s3://honua-raw-data/ > /dev/null 2>&1

    # Simulate processing: copy to processed
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 cp s3://honua-raw-data/raw.geojson s3://honua-processed-data/processed.geojson > /dev/null 2>&1

    # Verify all buckets exist and have expected files
    raw_exists=$(aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 ls s3://honua-raw-data/ | grep -c "raw.geojson" || echo "0")
    processed_exists=$(aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 ls s3://honua-processed-data/ | grep -c "processed.geojson" || echo "0")

    if [ "$raw_exists" -eq "1" ] && [ "$processed_exists" -eq "1" ]; then
        test_pass
    else
        test_fail "Data lake buckets not set up correctly"
    fi

    # Cleanup
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 rb s3://honua-raw-data --force 2>/dev/null || true
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 rb s3://honua-processed-data --force 2>/dev/null || true
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 rb s3://honua-analytics --force 2>/dev/null || true
    rm -f /tmp/raw.geojson
}

# Main execution
main() {
    echo -e "${GREEN}╔══════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}║  Honua LocalStack AWS E2E Test Suite                            ║${NC}"
    echo -e "${GREEN}╚══════════════════════════════════════════════════════════════════╝${NC}\n"

    # Check dependencies
    if ! command -v docker &> /dev/null; then
        echo -e "${RED}ERROR: Docker is not installed${NC}"
        exit 1
    fi

    if ! command -v aws &> /dev/null; then
        echo -e "${RED}ERROR: AWS CLI is not installed${NC}"
        echo -e "${YELLOW}Install with: pip install awscli-local${NC}"
        exit 1
    fi

    # Check if LocalStack is already running
    if docker ps | grep -q honua-localstack-test; then
        echo -e "${YELLOW}LocalStack container already running, reusing...${NC}"
    else
        if ! start_localstack; then
            echo -e "${RED}Failed to start LocalStack${NC}"
            exit 1
        fi
    fi

    # Export AWS credentials for LocalStack
    export AWS_ACCESS_KEY_ID="test"
    export AWS_SECRET_ACCESS_KEY="test"
    export AWS_DEFAULT_REGION="us-east-1"

    # Run all tests
    test_s3_operations
    test_s3_versioning
    test_dynamodb_operations
    test_secrets_manager
    test_s3_public_access
    test_multi_bucket_datalake

    # Print results
    echo -e "\n${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}Test Results${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "Total Tests: $TESTS_TOTAL"
    echo -e "${GREEN}Passed: $TESTS_PASSED${NC}"
    echo -e "${RED}Failed: $TESTS_FAILED${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}\n"

    if [ "$TESTS_FAILED" -eq "0" ]; then
        echo -e "${GREEN}✓ All tests passed!${NC}\n"
        exit 0
    else
        echo -e "${RED}✗ Some tests failed${NC}\n"
        exit 1
    fi
}

main "$@"
