using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Support;

internal sealed class RequiresDockerFactAttribute : FactAttribute
{
    public RequiresDockerFactAttribute()
    {
        if (!DockerTestHelper.IsDockerAvailable)
        {
            Skip = "Docker is required for this test environment.";
        }
    }
}
