// <copyright file="RetryPublisherFactory.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Honua.Server.AlertReceiver.Services;

public static class RetryPublisherFactory
{
    public static IAlertPublisher Wrap(
        IAlertPublisher publisher,
        IConfiguration configuration,
        ILogger<RetryAlertPublisher> logger)
    {
        return new RetryAlertPublisher(publisher, configuration, logger);
    }
}
