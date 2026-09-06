using Catalog.Application.Queries;
using Catalog.Application.Repositories;
using FluentAssertions;
using Moq;

namespace Catalog.Tests.Application.Queries;

/// <summary>
/// Unit tests for <see cref="GetTitleQueryHandler"/>.
/// Read repository is mocked — no MongoDB dependency (§14).
/// </summary>
public sealed class GetTitleQueryHandlerTests
{
    private readonly Mock<ICatalogReadRepository> _readMock = new();
    private readonly GetTitleQueryHandler         _handler;

    public GetTitleQueryHandlerTests()
    {
        _handler = new GetTitleQueryHandler(_readMock.Object);
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TitleNotFound_ReturnsNull()
    {
        // Arrange
        _readMock.Setup(r => r.FindDetailByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Catalog.Application.DTOs.TitleDetailDto?)null);

        // Act
        var result = await _handler.Handle(new GetTitleQuery("unknown-id"), CancellationToken.None);

        // Assert — null lets the controller decide on 404 (§11)
        result.Should().BeNull();
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TitleExists_ReturnsDtoFromRepository()
    {
        // Arrange
        var dto = TitleTestData.ValidDetailDto("known-id");
        _readMock.Setup(r => r.FindDetailByIdAsync("known-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _handler.Handle(new GetTitleQuery("known-id"), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.TitleId.Should().Be("known-id");
    }

    [Fact]
    public async Task Handle_AnyId_PassesIdToRepository()
    {
        // Arrange
        var dto = TitleTestData.ValidDetailDto("target-id");
        _readMock.Setup(r => r.FindDetailByIdAsync("target-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        await _handler.Handle(new GetTitleQuery("target-id"), CancellationToken.None);

        // Assert — handler must not re-map or transform the ID before calling the repository
        _readMock.Verify(r => r.FindDetailByIdAsync("target-id", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
