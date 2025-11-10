// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;

namespace Honua.MapSDK.Services.Routing;

/// <summary>
/// Exception thrown when routing operations fail
/// </summary>
public class RoutingException : Exception
{
    /// <summary>
    /// Error code from routing provider
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Routing engine that threw the error
    /// </summary>
    public string? RoutingEngine { get; set; }

    public RoutingException(string message) : base(message)
    {
    }

    public RoutingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public RoutingException(string message, string errorCode, string routingEngine)
        : base(message)
    {
        ErrorCode = errorCode;
        RoutingEngine = routingEngine;
    }
}
