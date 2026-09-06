using FluentValidation;
using MediatR;

namespace Catalog.Application.Behaviours;

/// <summary>
/// MediatR pipeline behaviour that runs all registered FluentValidation validators
/// for a request before passing it to the handler.
/// </summary>
/// <remarks>
/// Placing validation in a pipeline behaviour rather than inside each handler:
/// (a) removes per-handler boilerplate (§5 SRP — handlers have one job: handle),
/// (b) guarantees validation fires for every request type that has a validator,
///     including future ones, without any additional wiring.
///
/// If validation fails, a <see cref="ValidationException"/> is thrown.  The global
/// exception middleware in <c>Catalog.Api</c> is responsible for mapping this to
/// a <c>ValidationProblemDetails</c> 400 response matching the OpenAPI spec (§10).
///
/// The behaviour does nothing when no validator is registered for <typeparamref name="TRequest"/> —
/// not all requests need validators (e.g. <c>ListGenresQuery</c> has no parameters).
/// </remarks>
public sealed class ValidationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next().ConfigureAwait(false);
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task
            .WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)))
            .ConfigureAwait(false))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next().ConfigureAwait(false);
    }
}
