using Catalog.Domain.Aggregates;
using Catalog.Domain.Exceptions;
using Catalog.Domain.ValueObjects;
using FluentAssertions;

namespace Catalog.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="Title"/> aggregate root.
/// Covers every constructor guard and the <see cref="Title.UpdateMetadata"/> invariants.
/// §14: MethodName_Scenario_ExpectedResult naming; one logical assertion focus per test.
/// </summary>
public sealed class TitleTests
{
    // ── Shared valid values ───────────────────────────────────────────────────

    private static readonly TitleId       ValidId      = TitleId.NewId();
    private static readonly IReadOnlyList<Genre> ValidGenres = [new Genre("Action")];
    private const  string   ValidName    = "Test Title";
    private const  int      ValidYear    = 2020;
    private const  string   ValidRating  = "PG-13";
    private const  int      ValidMinutes = 90;

    private static Title MakeValid() => new(
        ValidId, ValidName, ValidGenres, ValidYear,
        new MaturityRating(ValidRating), ValidMinutes);

    // ── Constructor — TitleId guard ───────────────────────────────────────────

    [Fact]
    public void Constructor_NullTitleId_ThrowsTitleValidationException()
    {
        var act = () => new Title(null!, ValidName, ValidGenres, ValidYear,
            new MaturityRating(ValidRating), ValidMinutes);

        act.Should().Throw<TitleValidationException>();
    }

    // ── Constructor — Name guards ─────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyName_ThrowsTitleValidationException()
    {
        var act = () => new Title(ValidId, "", ValidGenres, ValidYear,
            new MaturityRating(ValidRating), ValidMinutes);

        act.Should().Throw<TitleValidationException>();
    }

    [Fact]
    public void Constructor_WhitespaceName_ThrowsTitleValidationException()
    {
        var act = () => new Title(ValidId, "   ", ValidGenres, ValidYear,
            new MaturityRating(ValidRating), ValidMinutes);

        act.Should().Throw<TitleValidationException>();
    }

    [Fact]
    public void Constructor_NameExceedsMaxLength_ThrowsTitleValidationException()
    {
        var tooLong = new string('x', Title.MaxNameLength + 1);

        var act = () => new Title(ValidId, tooLong, ValidGenres, ValidYear,
            new MaturityRating(ValidRating), ValidMinutes);

        act.Should().Throw<TitleValidationException>();
    }

    [Fact]
    public void Constructor_NameAtMaxLength_DoesNotThrow()
    {
        var atMax = new string('x', Title.MaxNameLength);

        var act = () => new Title(ValidId, atMax, ValidGenres, ValidYear,
            new MaturityRating(ValidRating), ValidMinutes);

        act.Should().NotThrow();
    }

    // ── Constructor — Genres guard ────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyGenreList_ThrowsTitleValidationException()
    {
        var act = () => new Title(ValidId, ValidName, [], ValidYear,
            new MaturityRating(ValidRating), ValidMinutes);

        act.Should().Throw<TitleValidationException>();
    }

    // ── Constructor — ReleaseYear guard ──────────────────────────────────────

    [Fact]
    public void Constructor_ReleaseYearBelowMinimum_ThrowsTitleValidationException()
    {
        var act = () => new Title(ValidId, ValidName, ValidGenres, Title.MinReleaseYear - 1,
            new MaturityRating(ValidRating), ValidMinutes);

        act.Should().Throw<TitleValidationException>();
    }

    [Fact]
    public void Constructor_ReleaseYearInFuture_ThrowsTitleValidationException()
    {
        var act = () => new Title(ValidId, ValidName, ValidGenres, DateTime.UtcNow.Year + 1,
            new MaturityRating(ValidRating), ValidMinutes);

        act.Should().Throw<TitleValidationException>();
    }

    [Fact]
    public void Constructor_ReleaseYearAtMinimum_DoesNotThrow()
    {
        var act = () => new Title(ValidId, ValidName, ValidGenres, Title.MinReleaseYear,
            new MaturityRating(ValidRating), ValidMinutes);

        act.Should().NotThrow();
    }

    // ── Constructor — DurationMinutes guard ───────────────────────────────────

    [Fact]
    public void Constructor_DurationBelowMinimum_ThrowsTitleValidationException()
    {
        var act = () => new Title(ValidId, ValidName, ValidGenres, ValidYear,
            new MaturityRating(ValidRating), Title.MinDurationMinutes - 1);

        act.Should().Throw<TitleValidationException>();
    }

    [Fact]
    public void Constructor_DurationAtMinimum_DoesNotThrow()
    {
        var act = () => new Title(ValidId, ValidName, ValidGenres, ValidYear,
            new MaturityRating(ValidRating), Title.MinDurationMinutes);

        act.Should().NotThrow();
    }

    // ── Constructor — MaturityRating guard ───────────────────────────────────

    [Fact]
    public void Constructor_NullMaturityRating_ThrowsTitleValidationException()
    {
        var act = () => new Title(ValidId, ValidName, ValidGenres, ValidYear,
            null!, ValidMinutes);

        act.Should().Throw<TitleValidationException>();
    }

    // ── Constructor — happy path ──────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_SetsAllProperties()
    {
        var title = new Title(
            ValidId, "  Inception  ", ValidGenres, ValidYear,
            new MaturityRating(ValidRating), ValidMinutes,
            description: "A dream film",
            cast: ["Leonardo DiCaprio"],
            posterUrl: "https://example.com/poster.jpg",
            streamingAssetId: "asset-1");

        title.TitleId.Should().Be(ValidId);
        title.Name.Should().Be("Inception");           // trimmed
        title.Genres.Should().HaveCount(1);
        title.ReleaseYear.Should().Be(ValidYear);
        title.MaturityRating.Value.Should().Be(ValidRating);
        title.DurationMinutes.Should().Be(ValidMinutes);
        title.Description.Should().Be("A dream film");
        title.Cast.Should().ContainSingle(c => c == "Leonardo DiCaprio");
        title.PosterUrl.Should().Be("https://example.com/poster.jpg");
        title.StreamingAssetId.Should().Be("asset-1");
    }

    [Fact]
    public void Constructor_NullCast_DefaultsToEmptyList()
    {
        var title = MakeValid();
        title.Cast.Should().BeEmpty();
    }

    // ── UpdateMetadata ────────────────────────────────────────────────────────

    [Fact]
    public void UpdateMetadata_ValidArgs_UpdatesAllMutableProperties()
    {
        var title    = MakeValid();
        var newGenres = new List<Genre> { new("Comedy") }.AsReadOnly();

        title.UpdateMetadata(
            "New Name", newGenres, 2022,
            new MaturityRating("R"), 120,
            description: "Updated",
            cast: ["Actor"],
            posterUrl: "https://new.com/img.jpg",
            streamingAssetId: "new-asset");

        title.Name.Should().Be("New Name");
        title.Genres.Should().ContainSingle(g => g.Value == "Comedy");
        title.ReleaseYear.Should().Be(2022);
        title.MaturityRating.Value.Should().Be("R");
        title.DurationMinutes.Should().Be(120);
        title.Description.Should().Be("Updated");
        title.Cast.Should().ContainSingle(c => c == "Actor");
    }

    [Fact]
    public void UpdateMetadata_TitleIdNotChanged_TitleIdRemainsOriginal()
    {
        // TitleId is immutable — UpdateMetadata must not expose a path to change it.
        var title      = MakeValid();
        var originalId = title.TitleId;
        var newGenres  = new List<Genre> { new("Drama") }.AsReadOnly();

        title.UpdateMetadata("New Name", newGenres, 2022, new MaturityRating("R"), 100);

        title.TitleId.Should().Be(originalId);
    }

    [Fact]
    public void UpdateMetadata_EmptyName_ThrowsTitleValidationException()
    {
        var title    = MakeValid();
        var newGenres = new List<Genre> { new("Drama") }.AsReadOnly();

        var act = () => title.UpdateMetadata("", newGenres, 2022, new MaturityRating("R"), 100);

        act.Should().Throw<TitleValidationException>();
    }
}
