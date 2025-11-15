// <copyright file="IAlertPublisher.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Honua.Server.AlertReceiver.Models;

namespace Honua.Server.AlertReceiver.Services;

public interface IAlertPublisher
{
    Task PublishAsync(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken = default);

    Task<AlertDeliveryResult> PublishWithResultAsync(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken = default);
}
