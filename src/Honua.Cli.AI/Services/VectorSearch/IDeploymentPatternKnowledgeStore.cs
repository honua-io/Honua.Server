// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.VectorSearch;

public interface IDeploymentPatternKnowledgeStore
{
    Task IndexApprovedPatternAsync(DeploymentPattern pattern, CancellationToken cancellationToken = default);

    Task<List<PatternSearchResult>> SearchPatternsAsync(DeploymentRequirements requirements, CancellationToken cancellationToken = default);
}
