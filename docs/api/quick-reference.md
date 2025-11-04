# Honua Build Orchestrator API - Quick Reference

## 🔗 Quick Links

| Resource | URL |
|----------|-----|
| Swagger UI | `/api-docs` |
| ReDoc | `/docs` |
| OpenAPI Spec | `/api-docs/v1/openapi.json` |
| Production API | `https://api.honua.io` |
| Staging API | `https://api-staging.honua.io` |

## 🔑 Authentication

### JWT Token
```bash
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### API Key
```bash
X-API-Key: honua_sk_abc123def456ghi789jkl012
```

## 📡 Intake API Endpoints

### Start Conversation
```bash
POST /api/intake/start
{
  "customerId": "cust_abc123"
}
```

### Send Message
```bash
POST /api/intake/message
{
  "conversationId": "conv_xyz789",
  "message": "I need ESRI compatibility on AWS"
}
```

### Get Conversation
```bash
GET /api/intake/conversations/{conversationId}
```

### Trigger Build
```bash
POST /api/intake/build
{
  "conversationId": "conv_xyz789",
  "customerId": "cust_abc123",
  "buildName": "my-server"
}
```

### Get Build Status
```bash
GET /api/intake/builds/{buildId}/status
```

## 🐍 Python Examples

```python
import requests

# Start conversation
response = requests.post(
    'https://api.honua.io/api/intake/start',
    headers={'Authorization': 'Bearer TOKEN'},
    json={'customerId': 'cust_abc123'}
)
conv_id = response.json()['conversationId']

# Send message
response = requests.post(
    'https://api.honua.io/api/intake/message',
    headers={'Authorization': 'Bearer TOKEN'},
    json={
        'conversationId': conv_id,
        'message': 'I need ESRI compatibility'
    }
)
```

## 💻 JavaScript Examples

```javascript
// Start conversation
const response = await fetch('https://api.honua.io/api/intake/start', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({ customerId: 'cust_abc123' })
});

const { conversationId } = await response.json();

// Send message
await fetch('https://api.honua.io/api/intake/message', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    conversationId,
    message: 'I need ESRI compatibility'
  })
});
```

## 🔧 C# Examples

```csharp
using System.Net.Http.Json;

var client = new HttpClient();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);

// Start conversation
var startResponse = await client.PostAsJsonAsync(
    "https://api.honua.io/api/intake/start",
    new { customerId = "cust_abc123" }
);
var conversation = await startResponse.Content.ReadFromJsonAsync<ConversationResponse>();

// Send message
await client.PostAsJsonAsync(
    "https://api.honua.io/api/intake/message",
    new {
        conversationId = conversation.ConversationId,
        message = "I need ESRI compatibility"
    }
);
```

## 🌐 curl Examples

```bash
# Start conversation
curl -X POST https://api.honua.io/api/intake/start \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"customerId":"cust_abc123"}'

# Send message
curl -X POST https://api.honua.io/api/intake/message \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId":"conv_xyz789",
    "message":"I need ESRI compatibility"
  }'

# Trigger build
curl -X POST https://api.honua.io/api/intake/build \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId":"conv_xyz789",
    "customerId":"cust_abc123",
    "buildName":"my-server"
  }'

# Check status
curl -X GET https://api.honua.io/api/intake/builds/build_abc123/status \
  -H "Authorization: Bearer $TOKEN"
```

## ⚠️ Common Errors

| Code | Error | Solution |
|------|-------|----------|
| 400 | Bad Request | Check request body format |
| 401 | Unauthorized | Verify token is valid |
| 404 | Not Found | Check conversation/build ID |
| 429 | Rate Limited | Wait for reset time |
| 500 | Server Error | Contact support with request ID |

## 📊 Rate Limits

| Tier | Requests/Hour | Concurrent Builds |
|------|---------------|-------------------|
| Free | 100 | 10 |
| Pro | 1,000 | 50 |
| Enterprise | 10,000 | Unlimited |
| Enterprise ASP | Unlimited | Unlimited |

## 🔍 Response Headers

```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 87
X-RateLimit-Reset: 1735473600
X-Request-Id: req_abc123def456
X-API-Version: v1
```

## 📦 Build Status Values

| Status | Meaning |
|--------|---------|
| `pending` | Queued, waiting to start |
| `building` | Active build in progress |
| `completed` | Build finished successfully |
| `failed` | Build encountered an error |
| `cached` | Delivered from cache (instant) |

## 🎯 Complete Workflow

```python
import requests
import time

API = 'https://api.honua.io'
headers = {'Authorization': 'Bearer TOKEN'}

# 1. Start conversation
r = requests.post(f'{API}/api/intake/start', headers=headers, json={'customerId': 'cust_abc123'})
conv_id = r.json()['conversationId']

# 2. Interactive conversation
while True:
    msg = input('You: ')
    r = requests.post(f'{API}/api/intake/message', headers=headers,
                      json={'conversationId': conv_id, 'message': msg})
    result = r.json()
    print(f"AI: {result['message']}\n")

    if result['intakeComplete']:
        print(f"Cost: ${result['estimatedMonthlyCost']}/month")
        break

# 3. Trigger build
r = requests.post(f'{API}/api/intake/build', headers=headers,
                  json={'conversationId': conv_id, 'customerId': 'cust_abc123',
                        'buildName': 'my-server'})
build_id = r.json()['buildId']

# 4. Poll status
while True:
    r = requests.get(f'{API}/api/intake/builds/{build_id}/status', headers=headers)
    status = r.json()
    print(f"[{status['status']}] {status['progress']}%")

    if status['status'] in ['completed', 'failed']:
        break
    time.sleep(10)

# 5. Deploy
if status['status'] == 'completed':
    print(f"Image: {status['imageReference']}")
```

## 📚 Further Reading

- [Full Documentation](/docs/api/README.md)
- [Getting Started](/docs/api/getting-started.md)
- [Authentication Guide](/docs/api/authentication.md)
- [Error Handling](/docs/api/errors.md)
- [Rate Limits](/docs/api/rate-limits.md)

## 🆘 Support

- **Email**: support@honua.io
- **Docs**: https://docs.honua.io
- **Status**: https://status.honua.io
- **Discord**: https://discord.gg/honua
