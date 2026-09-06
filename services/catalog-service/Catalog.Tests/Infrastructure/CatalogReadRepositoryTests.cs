using Catalog.Domain.Aggregates;
using Catalog.Domain.ValueObjects;
using Catalog.Infrastructure.Indexes;
using Catalog.Infrastructure.Persistence;
using FluentAssertions;
using MongoDB.Driver;

namespace Catalog.Tests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="CatalogReadRepository"/> against a real
/// MongoDB container.  §14: "Infrastructure gets integration tests against a
/// real (containerised) database, not mocks."
///
/// Tests that require text search or genre filtering first call
/// <see cref="TitleIndexes.ApplyAsync"/> on the collection — without the indexes,
/// those queries would fail or behave incorrectly, making the index-application
/// test implicit in the search tests.
/// </summary>
[Collection(MongoDbCollection.Name)]
public sealed class CatalogReadRepositoryTests
{
    private readonly MongoDbFixture _fixture;

    public CatalogReadRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static Title MakeTitle(
        string name,
        string[]? genres = null,
        int year = 2020,
        string rating = "PG-13",
        int duration = 90,
        string? description = null) => new(
        TitleId.NewId(),
        name,
        (genres ?? ["Action"]).Select(g => new Genre(g)).ToList().AsReadOnly(),
        year,
        new MaturityRating(rating),
        duration,
        description: description);

    private async Task<(IMongoCollection<Title> col, CatalogReadRepository read, CatalogWriteRepository write)>
        MakeReposAsync(bool applyIndexes = false)
    {
        var col   = _fixture.CreateCollection();
        var write = new CatalogWriteRepository(col);
        var read  = new CatalogReadRepository(col);

        if (applyIndexes)
        {
            await TitleIndexes.ApplyAsync(col);
        }

        return (col, read, write);
    }

    // ── FindDetailByIdAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task FindDetailByIdAsync_UnknownId_ReturnsNull()
    {
        // Arrange
        var (_, read, _) = await MakeReposAsync();

        // Act
        var result = await read.FindDetailByIdAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindDetailByIdAsync_KnownId_ReturnsMatchingDto()
    {
        // Arrange
        var (_, read, write) = await MakeReposAsync();
        var title = MakeTitle("Inception");
        await write.AddAsync(title);

        // Act
        var result = await read.FindDetailByIdAsync(title.TitleId.Value);

        // Assert
        result.Should().NotBeNull();
        result!.TitleId.Should().Be(title.TitleId.Value);
    }

    [Fact]
    public async Task FindDetailByIdAsync_KnownId_ProjectsAllFields()
    {
        // Arrange — projection must carry every field from the stored document
        var title = MakeTitle("The Matrix",
            genres: ["Action", "Sci-Fi"],
            year: 1999,
            rating: "R",
            duration: 136,
            description: "A computer hacker discovers the truth about reality.");

        var (_, read, write) = await MakeReposAsync();
        await write.AddAsync(title);

        // Act
        var result = await read.FindDetailByIdAsync(title.TitleId.Value);

        // Assert — each field in a separate assertion so failures are localised
        result!.Name.Should().Be("The Matrix");
        result.Genres.Should().BeEquivalentTo(["Action", "Sci-Fi"]);
        result.ReleaseYear.Should().Be(1999);
        result.MaturityRating.Should().Be("R");
        result.DurationMinutes.Should().Be(136);
        result.Description.Should().Be("A computer hacker discovers the truth about reality.");
    }

    // ── SearchAsync — pagination ──────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_MultipleDocuments_ReturnsCorrectTotalCount()
    {
        // Arrange
        var (_, read, write) = await MakeReposAsync();
        await write.AddAsync(MakeTitle("Title A"));
        await write.AddAsync(MakeTitle("Title B"));
        await write.AddAsync(MakeTitle("Title C"));

        // Act
        var result = await read.SearchAsync(null, null, page: 1, pageSize: 20);

        // Assert
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task SearchAsync_PageSizeLessThanTotal_ReturnsOnlyPageSizeItems()
    {
        // Arrange
        var (_, read, write) = await MakeReposAsync();
        for (var i = 0; i < 5; i++) await write.AddAsync(MakeTitle($"Title {i}"));

        // Act
        var result = await read.SearchAsync(null, null, page: 1, pageSize: 2);

        // Assert
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_SecondPage_ReturnsDifferentItemsFromFirstPage()
    {
        // Arrange
        var (_, read, write) = await MakeReposAsync();
        for (var i = 0; i < 4; i++) await write.AddAsync(MakeTitle($"Title {i}"));

        // Act
        var page1 = await read.SearchAsync(null, null, page: 1, pageSize: 2);
        var page2 = await read.SearchAsync(null, null, page: 2, pageSize: 2);

        // Assert
        page1.Items.Select(t => t.TitleId).Should()
            .NotIntersectWith(page2.Items.Select(t => t.TitleId));
    }

    [Fact]
    public async Task SearchAsync_PageBeyondTotal_ReturnsEmptyItems()
    {
        // Arrange
        var (_, read, write) = await MakeReposAsync();
        await write.AddAsync(MakeTitle("Only One"));

        // Act
        var result = await read.SearchAsync(null, null, page: 999, pageSize: 20);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(1); // total count is unaffected by current page
    }

    // ── SearchAsync — genre filter ────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_GenreFilter_ReturnsOnlyMatchingGenre()
    {
        // Arrange — indexes required for genre filter to use the index path
        var (_, read, write) = await MakeReposAsync(applyIndexes: true);
        await write.AddAsync(MakeTitle("Action Film", genres: ["Action"]));
        await write.AddAsync(MakeTitle("Drama Film",  genres: ["Drama"]));

        // Act
        var result = await read.SearchAsync(null, "Action", page: 1, pageSize: 20);

        // Assert
        result.Items.Should().ContainSingle(t => t.Name == "Action Film");
        result.Items.Should().NotContain(t => t.Name == "Drama Film");
    }

    [Fact]
    public async Task SearchAsync_GenreFilter_TitleWithMultipleGenresMatchesAnyGenre()
    {
        // Arrange
        var (_, read, write) = await MakeReposAsync(applyIndexes: true);
        await write.AddAsync(MakeTitle("Multi-Genre", genres: ["Action", "Thriller"]));

        // Act — filter by the second genre
        var result = await read.SearchAsync(null, "Thriller", page: 1, pageSize: 20);

        // Assert
        result.Items.Should().ContainSingle(t => t.Name == "Multi-Genre");
    }

    // ── SearchAsync — text search ─────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_TextSearch_ReturnsMatchingTitleByName()
    {
        // Arrange — text index required for $text operator
        var (_, read, write) = await MakeReposAsync(applyIndexes: true);
        await write.AddAsync(MakeTitle("Interstellar"));
        await write.AddAsync(MakeTitle("Inception"));

        // Act
        var result = await read.SearchAsync("Interstellar", null, page: 1, pageSize: 20);

        // Assert
        result.Items.Should().ContainSingle(t => t.Name == "Interstellar");
    }

    // ── ListDistinctGenresAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ListDistinctGenresAsync_MultipleDocuments_ReturnsUniqueGenres()
    {
        // Arrange
        var (_, read, write) = await MakeReposAsync();
        await write.AddAsync(MakeTitle("A", genres: ["Action", "Drama"]));
        await write.AddAsync(MakeTitle("B", genres: ["Drama", "Comedy"]));

        // Act
        var result = await read.ListDistinctGenresAsync();

        // Assert — Action, Drama, Comedy — Drama appears in both documents but only once
        result.Should().BeEquivalentTo(["Action", "Drama", "Comedy"]);
    }

    [Fact]
    public async Task ListDistinctGenresAsync_EmptyCollection_ReturnsEmptyList()
    {
        // Arrange
        var (_, read, _) = await MakeReposAsync();

        // Act
        var result = await read.ListDistinctGenresAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListDistinctGenresAsync_ReturnsGenresInAlphabeticalOrder()
    {
        // Arrange — CatalogReadRepository sorts alphabetically before returning
        var (_, read, write) = await MakeReposAsync();
        await write.AddAsync(MakeTitle("Z", genres: ["Thriller"]));
        await write.AddAsync(MakeTitle("A", genres: ["Action"]));
        await write.AddAsync(MakeTitle("C", genres: ["Comedy"]));

        // Act
        var result = await read.ListDistinctGenresAsync();

        // Assert
        result.Should().BeInAscendingOrder();
    }
}
