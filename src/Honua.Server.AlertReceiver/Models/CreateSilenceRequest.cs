// <copyright file="CreateSilenceRequest.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Honua.Server.AlertReceiver.Models;

public sealed class CreateSilenceRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 256 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Matchers are required")]
    [MaxLength(50, ErrorMessage = "Maximum 50 matchers allowed")]
    public Dictionary<string, string> Matchers { get; set; } = new();

    [Required(ErrorMessage = "CreatedBy is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "CreatedBy must be between 1 and 256 characters")]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? StartsAt { get; set; }

    [Required(ErrorMessage = "EndsAt is required")]
    public DateTimeOffset EndsAt { get; set; }

    [StringLength(1000, ErrorMessage = "Comment must be 1000 characters or less")]
    public string? Comment { get; set; }
}
