# Webhook Security - Signature Validation

## Overview

The Alert Receiver supports webhook signature validation using HMAC-SHA256 to ensure that incoming webhook requests are authentic and haven't been tampered with. This prevents spoofing attacks where malicious actors send fake alerts.

## How It Works

1. **Signature Generation**: The webhook sender computes an HMAC-SHA256 hash of the request body using a shared secret
2. **Signature Transmission**: The signature is sent in an HTTP header (default: `X-Hub-Signature-256`)
3. **Signature Validation**: The Alert Receiver computes the expected signature and compares it using constant-time comparison
4. **Request Processing**: Only requests with valid signatures are processed

## Security Features

- **HMAC-SHA256**: Industry-standard cryptographic hashing
- **Constant-Time Comparison**: Prevents timing attacks using `CryptographicOperations.FixedTimeEquals`
- **HTTPS Enforcement**: Configurable requirement for HTTPS connections
- **Payload Size Limits**: Prevents DoS attacks via large payloads
- **Replay Attack Protection**: Optional timestamp validation
- **Secret Rotation**: Support for multiple secrets during rotation

## Configuration

### Environment Variables (Recommended)

```bash
# Required: Shared secret for webhook validation
# SECURITY: Must be at least 64 characters (512 bits) for HMAC-SHA256
# Generate: openssl rand -base64 64
Webhook__Security__SharedSecret="your-secure-secret-key-minimum-64-chars"

# Optional: Require signature validation (default: true)
Webhook__Security__RequireSignature=true

# Optional: Signature header name (default: X-Hub-Signature-256)
Webhook__Security__SignatureHeaderName="X-Hub-Signature-256"

# Optional: Maximum payload size in bytes (default: 1MB)
Webhook__Security__MaxPayloadSize=1048576

# Optional: Allow HTTP (non-HTTPS) connections - ONLY for development
Webhook__Security__AllowInsecureHttp=false

# Optional: Maximum webhook age in seconds for replay protection (default: 300)
Webhook__Security__MaxWebhookAge=300

# Optional: Timestamp header name (default: X-Webhook-Timestamp)
Webhook__Security__TimestampHeaderName="X-Webhook-Timestamp"
```

### appsettings.json (Alternative)

```json
{
  "Webhook": {
    "Security": {
      "RequireSignature": true,
      "SignatureHeaderName": "X-Hub-Signature-256",
      "SharedSecret": "your-secret-here",
      "MaxPayloadSize": 1048576,
      "AllowInsecureHttp": false,
      "MaxWebhookAge": 300,
      "TimestampHeaderName": "X-Webhook-Timestamp"
    }
  }
}
```

**WARNING**: Never commit secrets to source control. Always use environment variables or secure secret management for production.

## Generating a Secure Secret

```bash
# Generate a 64-byte (512-bit) random secret (RECOMMENDED)
# NIST SP 800-107 recommends key length >= hash output (256 bits minimum)
openssl rand -base64 64

# Example output:
# wZ3xKj7nP9qR5sT8uV2wX4yZ6aB1cD3eF5gH7iJ9kL0mN2oP4qR6sT8uV0wX2yZ4aB6cD8eF0gH2iJ4kL6mN8oP==

# Alternative: Hex encoding (128 hex characters = 512 bits)
openssl rand -hex 64

# Example output:
# c74d97b01eae257e44aa9d5bade97baf4a98cd6f09f5db6b3b8a8e4f2c1e4e9a5b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2
```

**Security Requirements**:
- Minimum length: 64 characters (512 bits)
- Use cryptographically secure random generator (like OpenSSL)
- Avoid patterns, repeated characters, or dictionary words
- Store securely (environment variables or secrets manager)

## Webhook Endpoint

### Authenticated Endpoint (JWT)
```
POST /api/alerts
Authorization: Bearer <jwt-token>
```

### Webhook Endpoint (Signature-Validated)
```
POST /api/alerts/webhook
X-Hub-Signature-256: sha256=<hex_signature>
X-Webhook-Timestamp: <unix_timestamp>
Content-Type: application/json

{
  "name": "High CPU Usage",
  "severity": "critical",
  "source": "monitoring-system",
  "message": "CPU usage above 90% for 5 minutes"
}
```

## Client Implementation Examples

### Python

```python
import hmac
import hashlib
import time
import requests

def send_webhook_alert(url, secret, alert_data):
    # Serialize alert payload
    payload = json.dumps(alert_data).encode('utf-8')

    # Generate HMAC-SHA256 signature
    signature = hmac.new(
        secret.encode('utf-8'),
        payload,
        hashlib.sha256
    ).hexdigest()

    # Send request with signature
    headers = {
        'Content-Type': 'application/json',
        'X-Hub-Signature-256': f'sha256={signature}',
        'X-Webhook-Timestamp': str(int(time.time()))
    }

    response = requests.post(url, data=payload, headers=headers)
    return response

# Example usage
alert = {
    "name": "High CPU Usage",
    "severity": "critical",
    "source": "monitoring-system",
    "message": "CPU usage above 90%"
}

send_webhook_alert(
    'https://alerts.example.com/api/alerts/webhook',
    'your-shared-secret',
    alert
)
```

### Node.js / TypeScript

```typescript
import crypto from 'crypto';
import axios from 'axios';

interface Alert {
  name: string;
  severity: string;
  source: string;
  message: string;
}

async function sendWebhookAlert(
  url: string,
  secret: string,
  alert: Alert
): Promise<void> {
  // Serialize alert payload
  const payload = JSON.stringify(alert);

  // Generate HMAC-SHA256 signature
  const hmac = crypto.createHmac('sha256', secret);
  hmac.update(payload);
  const signature = `sha256=${hmac.digest('hex')}`;

  // Send request with signature
  await axios.post(url, payload, {
    headers: {
      'Content-Type': 'application/json',
      'X-Hub-Signature-256': signature,
      'X-Webhook-Timestamp': Math.floor(Date.now() / 1000).toString()
    }
  });
}

// Example usage
const alert: Alert = {
  name: 'High CPU Usage',
  severity: 'critical',
  source: 'monitoring-system',
  message: 'CPU usage above 90%'
};

await sendWebhookAlert(
  'https://alerts.example.com/api/alerts/webhook',
  'your-shared-secret',
  alert
);
```

### C# / .NET

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class WebhookClient
{
    private readonly HttpClient _httpClient;
    private readonly string _secret;

    public WebhookClient(string secret)
    {
        _httpClient = new HttpClient();
        _secret = secret;
    }

    public async Task<HttpResponseMessage> SendAlertAsync(string url, object alert)
    {
        // Serialize alert payload
        var payload = JsonSerializer.Serialize(alert);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        // Generate HMAC-SHA256 signature
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
        var hash = hmac.ComputeHash(payloadBytes);
        var signature = $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";

        // Create request
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Hub-Signature-256", signature);
        request.Headers.Add("X-Webhook-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

        // Send request
        return await _httpClient.SendAsync(request);
    }
}

// Example usage
var client = new WebhookClient("your-shared-secret");
var alert = new
{
    name = "High CPU Usage",
    severity = "critical",
    source = "monitoring-system",
    message = "CPU usage above 90%"
};

var response = await client.SendAlertAsync(
    "https://alerts.example.com/api/alerts/webhook",
    alert
);
```

### Bash / cURL

```bash
#!/bin/bash

SECRET="your-shared-secret"
URL="https://alerts.example.com/api/alerts/webhook"

# Alert payload
PAYLOAD='{
  "name": "High CPU Usage",
  "severity": "critical",
  "source": "monitoring-system",
  "message": "CPU usage above 90%"
}'

# Generate signature
SIGNATURE=$(echo -n "$PAYLOAD" | openssl dgst -sha256 -hmac "$SECRET" | sed 's/^.* //')

# Current timestamp
TIMESTAMP=$(date +%s)

# Send webhook
curl -X POST "$URL" \
  -H "Content-Type: application/json" \
  -H "X-Hub-Signature-256: sha256=$SIGNATURE" \
  -H "X-Webhook-Timestamp: $TIMESTAMP" \
  -d "$PAYLOAD"
```

## Secret Rotation

To rotate secrets without downtime:

1. **Add new secret to additional secrets**:
   ```bash
   Webhook__Security__SharedSecret="old-secret"
   Webhook__Security__AdditionalSecrets__0="new-secret"
   ```

2. **Update webhook senders** to use the new secret

3. **Remove old secret** after all senders are updated:
   ```bash
   Webhook__Security__SharedSecret="new-secret"
   # Remove AdditionalSecrets__0
   ```

## Testing Signature Validation

### Valid Request (should succeed)

```bash
SECRET="test-secret"
PAYLOAD='{"name":"test","severity":"info","source":"test"}'
SIGNATURE=$(echo -n "$PAYLOAD" | openssl dgst -sha256 -hmac "$SECRET" | sed 's/^.* //')

curl -X POST http://localhost:5000/api/alerts/webhook \
  -H "Content-Type: application/json" \
  -H "X-Hub-Signature-256: sha256=$SIGNATURE" \
  -d "$PAYLOAD"
```

### Invalid Request (should fail with 401)

```bash
curl -X POST http://localhost:5000/api/alerts/webhook \
  -H "Content-Type: application/json" \
  -H "X-Hub-Signature-256: sha256=invalid" \
  -d '{"name":"test","severity":"info","source":"test"}'
```

## Troubleshooting

### Error: "Invalid webhook signature"

**Causes**:
- Incorrect shared secret
- Payload was modified after signing
- Signature format is incorrect
- Clock skew between sender and receiver (if timestamp validation enabled)

**Solutions**:
1. Verify the shared secret matches on both sides
2. Ensure the signature is computed on the exact payload sent
3. Check signature format: `sha256=<hex_signature>`
4. Verify no proxy/middleware is modifying the request body
5. Check server logs for detailed error messages

### Error: "Missing timestamp header"

**Cause**: Timestamp validation is enabled but `X-Webhook-Timestamp` header is missing

**Solution**: Include the timestamp header with Unix timestamp (seconds since epoch)

### Error: "Webhook timestamp too old"

**Cause**: Request timestamp exceeds `MaxWebhookAge` (default: 5 minutes)

**Solutions**:
1. Ensure sender's clock is synchronized (use NTP)
2. Increase `MaxWebhookAge` if needed
3. Reduce network latency between sender and receiver

### Error: "HTTPS is required"

**Cause**: HTTP request sent but `AllowInsecureHttp` is false

**Solution**: Use HTTPS or set `AllowInsecureHttp=true` (development only)

## Security Best Practices

1. **Always use HTTPS in production** to prevent man-in-the-middle attacks
2. **Keep secrets secure**: Use environment variables or secret management services (Azure Key Vault, AWS Secrets Manager)
3. **Rotate secrets regularly**: At least every 90 days, use AdditionalSecrets for zero-downtime rotation
4. **Monitor failed validations**: Set up alerts for repeated validation failures (potential attack indicator)
5. **Use strong secrets**: Minimum 64 characters (512 bits), cryptographically random with high entropy
6. **Enable timestamp validation**: Prevents replay attacks (default: 5-minute window)
7. **Limit payload sizes**: Prevents DoS attacks (default: 1MB limit)
8. **Rate limit webhook endpoints**: Add additional DoS protection using ASP.NET Core rate limiting
9. **Validate entropy**: Avoid weak patterns, repeated characters, or dictionary words in secrets

## Migration Guide

### Existing Deployments

To enable signature validation on existing deployments without breaking existing clients:

1. **Set `RequireSignature=false` initially**:
   ```bash
   Webhook__Security__RequireSignature=false
   Webhook__Security__SharedSecret="your-new-secret"
   ```

2. **Update all webhook senders** to include signatures

3. **Enable signature validation**:
   ```bash
   Webhook__Security__RequireSignature=true
   ```

4. **Monitor logs** for failed validations and update any remaining clients

## Performance Considerations

- Signature validation adds minimal overhead (~1-2ms for typical payloads)
- HMAC-SHA256 is highly optimized in .NET
- Constant-time comparison prevents timing attacks without performance penalty
- Request body buffering is enabled for validation (allows multiple reads)

## References

- [HMAC RFC 2104](https://tools.ietf.org/html/rfc2104)
- [GitHub Webhook Security](https://docs.github.com/en/developers/webhooks-and-events/webhooks/securing-your-webhooks)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
