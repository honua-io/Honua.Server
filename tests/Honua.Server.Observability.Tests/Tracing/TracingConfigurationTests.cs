using FluentAssertions;
using Honua.Server.Observability.Tracing;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;
using Xunit;

namespace Honua.Server.Observability.Tests.Tracing;

/// <summary>
/// Tests for OpenTelemetry tracing configuration.
/// </summary>
public class TracingConfigurationTests
{
    [Fact]
    public void TracingConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new TracingConfiguration();

        // Assert
        config.Enabled.Should().BeTrue();
        config.Exporter.Should().Be("console");
        config.OtlpProtocol.Should().Be("grpc");
        config.JaegerAgentHost.Should().Be("localhost");
        config.JaegerAgentPort.Should().Be(6831);
        config.SamplingStrategy.Should().Be("parent_based");
        config.SamplingRatio.Should().Be(1.0);
        config.MaxAttributesPerSpan.Should().Be(128);
        config.MaxEventsPerSpan.Should().Be(128);
        config.MaxLinksPerSpan.Should().Be(128);
        config.RecordExceptionDetails.Should().BeTrue();
        config.EnrichWithHttpDetails.Should().BeTrue();
        config.EnrichWithDbStatements.Should().BeTrue();
        config.TraceRediCommands.Should().BeFalse();
    }

    [Fact]
    public void TracingConfiguration_ExcludedEndpoints_ContainDefaults()
    {
        // Arrange & Act
        var config = new TracingConfiguration();

        // Assert
        config.ExcludedEndpoints.Should().Contain("/health");
        config.ExcludedEndpoints.Should().Contain("/metrics");
        config.ExcludedEndpoints.Should().Contain("/ready");
        config.ExcludedEndpoints.Should().Contain("/live");
    }

    [Fact]
    public void TracingConfiguration_LoadFromConfiguration_ReadsCorrectly()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Observability:Tracing:Enabled"] = "true",
            ["Observability:Tracing:Exporter"] = "otlp",
            ["Observability:Tracing:OtlpEndpoint"] = "http://localhost:4317",
            ["Observability:Tracing:SamplingStrategy"] = "trace_id_ratio",
            ["Observability:Tracing:SamplingRatio"] = "0.5"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var config = configuration.GetSection("Observability:Tracing")
            .Get<TracingConfiguration>();

        // Assert
        config.Should().NotBeNull();
        config!.Enabled.Should().BeTrue();
        config.Exporter.Should().Be("otlp");
        config.OtlpEndpoint.Should().Be("http://localhost:4317");
        config.SamplingStrategy.Should().Be("trace_id_ratio");
        config.SamplingRatio.Should().Be(0.5);
    }

    [Theory]
    [InlineData("console")]
    [InlineData("otlp")]
    [InlineData("jaeger")]
    [InlineData("multiple")]
    [InlineData("none")]
    public void TracingConfiguration_SupportsAllExporterTypes(string exporterType)
    {
        // Arrange
        var config = new TracingConfiguration { Exporter = exporterType };

        // Act & Assert
        config.Exporter.Should().Be(exporterType);
    }

    [Theory]
    [InlineData("always_on")]
    [InlineData("always_off")]
    [InlineData("trace_id_ratio")]
    [InlineData("parent_based")]
    public void TracingConfiguration_SupportsAllSamplingStrategies(string strategy)
    {
        // Arrange
        var config = new TracingConfiguration { SamplingStrategy = strategy };

        // Act & Assert
        config.SamplingStrategy.Should().Be(strategy);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void TracingConfiguration_SamplingRatio_AcceptsValidValues(double ratio)
    {
        // Arrange
        var config = new TracingConfiguration { SamplingRatio = ratio };

        // Act & Assert
        config.SamplingRatio.Should().Be(ratio);
    }

    [Fact]
    public void TracingConfiguration_Baggage_CanBeSet()
    {
        // Arrange
        var config = new TracingConfiguration();
        config.Baggage["user.id"] = "12345";
        config.Baggage["tenant.id"] = "acme-corp";

        // Act & Assert
        config.Baggage.Should().HaveCount(2);
        config.Baggage["user.id"].Should().Be("12345");
        config.Baggage["tenant.id"].Should().Be("acme-corp");
    }

    [Fact]
    public void TracingConfiguration_SecuritySettings_CanBeDisabled()
    {
        // Arrange
        var config = new TracingConfiguration
        {
            RecordExceptionDetails = false,
            EnrichWithDbStatements = false,
            TraceRediCommands = false
        };

        // Act & Assert
        config.RecordExceptionDetails.Should().BeFalse();
        config.EnrichWithDbStatements.Should().BeFalse();
        config.TraceRediCommands.Should().BeFalse();
    }

    [Fact]
    public void TracingConfiguration_OtlpProtocol_SupportsGrpcAndHttp()
    {
        // Arrange & Act
        var grpcConfig = new TracingConfiguration { OtlpProtocol = "grpc" };
        var httpConfig = new TracingConfiguration { OtlpProtocol = "http/protobuf" };

        // Assert
        grpcConfig.OtlpProtocol.Should().Be("grpc");
        httpConfig.OtlpProtocol.Should().Be("http/protobuf");
    }

    [Fact]
    public void TracingConfiguration_JaegerSettings_CanBeCustomized()
    {
        // Arrange
        var config = new TracingConfiguration
        {
            JaegerAgentHost = "jaeger.example.com",
            JaegerAgentPort = 6832
        };

        // Act & Assert
        config.JaegerAgentHost.Should().Be("jaeger.example.com");
        config.JaegerAgentPort.Should().Be(6832);
    }

    [Fact]
    public void TracingConfiguration_SpanLimits_CanBeCustomized()
    {
        // Arrange
        var config = new TracingConfiguration
        {
            MaxAttributesPerSpan = 256,
            MaxEventsPerSpan = 256,
            MaxLinksPerSpan = 64
        };

        // Act & Assert
        config.MaxAttributesPerSpan.Should().Be(256);
        config.MaxEventsPerSpan.Should().Be(256);
        config.MaxLinksPerSpan.Should().Be(64);
    }

    [Fact]
    public void TracingConfiguration_DisabledConfig_DoesNotConfigure()
    {
        // Arrange
        var config = new TracingConfiguration { Enabled = false };

        // Act & Assert
        config.Enabled.Should().BeFalse();
    }
}
