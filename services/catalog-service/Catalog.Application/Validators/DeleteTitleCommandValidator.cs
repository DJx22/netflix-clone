using Catalog.Application.Commands;
using FluentValidation;

namespace Catalog.Application.Validators;

/// <summary>
/// Validates <see cref="DeleteTitleCommand"/> before the handler runs.
/// </summary>
public sealed class DeleteTitleCommandValidator : AbstractValidator<DeleteTitleCommand>
{
    public DeleteTitleCommandValidator()
    {
        RuleFor(c => c.TitleId)
            .NotEmpty();
    }
}
