// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;

namespace Honua.Cli.AI.Services.Guardrails;

public interface IResourceEnvelopeCatalog
{
    /// <summary>
    /// Resolves the guardrail envelope for the given provider and workload profile.
    /// Throws if an envelope does not exist.
    /// </summary>
    ResourceEnvelope Resolve(string cloudProvider, string workloadProfile);

    /// <summary>
    /// Returns the default workload profile for a provider (used when none specified).
    /// </summary>
    string GetDefaultWorkloadProfile(string cloudProvider);

    /// <summary>
    /// Enumerates the known profiles for the provider (for UI/planning).
    /// </summary>
    IReadOnlyCollection<string> ListProfiles(string cloudProvider);
}
