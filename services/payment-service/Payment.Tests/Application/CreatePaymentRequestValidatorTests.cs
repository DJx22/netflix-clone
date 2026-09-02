using FluentValidation.TestHelper;
using Payment.Application.DTOs;
using Payment.Application.Validators;

namespace Payment.Tests.Application;

/// <summary>
/// Unit tests for <see cref="CreatePaymentRequestValidator"/>.
/// Validates every rule registered in the validator against the CreatePaymentRequest DTO
/// (openapi.yaml: subscriptionId, amount, currency are required; currency is ISO 4217 3-letter).
/// </summary>
public sealed class CreatePaymentRequestValidatorTests
{
    private readonly CreatePaymentRequestValidator _validator = new();

    // ── Baseline: fully valid request ─────────────────────────────────────────

    [Fact]
    public void Validate_ValidRequest_PassesWithNoErrors()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── SubscriptionId ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptySubscriptionId_FailsWithSubscriptionIdError()
    {
        // Arrange
        var request = ValidRequest() with { SubscriptionId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SubscriptionId);
    }

    // ── Amount ────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_NegativeAmount_FailsWithAmountError()
    {
        // Arrange
        var request = ValidRequest() with { Amount = -0.01m };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Validate_ZeroAmount_Passes()
    {
        // Arrange — zero is valid: free-tier / trial charge (Money contract + spec)
        var request = ValidRequest() with { Amount = 0m };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Validate_PositiveAmount_Passes()
    {
        // Arrange
        var request = ValidRequest() with { Amount = 9.99m };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    [Theory]
    [InlineData("9.999")]   // 3 decimal places
    [InlineData("1.001")]   // 3 decimal places
    [InlineData("0.001")]   // 3 decimal places
    public void Validate_AmountWithMoreThanTwoDecimalPlaces_FailsWithAmountError(string rawAmount)
    {
        // Arrange
        var amount = decimal.Parse(rawAmount, System.Globalization.CultureInfo.InvariantCulture);
        var request = ValidRequest() with { Amount = amount };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Amount)
              .WithErrorMessage("Amount must not have more than 2 decimal places.");
    }

    [Theory]
    [InlineData("9.99")]   // exactly 2 dp
    [InlineData("10.00")]  // 2 dp with trailing zero
    [InlineData("100")]    // whole number
    public void Validate_AmountWithAtMostTwoDecimalPlaces_Passes(string rawAmount)
    {
        // Arrange
        var amount = decimal.Parse(rawAmount, System.Globalization.CultureInfo.InvariantCulture);
        var request = ValidRequest() with { Amount = amount };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    // ── Currency ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptyCurrency_FailsWithCurrencyError()
    {
        // Arrange
        var request = ValidRequest() with { Currency = string.Empty };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Theory]
    [InlineData("US")]    // 2 characters — too short
    [InlineData("USDD")]  // 4 characters — too long
    public void Validate_CurrencyWrongLength_FailsWithCurrencyError(string currency)
    {
        // Arrange
        var request = ValidRequest() with { Currency = currency };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Theory]
    [InlineData("1US")]   // leading digit
    [InlineData("U$D")]   // symbol
    [InlineData("U D")]   // space
    public void Validate_CurrencyContainsNonLetters_FailsWithCurrencyError(string currency)
    {
        // Arrange
        var request = ValidRequest() with { Currency = currency };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Theory]
    [InlineData("USD")]   // upper-case — canonical ISO 4217
    [InlineData("usd")]   // lower-case — accepted by validator; normalised by Money
    [InlineData("Eur")]   // mixed-case
    public void Validate_ThreeLetterCurrency_Passes(string currency)
    {
        // Arrange
        var request = ValidRequest() with { Currency = currency };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Currency);
    }

    // ── SimulateFailure (optional, no validation rule) ────────────────────────

    [Fact]
    public void Validate_SimulateFailureNull_Passes()
    {
        // Arrange — null is the spec-default (randomised outcome)
        var request = ValidRequest() with { SimulateFailure = null };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_SimulateFailureExplicit_Passes(bool value)
    {
        // Arrange
        var request = ValidRequest() with { SimulateFailure = value };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CreatePaymentRequest ValidRequest() =>
        new(
            SubscriptionId: Guid.NewGuid(),
            Amount:         9.99m,
            Currency:       "USD",
            SimulateFailure: null);
}
