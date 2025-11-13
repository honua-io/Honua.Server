// <copyright file="AcknowledgeRequest.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Honua.Server.AlertReceiver.Models;

public sealed class AcknowledgeRequest
{
    [Required(ErrorMessage = "Fingerprint is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Fingerprint must be between 1 and 256 characters")]
    public string Fingerprint { get; set; } = string.Empty;

    [Required(ErrorMessage = "AcknowledgedBy is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "AcknowledgedBy must be between 1 and 256 characters")]
    public string AcknowledgedBy { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Comment must be 1000 characters or less")]
    public string? Comment { get; set; }

    [Range(1, 43200, ErrorMessage = "ExpiresInMinutes must be between 1 and 43200 (30 days)")]
    public int? ExpiresInMinutes { get; set; }
}
