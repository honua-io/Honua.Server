using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;

namespace Honua.Cli.AI.TestSupport;

/// <summary>
/// No-op Azure CLI implementation that returns an empty response.
/// </summary>
public sealed class NoopAzureCli : IAzureCli
{
    public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments) =>
        Task.FromResult(string.Empty);
}

/// <summary>
/// No-op gcloud CLI implementation that returns an empty response.
/// </summary>
public sealed class NoopGcloudCli : IGcloudCli
{
    public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments) =>
        Task.FromResult(string.Empty);
}

/// <summary>
/// No-op AWS CLI implementation used for unit and integration tests.
/// </summary>
public sealed class NoopAwsCli : IAwsCli
{
    public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments) =>
        Task.FromResult(string.Empty);
}
