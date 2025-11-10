// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Base exception for authorization failures.
/// </summary>
public class AuthorizationException : HonuaException
{
    public AuthorizationException(string message) : base(message, "AUTHORIZATION_FAILED")
    {
    }

    public AuthorizationException(string message, Exception innerException) : base(message, "AUTHORIZATION_FAILED", innerException)
    {
    }

    public AuthorizationException(string message, string? errorCode) : base(message, errorCode)
    {
    }

    public AuthorizationException(string message, string? errorCode, Exception innerException) : base(message, errorCode, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a user lacks permission to perform an operation.
/// This is NOT a transient error.
/// </summary>
public sealed class InsufficientPermissionsException : AuthorizationException
{
    public string? UserId { get; }
    public string? ResourceId { get; }
    public string? RequiredPermission { get; }

    public InsufficientPermissionsException(string message)
        : base(message, "INSUFFICIENT_PERMISSIONS")
    {
    }

    public InsufficientPermissionsException(string userId, string resourceId, string requiredPermission)
        : base($"User '{userId}' does not have permission '{requiredPermission}' for resource '{resourceId}'", "INSUFFICIENT_PERMISSIONS")
    {
        UserId = userId;
        ResourceId = resourceId;
        RequiredPermission = requiredPermission;
    }
}

/// <summary>
/// Exception thrown when access to a resource is denied.
/// This is NOT a transient error.
/// </summary>
public sealed class AccessDeniedException : AuthorizationException
{
    public string? ResourceId { get; }
    public string? ResourceType { get; }

    public AccessDeniedException(string message)
        : base(message, "ACCESS_DENIED")
    {
    }

    public AccessDeniedException(string resourceType, string resourceId, string message)
        : base(message, "ACCESS_DENIED")
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
}

/// <summary>
/// Exception thrown when a resource is not found during authorization check.
/// This is NOT a transient error.
/// </summary>
public sealed class ResourceNotFoundException : AuthorizationException
{
    public string? ResourceId { get; }
    public string? ResourceType { get; }

    public ResourceNotFoundException(string resourceType, string resourceId)
        : base($"Resource '{resourceId}' of type '{resourceType}' was not found", "RESOURCE_NOT_FOUND")
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
}

/// <summary>
/// Exception thrown when authorization service is unavailable.
/// This is a transient error that can be retried.
/// </summary>
public sealed class AuthorizationServiceUnavailableException : AuthorizationException, ITransientException
{
    public string? ServiceName { get; }
    public bool IsTransient => true;

    public AuthorizationServiceUnavailableException(string message)
        : base(message, "AUTHZ_SERVICE_UNAVAILABLE")
    {
    }

    public AuthorizationServiceUnavailableException(string serviceName, string message, Exception innerException)
        : base(message, "AUTHZ_SERVICE_UNAVAILABLE", innerException)
    {
        ServiceName = serviceName;
    }
}

/// <summary>
/// Exception thrown when policy evaluation fails.
/// This may be a transient error depending on the cause.
/// </summary>
public sealed class PolicyEvaluationException : AuthorizationException
{
    public string? PolicyName { get; }

    public PolicyEvaluationException(string policyName, string message)
        : base(message, "POLICY_EVALUATION_FAILED")
    {
        PolicyName = policyName;
    }

    public PolicyEvaluationException(string policyName, string message, Exception innerException)
        : base(message, "POLICY_EVALUATION_FAILED", innerException)
    {
        PolicyName = policyName;
    }
}
