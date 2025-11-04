// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentValidation;
using Honua.Server.Host.Admin.Models;

namespace Honua.Server.Host.Admin.Validators;

/// <summary>
/// Validator for CreateServiceRequest.
/// </summary>
public sealed class CreateServiceRequestValidator : AbstractValidator<CreateServiceRequest>
{
    public CreateServiceRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Service ID is required")
            .MaximumLength(100)
            .WithMessage("Service ID must not exceed 100 characters")
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Service ID must contain only alphanumeric characters, hyphens, and underscores");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Service title is required")
            .MaximumLength(200)
            .WithMessage("Service title must not exceed 200 characters");

        RuleFor(x => x.FolderId)
            .NotEmpty()
            .WithMessage("Folder ID is required")
            .MaximumLength(100)
            .WithMessage("Folder ID must not exceed 100 characters");

        RuleFor(x => x.ServiceType)
            .NotEmpty()
            .WithMessage("Service type is required")
            .Must(type => new[] { "WMS", "WFS", "WMTS", "OGC_API", "VECTOR_TILES" }.Contains(type.ToUpperInvariant()))
            .WithMessage("Service type must be one of: WMS, WFS, WMTS, OGC_API, VECTOR_TILES");

        RuleFor(x => x.DataSourceId)
            .NotEmpty()
            .WithMessage("Data source ID is required")
            .MaximumLength(100)
            .WithMessage("Data source ID must not exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters")
            .When(x => x.Description is not null);

        RuleFor(x => x.Keywords)
            .Must(keywords => keywords.Count <= 20)
            .WithMessage("Maximum 20 keywords allowed")
            .When(x => x.Keywords is not null);

        RuleForEach(x => x.Keywords)
            .MaximumLength(50)
            .WithMessage("Each keyword must not exceed 50 characters")
            .When(x => x.Keywords is not null);
    }
}

/// <summary>
/// Validator for UpdateServiceRequest.
/// </summary>
public sealed class UpdateServiceRequestValidator : AbstractValidator<UpdateServiceRequest>
{
    public UpdateServiceRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Service title is required")
            .MaximumLength(200)
            .WithMessage("Service title must not exceed 200 characters");

        RuleFor(x => x.FolderId)
            .NotEmpty()
            .WithMessage("Folder ID is required")
            .MaximumLength(100)
            .WithMessage("Folder ID must not exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters")
            .When(x => x.Description is not null);

        RuleFor(x => x.Keywords)
            .Must(keywords => keywords.Count <= 20)
            .WithMessage("Maximum 20 keywords allowed")
            .When(x => x.Keywords is not null);

        RuleForEach(x => x.Keywords)
            .MaximumLength(50)
            .WithMessage("Each keyword must not exceed 50 characters")
            .When(x => x.Keywords is not null);
    }
}
