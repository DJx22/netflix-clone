using FluentAssertions;
using Moq;
using Subscription.Application.Services;
using Subscription.Domain.Entities;
using Subscription.Domain.Interfaces;
using Subscription.Domain.ValueObjects;

namespace Subscription.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="PlanService"/>.
/// </summary>
public sealed class PlanServiceTests
{
    private readonly Mock<IPlanRepository> _planRepoMock = new();
    private readonly PlanService           _service;

    public PlanServiceTests()
    {
        _service = new PlanService(_planRepoMock.Object);
    }

    [Fact]
    public async Task ListPlansAsync_WithPlans_ReturnsMappedResponses()
    {
        // GET /api/v1/plans returns the full plan list (openapi.yaml).
        var plans = new List<Plan>
        {
            new("basic",   "Basic",   new Money(8.99m),  1, new VideoQuality("SD")),
            new("premium", "Premium", new Money(17.99m), 4, new VideoQuality("4K")),
        };

        _planRepoMock.Setup(r => r.ListAllAsync(default)).ReturnsAsync(plans);

        var result = await _service.ListPlansAsync();

        result.Should().HaveCount(2);
        result.Should().Contain(p => p.PlanId == "basic" && p.Name == "Basic");
        result.Should().Contain(p => p.PlanId == "premium" && p.VideoQuality == "4K");
    }

    [Fact]
    public async Task ListPlansAsync_NoPlansSeed_ReturnsEmptyList()
    {
        _planRepoMock.Setup(r => r.ListAllAsync(default)).ReturnsAsync(new List<Plan>());

        var result = await _service.ListPlansAsync();

        result.Should().BeEmpty();
    }
}
