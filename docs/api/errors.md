# Error Handling

The Honua Build Orchestrator API uses standard HTTP status codes and returns detailed error information in a consistent format.

## Error Response Format

All error responses follow this structure:

```json
{
  "error": "Human-readable error message",
  "status": 400,
  "title": "Error Type",
  "timestamp": "2025-10-29T10:40:00Z",
  "details": {}  // Optional additional context
}
```

## HTTP Status Codes

### 2xx Success

- **200 OK** - Request succeeded
- **201 Created** - Resource created successfully
- **204 No Content** - Request succeeded with no content to return

### 4xx Client Errors

- **400 Bad Request** - Invalid request parameters or body
- **401 Unauthorized** - Missing or invalid authentication
- **403 Forbidden** - Insufficient permissions
- **404 Not Found** - Resource not found
- **409 Conflict** - Request conflicts with current state
- **422 Unprocessable Entity** - Validation failed
- **429 Too Many Requests** - Rate limit exceeded

### 5xx Server Errors

- **500 Internal Server Error** - Unexpected server error
- **502 Bad Gateway** - Upstream service error
- **503 Service Unavailable** - Service temporarily unavailable
- **504 Gateway Timeout** - Upstream service timeout

## Common Errors

### 400 Bad Request

**Missing Required Field:**
```json
{
  "error": "ConversationId is required",
  "status": 400,
  "title": "Bad Request"
}
```

**Validation Error:**
```json
{
  "error": "Validation failed",
  "status": 400,
  "title": "Bad Request",
  "errors": {
    "ConversationId": ["The ConversationId field is required."],
    "Message": ["The Message field is required."]
  }
}
```

### 401 Unauthorized

```json
{
  "error": "Invalid or expired authentication token",
  "status": 401,
  "title": "Unauthorized"
}
```

### 404 Not Found

```json
{
  "error": "Conversation conv_xyz789 not found",
  "status": 404,
  "title": "Not Found"
}
```

### 429 Rate Limit Exceeded

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

### 500 Internal Server Error

```json
{
  "error": "An unexpected error occurred. Please contact support with request ID.",
  "status": 500,
  "title": "Internal Server Error",
  "requestId": "req_abc123def456"
}
```

## Error Handling Best Practices

### Python

```python
import requests

try:
    response = requests.post(
        'https://api.honua.io/api/intake/message',
        headers={'Authorization': 'Bearer TOKEN'},
        json={'conversationId': 'conv_xyz', 'message': 'Hello'}
    )
    response.raise_for_status()
    data = response.json()

except requests.exceptions.HTTPError as e:
    error = e.response.json()
    if e.response.status_code == 404:
        print(f"Conversation not found: {error['error']}")
    elif e.response.status_code == 429:
        print(f"Rate limited. Retry after {error['retryAfter']} seconds")
    else:
        print(f"HTTP {error['status']}: {error['error']}")

except requests.exceptions.RequestException as e:
    print(f"Network error: {e}")
```

### JavaScript

```javascript
try {
  const response = await fetch('https://api.honua.io/api/intake/message', {
    method: 'POST',
    headers: {
      'Authorization': 'Bearer TOKEN',
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      conversationId: 'conv_xyz',
      message: 'Hello'
    })
  });

  if (!response.ok) {
    const error = await response.json();

    if (response.status === 404) {
      console.error('Conversation not found:', error.error);
    } else if (response.status === 429) {
      console.error(`Rate limited. Retry after ${error.retryAfter}s`);
    } else {
      console.error(`HTTP ${error.status}: ${error.error}`);
    }

    throw new Error(error.error);
  }

  const data = await response.json();
  // Process data

} catch (error) {
  console.error('Request failed:', error);
}
```

### C#

```csharp
using System.Net.Http.Json;

try
{
    var response = await client.PostAsJsonAsync(
        "https://api.honua.io/api/intake/message",
        new { conversationId = "conv_xyz", message = "Hello" }
    );

    response.EnsureSuccessStatusCode();
    var data = await response.Content.ReadFromJsonAsync<IntakeResponse>();
}
catch (HttpRequestException e)
{
    var error = await e.Response?.Content.ReadFromJsonAsync<ErrorResponse>();

    if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        Console.WriteLine($"Conversation not found: {error?.Error}");
    }
    else if (e.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    {
        Console.WriteLine($"Rate limited. Retry after {error?.RetryAfter}s");
    }
    else
    {
        Console.WriteLine($"HTTP {error?.Status}: {error?.Error}");
    }
}
```

## Retry Strategy

Implement exponential backoff for transient errors (5xx, 429):

```python
import time
import requests

def api_call_with_retry(url, max_retries=3, backoff_factor=2):
    for attempt in range(max_retries):
        try:
            response = requests.post(url, ...)
            response.raise_for_status()
            return response.json()

        except requests.exceptions.HTTPError as e:
            # Don't retry client errors (4xx except 429)
            if 400 <= e.response.status_code < 500 and e.response.status_code != 429:
                raise

            # Last attempt - raise error
            if attempt == max_retries - 1:
                raise

            # Calculate backoff
            wait_time = backoff_factor ** attempt
            print(f"Retrying in {wait_time}s...")
            time.sleep(wait_time)
```

## Logging Errors

Always log errors with context for debugging:

```python
import logging

logger = logging.getLogger(__name__)

try:
    response = requests.post(url, json=payload)
    response.raise_for_status()

except requests.exceptions.HTTPError as e:
    logger.error(
        "API request failed",
        extra={
            'url': url,
            'status_code': e.response.status_code,
            'error': e.response.json(),
            'request_payload': payload
        }
    )
    raise
```

## Support

If you encounter persistent errors:

1. Check [API Status Page](https://status.honua.io)
2. Review [Documentation](https://docs.honua.io)
3. Contact support@honua.io with:
   - Request ID (from error response)
   - Timestamp
   - Full error response
   - Steps to reproduce
