// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentValidation;
using Honua.Server.Host.Admin.Models;

namespace Honua.Server.Host.Admin.Validators;

/// <summary>
/// Validator for CreateLayerRequest.
/// </summary>
public sealed class CreateLayerRequestValidator : AbstractValidator<CreateLayerRequest>
{
    public CreateLayerRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Layer ID is required")
            .MaximumLength(100)
            .WithMessage("Layer ID must not exceed 100 characters")
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Layer ID must contain only alphanumeric characters, hyphens, and underscores");

        RuleFor(x => x.ServiceId)
            .NotEmpty()
            .WithMessage("Service ID is required")
            .MaximumLength(100)
            .WithMessage("Service ID must not exceed 100 characters");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Layer title is required")
            .MaximumLength(200)
            .WithMessage("Layer title must not exceed 200 characters");

        RuleFor(x => x.GeometryType)
            .NotEmpty()
            .WithMessage("Geometry type is required")
            .Must(type => new[] { "POINT", "LINESTRING", "POLYGON", "MULTIPOINT", "MULTILINESTRING", "MULTIPOLYGON", "GEOMETRY", "GEOMETRYCOLLECTION" }
                .Contains(type.ToUpperInvariant()))
            .WithMessage("Geometry type must be one of: POINT, LINESTRING, POLYGON, MULTIPOINT, MULTILINESTRING, MULTIPOLYGON, GEOMETRY, GEOMETRYCOLLECTION");

        RuleFor(x => x.IdField)
            .NotEmpty()
            .WithMessage("ID field is required")
            .MaximumLength(100)
            .WithMessage("ID field must not exceed 100 characters");

        RuleFor(x => x.GeometryField)
            .NotEmpty()
            .WithMessage("Geometry field is required")
            .MaximumLength(100)
            .WithMessage("Geometry field must not exceed 100 characters");

        RuleFor(x => x.DisplayField)
            .MaximumLength(100)
            .WithMessage("Display field must not exceed 100 characters")
            .When(x => x.DisplayField is not null);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters")
            .When(x => x.Description is not null);

        RuleFor(x => x.Crs)
            .NotEmpty()
            .WithMessage("At least one CRS is required")
            .Must(crs => crs.Count <= 10)
            .WithMessage("Maximum 10 CRS values allowed");

        RuleForEach(x => x.Crs)
            .NotEmpty()
            .WithMessage("CRS value cannot be empty")
            .Matches("^EPSG:\\d+$")
            .WithMessage("CRS must be in format 'EPSG:####'");

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
/// Validator for UpdateLayerRequest.
/// </summary>
public sealed class UpdateLayerRequestValidator : AbstractValidator<UpdateLayerRequest>
{
    public UpdateLayerRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Layer title is required")
            .MaximumLength(200)
            .WithMessage("Layer title must not exceed 200 characters");

        RuleFor(x => x.DisplayField)
            .MaximumLength(100)
            .WithMessage("Display field must not exceed 100 characters")
            .When(x => x.DisplayField is not null);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters")
            .When(x => x.Description is not null);

        RuleFor(x => x.Crs)
            .Must(crs => crs.Count <= 10)
            .WithMessage("Maximum 10 CRS values allowed")
            .When(x => x.Crs?.Count > 0);

        RuleForEach(x => x.Crs)
            .NotEmpty()
            .WithMessage("CRS value cannot be empty")
            .Matches("^EPSG:\\d+$")
            .WithMessage("CRS must be in format 'EPSG:####'")
            .When(x => x.Crs?.Count > 0);

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
