using Catalog.Application.Commands;
using Catalog.Application.Repositories;
using Catalog.Domain.Exceptions;
using Catalog.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Catalog.Tests.Application.Commands;

/// <summary>
/// Unit tests for <see cref="UpdateTitleCommandHandler"/>.
/// Repository is mocked — tests handler orchestration only, not persistence (§14).
/// </summary>
public sealed class UpdateTitleCommandHandlerTests
{
    private readonly Mock<ICatalogWriteRepository> _writeMock = new();
    private readonly UpdateTitleCommandHandler     _handler;

    public UpdateTitleCommandHandlerTests()
    {
        _handler = new UpdateTitleCommandHandler(_writeMock.Object);
    }

    private static UpdateTitleCommand ValidCommand(string titleId) => new(
        titleId,
        Name: "Updated Name",
        Description: null,
        Genres: ["Drama"],
        ReleaseYear: 2021,
        MaturityRating: "R",
        Cast: [],
        DurationMinutes: 100,
        PosterUrl: null,
        StreamingAssetId: null);

    // ── 404 path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TitleNotFound_ThrowsTitleNotFoundException()
    {
        // Arrange
        var id = "nonexistent-id";
        _writeMock.Setup(r => r.FindByIdAsync(It.IsAny<TitleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Catalog.Domain.Aggregates.Title?)null);

        // Act
        var act = () => _handler.Handle(ValidCommand(id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TitleNotFoundException>();
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TitleExists_CallsUpdateAsync()
    {
        // Arrange
        var existing = TitleTestData.ValidTitle("existing-id");
        _writeMock.Setup(r => r.FindByIdAsync(It.IsAny<TitleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        await _handler.Handle(ValidCommand("existing-id"), CancellationToken.None);

        // Assert
        _writeMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TitleExists_ReturnsDtoWithUpdatedName()
    {
        // Arrange
        var existing = TitleTestData.ValidTitle("existing-id");
        _writeMock.Setup(r => r.FindByIdAsync(It.IsAny<TitleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _handler.Handle(ValidCommand("existing-id"), CancellationToken.None);

        // Assert
        result.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task Handle_TitleExists_TitleIdNotChanged()
    {
        // Arrange — TitleId is immutable; the returned DTO must carry the original ID
        var existing = TitleTestData.ValidTitle("original-id");
        _writeMock.Setup(r => r.FindByIdAsync(It.IsAny<TitleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _handler.Handle(ValidCommand("original-id"), CancellationToken.None);

        // Assert
        result.TitleId.Should().Be("original-id");
    }

    [Fact]
    public async Task Handle_TitleExists_FindByIdCalledBeforeUpdate()
    {
        // Arrange — ordering matters: handler must load before mutating
        var callOrder = new List<string>();
        var existing  = TitleTestData.ValidTitle("id");

        _writeMock.Setup(r => r.FindByIdAsync(It.IsAny<TitleId>(), It.IsAny<CancellationToken>()))
            .Callback<TitleId, CancellationToken>((_, _) => callOrder.Add("find"))
            .ReturnsAsync(existing);

        _writeMock.Setup(r => r.UpdateAsync(It.IsAny<Catalog.Domain.Aggregates.Title>(), It.IsAny<CancellationToken>()))
            .Callback<Catalog.Domain.Aggregates.Title, CancellationToken>((_, _) => callOrder.Add("update"))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(ValidCommand("id"), CancellationToken.None);

        // Assert
        callOrder.Should().ContainInOrder("find", "update");
    }
}
