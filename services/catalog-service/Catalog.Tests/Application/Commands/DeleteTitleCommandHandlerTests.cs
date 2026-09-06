using Catalog.Application.Commands;
using Catalog.Application.Repositories;
using Catalog.Domain.Exceptions;
using Catalog.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using Moq;

namespace Catalog.Tests.Application.Commands;

/// <summary>
/// Unit tests for <see cref="DeleteTitleCommandHandler"/>.
/// Key invariant: handler must throw <see cref="TitleNotFoundException"/> when no
/// title exists, rather than silently succeeding (MongoDB's DeleteOne returns success
/// on zero matches — the handler exists specifically to prevent that silent 204).
/// </summary>
public sealed class DeleteTitleCommandHandlerTests
{
    private readonly Mock<ICatalogWriteRepository> _writeMock = new();
    private readonly DeleteTitleCommandHandler     _handler;

    public DeleteTitleCommandHandlerTests()
    {
        _handler = new DeleteTitleCommandHandler(_writeMock.Object);
    }

    // ── 404 path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TitleNotFound_ThrowsTitleNotFoundException()
    {
        // Arrange
        _writeMock.Setup(r => r.FindByIdAsync(It.IsAny<TitleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Catalog.Domain.Aggregates.Title?)null);

        var command = new DeleteTitleCommand("nonexistent-id");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TitleNotFoundException>();
    }

    [Fact]
    public async Task Handle_TitleNotFound_DeleteAsyncNeverCalled()
    {
        // Arrange — if the title doesn't exist, DeleteAsync must not be called at all
        _writeMock.Setup(r => r.FindByIdAsync(It.IsAny<TitleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Catalog.Domain.Aggregates.Title?)null);

        // Act (ignoring exception)
        try { await _handler.Handle(new DeleteTitleCommand("id"), CancellationToken.None); }
        catch (TitleNotFoundException) { }

        // Assert
        _writeMock.Verify(r => r.DeleteAsync(It.IsAny<TitleId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TitleExists_CallsDeleteAsyncOnce()
    {
        // Arrange
        var existing = TitleTestData.ValidTitle("delete-me");
        _writeMock.Setup(r => r.FindByIdAsync(It.IsAny<TitleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        await _handler.Handle(new DeleteTitleCommand("delete-me"), CancellationToken.None);

        // Assert
        _writeMock.Verify(r => r.DeleteAsync(
            It.Is<TitleId>(tid => tid.Value == "delete-me"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TitleExists_ReturnsUnit()
    {
        // Arrange
        var existing = TitleTestData.ValidTitle("id");
        _writeMock.Setup(r => r.FindByIdAsync(It.IsAny<TitleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _handler.Handle(new DeleteTitleCommand("id"), CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
    }
}
