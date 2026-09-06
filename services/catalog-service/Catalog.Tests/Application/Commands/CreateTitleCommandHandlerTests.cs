using Catalog.Application.Commands;
using Catalog.Application.Repositories;
using FluentAssertions;
using Moq;

namespace Catalog.Tests.Application.Commands;

/// <summary>
/// Unit tests for <see cref="CreateTitleCommandHandler"/>.
/// Repository is mocked — no MongoDB dependency here (§14).
/// §14: MethodName_Scenario_ExpectedResult naming; AAA structure; one logical assertion per test.
/// </summary>
public sealed class CreateTitleCommandHandlerTests
{
    private readonly Mock<ICatalogWriteRepository> _writeMock = new();
    private readonly CreateTitleCommandHandler     _handler;

    public CreateTitleCommandHandlerTests()
    {
        _handler = new CreateTitleCommandHandler(_writeMock.Object);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCommand_CallsAddAsync()
    {
        // Arrange
        var command = TitleTestData.ValidCreateCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — AddAsync called exactly once; the aggregate passed to it is not null
        _writeMock.Verify(r => r.AddAsync(
            It.Is<Catalog.Domain.Aggregates.Title>(t => t != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsDtoWithMatchingName()
    {
        // Arrange
        var command = TitleTestData.ValidCreateCommand(name: "Inception");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Name.Should().Be("Inception");
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsDtoWithNewNonEmptyTitleId()
    {
        // Arrange
        var command = TitleTestData.ValidCreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — handler generates the ID; it must not be blank
        result.TitleId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsDtoWithMatchingGenres()
    {
        // Arrange
        var command = TitleTestData.ValidCreateCommand(genres: ["Action", "Thriller"]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Genres.Should().BeEquivalentTo(["Action", "Thriller"]);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsDtoWithMatchingReleaseYear()
    {
        // Arrange
        var command = TitleTestData.ValidCreateCommand(releaseYear: 2010);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ReleaseYear.Should().Be(2010);
    }
}
