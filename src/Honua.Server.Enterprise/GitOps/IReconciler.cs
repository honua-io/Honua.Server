// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.GitOps;

/// <summary>
/// Reconciles desired state (from Git) with actual state (deployed configuration)
/// </summary>
public interface IReconciler
{
    /// <summary>
    /// Reconcile an environment to match the desired state in Git
    /// </summary>
    Task ReconcileAsync(
        string environment,
        string commit,
        string initiatedBy,
        CancellationToken cancellationToken = default);
}
