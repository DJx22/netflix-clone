using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Streaming.Domain.Entities;
using Streaming.Infrastructure.Persistence.Repositories;

namespace Streaming.Tests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="PlaybackPositionRepository"/> against a real
/// SQL Server container.
/// §14: "Infrastructure gets integration tests against a real (containerised) database,
/// not mocks."
/// <para>
/// Each test uses a unique <c>(TitleId, ProfileId)</c> pair (random values) to avoid
/// state bleed between tests.  When a test needs multiple contexts (load → mutate →
/// verify), it uses separate <see cref="StreamingDbContext"/> instances to ensure
/// persistence actually occurred — not just that in-memory change tracking remembered
/// the mutation.
/// </para>
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlaybackPositionRepositoryTests
{
    private readonly DatabaseFixture _fixture;

    public PlaybackPositionRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // ── UpsertAsync / FindAsync (new row) ─────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_NewPosition_CanBeFoundByCompositeKey()
    {
        // Arrange
        var titleId   = $"title-{Guid.NewGuid()}";
        var profileId = Guid.NewGuid();
        var position  = new PlaybackPosition(titleId, profileId, 300, DateTime.UtcNow);

        // Act — upsert in context A
        await using var upsertCtx = _fixture.CreateContext();
        var repo = new PlaybackPositionRepository(upsertCtx);
        await repo.UpsertAsync(position);

        // Assert — find in context B proves the row was persisted, not just tracked
        await using var findCtx = _fixture.CreateContext();
        var found = await new PlaybackPositionRepository(findCtx).FindAsync(titleId, profileId);

        found.Should().NotBeNull();
        found!.TitleId.Should().Be(titleId);
        found.ProfileId.Should().Be(profileId);
        found.PositionSeconds.Should().Be(300);
    }

    // ── UpsertAsync (update existing row) — composite key uniqueness proof ────

    [Fact]
    public async Task UpsertAsync_ExistingPosition_UpdatesRatherThanDuplicates()
    {
        // Arrange — insert the initial row.
        var titleId   = $"title-{Guid.NewGuid()}";
        var profileId = Guid.NewGuid();
        var initial   = new PlaybackPosition(titleId, profileId, 100, DateTime.UtcNow.AddMinutes(-5));

        await using var seedCtx = _fixture.CreateContext();
        await new PlaybackPositionRepository(seedCtx).UpsertAsync(initial);

        // Act — load in a new context, mutate, upsert again.
        await using var updateCtx = _fixture.CreateContext();
        var loaded = await new PlaybackPositionRepository(updateCtx).FindAsync(titleId, profileId);
        loaded!.UpdatePosition(750, DateTime.UtcNow);
        await new PlaybackPositionRepository(updateCtx).UpsertAsync(loaded);

        // Assert — exactly one row, updated value, not a second row.
        await using var verifyCtx = _fixture.CreateContext();
        var allRows = await verifyCtx.PlaybackPositions
            .Where(p => p.TitleId == titleId && p.ProfileId == profileId)
            .ToListAsync();

        allRows.Should().HaveCount(1,
            because: "composite key must prevent duplicate rows for the same TitleId/ProfileId pair");
        allRows[0].PositionSeconds.Should().Be(750);
    }

    // ── FindAsync — unknown composite key ─────────────────────────────────────

    [Fact]
    public async Task FindAsync_UnknownCompositeKey_ReturnsNull()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await new PlaybackPositionRepository(ctx)
            .FindAsync($"title-{Guid.NewGuid()}", Guid.NewGuid());

        result.Should().BeNull();
    }

    // ── UTC kind round-trip ───────────────────────────────────────────────────

    [Fact]
    public async Task FindAsync_RoundTrip_UpdatedAtUtcHasUtcKind()
    {
        // PlaybackPositionConfiguration re-asserts UTC kind after SQL Server strips it.
        var titleId   = $"title-{Guid.NewGuid()}";
        var profileId = Guid.NewGuid();
        var position  = new PlaybackPosition(titleId, profileId, 60,
            new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        await using var seedCtx = _fixture.CreateContext();
        await new PlaybackPositionRepository(seedCtx).UpsertAsync(position);

        await using var verifyCtx = _fixture.CreateContext();
        var loaded = await new PlaybackPositionRepository(verifyCtx).FindAsync(titleId, profileId);

        loaded!.UpdatedAtUtc.Kind.Should().Be(DateTimeKind.Utc,
            because: "PlaybackPositionConfiguration must restore DateTimeKind.Utc after the SQL Server round-trip");
    }
}
