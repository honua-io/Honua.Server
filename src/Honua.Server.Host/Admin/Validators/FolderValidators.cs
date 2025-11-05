// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentValidation;
using Honua.Server.Host.Admin.Models;

namespace Honua.Server.Host.Admin.Validators;

/// <summary>
/// Validator for CreateFolderRequest.
/// </summary>
public sealed class CreateFolderRequestValidator : AbstractValidator<CreateFolderRequest>
{
    public CreateFolderRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Folder ID is required")
            .MaximumLength(100)
            .WithMessage("Folder ID must not exceed 100 characters")
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Folder ID must contain only alphanumeric characters, hyphens, and underscores");

        RuleFor(x => x.Title)
            .MaximumLength(200)
            .WithMessage("Folder title must not exceed 200 characters")
            .When(x => x.Title is not null);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Order must be a non-negative integer")
            .When(x => x.Order.HasValue);
    }
}

/// <summary>
/// Validator for UpdateFolderRequest.
/// </summary>
public sealed class UpdateFolderRequestValidator : AbstractValidator<UpdateFolderRequest>
{
    public UpdateFolderRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(200)
            .WithMessage("Folder title must not exceed 200 characters")
            .When(x => x.Title is not null);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Order must be a non-negative integer")
            .When(x => x.Order.HasValue);
    }
}
