// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Centralized error code constants for all exceptions in the Honua system.
/// Error codes follow the pattern: CATEGORY_SPECIFIC_DESCRIPTION
/// </summary>
public static class ErrorCodes
{
    // ========================================
    // Authentication Error Codes (AUTH_*)
    // ========================================

    /// <summary>
    /// Authentication failed - generic authentication error.
    /// </summary>
    public const string AUTHENTICATION_FAILED = "AUTHENTICATION_FAILED";

    /// <summary>
    /// Invalid credentials provided.
    /// </summary>
    public const string INVALID_CREDENTIALS = "INVALID_CREDENTIALS";

    /// <summary>
    /// Invalid or expired authentication token.
    /// </summary>
    public const string INVALID_TOKEN = "INVALID_TOKEN";

    /// <summary>
    /// Authentication service is unavailable.
    /// </summary>
    public const string AUTH_SERVICE_UNAVAILABLE = "AUTH_SERVICE_UNAVAILABLE";

    /// <summary>
    /// Multi-factor authentication failed.
    /// </summary>
    public const string MFA_FAILED = "MFA_FAILED";

    /// <summary>
    /// User account is locked.
    /// </summary>
    public const string ACCOUNT_LOCKED = "ACCOUNT_LOCKED";

    // ========================================
    // Authorization Error Codes (AUTHZ_*)
    // ========================================

    /// <summary>
    /// Authorization failed - generic authorization error.
    /// </summary>
    public const string AUTHORIZATION_FAILED = "AUTHORIZATION_FAILED";

    /// <summary>
    /// User has insufficient permissions.
    /// </summary>
    public const string INSUFFICIENT_PERMISSIONS = "INSUFFICIENT_PERMISSIONS";

    /// <summary>
    /// Access to resource is denied.
    /// </summary>
    public const string ACCESS_DENIED = "ACCESS_DENIED";

    /// <summary>
    /// Resource was not found during authorization check.
    /// </summary>
    public const string RESOURCE_NOT_FOUND = "RESOURCE_NOT_FOUND";

    /// <summary>
    /// Authorization service is unavailable.
    /// </summary>
    public const string AUTHZ_SERVICE_UNAVAILABLE = "AUTHZ_SERVICE_UNAVAILABLE";

    /// <summary>
    /// Policy evaluation failed.
    /// </summary>
    public const string POLICY_EVALUATION_FAILED = "POLICY_EVALUATION_FAILED";

    // ========================================
    // Data Error Codes (DATA_*)
    // ========================================

    /// <summary>
    /// Generic data operation failure.
    /// </summary>
    public const string DATA_OPERATION_FAILED = "DATA_OPERATION_FAILED";

    /// <summary>
    /// Feature was not found.
    /// </summary>
    public const string FEATURE_NOT_FOUND = "FEATURE_NOT_FOUND";

    /// <summary>
    /// Feature validation failed.
    /// </summary>
    public const string FEATURE_VALIDATION_FAILED = "FEATURE_VALIDATION_FAILED";

    /// <summary>
    /// Data store provider error.
    /// </summary>
    public const string DATA_STORE_PROVIDER_ERROR = "DATA_STORE_PROVIDER_ERROR";

    /// <summary>
    /// Connection string is invalid or missing.
    /// </summary>
    public const string CONNECTION_STRING_INVALID = "CONNECTION_STRING_INVALID";

    /// <summary>
    /// Concurrent update conflict detected.
    /// </summary>
    public const string CONCURRENCY_CONFLICT = "CONCURRENCY_CONFLICT";

    // ========================================
    // Data Store Error Codes (DATA_STORE_*)
    // ========================================

    /// <summary>
    /// Failed to connect to data store.
    /// </summary>
    public const string DATA_STORE_CONNECTION_FAILED = "DATA_STORE_CONNECTION_FAILED";

    /// <summary>
    /// Data store operation timed out.
    /// </summary>
    public const string DATA_STORE_TIMEOUT = "DATA_STORE_TIMEOUT";

    /// <summary>
    /// Data store is unavailable.
    /// </summary>
    public const string DATA_STORE_UNAVAILABLE = "DATA_STORE_UNAVAILABLE";

    /// <summary>
    /// Data store constraint violation.
    /// </summary>
    public const string DATA_STORE_CONSTRAINT_VIOLATION = "DATA_STORE_CONSTRAINT_VIOLATION";

    /// <summary>
    /// Data store deadlock detected.
    /// </summary>
    public const string DATA_STORE_DEADLOCK = "DATA_STORE_DEADLOCK";

    // ========================================
    // Service Error Codes (SERVICE_*)
    // ========================================

    /// <summary>
    /// Generic service error.
    /// </summary>
    public const string SERVICE_ERROR = "SERVICE_ERROR";

    /// <summary>
    /// External service is unavailable.
    /// </summary>
    public const string SERVICE_UNAVAILABLE = "SERVICE_UNAVAILABLE";

    /// <summary>
    /// Circuit breaker is open.
    /// </summary>
    public const string CIRCUIT_BREAKER_OPEN = "CIRCUIT_BREAKER_OPEN";

    /// <summary>
    /// Service operation timed out.
    /// </summary>
    public const string SERVICE_TIMEOUT = "SERVICE_TIMEOUT";

    /// <summary>
    /// Service is throttled due to rate limiting.
    /// </summary>
    public const string SERVICE_THROTTLED = "SERVICE_THROTTLED";

    // ========================================
    // Migration Error Codes (MIGRATION_*)
    // ========================================

    /// <summary>
    /// Generic migration operation failure.
    /// </summary>
    public const string MIGRATION_FAILED = "MIGRATION_FAILED";

    /// <summary>
    /// Migration validation failed.
    /// </summary>
    public const string MIGRATION_VALIDATION_FAILED = "MIGRATION_VALIDATION_FAILED";

    /// <summary>
    /// Migration source has unsupported features.
    /// </summary>
    public const string UNSUPPORTED_MIGRATION_SOURCE = "UNSUPPORTED_MIGRATION_SOURCE";

    // ========================================
    // Geometry Validation Error Codes (GEOMETRY_*)
    // ========================================

    /// <summary>
    /// Geometry validation failed.
    /// </summary>
    public const string GEOMETRY_VALIDATION_FAILED = "GEOMETRY_VALIDATION_FAILED";

    /// <summary>
    /// Geometry is invalid according to OGC standards.
    /// </summary>
    public const string INVALID_GEOMETRY = "INVALID_GEOMETRY";

    /// <summary>
    /// Invalid or unsupported spatial reference.
    /// </summary>
    public const string INVALID_SPATIAL_REFERENCE = "INVALID_SPATIAL_REFERENCE";

    /// <summary>
    /// Geometry coordinates are out of valid bounds.
    /// </summary>
    public const string GEOMETRY_OUT_OF_BOUNDS = "GEOMETRY_OUT_OF_BOUNDS";

    /// <summary>
    /// Geometry type is not supported for the operation.
    /// </summary>
    public const string UNSUPPORTED_GEOMETRY_TYPE = "UNSUPPORTED_GEOMETRY_TYPE";

    /// <summary>
    /// Geometry transformation failed.
    /// </summary>
    public const string GEOMETRY_TRANSFORMATION_FAILED = "GEOMETRY_TRANSFORMATION_FAILED";

    /// <summary>
    /// Geometry serialization failed.
    /// </summary>
    public const string GEOMETRY_SERIALIZATION_FAILED = "GEOMETRY_SERIALIZATION_FAILED";

    // ========================================
    // Serialization Error Codes (SERIALIZATION_*)
    // ========================================

    /// <summary>
    /// Feature serialization failed.
    /// </summary>
    public const string FEATURE_SERIALIZATION_FAILED = "FEATURE_SERIALIZATION_FAILED";

    // ========================================
    // Resilience Error Codes (RESILIENCE_*)
    // ========================================

    /// <summary>
    /// Tenant resource limit exceeded.
    /// </summary>
    public const string TENANT_RESOURCE_LIMIT_EXCEEDED = "TENANT_RESOURCE_LIMIT_EXCEEDED";

    /// <summary>
    /// Memory threshold exceeded.
    /// </summary>
    public const string MEMORY_THRESHOLD_EXCEEDED = "MEMORY_THRESHOLD_EXCEEDED";

    /// <summary>
    /// Bulkhead rejected the operation.
    /// </summary>
    public const string BULKHEAD_REJECTED = "BULKHEAD_REJECTED";

    // ========================================
    // Cache Error Codes (CACHE_*)
    // ========================================

    /// <summary>
    /// Generic cache error.
    /// </summary>
    public const string CACHE_ERROR = "CACHE_ERROR";

    /// <summary>
    /// Cache is unavailable.
    /// </summary>
    public const string CACHE_UNAVAILABLE = "CACHE_UNAVAILABLE";

    /// <summary>
    /// Cache key was not found.
    /// </summary>
    public const string CACHE_KEY_NOT_FOUND = "CACHE_KEY_NOT_FOUND";

    /// <summary>
    /// Cache write operation failed.
    /// </summary>
    public const string CACHE_WRITE_FAILED = "CACHE_WRITE_FAILED";

    /// <summary>
    /// Cache invalidation failed.
    /// </summary>
    public const string CACHE_INVALIDATION_FAILED = "CACHE_INVALIDATION_FAILED";

    // ========================================
    // Raster Error Codes (RASTER_*)
    // ========================================

    /// <summary>
    /// Generic raster processing error.
    /// </summary>
    public const string RASTER_ERROR = "RASTER_ERROR";

    /// <summary>
    /// Raster processing failed.
    /// </summary>
    public const string RASTER_PROCESSING_FAILED = "RASTER_PROCESSING_FAILED";

    /// <summary>
    /// Raster source was not found.
    /// </summary>
    public const string RASTER_SOURCE_NOT_FOUND = "RASTER_SOURCE_NOT_FOUND";

    /// <summary>
    /// Raster format is not supported.
    /// </summary>
    public const string UNSUPPORTED_RASTER_FORMAT = "UNSUPPORTED_RASTER_FORMAT";

    // ========================================
    // Domain Error Codes (DOMAIN_*)
    // ========================================

    /// <summary>
    /// Generic domain rule violation.
    /// </summary>
    public const string DOMAIN_RULE_VIOLATION = "DOMAIN_RULE_VIOLATION";
}
