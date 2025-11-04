# Circuit Breaker Integration Guide

This document provides examples of how to integrate the `ICircuitBreakerService` into your code.

## Overview

The circuit breaker service provides three pre-configured policies:
- **Database Policy**: For database operations (timeout: 30s, retry: 3 attempts)
- **External API Policy**: For HTTP API calls (timeout: 60s, retry: 3 attempts)
- **Storage Policy**: For cloud storage (S3, Azure Blob, GCS) (timeout: 30s, retry: 3 attempts)

Each policy combines:
1. **Timeout**: Prevents operations from hanging indefinitely
2. **Retry**: Automatically retries transient failures with exponential backoff
3. **Circuit Breaker**: Fails fast when a service is down, preventing cascading failures

## Example 1: Database Operations

```csharp
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.DependencyInjection;

public class UserRepository
{
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly IDbConnection _connection;

    public UserRepository(ICircuitBreakerService circuitBreaker, IDbConnection connection)
    {
        _circuitBreaker = circuitBreaker;
        _connection = connection;
    }

    public async Task<User?> GetUserByIdAsync(int userId, CancellationToken cancellationToken)
    {
        var pipeline = _circuitBreaker.GetDatabasePolicy<User?>();

        return await pipeline.ExecuteAsync(async ct =>
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT * FROM Users WHERE Id = @userId";
            command.Parameters.Add(new SqlParameter("@userId", userId));

            using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new User
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                };
            }

            return null;
        }, cancellationToken);
    }
}
```

## Example 2: External API Calls

```csharp
using Honua.Server.Core.Resilience;
using System.Net.Http;

public class WeatherApiClient
{
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly HttpClient _httpClient;

    public WeatherApiClient(ICircuitBreakerService circuitBreaker, HttpClient httpClient)
    {
        _circuitBreaker = circuitBreaker;
        _httpClient = httpClient;
    }

    public async Task<WeatherData?> GetWeatherAsync(string location, CancellationToken cancellationToken)
    {
        var pipeline = _circuitBreaker.GetExternalApiPolicy<WeatherData?>();

        return await pipeline.ExecuteAsync(async ct =>
        {
            var response = await _httpClient.GetAsync($"https://api.weather.com/data?location={location}", ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<WeatherData>(json);
        }, cancellationToken);
    }
}
```

## Example 3: Cloud Storage Operations

```csharp
using Honua.Server.Core.Resilience;
using Amazon.S3;
using Amazon.S3.Model;

public class DocumentStore
{
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public DocumentStore(ICircuitBreakerService circuitBreaker, IAmazonS3 s3Client, string bucketName)
    {
        _circuitBreaker = circuitBreaker;
        _s3Client = s3Client;
        _bucketName = bucketName;
    }

    public async Task<Stream?> GetDocumentAsync(string documentId, CancellationToken cancellationToken)
    {
        var pipeline = _circuitBreaker.GetStoragePolicy<Stream?>();

        return await pipeline.ExecuteAsync(async ct =>
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = documentId
            };

            var response = await _s3Client.GetObjectAsync(request, ct);
            return response.ResponseStream;
        }, cancellationToken);
    }

    public async Task UploadDocumentAsync(string documentId, Stream content, CancellationToken cancellationToken)
    {
        var pipeline = _circuitBreaker.GetStoragePolicy<bool>();

        await pipeline.ExecuteAsync(async ct =>
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = documentId,
                InputStream = content
            };

            await _s3Client.PutObjectAsync(request, ct);
            return true;
        }, cancellationToken);
    }
}
```

## Example 4: Integrating into CloudAttachmentStoreBase

For the existing attachment stores, you can wrap operations like this:

```csharp
public class S3AttachmentStore : CloudAttachmentStoreBase<IAmazonS3, AmazonS3Exception>
{
    private readonly ICircuitBreakerService _circuitBreaker;

    public S3AttachmentStore(
        IAmazonS3 client,
        string bucketName,
        string? prefix,
        ICircuitBreakerService circuitBreaker,
        bool ownsClient = false)
        : base(client, prefix, AttachmentStoreProviderKeys.S3, ownsClient)
    {
        _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
    }

    protected override async Task PutObjectAsync(
        string objectKey,
        Stream content,
        string mimeType,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var pipeline = _circuitBreaker.GetStoragePolicy<bool>();

        await pipeline.ExecuteAsync(async ct =>
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = content,
                ContentType = mimeType,
                AutoCloseStream = false
            };

            foreach (var kvp in metadata)
            {
                putRequest.Metadata[kvp.Key] = kvp.Value;
            }

            await Client.PutObjectAsync(putRequest, ct);
            return true;
        }, cancellationToken);
    }
}
```

## Checking Circuit Breaker State

You can check the state of circuit breakers programmatically:

```csharp
public class HealthService
{
    private readonly ICircuitBreakerService _circuitBreaker;

    public HealthService(ICircuitBreakerService circuitBreaker)
    {
        _circuitBreaker = circuitBreaker;
    }

    public bool IsDatabaseHealthy()
    {
        var state = _circuitBreaker.GetCircuitState("database");
        return state == CircuitState.Closed;
    }

    public bool IsStorageHealthy()
    {
        var state = _circuitBreaker.GetCircuitState("storage");
        return state == CircuitState.Closed;
    }

    public bool IsExternalApiHealthy()
    {
        var state = _circuitBreaker.GetCircuitState("externalapi");
        return state == CircuitState.Closed;
    }
}
```

## Configuration

Circuit breaker settings can be configured in `appsettings.json`:

```json
{
  "Resilience": {
    "CircuitBreaker": {
      "Database": {
        "Enabled": true,
        "FailureRatio": 0.5,
        "MinimumThroughput": 10,
        "SamplingDuration": "00:00:30",
        "BreakDuration": "00:00:30"
      },
      "ExternalApi": {
        "Enabled": true,
        "FailureRatio": 0.5,
        "MinimumThroughput": 10,
        "SamplingDuration": "00:00:30",
        "BreakDuration": "00:01:00"
      },
      "Storage": {
        "Enabled": true,
        "FailureRatio": 0.5,
        "MinimumThroughput": 10,
        "SamplingDuration": "00:00:30",
        "BreakDuration": "00:00:30"
      }
    }
  }
}
```

## How Circuit Breaker Works

1. **Closed (Normal)**: All requests pass through. Failures are counted.
2. **Open (Service Down)**: After failure threshold is reached, circuit opens. All requests fail fast without calling the service.
3. **Half-Open (Testing Recovery)**: After break duration expires, circuit allows one test request. If successful, circuit closes. If failed, circuit re-opens.

## Benefits

- **Prevents Cascading Failures**: Failing fast when a dependency is down prevents thread exhaustion
- **Automatic Recovery**: Circuit automatically tests recovery after the break duration
- **Observability**: Circuit state transitions are logged and exposed via metrics
- **Health Checks**: Circuit state is exposed via `/health` endpoint
- **Graceful Degradation**: Applications can handle open circuits gracefully (e.g., show cached data)

## Metrics

Circuit breaker metrics are available via OpenTelemetry:
- `honua.circuit_breaker.state_transitions` - Counter of state changes
- `honua.circuit_breaker.breaks` - Counter of circuit opens
- `honua.circuit_breaker.closures` - Counter of circuit closes
- `honua.circuit_breaker.half_opens` - Counter of half-open transitions
- `honua.circuit_breaker.state` - Current circuit state gauge

## Best Practices

1. **Use the Right Policy**: Use database policy for DB, storage policy for S3/Blob, external API for HTTP
2. **Handle Open Circuits**: Implement fallbacks when circuits are open (e.g., cached data, default values)
3. **Monitor Metrics**: Track circuit breaker metrics to identify problematic dependencies
4. **Tune Configuration**: Adjust failure ratio and break duration based on your needs
5. **Test Resilience**: Use chaos engineering to test circuit breaker behavior
