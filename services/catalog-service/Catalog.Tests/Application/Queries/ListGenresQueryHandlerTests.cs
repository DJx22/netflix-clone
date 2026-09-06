using Catalog.Application.Queries;
using Catalog.Application.Repositories;
using FluentAssertions;
using Moq;

namespace Catalog.Tests.Application.Queries;

/// <summary>
/// Unit tests for <see cref="ListGenresQueryHandler"/>.
/// </summary>
public sealed class ListGenresQueryHandlerTests
{
    private readonly Mock<ICatalogReadRepository> _readMock = new();
    private readonly ListGenresQueryHandler       _handler;

    public ListGenresQueryHandlerTests()
    {
        _handler = new ListGenresQueryHandler(_readMock.Object);
    }

    [Fact]
    public async Task Handle_RepositoryReturnsGenres_ReturnsThemUnchanged()
    {
        // Arrange
        var genres = new List<string> { "Action", "Comedy", "Drama" };
        _readMock.Setup(r => r.ListDistinctGenresAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(genres);

        // Act
        var result = await _handler.Handle(new ListGenresQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(genres);
    }

    [Fact]
    public async Task Handle_RepositoryReturnsEmpty_ReturnsEmptyList()
    {
        // Arrange
        _readMock.Setup(r => r.ListDistinctGenresAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(new ListGenresQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}
