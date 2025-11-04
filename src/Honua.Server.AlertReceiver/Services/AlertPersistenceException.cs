// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.AlertReceiver.Services;

public sealed class AlertPersistenceException : Exception
{
    public AlertPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
