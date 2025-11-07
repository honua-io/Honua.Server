// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Authentication;

/// <summary>
/// Service for creating and managing JWT (JSON Web Token) authentication tokens for local authentication.
/// Used when authentication mode is set to "local" for username/password authentication.
/// </summary>
/// <remarks>
/// This service generates signed JWT tokens containing user identity (subject) and role claims.
/// The tokens are used for stateless authentication across API requests. Tokens are self-contained
/// and validated using cryptographic signatures, eliminating the need for server-side session storage.
/// </remarks>
public interface ILocalTokenService
{
    /// <summary>
    /// Creates a new JWT authentication token for the specified user with assigned roles.
    /// </summary>
    /// <param name="subject">The unique user identifier (subject claim in JWT). Typically the user ID or username.</param>
    /// <param name="roles">The collection of role names assigned to the user (e.g., "administrator", "editor", "viewer").</param>
    /// <param name="lifetime">
    /// The optional token lifetime duration. If not specified, uses the default configured token lifetime.
    /// After this duration expires, the token will no longer be valid and the user must re-authenticate.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A signed JWT token string that can be used for authentication in subsequent requests.</returns>
    /// <exception cref="System.ArgumentException">Thrown when subject is null or empty.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown when roles collection is null.</exception>
    Task<string> CreateTokenAsync(
        string subject,
        IReadOnlyCollection<string> roles,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default);
}
