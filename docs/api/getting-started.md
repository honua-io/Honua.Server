# Getting Started with Honua Build Orchestrator API

This guide will help you get started with the Honua Build Orchestrator API in minutes.

## Prerequisites

1. **Honua Account**: Sign up at https://honua.io/signup
2. **API Credentials**: Generate from your dashboard at https://dashboard.honua.io
3. **HTTP Client**: curl, Postman, or your preferred HTTP client

## Authentication Setup

### Option 1: JWT Token (Recommended)

1. Log in to your dashboard
2. Navigate to **Settings > API Keys**
3. Click **Generate New Key**
4. Copy your JWT token
5. Include in requests:

```bash
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Option 2: API Key

1. Generate an API key from your dashboard
2. Include in requests:

```bash
X-API-Key: honua_sk_abc123def456
```

See [Authentication Guide](authentication.md) for detailed information.

## Your First API Call

Let's start a conversation with the AI to configure a custom server build.

### Step 1: Start a Conversation

```bash
curl -X POST https://api.honua.io/api/intake/start \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust_abc123"
  }'
```

**Response:**

```json
{
  "conversationId": "conv_xyz789abc123",
  "initialMessage": "Hi! I'm here to help you build a custom Honua Server...",
  "startedAt": "2025-10-29T10:30:00Z",
  "customerId": "cust_abc123"
}
```

### Step 2: Send Messages

```bash
curl -X POST https://api.honua.io/api/intake/message \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "conv_xyz789abc123",
    "message": "I need to deploy an ESRI-compatible server on AWS with PostgreSQL"
  }'
```

**Response:**

```json
{
  "conversationId": "conv_xyz789abc123",
  "message": "Great choice! A few more questions...",
  "intakeComplete": false,
  "requirements": null,
  "timestamp": "2025-10-29T10:31:15Z"
}
```

### Step 3: Continue Until Complete

Keep sending messages until `intakeComplete` is `true`:

```json
{
  "conversationId": "conv_xyz789abc123",
  "message": "Perfect! I have everything I need...",
  "intakeComplete": true,
  "requirements": {
    "protocols": ["ESRI-REST", "WFS-2.0", "WMS-1.3.0"],
    "databases": ["PostgreSQL-PostGIS"],
    "cloudProvider": "aws",
    "architecture": "linux-arm64",
    "tier": "Pro"
  },
  "estimatedMonthlyCost": 95.00,
  "costBreakdown": {
    "compute": 50.00,
    "storage": 10.00,
    "database": 25.00,
    "networking": 10.00
  },
  "timestamp": "2025-10-29T10:35:22Z"
}
```

### Step 4: Trigger the Build

```bash
curl -X POST https://api.honua.io/api/intake/build \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "conv_xyz789abc123",
    "customerId": "cust_abc123",
    "buildName": "production-geospatial-server"
  }'
```

**Response:**

```json
{
  "success": true,
  "buildId": "build_def456ghi789",
  "manifest": { ... },
  "registryResult": {
    "success": true,
    "credential": {
      "registryUrl": "123456789012.dkr.ecr.us-west-2.amazonaws.com",
      "username": "AWS",
      "password": "eyJwYXlsb2FkIjoiZXlKMGIydGxiaUk2...",
      "expiresAt": "2025-10-30T10:40:00Z"
    }
  },
  "triggeredAt": "2025-10-29T10:40:00Z"
}
```

### Step 5: Monitor Build Progress

```bash
curl -X GET https://api.honua.io/api/intake/builds/build_def456ghi789/status \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

**Response (In Progress):**

```json
{
  "buildId": "build_def456ghi789",
  "status": "building",
  "progress": 45,
  "currentStage": "Installing database connectors",
  "startedAt": "2025-10-29T10:40:00Z"
}
```

**Response (Completed):**

```json
{
  "buildId": "build_def456ghi789",
  "status": "completed",
  "progress": 100,
  "currentStage": "Build completed successfully",
  "imageReference": "123456789012.dkr.ecr.us-west-2.amazonaws.com/honua/cust_abc123/production-geospatial-server:latest-arm64",
  "startedAt": "2025-10-29T10:40:00Z",
  "completedAt": "2025-10-29T10:55:30Z"
}
```

### Step 6: Deploy Your Container

```bash
# Login to registry
docker login 123456789012.dkr.ecr.us-west-2.amazonaws.com \
  -u AWS \
  -p eyJwYXlsb2FkIjoiZXlKMGIydGxiaUk2...

# Pull image
docker pull 123456789012.dkr.ecr.us-west-2.amazonaws.com/honua/cust_abc123/production-geospatial-server:latest-arm64

# Run container
docker run -d \
  -p 8080:8080 \
  -e DATABASE_URL="postgresql://user:pass@host:5432/db" \
  -e LICENSE_KEY="HONUA-PRO-ABC123-DEF456-GHI789-JKL012" \
  123456789012.dkr.ecr.us-west-2.amazonaws.com/honua/cust_abc123/production-geospatial-server:latest-arm64
```

## What's Next?

Now that you've completed your first build:

1. **Explore APIs**: Learn about all available endpoints
   - [Intake API](intake-api.md) - AI-guided configuration
   - [Build API](build-api.md) - Build management
   - [License API](license-api.md) - License operations
   - [Registry API](registry-api.md) - Registry management

2. **Set Up Webhooks**: Get real-time notifications
   - [Webhooks Guide](webhooks.md)

3. **Integrate with CI/CD**: Automate your deployments
   - GitHub Actions examples
   - GitLab CI examples
   - Jenkins examples

4. **Use SDKs**: Speed up development with official clients
   - [C# SDK](sdks/csharp.md)
   - [Python SDK](sdks/python.md)
   - [JavaScript SDK](sdks/javascript.md)

5. **Best Practices**: Learn optimization techniques
   - Caching strategies
   - Cost optimization
   - Performance tuning
   - Security hardening

## Common Use Cases

### Migrating from ArcGIS Server

```bash
# Tell the AI about your migration
curl -X POST https://api.honua.io/api/intake/message \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "conv_xyz789abc123",
    "message": "I am migrating from ArcGIS Server 10.9 with 100+ layers and need full ESRI REST API compatibility"
  }'
```

### STAC Catalog Deployment

```bash
curl -X POST https://api.honua.io/api/intake/message \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "conv_xyz789abc123",
    "message": "I need a STAC catalog server for satellite imagery with PostgreSQL backend"
  }'
```

### Multi-Cloud Deployment

```bash
curl -X POST https://api.honua.io/api/intake/message \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "conv_xyz789abc123",
    "message": "I need builds for AWS, Azure, and GCP with identical configurations"
  }'
```

## Rate Limits

- **Free Tier**: 100 requests/hour
- **Pro Tier**: 1,000 requests/hour
- **Enterprise**: 10,000 requests/hour
- **Enterprise ASP**: Unlimited

See [Rate Limits](rate-limits.md) for details.

## Error Handling

All errors follow a consistent format:

```json
{
  "error": "Conversation conv_xyz789abc123 not found",
  "status": 404,
  "title": "Not Found",
  "timestamp": "2025-10-29T10:40:00Z"
}
```

See [Error Handling Guide](errors.md) for complete error reference.

## Getting Help

- **Documentation**: https://docs.honua.io
- **API Reference**: https://api.honua.io/docs
- **Support Email**: support@honua.io
- **Community**: https://discord.gg/honua
- **Status Page**: https://status.honua.io

## Next Steps

Ready to dive deeper? Check out:

- [Authentication Guide](authentication.md) - Secure your API access
- [Intake API Reference](intake-api.md) - Complete endpoint documentation
- [Code Examples](examples/) - Sample code in your language
- [Best Practices](best-practices.md) - Production deployment tips
