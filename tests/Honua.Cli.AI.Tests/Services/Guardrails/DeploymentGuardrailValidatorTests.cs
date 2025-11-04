using FluentAssertions;
using Honua.Cli.AI.Services.Guardrails;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Guardrails;

public class DeploymentGuardrailValidatorTests
{
    private readonly DeploymentGuardrailValidator _validator = new(new ResourceEnvelopeCatalog());

    [Fact]
    public void Validate_UsesDefaultProfile_When_NotSpecified()
    {
        var result = _validator.Validate("AWS", null, null);

        result.IsValid.Should().BeTrue();
        result.Decision.WorkloadProfile.Should().Be("api-standard");
        result.Decision.Envelope.CloudProvider.Should().Be("AWS");
    }

    [Fact]
    public void Validate_Fails_When_Request_Below_Guardrail()
    {
        var sizing = new DeploymentSizingRequest(RequestedVCpu: 0.5m, RequestedMemoryGb: 1.5m);

        var result = _validator.Validate("AWS", "api-small", sizing);

        result.IsValid.Should().BeFalse();
        result.Decision.Violations.Should().ContainSingle(v => v.Field == nameof(DeploymentSizingRequest.RequestedVCpu));
    }
}
