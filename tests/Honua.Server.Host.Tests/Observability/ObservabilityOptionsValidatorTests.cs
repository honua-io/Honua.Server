using Honua.Server.Host.Observability;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Host.Tests.Observability;

/// <summary>
/// Tests for observability configuration validation.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public sealed class ObservabilityOptionsValidatorTests
{
    [Fact]
    public void ObservabilityValidator_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var validator = new ObservabilityOptionsValidator();
        var options = new ObservabilityOptions
        {
            Metrics = new ObservabilityOptions.MetricsOptions
            {
                Enabled = true,
                Endpoint = "/metrics"
            },
            Tracing = new ObservabilityOptions.TracingOptions
            {
                Exporter = "otlp",
                OtlpEndpoint = "http://localhost:4317"
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ObservabilityValidator_WithMetricsEnabledButNoEndpoint_ReturnsFail()
    {
        // Arrange
        var validator = new ObservabilityOptionsValidator();
        var options = new ObservabilityOptions
        {
            Metrics = new ObservabilityOptions.MetricsOptions
            {
                Enabled = true,
                Endpoint = null!
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("endpoint"));
    }

    [Fact]
    public void ObservabilityValidator_WithMetricsEndpointNotStartingWithSlash_ReturnsFail()
    {
        // Arrange
        var validator = new ObservabilityOptionsValidator();
        var options = new ObservabilityOptions
        {
            Metrics = new ObservabilityOptions.MetricsOptions
            {
                Enabled = true,
                Endpoint = "metrics" // Missing leading slash
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("must start with"));
    }

    [Fact]
    public void ObservabilityValidator_WithInvalidTracingExporter_ReturnsFail()
    {
        // Arrange
        var validator = new ObservabilityOptionsValidator();
        var options = new ObservabilityOptions
        {
            Tracing = new ObservabilityOptions.TracingOptions
            {
                Exporter = "invalid-exporter"
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("exporter") && f.Contains("invalid"));
    }

    [Fact]
    public void ObservabilityValidator_WithOtlpExporterButNoEndpoint_ReturnsFail()
    {
        // Arrange
        var validator = new ObservabilityOptionsValidator();
        var options = new ObservabilityOptions
        {
            Tracing = new ObservabilityOptions.TracingOptions
            {
                Exporter = "otlp",
                OtlpEndpoint = null
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("OTLP endpoint"));
    }

    [Fact]
    public void ObservabilityValidator_WithInvalidOtlpEndpointUrl_ReturnsFail()
    {
        // Arrange
        var validator = new ObservabilityOptionsValidator();
        var options = new ObservabilityOptions
        {
            Tracing = new ObservabilityOptions.TracingOptions
            {
                Exporter = "otlp",
                OtlpEndpoint = "not-a-valid-url"
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("not a valid URL"));
    }

    [Fact]
    public void ObservabilityValidator_WithOtlpEndpointInvalidScheme_ReturnsFail()
    {
        // Arrange
        var validator = new ObservabilityOptionsValidator();
        var options = new ObservabilityOptions
        {
            Tracing = new ObservabilityOptions.TracingOptions
            {
                Exporter = "otlp",
                OtlpEndpoint = "ftp://localhost:4317" // Invalid scheme
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("http or https"));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("console")]
    [InlineData("otlp")]
    public void ObservabilityValidator_WithValidTracingExporters_ReturnsSuccessOrCorrectValidation(string exporter)
    {
        // Arrange
        var validator = new ObservabilityOptionsValidator();
        var options = new ObservabilityOptions
        {
            Tracing = new ObservabilityOptions.TracingOptions
            {
                Exporter = exporter,
                OtlpEndpoint = exporter == "otlp" ? "http://localhost:4317" : null
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        if (exporter == "otlp")
        {
            Assert.True(result.Succeeded);
        }
        else
        {
            // For "none" and "console", endpoint is not required
            Assert.True(result.Succeeded);
        }
    }

    [Fact]
    public void ObservabilityValidator_WithMetricsDisabled_AllowsAnyEndpoint()
    {
        // Arrange
        var validator = new ObservabilityOptionsValidator();
        var options = new ObservabilityOptions
        {
            Metrics = new ObservabilityOptions.MetricsOptions
            {
                Enabled = false,
                Endpoint = "invalid-no-slash" // Should not fail when disabled
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("http://localhost:4317")]
    [InlineData("https://localhost:4318")]
    [InlineData("http://jaeger:4317")]
    [InlineData("https://otel-collector.example.com:443")]
    public void ObservabilityValidator_WithValidOtlpEndpoints_ReturnsSuccess(string endpoint)
    {
        // Arrange
        var validator = new ObservabilityOptionsValidator();
        var options = new ObservabilityOptions
        {
            Tracing = new ObservabilityOptions.TracingOptions
            {
                Exporter = "otlp",
                OtlpEndpoint = endpoint
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded, $"Endpoint '{endpoint}' should be valid");
    }
}
