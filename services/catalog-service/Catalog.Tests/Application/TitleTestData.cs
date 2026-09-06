using Catalog.Application.Commands;
using Catalog.Application.DTOs;
using Catalog.Domain.Aggregates;
using Catalog.Domain.ValueObjects;

namespace Catalog.Tests.Application;

/// <summary>
/// Factory helpers shared by all Application-layer unit tests.
/// Centralising test data here means that when the Title constructor signature
/// changes, only this file needs updating (DRY across all handler tests).
/// </summary>
internal static class TitleTestData
{
    internal const string ValidName          = "Test Title";
    internal const string ValidGenre         = "Action";
    internal const int    ValidReleaseYear   = 2020;
    internal const string ValidMaturityRating = "PG-13";
    internal const int    ValidDurationMinutes = 90;

    /// <summary>A minimal valid <see cref="CreateTitleCommand"/>.</summary>
    internal static CreateTitleCommand ValidCreateCommand(
        string? name          = null,
        string[]? genres      = null,
        int? releaseYear      = null,
        string? rating        = null,
        int? durationMinutes  = null) =>
        new(
            name     ?? ValidName,
            Description: null,
            genres   ?? [ValidGenre],
            releaseYear      ?? ValidReleaseYear,
            rating   ?? ValidMaturityRating,
            Cast: [],
            durationMinutes  ?? ValidDurationMinutes,
            PosterUrl: null,
            StreamingAssetId: null);

    /// <summary>A minimal valid in-memory <see cref="Title"/> aggregate.</summary>
    internal static Title ValidTitle(string? id = null) => new(
        id is null ? TitleId.NewId() : new TitleId(id),
        ValidName,
        [new Genre(ValidGenre)],
        ValidReleaseYear,
        new MaturityRating(ValidMaturityRating),
        ValidDurationMinutes);

    /// <summary>A minimal valid <see cref="TitleDetailDto"/>.</summary>
    internal static TitleDetailDto ValidDetailDto(string titleId) => new(
        titleId, ValidName, [ValidGenre], ValidReleaseYear,
        PosterUrl: null, Description: null,
        Cast: [], ValidDurationMinutes, ValidMaturityRating,
        StreamingAssetId: null);
}
