// <copyright file="GenericAlertBatch.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Honua.Server.AlertReceiver.Models;

public sealed class GenericAlertBatch
{
    [JsonPropertyName("alerts")]
    [Required(ErrorMessage = "Alerts array is required")]
    [MaxLength(100, ErrorMessage = "Maximum 100 alerts per batch")]
    public List<GenericAlert> Alerts { get; set; } = new();
}
