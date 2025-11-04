# Backend configuration for development environment
# This file configures remote state storage in S3

# Note: Before running terraform init, ensure:
# 1. S3 bucket "honua-terraform-state-dev" exists
# 2. DynamoDB table "honua-terraform-locks" exists with LockID as partition key
# 3. Bucket versioning is enabled
# 4. Bucket encryption is enabled

# To create the S3 bucket and DynamoDB table:
#   aws s3api create-bucket --bucket honua-terraform-state-dev --region us-east-1
#   aws s3api put-bucket-versioning --bucket honua-terraform-state-dev --versioning-configuration Status=Enabled
#   aws s3api put-bucket-encryption --bucket honua-terraform-state-dev --server-side-encryption-configuration '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}'
#   aws dynamodb create-table --table-name honua-terraform-locks --attribute-definitions AttributeName=LockID,AttributeType=S --key-schema AttributeName=LockID,KeyType=HASH --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5
