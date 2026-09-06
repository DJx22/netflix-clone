using Catalog.Domain.Aggregates;
using Catalog.Domain.Exceptions;
using Catalog.Domain.ValueObjects;
using Catalog.Infrastructure.Persistence;
using FluentAssertions;

namespace Catalog.Tests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="CatalogWriteRepository"/> against a real
/// MongoDB container.  §14: "Infrastructure gets integration tests against a
/// real (containerised) database, not mocks."
///
/// Each test obtains a fresh collection from <see cref="MongoDbFixture.CreateCollection"/>
/// so tests never share state — no cleanup needed between tests.
/// </summary>
[Collection(MongoDbCollection.Name)]
public sealed class CatalogWriteRepositoryTests
{
    private readonly MongoDbFixture _fixture;

    public CatalogWriteRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static Title MakeTitle(string? id = null, string name = "Test Title") => new(
        id is null ? TitleId.NewId() : new TitleId(id),
        name,
        genres: [new Genre("Action")],
        releaseYear: 2020,
        new MaturityRating("PG-13"),
        durationMinutes: 90);

    private CatalogWriteRepository MakeRepo()
        => new(_fixture.CreateCollection());

    // ── AddAsync / FindByIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_NewTitle_CanBeFoundById()
    {
        // Arrange
        var title = MakeTitle();
        var col   = _fixture.CreateCollection();
        var repo  = new CatalogWriteRepository(col);

        // Act
        await repo.AddAsync(title);

        // Assert — use a second repository instance on the same collection to confirm round-trip
        var found = await new CatalogWriteRepository(col).FindByIdAsync(title.TitleId);
        found.Should().NotBeNull();
        found!.TitleId.Should().Be(title.TitleId);
    }

    [Fact]
    public async Task AddAsync_Title_PersistsName()
    {
        // Arrange
        var title = MakeTitle(name: "Interstellar");
        var col   = _fixture.CreateCollection();
        var repo  = new CatalogWriteRepository(col);
        await repo.AddAsync(title);

        // Act
        var found = await new CatalogWriteRepository(col).FindByIdAsync(title.TitleId);

        // Assert — each property persisted independently so mapping failures are localised
        found!.Name.Should().Be("Interstellar");
    }

    [Fact]
    public async Task FindByIdAsync_UnknownId_ReturnsNull()
    {
        // Arrange
        var repo = MakeRepo();

        // Act
        var result = await repo.FindByIdAsync(TitleId.NewId());

        // Assert
        result.Should().BeNull();
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ExistingTitle_PersistsNewName()
    {
        // Arrange
        var title = MakeTitle();
        var col   = _fixture.CreateCollection();
        await new CatalogWriteRepository(col).AddAsync(title);

        title.UpdateMetadata(
            "Updated Name",
            [new Genre("Drama")],
            releaseYear: 2021,
            new MaturityRating("R"),
            durationMinutes: 120);

        // Act
        await new CatalogWriteRepository(col).UpdateAsync(title);

        // Assert — fresh repo instance to confirm persistence, not in-memory change
        var found = await new CatalogWriteRepository(col).FindByIdAsync(title.TitleId);
        found!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateAsync_ExistingTitle_PersistsNewGenres()
    {
        // Arrange
        var title = MakeTitle();
        var col   = _fixture.CreateCollection();
        await new CatalogWriteRepository(col).AddAsync(title);

        title.UpdateMetadata(
            "Same Name",
            [new Genre("Comedy"), new Genre("Family")],
            releaseYear: 2021,
            new MaturityRating("G"),
            durationMinutes: 100);

        // Act
        await new CatalogWriteRepository(col).UpdateAsync(title);

        // Assert
        var found = await new CatalogWriteRepository(col).FindByIdAsync(title.TitleId);
        found!.Genres.Select(g => g.Value).Should().BeEquivalentTo(["Comedy", "Family"]);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingTitle_TitleNoLongerFound()
    {
        // Arrange
        var title = MakeTitle();
        var col   = _fixture.CreateCollection();
        await new CatalogWriteRepository(col).AddAsync(title);

        // Act
        await new CatalogWriteRepository(col).DeleteAsync(title.TitleId);

        // Assert
        var found = await new CatalogWriteRepository(col).FindByIdAsync(title.TitleId);
        found.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_DoesNotThrow()
    {
        // Arrange — MongoDB DeleteOne silently succeeds on zero matches;
        // the handler does the existence check (tested in unit tests).
        // This test confirms the repository method itself doesn't throw.
        var repo = MakeRepo();

        // Act
        var act = () => repo.DeleteAsync(TitleId.NewId());

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── Round-trip mapping ────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_OptionalFields_RoundTripsNullValues()
    {
        // Arrange — optional fields are null; serialization must preserve null, not default strings
        var title = MakeTitle();  // description, posterUrl, streamingAssetId all null
        var col   = _fixture.CreateCollection();
        await new CatalogWriteRepository(col).AddAsync(title);

        // Act
        var found = await new CatalogWriteRepository(col).FindByIdAsync(title.TitleId);

        // Assert
        found!.Description.Should().BeNull();
        found.PosterUrl.Should().BeNull();
        found.StreamingAssetId.Should().BeNull();
    }
}
