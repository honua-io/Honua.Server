using Honua.Server.Core.Observability;
using Serilog;
using Serilog.Events;

namespace Honua.Examples.Alerting;

/// <summary>
/// Example of configuring Serilog to automatically send errors as alerts.
/// </summary>
public class SerilogAlertExample
{
    /// <summary>
    /// Configure Serilog with alert sink.
    /// Add this to your Program.cs or Startup.cs.
    /// </summary>
    public static void ConfigureSerilogWithAlerts()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Honua")
            .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
            .WriteTo.Console()
            .WriteTo.AlertReceiver(
                alertReceiverUrl: "http://alert-receiver:8080",
                alertReceiverToken: Environment.GetEnvironmentVariable("ALERT_RECEIVER_TOKEN"),
                environment: Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                serviceName: "honua-api",
                minimumLevel: LogEventLevel.Error) // Only Error and Fatal logs become alerts
            .CreateLogger();
    }

    /// <summary>
    /// Now any error log automatically triggers an alert.
    /// No need to call IAlertClient explicitly!
    /// </summary>
    public class MyService
    {
        private readonly ILogger<MyService> _logger;

        public MyService(ILogger<MyService> logger)
        {
            _logger = logger;
        }

        public async Task ProcessData()
        {
            try
            {
                // Your code here
                await DoSomething();
            }
            catch (Exception ex)
            {
                // This error log automatically sends an alert!
                _logger.LogError(ex, "Failed to process data for order {OrderId}", "12345");

                // Alert will be sent with:
                // - name: "ApplicationError"
                // - severity: "high" (Error level)
                // - description: "Failed to process data for order 12345"
                // - labels: { OrderId: "12345" }
                // - context: { exception details }

                throw;
            }
        }

        public async Task CriticalOperation()
        {
            try
            {
                await PerformCriticalTask();
            }
            catch (Exception ex)
            {
                // Fatal logs send critical alerts
                _logger.LogCritical(ex, "Critical operation failed - data may be corrupted");

                // Alert will be sent with:
                // - severity: "critical" (Fatal level)

                throw;
            }
        }

        public void ValidateConfiguration()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL")))
            {
                // Error log = alert
                _logger.LogError("DATABASE_URL environment variable is not set");
                throw new InvalidOperationException("Missing required configuration");
            }
        }

        public async Task MonitorExternalService()
        {
            try
            {
                var response = await CallExternalService();
                if (!response.IsSuccessful)
                {
                    // Warning logs don't trigger alerts by default (minimumLevel: Error)
                    _logger.LogWarning("External service returned error: {Error}", response.Error);
                }
            }
            catch (HttpRequestException ex)
            {
                // Error log = alert
                _logger.LogError(ex, "External service unavailable");
            }
        }

        // Mock methods
        private Task DoSomething() => Task.CompletedTask;
        private Task PerformCriticalTask() => Task.CompletedTask;
        private Task<ServiceResponse> CallExternalService() => Task.FromResult(new ServiceResponse());

        private class ServiceResponse
        {
            public bool IsSuccessful { get; set; }
            public string Error { get; set; } = "";
        }
    }

    /// <summary>
    /// Example with structured logging properties.
    /// </summary>
    public class AdvancedLoggingExample
    {
        private readonly ILogger<AdvancedLoggingExample> _logger;

        public AdvancedLoggingExample(ILogger<AdvancedLoggingExample> logger)
        {
            _logger = logger;
        }

        public async Task ProcessOrder(string orderId, decimal amount, string customerId)
        {
            try
            {
                await ProcessPayment(orderId, amount);
            }
            catch (Exception ex)
            {
                // Structured properties become alert labels
                _logger.LogError(ex,
                    "Payment processing failed for order {OrderId}, customer {CustomerId}, amount ${Amount}",
                    orderId, customerId, amount);

                // Alert will include labels:
                // - OrderId: "12345"
                // - CustomerId: "cust_abc"
                // - Amount: "99.99"

                throw;
            }
        }

        public async Task MonitorDatabasePerformance()
        {
            var queryTime = await MeasureQueryTime();

            if (queryTime > TimeSpan.FromSeconds(5))
            {
                // Error with structured data
                _logger.LogError(
                    "Slow database query detected: {QueryTimeMs}ms (threshold: {ThresholdMs}ms)",
                    queryTime.TotalMilliseconds, 5000);

                // Alert labels:
                // - QueryTimeMs: "7500"
                // - ThresholdMs: "5000"
            }
        }

        // Mock methods
        private Task ProcessPayment(string orderId, decimal amount) => Task.CompletedTask;
        private Task<TimeSpan> MeasureQueryTime() => Task.FromResult(TimeSpan.FromSeconds(2));
    }
}

/// <summary>
/// Example: Configure different alert levels for different environments.
/// </summary>
public class EnvironmentSpecificAlertConfiguration
{
    public static void ConfigureByEnvironment(string environment)
    {
        var minimumAlertLevel = environment switch
        {
            "Production" => LogEventLevel.Error,      // Only errors and fatal
            "Staging" => LogEventLevel.Warning,       // Warnings, errors, and fatal
            "Development" => LogEventLevel.Fatal,     // Only fatal (very limited alerting)
            _ => LogEventLevel.Error
        };

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.AlertReceiver(
                alertReceiverUrl: "http://alert-receiver:8080",
                alertReceiverToken: Environment.GetEnvironmentVariable("ALERT_RECEIVER_TOKEN"),
                environment: environment,
                serviceName: "honua-api",
                minimumLevel: minimumAlertLevel)
            .CreateLogger();
    }
}
