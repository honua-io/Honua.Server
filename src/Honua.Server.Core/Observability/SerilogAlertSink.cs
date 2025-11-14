// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Serilog sink that sends Error and Fatal log events as alerts.
/// Automatically alerts on application errors without Prometheus.
/// </summary>
public sealed class SerilogAlertSink : ILogEventSink, IDisposable, IAsyncDisposable
{
    private static readonly Lazy<SocketsHttpHandler> SharedHandler = new(() => new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 20,
        AllowAutoRedirect = false
    });

    private readonly HttpClient _httpClient;
    private readonly string? _alertReceiverUrl;
    private readonly string? _alertReceiverToken;
    private readonly string? _environment;
    private readonly string? _serviceName;
    private readonly LogEventLevel _minimumLevel;
    private readonly bool _ownsHttpClient;
    private static readonly ActivitySource _activitySource = new("Honua.Observability.AlertSink");

    // Bounded channel to prevent memory leaks from unbounded Task.Run calls
    private readonly Channel<LogEvent> _alertQueue;
    private readonly SemaphoreSlim _alertSemaphore = new(10, 10); // Max 10 concurrent alert sends
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _processingTask;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of SerilogAlertSink.
    /// </summary>
    /// <param name="alertReceiverUrl">The URL of the alert receiver service.</param>
    /// <param name="alertReceiverToken">Optional bearer token for authentication.</param>
    /// <param name="environment">Environment name (e.g., "production", "staging").</param>
    /// <param name="serviceName">Service name for alert context.</param>
    /// <param name="minimumLevel">Minimum log level to trigger alerts.</param>
    /// <param name="httpClient">Optional HttpClient instance. If not provided, creates a new one (not recommended - use IHttpClientFactory).</param>
    public SerilogAlertSink(
        string? alertReceiverUrl,
        string? alertReceiverToken,
        string? environment = null,
        string? serviceName = null,
        LogEventLevel minimumLevel = LogEventLevel.Error,
        HttpClient? httpClient = null)
    {
        _alertReceiverUrl = alertReceiverUrl;
        _alertReceiverToken = alertReceiverToken;
        _environment = environment ?? "production";
        _serviceName = serviceName ?? "honua-api";
        _minimumLevel = minimumLevel;

        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = CreateDefaultHttpClient();
            _ownsHttpClient = true;
        }

        if (!string.IsNullOrWhiteSpace(_alertReceiverToken) && httpClient != null)
        {
            // Only set default authorization header when using caller-provided client
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _alertReceiverToken);
        }

        // Initialize bounded channel with drop-oldest strategy to prevent unbounded memory growth
        _alertQueue = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        // Start background processing task
        _processingTask = Task.Run(() => ProcessAlertsAsync(_shutdownCts.Token));
    }

    public void Emit(LogEvent logEvent)
    {
        if (string.IsNullOrWhiteSpace(_alertReceiverUrl))
        {
            return;
        }

        if (logEvent.Level < _minimumLevel)
        {
            return;
        }

        // Use bounded channel instead of fire-and-forget Task.Run
        // This prevents thread pool exhaustion and memory leaks
        _alertQueue.Writer.TryWrite(logEvent);
    }

    private async Task ProcessAlertsAsync(CancellationToken cancellationToken)
    {
        await foreach (var logEvent in _alertQueue.Reader.ReadAllAsync(cancellationToken))
        {
            // Use semaphore to limit concurrent alert sends (max 10 at a time)
            await _alertSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            // Process alert in background but with bounded concurrency
            _ = Task.Run(async () =>
            {
                try
                {
                    await SendAlertAsync(logEvent).ConfigureAwait(false);
                }
                finally
                {
                    _alertSemaphore.Release();
                }
            }, cancellationToken);
        }
    }

    private async Task SendAlertAsync(LogEvent logEvent)
    {
        using var activity = _activitySource.StartActivity("SendAlert");
        activity?.SetTag("log.level", logEvent.Level.ToString());
        activity?.SetTag("alert.receiver", _alertReceiverUrl);

        try
        {
            var severity = logEvent.Level switch
            {
                LogEventLevel.Fatal => "critical",
                LogEventLevel.Error => "high",
                LogEventLevel.Warning => "medium",
                _ => "low"
            };

            var labels = new Dictionary<string, string>();
            var context = new Dictionary<string, object>();

            // Extract structured properties
            foreach (var property in logEvent.Properties)
            {
                var value = GetPropertyValue(property.Value);

                // Use as label if simple string
                if (value is string strValue && strValue.Length < 100)
                {
                    labels[property.Key] = strValue;
                }
                else
                {
                    context[property.Key] = value ?? "";
                }
            }

            var alert = new
            {
                name = "ApplicationError",
                severity = severity,
                status = "firing",
                summary = logEvent.MessageTemplate.Text,
                description = logEvent.RenderMessage(),
                source = "application-logs",
                service = _serviceName,
                environment = _environment,
                labels = labels,
                context = context,
                timestamp = logEvent.Timestamp.UtcDateTime
            };

            var json = JsonSerializer.Serialize(alert);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var endpoint = BuildAlertEndpoint();
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };

            if (!string.IsNullOrWhiteSpace(_alertReceiverToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _alertReceiverToken);
            }

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            activity?.SetTag("alert.sent", response.IsSuccessStatusCode);
            activity?.SetTag("http.status_code", (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            // Swallow exceptions - logging failures shouldn't break the app
            // But record to telemetry for observability
            activity?.SetTag("error", true);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
    }

    private string BuildAlertEndpoint()
    {
        if (string.IsNullOrWhiteSpace(_alertReceiverUrl))
        {
            throw new InvalidOperationException("Alert receiver URL is not configured.");
        }

        return $"{_alertReceiverUrl.TrimEnd('/')}/api/alerts";
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient(SharedHandler.Value, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        return client;
    }

    private static object? GetPropertyValue(LogEventPropertyValue value)
    {
        return value switch
        {
            ScalarValue sv => sv.Value,
            SequenceValue sv => sv.Elements.Select(GetPropertyValue).ToList(),
            StructureValue sv => sv.Properties.ToDictionary(p => p.Name, p => GetPropertyValue(p.Value)),
            DictionaryValue dv => dv.Elements.ToDictionary(
                kvp => GetPropertyValue(kvp.Key)?.ToString() ?? "",
                kvp => GetPropertyValue(kvp.Value)),
            _ => value.ToString()
        };
    }

    public void Dispose()
    {
        // Use synchronous version for IDisposable compatibility
        // ASP.NET Core best practice: Provide both Dispose and DisposeAsync
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Signal shutdown and wait for processing to complete
        _shutdownCts.Cancel();
        _alertQueue.Writer.Complete();

        try
        {
            // ASP.NET Core best practice: Use async wait instead of blocking Wait()
            // Give processing task time to finish (best-effort)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _processingTask.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown or timeout
        }
        catch
        {
            // Ignore disposal errors
        }

        _shutdownCts.Dispose();
        _alertSemaphore.Dispose();

        if (_ownsHttpClient)
        {
            _httpClient?.Dispose();
        }
    }
}

/// <summary>
/// Extension methods for configuring the alert sink.
/// </summary>
public static class SerilogAlertSinkExtensions
{
    public static Serilog.LoggerConfiguration AlertReceiver(
        this Serilog.Configuration.LoggerSinkConfiguration sinkConfiguration,
        string alertReceiverUrl,
        string? alertReceiverToken = null,
        string? environment = null,
        string? serviceName = null,
        LogEventLevel minimumLevel = LogEventLevel.Error)
    {
        return sinkConfiguration.Sink(new SerilogAlertSink(
            alertReceiverUrl,
            alertReceiverToken,
            environment,
            serviceName,
            minimumLevel));
    }
}
