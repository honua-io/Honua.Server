# Authentication

The Honua Build Orchestrator API supports two authentication methods: JWT Bearer tokens and API Keys.

## Authentication Methods

### JWT Bearer Token (Recommended)

JWT tokens provide the most secure and flexible authentication method.

**Advantages:**
- Short-lived tokens (configurable expiration)
- Can include custom claims (customer ID, roles, permissions)
- Can be revoked individually
- Supports refresh tokens
- Better audit trail

**Usage:**

```bash
curl -X GET https://api.honua.io/api/intake/conversations/conv_xyz789 \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

**Obtaining a Token:**

```bash
curl -X POST https://api.honua.io/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "username": "your-email@example.com",
    "password": "your-password"
  }'
```

**Response:**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600,
  "tokenType": "Bearer"
}
```

### API Key

API Keys are simpler but less flexible. Use for server-to-server communication.

**Advantages:**
- Simple to use
- No expiration (until revoked)
- Easy to rotate
- Good for scripts and automation

**Disadvantages:**
- Less secure (if leaked, valid until revoked)
- No fine-grained permissions
- Cannot include custom claims

**Usage:**

```bash
curl -X GET https://api.honua.io/api/intake/conversations/conv_xyz789 \
  -H "X-API-Key: honua_sk_abc123def456ghi789jkl012"
```

**Generating an API Key:**

1. Log in to https://dashboard.honua.io
2. Navigate to **Settings > API Keys**
3. Click **Generate New Key**
4. Copy your key (shown only once)
5. Store securely (use environment variables or secret manager)

## Token Management

### Refreshing JWT Tokens

```bash
curl -X POST https://api.honua.io/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }'
```

**Response:**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

### Revoking Tokens

```bash
curl -X POST https://api.honua.io/auth/revoke \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }'
```

### Rotating API Keys

```bash
curl -X POST https://api.honua.io/auth/rotate-key \
  -H "X-API-Key: honua_sk_abc123def456ghi789jkl012" \
  -d '{
    "keyId": "key_abc123"
  }'
```

**Response:**

```json
{
  "oldKey": "honua_sk_abc123def456ghi789jkl012",
  "newKey": "honua_sk_mno345pqr678stu901vwx234",
  "createdAt": "2025-10-29T10:40:00Z",
  "expiresAt": null
}
```

## Permissions and Scopes

### JWT Token Scopes

Tokens can include scopes to limit permissions:

```json
{
  "sub": "user_abc123",
  "customerId": "cust_def456",
  "scopes": [
    "intake:read",
    "intake:write",
    "builds:read",
    "builds:write",
    "licenses:read",
    "registry:read"
  ],
  "role": "customer",
  "exp": 1735470000
}
```

**Available Scopes:**

- `intake:read` - Read conversations
- `intake:write` - Start conversations, send messages, trigger builds
- `builds:read` - View build status and history
- `builds:write` - Cancel builds, modify build queue
- `licenses:read` - View licenses
- `licenses:write` - Generate, revoke licenses
- `registry:read` - View registry credentials
- `registry:write` - Provision registries, manage credentials
- `admin:read` - View admin data (queue, metrics, users)
- `admin:write` - Manage system configuration

### Role-Based Access Control

**Roles:**

- **Customer** - Standard customer access
  - Full access to own resources
  - Cannot access other customers' data
  - Cannot perform admin operations

- **Customer Admin** - Customer administrator
  - Can manage users within customer account
  - Can view usage and billing
  - Can manage API keys for customer

- **Support** - Honua support staff
  - Read-only access to customer data (with consent)
  - Can view logs and metrics
  - Cannot modify customer resources

- **Admin** - Honua system administrator
  - Full system access
  - Can manage all customers
  - Can configure system settings

## Security Best Practices

### 1. Never Commit Credentials

```bash
# Bad - DON'T DO THIS
export API_KEY="honua_sk_abc123def456ghi789jkl012"
git add .env
git commit -m "Add API key"

# Good - Use environment variables
export HONUA_API_KEY="honua_sk_abc123def456ghi789jkl012"

# Better - Use secret manager
aws secretsmanager get-secret-value --secret-id honua/api-key
```

### 2. Rotate Keys Regularly

```bash
# Rotate every 90 days
curl -X POST https://api.honua.io/auth/rotate-key \
  -H "X-API-Key: $HONUA_API_KEY"
```

### 3. Use Minimal Scopes

```bash
# Request only needed scopes
curl -X POST https://api.honua.io/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "username": "your-email@example.com",
    "password": "your-password",
    "scopes": ["intake:read", "builds:read"]
  }'
```

### 4. Implement Token Refresh

```javascript
// JavaScript example
async function getAccessToken() {
  if (isTokenExpired(accessToken)) {
    const response = await fetch('https://api.honua.io/auth/refresh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken })
    });
    const data = await response.json();
    accessToken = data.accessToken;
  }
  return accessToken;
}
```

### 5. Secure Token Storage

**Web Applications:**
```javascript
// Store in httpOnly cookie (NOT localStorage)
// Server sets cookie with httpOnly flag
Set-Cookie: honua_token=eyJhbGciOiJ...; HttpOnly; Secure; SameSite=Strict
```

**Mobile Applications:**
```swift
// iOS - Use Keychain
let keychain = KeychainSwift()
keychain.set(token, forKey: "honua_access_token")
```

**Server Applications:**
```bash
# Use environment variables or secret manager
export HONUA_API_KEY=$(aws secretsmanager get-secret-value \
  --secret-id honua/api-key \
  --query SecretString \
  --output text)
```

## Authentication Errors

### 401 Unauthorized

**Missing Token:**
```json
{
  "error": "No authentication credentials provided",
  "status": 401,
  "title": "Unauthorized"
}
```

**Invalid Token:**
```json
{
  "error": "Invalid or expired authentication token",
  "status": 401,
  "title": "Unauthorized"
}
```

**Expired Token:**
```json
{
  "error": "Authentication token has expired",
  "status": 401,
  "title": "Unauthorized",
  "expiresAt": "2025-10-29T09:40:00Z"
}
```

### 403 Forbidden

**Insufficient Permissions:**
```json
{
  "error": "Insufficient permissions for this operation",
  "status": 403,
  "title": "Forbidden",
  "requiredScope": "admin:write"
}
```

**Account Suspended:**
```json
{
  "error": "Account has been suspended. Please contact support.",
  "status": 403,
  "title": "Forbidden"
}
```

## OAuth 2.0 (Coming Soon)

OAuth 2.0 support for third-party integrations is planned for Q2 2026.

**Planned Flows:**
- Authorization Code Flow (for web apps)
- PKCE Flow (for mobile/SPA)
- Client Credentials Flow (for server-to-server)

**Planned Providers:**
- Google
- Microsoft Azure AD
- GitHub
- Okta
- Auth0

## SSO Integration (Enterprise)

Enterprise customers can integrate with their existing SSO provider.

**Supported Protocols:**
- SAML 2.0
- OpenID Connect (OIDC)

**Supported Providers:**
- Okta
- Azure AD
- Google Workspace
- OneLogin
- Auth0
- Custom SAML/OIDC providers

Contact enterprise@honua.io for SSO setup.

## Code Examples

### Python

```python
import requests
import os

# Using JWT token
headers = {
    'Authorization': f'Bearer {os.environ["HONUA_JWT_TOKEN"]}'
}

response = requests.get(
    'https://api.honua.io/api/intake/conversations/conv_xyz789',
    headers=headers
)

# Using API key
headers = {
    'X-API-Key': os.environ['HONUA_API_KEY']
}

response = requests.get(
    'https://api.honua.io/api/intake/conversations/conv_xyz789',
    headers=headers
)
```

### C# / .NET

```csharp
using System.Net.Http.Headers;

var client = new HttpClient();

// Using JWT token
var token = Environment.GetEnvironmentVariable("HONUA_JWT_TOKEN");
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);

// Using API key
var apiKey = Environment.GetEnvironmentVariable("HONUA_API_KEY");
client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

var response = await client.GetAsync(
    "https://api.honua.io/api/intake/conversations/conv_xyz789"
);
```

### JavaScript / Node.js

```javascript
// Using JWT token
const response = await fetch(
  'https://api.honua.io/api/intake/conversations/conv_xyz789',
  {
    headers: {
      'Authorization': `Bearer ${process.env.HONUA_JWT_TOKEN}`
    }
  }
);

// Using API key
const response = await fetch(
  'https://api.honua.io/api/intake/conversations/conv_xyz789',
  {
    headers: {
      'X-API-Key': process.env.HONUA_API_KEY
    }
  }
);
```

### Go

```go
import (
    "net/http"
    "os"
)

// Using JWT token
token := os.Getenv("HONUA_JWT_TOKEN")
req, _ := http.NewRequest("GET",
    "https://api.honua.io/api/intake/conversations/conv_xyz789", nil)
req.Header.Set("Authorization", "Bearer "+token)

// Using API key
apiKey := os.Getenv("HONUA_API_KEY")
req.Header.Set("X-API-Key", apiKey)

client := &http.Client{}
resp, err := client.Do(req)
```

## Next Steps

- [Getting Started Guide](getting-started.md)
- [Intake API Reference](intake-api.md)
- [Error Handling](errors.md)
- [Rate Limits](rate-limits.md)
