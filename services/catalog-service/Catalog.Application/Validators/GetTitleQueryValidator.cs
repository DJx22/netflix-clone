using Catalog.Application.Queries;
using FluentValidation;

namespace Catalog.Application.Validators;

/// <summary>
/// Validates <see cref="GetTitleQuery"/> before the handler runs.
/// </summary>
public sealed class GetTitleQueryValidator : AbstractValidator<GetTitleQuery>
{
    public GetTitleQueryValidator()
    {
        RuleFor(q => q.TitleId)
            .NotEmpty();
    }
}
