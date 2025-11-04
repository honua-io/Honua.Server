# Intake API Reference

The Intake API provides an AI-powered conversational interface for configuring custom Honua Server builds.

## Base URL

```
https://api.honua.io/api/intake
```

## Endpoints

- [POST /start](#post-start) - Start a new conversation
- [POST /message](#post-message) - Send a message
- [GET /conversations/{conversationId}](#get-conversationsconversationid) - Get conversation history
- [POST /build](#post-build) - Trigger a build
- [GET /builds/{buildId}/status](#get-buildsbuildsidstatus) - Get build status

---

## POST /start

Start a new AI conversation for build configuration.

### Request

```http
POST /api/intake/start
Content-Type: application/json
Authorization: Bearer YOUR_JWT_TOKEN
```

**Body (optional):**

```json
{
  "customerId": "cust_abc123"
}
```

### Response

**200 OK**

```json
{
  "conversationId": "conv_xyz789abc123",
  "initialMessage": "Hi! I'm here to help you build a custom Honua Server tailored to your needs. To get started, could you tell me what kind of geospatial services you're looking to deploy?",
  "startedAt": "2025-10-29T10:30:00Z",
  "customerId": "cust_abc123"
}
```

### cURL Example

```bash
curl -X POST https://api.honua.io/api/intake/start \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"customerId": "cust_abc123"}'
```

### Python Example

```python
import requests

response = requests.post(
    'https://api.honua.io/api/intake/start',
    headers={'Authorization': 'Bearer YOUR_JWT_TOKEN'},
    json={'customerId': 'cust_abc123'}
)

data = response.json()
conversation_id = data['conversationId']
print(f"Conversation started: {conversation_id}")
print(f"AI says: {data['initialMessage']}")
```

### C# Example

```csharp
using System.Net.Http.Json;

var client = new HttpClient();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", "YOUR_JWT_TOKEN");

var request = new { customerId = "cust_abc123" };
var response = await client.PostAsJsonAsync(
    "https://api.honua.io/api/intake/start",
    request
);

var data = await response.Content.ReadFromJsonAsync<ConversationResponse>();
Console.WriteLine($"Conversation ID: {data.ConversationId}");
Console.WriteLine($"AI says: {data.InitialMessage}");
```

---

## POST /message

Send a message to continue the AI conversation.

### Request

```http
POST /api/intake/message
Content-Type: application/json
Authorization: Bearer YOUR_JWT_TOKEN
```

**Body:**

```json
{
  "conversationId": "conv_xyz789abc123",
  "message": "I need to deploy an ESRI-compatible server on AWS with PostgreSQL"
}
```

### Response

**In Progress (intakeComplete = false):**

```json
{
  "conversationId": "conv_xyz789abc123",
  "message": "Great choice! ESRI REST API compatibility with PostgreSQL on AWS is a popular combination. A few more questions:\n\n1. How many concurrent users do you expect?\n2. What's the approximate size of your spatial data?\n3. Do you need real-time features or primarily read-only access?",
  "intakeComplete": false,
  "requirements": null,
  "estimatedMonthlyCost": null,
  "costBreakdown": null,
  "timestamp": "2025-10-29T10:31:15Z"
}
```

**Complete (intakeComplete = true):**

```json
{
  "conversationId": "conv_xyz789abc123",
  "message": "Perfect! I have everything I need. Based on our conversation, I recommend:\n\n**Architecture:** ARM64 on AWS Graviton3 (40% cost savings)\n**Protocols:** ESRI REST API, WFS 2.0, WMS 1.3.0\n**Database:** PostgreSQL with PostGIS\n**Resources:** 4 vCPU, 8GB RAM (t4g.large)\n**Estimated Cost:** $95/month\n\nReady to proceed with the build?",
  "intakeComplete": true,
  "requirements": {
    "protocols": ["ESRI-REST", "WFS-2.0", "WMS-1.3.0"],
    "databases": ["PostgreSQL-PostGIS"],
    "cloudProvider": "aws",
    "architecture": "linux-arm64",
    "load": {
      "concurrentUsers": 50,
      "requestsPerSecond": 100,
      "dataVolumeGb": 50,
      "classification": "moderate"
    },
    "tier": "Pro",
    "advancedFeatures": [],
    "notes": "Customer prefers cost optimization with ARM64"
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

### cURL Example

```bash
curl -X POST https://api.honua.io/api/intake/message \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "conv_xyz789abc123",
    "message": "I need about 100 concurrent users with 50GB of data"
  }'
```

### Python Example

```python
import requests

def send_message(conversation_id, message):
    response = requests.post(
        'https://api.honua.io/api/intake/message',
        headers={'Authorization': 'Bearer YOUR_JWT_TOKEN'},
        json={
            'conversationId': conversation_id,
            'message': message
        }
    )
    return response.json()

# Conversation loop
conversation_id = "conv_xyz789abc123"
while True:
    user_message = input("You: ")
    if user_message.lower() == 'quit':
        break

    response = send_message(conversation_id, user_message)
    print(f"AI: {response['message']}\n")

    if response['intakeComplete']:
        print(f"Requirements extracted!")
        print(f"Estimated cost: ${response['estimatedMonthlyCost']}/month")
        break
```

### JavaScript Example

```javascript
async function sendMessage(conversationId, message) {
  const response = await fetch('https://api.honua.io/api/intake/message', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${process.env.HONUA_JWT_TOKEN}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ conversationId, message })
  });

  return await response.json();
}

// Usage
const result = await sendMessage(
  'conv_xyz789abc123',
  'I need about 100 concurrent users with 50GB of data'
);

console.log(`AI: ${result.message}`);

if (result.intakeComplete) {
  console.log('Requirements:', result.requirements);
  console.log(`Estimated cost: $${result.estimatedMonthlyCost}/month`);
}
```

### Error Responses

**400 Bad Request - Missing Fields:**

```json
{
  "error": "ConversationId is required",
  "status": 400,
  "title": "Bad Request",
  "timestamp": "2025-10-29T10:40:00Z"
}
```

**404 Not Found - Conversation Not Found:**

```json
{
  "error": "Conversation conv_xyz789abc123 not found",
  "status": 404,
  "title": "Not Found",
  "timestamp": "2025-10-29T10:40:00Z"
}
```

---

## GET /conversations/{conversationId}

Retrieve complete conversation history.

### Request

```http
GET /api/intake/conversations/conv_xyz789abc123
Authorization: Bearer YOUR_JWT_TOKEN
```

### Response

**200 OK**

```json
{
  "conversationId": "conv_xyz789abc123",
  "customerId": "cust_abc123",
  "messagesJson": "[{\"role\":\"assistant\",\"content\":\"Hi! I'm here to help...\"},{\"role\":\"user\",\"content\":\"I need ESRI compatibility\"}]",
  "status": "active",
  "requirementsJson": "{\"protocols\":[\"ESRI-REST\"],\"cloudProvider\":\"aws\"}",
  "createdAt": "2025-10-29T10:30:00Z",
  "updatedAt": "2025-10-29T10:35:22Z",
  "completedAt": null
}
```

### cURL Example

```bash
curl -X GET https://api.honua.io/api/intake/conversations/conv_xyz789abc123 \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### Python Example

```python
import requests
import json

response = requests.get(
    'https://api.honua.io/api/intake/conversations/conv_xyz789abc123',
    headers={'Authorization': 'Bearer YOUR_JWT_TOKEN'}
)

conversation = response.json()

# Parse messages
messages = json.loads(conversation['messagesJson'])
for msg in messages:
    role = msg['role'].capitalize()
    content = msg['content']
    print(f"{role}: {content}\n")

# Parse requirements if available
if conversation['requirementsJson']:
    requirements = json.loads(conversation['requirementsJson'])
    print(f"Requirements: {requirements}")
```

---

## POST /build

Trigger a container build from completed intake conversation.

### Request

```http
POST /api/intake/build
Content-Type: application/json
Authorization: Bearer YOUR_JWT_TOKEN
```

**Body:**

```json
{
  "conversationId": "conv_xyz789abc123",
  "customerId": "cust_abc123",
  "buildName": "production-geospatial-server",
  "tags": ["production", "aws", "esri-compatible"]
}
```

**Optional - Requirements Override:**

```json
{
  "conversationId": "conv_xyz789abc123",
  "customerId": "cust_abc123",
  "buildName": "production-geospatial-server",
  "requirementsOverride": {
    "protocols": ["ESRI-REST", "WFS-2.0", "WMS-1.3.0", "WMTS-1.0.0"],
    "databases": ["PostgreSQL-PostGIS"],
    "cloudProvider": "aws",
    "architecture": "linux-x64",
    "tier": "Enterprise"
  }
}
```

### Response

**200 OK**

```json
{
  "success": true,
  "buildId": "build_def456ghi789",
  "manifest": {
    "version": "1.0",
    "name": "production-geospatial-server",
    "architecture": "linux-arm64",
    "modules": ["ESRI-REST", "WFS-2.0", "WMS-1.3.0"],
    "databaseConnectors": ["PostgreSQL-PostGIS"],
    "cloudTargets": [{
      "provider": "aws",
      "region": "us-west-2",
      "instanceType": "t4g.large",
      "registryUrl": "123456789012.dkr.ecr.us-west-2.amazonaws.com"
    }],
    "resources": {
      "minCpu": 2,
      "minMemoryGb": 4,
      "recommendedCpu": 4,
      "recommendedMemoryGb": 8,
      "storageGb": 50
    },
    "tier": "Pro",
    "generatedAt": "2025-10-29T10:40:00Z"
  },
  "registryResult": {
    "success": true,
    "registryType": "AwsEcr",
    "customerId": "cust_abc123",
    "namespace": "honua/cust_abc123",
    "credential": {
      "registryUrl": "123456789012.dkr.ecr.us-west-2.amazonaws.com",
      "username": "AWS",
      "password": "eyJwYXlsb2FkIjoiZXlKMGIydGxiaUk2...",
      "expiresAt": "2025-10-30T10:40:00Z"
    },
    "provisionedAt": "2025-10-29T10:40:00Z"
  },
  "triggeredAt": "2025-10-29T10:40:00Z"
}
```

### cURL Example

```bash
curl -X POST https://api.honua.io/api/intake/build \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "conv_xyz789abc123",
    "customerId": "cust_abc123",
    "buildName": "production-geospatial-server",
    "tags": ["production", "aws"]
  }'
```

### Python Example

```python
import requests

response = requests.post(
    'https://api.honua.io/api/intake/build',
    headers={'Authorization': 'Bearer YOUR_JWT_TOKEN'},
    json={
        'conversationId': 'conv_xyz789abc123',
        'customerId': 'cust_abc123',
        'buildName': 'production-geospatial-server',
        'tags': ['production', 'aws']
    }
)

result = response.json()

if result['success']:
    print(f"Build triggered: {result['buildId']}")
    print(f"Image will be available at:")

    credential = result['registryResult']['credential']
    print(f"  Registry: {credential['registryUrl']}")
    print(f"  Username: {credential['username']}")
    print(f"  Password: {credential['password'][:20]}...")
    print(f"  Expires: {credential['expiresAt']}")
else:
    print(f"Build failed: {result.get('errorMessage')}")
```

### Error Responses

**400 Bad Request - Intake Not Complete:**

```json
{
  "error": "No requirements available. Complete the intake conversation first.",
  "status": 400,
  "title": "Bad Request",
  "timestamp": "2025-10-29T10:40:00Z"
}
```

**404 Not Found:**

```json
{
  "error": "Conversation conv_xyz789abc123 not found",
  "status": 404,
  "title": "Not Found",
  "timestamp": "2025-10-29T10:40:00Z"
}
```

**500 Internal Server Error - Registry Provisioning Failed:**

```json
{
  "error": "Failed to provision container registry",
  "details": "ECR repository creation failed: AccessDenied",
  "status": 500,
  "title": "Internal Server Error",
  "timestamp": "2025-10-29T10:40:00Z"
}
```

---

## GET /builds/{buildId}/status

Get real-time build status and progress.

### Request

```http
GET /api/intake/builds/build_def456ghi789/status
Authorization: Bearer YOUR_JWT_TOKEN
```

### Response

**Building:**

```json
{
  "buildId": "build_def456ghi789",
  "status": "building",
  "progress": 45,
  "currentStage": "Installing database connectors",
  "imageReference": null,
  "errorMessage": null,
  "logsUrl": "https://api.honua.io/api/builds/build_def456ghi789/logs",
  "startedAt": "2025-10-29T10:40:00Z",
  "completedAt": null
}
```

**Completed:**

```json
{
  "buildId": "build_def456ghi789",
  "status": "completed",
  "progress": 100,
  "currentStage": "Build completed successfully",
  "imageReference": "123456789012.dkr.ecr.us-west-2.amazonaws.com/honua/cust_abc123/production-geospatial-server:latest-arm64",
  "errorMessage": null,
  "logsUrl": "https://api.honua.io/api/builds/build_def456ghi789/logs",
  "startedAt": "2025-10-29T10:40:00Z",
  "completedAt": "2025-10-29T10:55:30Z"
}
```

**Failed:**

```json
{
  "buildId": "build_def456ghi789",
  "status": "failed",
  "progress": 67,
  "currentStage": "Build failed during database connector installation",
  "imageReference": null,
  "errorMessage": "Failed to download PostgreSQL connector: Connection timeout",
  "logsUrl": "https://api.honua.io/api/builds/build_def456ghi789/logs",
  "startedAt": "2025-10-29T10:40:00Z",
  "completedAt": "2025-10-29T10:50:15Z"
}
```

### cURL Example

```bash
curl -X GET https://api.honua.io/api/intake/builds/build_def456ghi789/status \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### Python Example (Polling)

```python
import requests
import time

def poll_build_status(build_id, interval=10):
    """Poll build status until complete or failed."""
    while True:
        response = requests.get(
            f'https://api.honua.io/api/intake/builds/{build_id}/status',
            headers={'Authorization': 'Bearer YOUR_JWT_TOKEN'}
        )

        status = response.json()

        print(f"[{status['status'].upper()}] {status['progress']}% - {status['currentStage']}")

        if status['status'] in ['completed', 'failed']:
            return status

        time.sleep(interval)

# Usage
build_id = "build_def456ghi789"
final_status = poll_build_status(build_id)

if final_status['status'] == 'completed':
    print(f"\nBuild completed successfully!")
    print(f"Image: {final_status['imageReference']}")
else:
    print(f"\nBuild failed: {final_status['errorMessage']}")
```

### JavaScript Example (Async Polling)

```javascript
async function pollBuildStatus(buildId, interval = 10000) {
  while (true) {
    const response = await fetch(
      `https://api.honua.io/api/intake/builds/${buildId}/status`,
      {
        headers: {
          'Authorization': `Bearer ${process.env.HONUA_JWT_TOKEN}`
        }
      }
    );

    const status = await response.json();

    console.log(`[${status.status.toUpperCase()}] ${status.progress}% - ${status.currentStage}`);

    if (status.status === 'completed' || status.status === 'failed') {
      return status;
    }

    await new Promise(resolve => setTimeout(resolve, interval));
  }
}

// Usage
const buildId = 'build_def456ghi789';
const finalStatus = await pollBuildStatus(buildId);

if (finalStatus.status === 'completed') {
  console.log(`\nBuild completed!`);
  console.log(`Image: ${finalStatus.imageReference}`);
} else {
  console.log(`\nBuild failed: ${finalStatus.errorMessage}`);
}
```

---

## Common Workflows

### Complete Build Flow

```python
import requests
import time
import json

# Configuration
API_BASE = 'https://api.honua.io'
headers = {'Authorization': 'Bearer YOUR_JWT_TOKEN'}

# 1. Start conversation
response = requests.post(
    f'{API_BASE}/api/intake/start',
    headers=headers,
    json={'customerId': 'cust_abc123'}
)
conversation = response.json()
conv_id = conversation['conversationId']

print(f"AI: {conversation['initialMessage']}\n")

# 2. Interactive conversation
while True:
    user_message = input("You: ")

    response = requests.post(
        f'{API_BASE}/api/intake/message',
        headers=headers,
        json={'conversationId': conv_id, 'message': user_message}
    )

    result = response.json()
    print(f"\nAI: {result['message']}\n")

    if result['intakeComplete']:
        print(f"\n✓ Requirements extracted!")
        print(f"Estimated cost: ${result['estimatedMonthlyCost']}/month")
        print(json.dumps(result['requirements'], indent=2))
        break

# 3. Trigger build
response = requests.post(
    f'{API_BASE}/api/intake/build',
    headers=headers,
    json={
        'conversationId': conv_id,
        'customerId': 'cust_abc123',
        'buildName': 'my-geospatial-server'
    }
)

build = response.json()
build_id = build['buildId']
credential = build['registryResult']['credential']

print(f"\n✓ Build triggered: {build_id}")

# 4. Poll build status
while True:
    response = requests.get(
        f'{API_BASE}/api/intake/builds/{build_id}/status',
        headers=headers
    )

    status = response.json()
    print(f"[{status['status']}] {status['progress']}% - {status['currentStage']}")

    if status['status'] in ['completed', 'failed']:
        break

    time.sleep(10)

# 5. Deploy
if status['status'] == 'completed':
    print(f"\n✓ Build completed!")
    print(f"\nDocker commands:")
    print(f"  docker login {credential['registryUrl']} -u {credential['username']} -p {credential['password']}")
    print(f"  docker pull {status['imageReference']}")
    print(f"  docker run -p 8080:8080 {status['imageReference']}")
```

## Next Steps

- [Build API Reference](build-api.md) - Build management endpoints
- [License API Reference](license-api.md) - License operations
- [Registry API Reference](registry-api.md) - Registry management
- [Webhooks](webhooks.md) - Real-time build notifications
