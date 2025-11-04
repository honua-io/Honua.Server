# Rate Limits

The Honua Build Orchestrator API implements rate limiting to ensure fair usage and system stability.

## Rate Limit Tiers

### Free Tier
- **100 requests/hour**
- **10 concurrent builds**
- **Best-effort SLA**

### Pro Tier ($149/month)
- **1,000 requests/hour**
- **50 concurrent builds**
- **99.5% SLA**

### Enterprise Tier ($599/month)
- **10,000 requests/hour**
- **Unlimited concurrent builds**
- **99.9% SLA**

### Enterprise ASP ($1,499/month)
- **Unlimited requests**
- **Unlimited concurrent builds**
- **99.99% SLA (configurable)**

## Rate Limit Headers

Every API response includes rate limit information:

```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 87
X-RateLimit-Reset: 1735473600
```

- **X-RateLimit-Limit** - Maximum requests allowed in window
- **X-RateLimit-Remaining** - Requests remaining in current window
- **X-RateLimit-Reset** - Unix timestamp when limit resets

## Rate Limit Exceeded Response

```http
HTTP/1.1 429 Too Many Requests
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1735473600
Retry-After: 60
```

```json
{
  "error": "Rate limit exceeded. Please try again later.",
  "status": 429,
  "title": "Too Many Requests",
  "retryAfter": 60,
  "limit": 100,
  "remaining": 0,
  "resetAt": "2025-10-29T11:00:00Z"
}
```

## Rate Limit Strategies

### Respect Headers

```python
import requests
import time

def api_call_with_rate_limit(url, headers, payload):
    response = requests.post(url, headers=headers, json=payload)

    # Check remaining requests
    remaining = int(response.headers.get('X-RateLimit-Remaining', 0))

    if remaining < 5:
        # Approaching limit - slow down
        reset_time = int(response.headers.get('X-RateLimit-Reset', 0))
        wait_time = max(0, reset_time - time.time())
        print(f"Approaching rate limit. Waiting {wait_time}s...")
        time.sleep(wait_time)

    return response.json()
```

### Handle 429 Errors

```python
import requests
import time

def api_call_with_backoff(url, headers, payload, max_retries=3):
    for attempt in range(max_retries):
        response = requests.post(url, headers=headers, json=payload)

        if response.status_code != 429:
            return response.json()

        # Rate limited - use Retry-After header
        retry_after = int(response.headers.get('Retry-After', 60))
        print(f"Rate limited. Retrying after {retry_after}s...")
        time.sleep(retry_after)

    raise Exception("Max retries exceeded due to rate limiting")
```

### Request Queuing

```python
import time
from collections import deque
from threading import Lock

class RateLimitedClient:
    def __init__(self, requests_per_second=10):
        self.rate = requests_per_second
        self.interval = 1.0 / requests_per_second
        self.last_request = 0
        self.lock = Lock()

    def call(self, url, **kwargs):
        with self.lock:
            now = time.time()
            time_since_last = now - self.last_request

            if time_since_last < self.interval:
                wait_time = self.interval - time_since_last
                time.sleep(wait_time)

            self.last_request = time.time()

        return requests.post(url, **kwargs)

# Usage
client = RateLimitedClient(requests_per_second=10)
response = client.call('https://api.honua.io/api/intake/message', json=payload)
```

## Best Practices

### 1. Cache Responses

```python
from functools import lru_cache
import requests

@lru_cache(maxsize=100)
def get_conversation(conversation_id):
    """Cache conversation data to reduce API calls."""
    response = requests.get(
        f'https://api.honua.io/api/intake/conversations/{conversation_id}',
        headers={'Authorization': 'Bearer TOKEN'}
    )
    return response.json()
```

### 2. Batch Operations

Instead of many small requests, batch when possible:

```python
# Bad - Many separate calls
for message in messages:
    send_message(conversation_id, message)

# Better - Combine into conversation
combined_message = "\n".join(messages)
send_message(conversation_id, combined_message)
```

### 3. Use Webhooks

For build status, use webhooks instead of polling:

```python
# Bad - Frequent polling
while True:
    status = get_build_status(build_id)
    if status['status'] in ['completed', 'failed']:
        break
    time.sleep(5)  # Wastes API calls

# Better - Use webhooks
# Set up webhook to receive notification when build completes
```

### 4. Exponential Backoff

```python
import time

def exponential_backoff_retry(func, max_retries=5):
    for attempt in range(max_retries):
        try:
            return func()
        except RateLimitError:
            if attempt == max_retries - 1:
                raise

            wait_time = 2 ** attempt  # 1s, 2s, 4s, 8s, 16s
            time.sleep(wait_time)
```

## Monitoring Usage

Track your API usage through the dashboard:

```bash
curl -X GET https://api.honua.io/api/admin/usage \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

Response:

```json
{
  "period": "hour",
  "limit": 1000,
  "used": 432,
  "remaining": 568,
  "resetAt": "2025-10-29T11:00:00Z",
  "percentage": 43.2
}
```

## Upgrading Limits

Need higher limits? Upgrade your plan:

1. Visit https://dashboard.honua.io/billing
2. Select a higher tier
3. Limits apply immediately after upgrade

Or contact enterprise@honua.io for custom limits.

## Special Limits

Some endpoints have additional limits:

### Build Trigger Limit
- **Free**: 10 builds/day
- **Pro**: 100 builds/day
- **Enterprise**: 500 builds/day
- **Enterprise ASP**: Unlimited

### Conversation Limit
- **Free**: 20 active conversations
- **Pro**: 100 active conversations
- **Enterprise**: Unlimited
- **Enterprise ASP**: Unlimited

### Webhook Delivery
- **All tiers**: 3 retry attempts per event
- **Timeout**: 10 seconds per attempt
- **Backoff**: Exponential (1s, 2s, 4s)

## Burst Allowance

All tiers include burst allowance:

- **Free**: 20 requests burst
- **Pro**: 100 requests burst
- **Enterprise**: 500 requests burst
- **Enterprise ASP**: 1000 requests burst

Burst allowance allows temporary spikes above your rate limit.

## Support

Questions about rate limits? Contact support@honua.io
