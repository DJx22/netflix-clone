using Catalog.Application.DTOs;
using Catalog.Application.Queries;
using Catalog.Application.Repositories;
using FluentAssertions;
using Moq;

namespace Catalog.Tests.Application.Queries;

/// <summary>
/// Unit tests for <see cref="SearchTitlesQueryHandler"/>.
/// Verifies that the handler delegates search/pagination parameters to the repository
/// unchanged — filter and paging logic live in Infrastructure (§14, ADR 0005).
/// </summary>
public sealed class SearchTitlesQueryHandlerTests
{
    private readonly Mock<ICatalogReadRepository> _readMock = new();
    private readonly SearchTitlesQueryHandler     _handler;

    public SearchTitlesQueryHandlerTests()
    {
        _handler = new SearchTitlesQueryHandler(_readMock.Object);
    }

    private static PagedResultDto<TitleSummaryDto> EmptyPage(int page = 1, int pageSize = 20) =>
        new([], page, pageSize, TotalCount: 0);

    // ── Parameter delegation ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SearchAndGenreFilters_PassedToRepositoryUnchanged()
    {
        // Arrange
        _readMock.Setup(r => r.SearchAsync("inception", "Drama", 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPage(2, 10));

        var query = new SearchTitlesQuery("inception", "Drama", Page: 2, PageSize: 10);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert — each filter and pagination param must reach the repository exactly as supplied
        _readMock.Verify(r =>
            r.SearchAsync("inception", "Drama", 2, 10, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NoFilters_PassesNullSearchAndNullGenre()
    {
        // Arrange
        _readMock.Setup(r => r.SearchAsync(null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPage());

        // Act
        await _handler.Handle(new SearchTitlesQuery(null, null, 1, 20), CancellationToken.None);

        // Assert
        _readMock.Verify(r =>
            r.SearchAsync(null, null, 1, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Return value ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RepositoryReturnsPage_HandlerReturnsItUnmodified()
    {
        // Arrange — handler must not alter the paged result from the repository
        var summaries = new List<TitleSummaryDto>
        {
            new("id-1", "Title One", ["Action"], 2020, null),
            new("id-2", "Title Two", ["Drama"],  2021, null)
        };
        var page = new PagedResultDto<TitleSummaryDto>(summaries, 1, 20, TotalCount: 2);

        _readMock.Setup(r => r.SearchAsync(null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        // Act
        var result = await _handler.Handle(new SearchTitlesQuery(null, null, 1, 20), CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
    }
}
