using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Streaming.Application.Abstractions;
using Streaming.Application.DTOs;
using Streaming.Application.Results;
using Streaming.Application.Services;
using Streaming.Domain.Entities;
using Streaming.Domain.Interfaces;

namespace Streaming.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="PlaybackService"/>.
/// All dependencies are mocked — no database, no real clock, no real HTTP client.
/// The point is to verify orchestration: the service calls repositories in the right
/// order, delegates Catalog only after a local miss, and never manufactures a
/// <see cref="MediaResponse"/> from Catalog data.
/// </summary>
public sealed class PlaybackServiceTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────

    private readonly Mock<IMediaAssetRepository>       _mediaRepoMock    = new();
    private readonly Mock<IPlaybackPositionRepository> _positionRepoMock = new();
    private readonly Mock<ICatalogClient>              _catalogMock      = new();
    private readonly Mock<IDateTimeProvider>           _clockMock        = new();
    private readonly PlaybackService                   _service;

    private static readonly string   TitleId   = "title-abc";
    private static readonly Guid     ProfileId = Guid.NewGuid();
    private static readonly DateTime UtcNow    = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public PlaybackServiceTests()
    {
        _clockMock.Setup(c => c.UtcNow).Returns(UtcNow);

        _service = new PlaybackService(
            _mediaRepoMock.Object,
            _positionRepoMock.Object,
            _catalogMock.Object,
            _clockMock.Object,
            NullLogger<PlaybackService>.Instance);
    }

    // ── GetMediaAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMediaAsync_MediaAssetExists_ReturnsFoundResult()
    {
        // Arrange
        var asset = new MediaAsset(TitleId, "https://cdn.example.com/a.mp4", "video/mp4", 3600);
        _mediaRepoMock
            .Setup(r => r.FindByTitleIdAsync(TitleId, default))
            .ReturnsAsync(asset);

        // Act
        var result = await _service.GetMediaAsync(TitleId);

        // Assert
        result.Should().BeOfType<GetMediaResult.Found>();
        var found = (GetMediaResult.Found)result;
        found.Response.TitleId.Should().Be(TitleId);
        found.Response.MediaUrl.Should().Be("https://cdn.example.com/a.mp4");
        found.Response.ContentType.Should().Be("video/mp4");
        found.Response.DurationSeconds.Should().Be(3600);
    }

    [Fact]
    public async Task GetMediaAsync_MediaAssetExists_CatalogIsNeverCalled()
    {
        // Arrange — local hit: Catalog must not be consulted.
        var asset = new MediaAsset(TitleId, "https://cdn.example.com/a.mp4", "video/mp4", 3600);
        _mediaRepoMock
            .Setup(r => r.FindByTitleIdAsync(TitleId, default))
            .ReturnsAsync(asset);

        // Act
        await _service.GetMediaAsync(TitleId);

        // Assert — ownership rule: Catalog is only called after a local miss.
        _catalogMock.Verify(
            c => c.CheckTitleExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetMediaAsync_MediaAssetNotFound_CatalogIsCalled()
    {
        // Arrange — local miss: Catalog diagnostic check must be triggered.
        _mediaRepoMock
            .Setup(r => r.FindByTitleIdAsync(TitleId, default))
            .ReturnsAsync((MediaAsset?)null);
        _catalogMock
            .Setup(c => c.CheckTitleExistsAsync(TitleId, default))
            .ReturnsAsync(CatalogTitleCheckResult.NotFound());

        // Act
        await _service.GetMediaAsync(TitleId);

        // Assert
        _catalogMock.Verify(
            c => c.CheckTitleExistsAsync(TitleId, default),
            Times.Once);
    }

    [Fact]
    public async Task GetMediaAsync_MediaAssetNotFound_CatalogFoundTitle_ReturnsNotFoundResult()
    {
        // Arrange — Catalog says title exists but Streaming has no MediaAsset.
        _mediaRepoMock
            .Setup(r => r.FindByTitleIdAsync(TitleId, default))
            .ReturnsAsync((MediaAsset?)null);
        _catalogMock
            .Setup(c => c.CheckTitleExistsAsync(TitleId, default))
            .ReturnsAsync(CatalogTitleCheckResult.Found());

        // Act
        var result = await _service.GetMediaAsync(TitleId);

        // Assert — Catalog Found must never manufacture a Streaming MediaResponse.
        result.Should().BeOfType<GetMediaResult.NotFound>(
            because: "StreamingDb is the sole source of truth for playable media; " +
                     "a Catalog hit after a local miss must never produce a GetMediaResult.Found");
    }

    [Fact]
    public async Task GetMediaAsync_CatalogFoundTitle_NeverManufacturesMediaResponse()
    {
        // Ownership proof (ADR 0006 §3): this test exists to make the invariant
        // explicit and machine-checked. A Catalog Found result must never result in
        // a GetMediaResult.Found — that would mean Catalog is acting as a fallback
        // media source, which violates the service boundary.

        // Arrange
        _mediaRepoMock
            .Setup(r => r.FindByTitleIdAsync(TitleId, default))
            .ReturnsAsync((MediaAsset?)null);
        _catalogMock
            .Setup(c => c.CheckTitleExistsAsync(TitleId, default))
            .ReturnsAsync(CatalogTitleCheckResult.Found());

        // Act
        var result = await _service.GetMediaAsync(TitleId);

        // Assert
        result.Should().NotBeOfType<GetMediaResult.Found>(
            because: "Catalog existence is diagnostic; it cannot produce a MediaResponse");
    }

    [Fact]
    public async Task GetMediaAsync_MediaAssetNotFound_CatalogNotFoundTitle_ReturnsNotFoundResult()
    {
        // Arrange
        _mediaRepoMock
            .Setup(r => r.FindByTitleIdAsync(TitleId, default))
            .ReturnsAsync((MediaAsset?)null);
        _catalogMock
            .Setup(c => c.CheckTitleExistsAsync(TitleId, default))
            .ReturnsAsync(CatalogTitleCheckResult.NotFound());

        // Act
        var result = await _service.GetMediaAsync(TitleId);

        // Assert
        result.Should().BeOfType<GetMediaResult.NotFound>();
    }

    [Fact]
    public async Task GetMediaAsync_MediaAssetNotFound_CatalogDependencyFailure_ReturnsNotFoundResult()
    {
        // Arrange — Catalog is unreachable; the result must still be NotFound, not thrown.
        _mediaRepoMock
            .Setup(r => r.FindByTitleIdAsync(TitleId, default))
            .ReturnsAsync((MediaAsset?)null);
        _catalogMock
            .Setup(c => c.CheckTitleExistsAsync(TitleId, default))
            .ReturnsAsync(CatalogTitleCheckResult.DependencyFailure());

        // Act
        var act = async () => await _service.GetMediaAsync(TitleId);

        // Assert — dependency failure must not propagate through the API path as an exception.
        await act.Should().NotThrowAsync();
        var result = await _service.GetMediaAsync(TitleId);
        result.Should().BeOfType<GetMediaResult.NotFound>();
    }

    // ── GetPositionAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPositionAsync_PositionExists_ReturnsFoundResult()
    {
        // Arrange
        var position = new PlaybackPosition(TitleId, ProfileId, 1800, UtcNow);
        _positionRepoMock
            .Setup(r => r.FindAsync(TitleId, ProfileId, default))
            .ReturnsAsync(position);

        // Act
        var result = await _service.GetPositionAsync(TitleId, ProfileId);

        // Assert
        result.Should().BeOfType<GetPositionResult.Found>();
        var found = (GetPositionResult.Found)result;
        found.Response.TitleId.Should().Be(TitleId);
        found.Response.ProfileId.Should().Be(ProfileId);
        found.Response.PositionSeconds.Should().Be(1800);
    }

    [Fact]
    public async Task GetPositionAsync_PositionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        _positionRepoMock
            .Setup(r => r.FindAsync(TitleId, ProfileId, default))
            .ReturnsAsync((PlaybackPosition?)null);

        // Act
        var result = await _service.GetPositionAsync(TitleId, ProfileId);

        // Assert — maps to 404 at the API layer.
        result.Should().BeOfType<GetPositionResult.NotFound>();
    }

    // ── SavePositionAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SavePositionAsync_ValidRequest_ExistingPosition_CallsUpsertWithUpdatedValues()
    {
        // Arrange — position already exists; service must call UpdatePosition then upsert.
        var existingPosition = new PlaybackPosition(TitleId, ProfileId, 500, UtcNow.AddMinutes(-10));
        var request          = new SavePositionRequest(ProfileId, PositionSeconds: 750);

        _positionRepoMock
            .Setup(r => r.FindAsync(TitleId, ProfileId, default))
            .ReturnsAsync(existingPosition);
        _positionRepoMock
            .Setup(r => r.UpsertAsync(existingPosition, default))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SavePositionAsync(TitleId, request);

        // Assert — upsert called once with the mutated entity.
        _positionRepoMock.Verify(r => r.UpsertAsync(existingPosition, default), Times.Once);
        existingPosition.PositionSeconds.Should().Be(750);
        existingPosition.UpdatedAtUtc.Should().Be(UtcNow);
    }

    [Fact]
    public async Task SavePositionAsync_ValidRequest_NewPosition_CallsUpsertWithNewEntity()
    {
        // Arrange — no prior position; service must construct a new PlaybackPosition.
        var request = new SavePositionRequest(ProfileId, PositionSeconds: 60);

        _positionRepoMock
            .Setup(r => r.FindAsync(TitleId, ProfileId, default))
            .ReturnsAsync((PlaybackPosition?)null);
        _positionRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<PlaybackPosition>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SavePositionAsync(TitleId, request);

        // Assert — upsert called once with a freshly constructed entity.
        _positionRepoMock.Verify(
            r => r.UpsertAsync(
                It.Is<PlaybackPosition>(p =>
                    p.TitleId         == TitleId &&
                    p.ProfileId       == ProfileId &&
                    p.PositionSeconds == 60 &&
                    p.UpdatedAtUtc    == UtcNow),
                default),
            Times.Once);
    }
}
