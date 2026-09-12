using System.Net;
using FluentAssertions;
using Streaming.Application.Abstractions;
using Streaming.Infrastructure.ExternalClients;

namespace Streaming.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="CatalogHttpClient"/>.
/// Uses a <see cref="FakeMessageHandler"/> to return canned HTTP responses without
/// any network calls or mock frameworks — the idiomatic pattern for testing typed
/// <c>HttpClient</c> wrappers in .NET.
/// §14: one logical assertion focus per test.
/// </summary>
public sealed class CatalogHttpClientTests
{
    private static CatalogHttpClient BuildClient(HttpMessageHandler handler, string baseUrl = "http://catalog/")
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        return new CatalogHttpClient(httpClient);
    }

    // ── HTTP 200 — title found ────────────────────────────────────────────────

    [Fact]
    public async Task CheckTitleExistsAsync_CatalogReturns200_ReturnsFound()
    {
        // Arrange
        var handler = new FakeMessageHandler(HttpStatusCode.OK);
        var client  = BuildClient(handler);

        // Act
        var result = await client.CheckTitleExistsAsync("title-xyz");

        // Assert — CatalogTitleCheckResult is a struct; use the boolean property.
        result.IsFound.Should().BeTrue();
    }

    // ── HTTP 404 — title not found ────────────────────────────────────────────

    [Fact]
    public async Task CheckTitleExistsAsync_CatalogReturns404_ReturnsNotFound()
    {
        // Arrange
        var handler = new FakeMessageHandler(HttpStatusCode.NotFound);
        var client  = BuildClient(handler);

        // Act
        var result = await client.CheckTitleExistsAsync("title-xyz");

        // Assert
        result.IsNotFound.Should().BeTrue();
    }

    // ── Unexpected HTTP status — dependency failure ────────────────────────────

    [Fact]
    public async Task CheckTitleExistsAsync_CatalogReturns503_ReturnsDependencyFailure()
    {
        // Assert — any non-200/404 status must be treated as a dependency failure
        // so the Streaming service never surfaces a 5xx from Catalog through its own API.
        var handler = new FakeMessageHandler(HttpStatusCode.ServiceUnavailable);
        var client  = BuildClient(handler);

        // Act
        var result = await client.CheckTitleExistsAsync("title-xyz");

        // Assert
        result.IsDependencyFailure.Should().BeTrue();
    }

    // ── Network failure — dependency failure ──────────────────────────────────

    [Fact]
    public async Task CheckTitleExistsAsync_NetworkFailure_ReturnsDependencyFailure()
    {
        // Arrange — simulates a connection refused / timeout scenario.
        var handler = new ThrowingMessageHandler(new HttpRequestException("Connection refused"));
        var client  = BuildClient(handler);

        // Act
        var result = await client.CheckTitleExistsAsync("title-xyz");

        // Assert — HttpRequestException must be swallowed and surfaced as DependencyFailure.
        result.IsDependencyFailure.Should().BeTrue();
    }

    // ── Correct TitleId in request URI ────────────────────────────────────────

    [Fact]
    public async Task CheckTitleExistsAsync_SendsCorrectTitleIdInUri()
    {
        // Arrange — capture the outbound request so we can inspect its URI.
        var capturingHandler = new CapturingMessageHandler(HttpStatusCode.OK);
        var client           = BuildClient(capturingHandler);
        const string titleId = "my-title-123";

        // Act
        await client.CheckTitleExistsAsync(titleId);

        // Assert — the URI must contain the exact title ID (URL-encoded if needed).
        capturingHandler.CapturedRequest.Should().NotBeNull();
        var requestUri = capturingHandler.CapturedRequest!.RequestUri!.ToString();
        requestUri.Should().Contain(Uri.EscapeDataString(titleId),
            because: "the client must embed the TitleId in the Catalog request URI");
    }

    // ── Fake HttpMessageHandlers ──────────────────────────────────────────────

    /// <summary>Returns a fixed <see cref="HttpStatusCode"/> for every request.</summary>
    private sealed class FakeMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public FakeMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_statusCode));
    }

    /// <summary>Throws an <see cref="HttpRequestException"/> for every request.</summary>
    private sealed class ThrowingMessageHandler : HttpMessageHandler
    {
        private readonly HttpRequestException _exception;

        public ThrowingMessageHandler(HttpRequestException exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw _exception;
    }

    /// <summary>Captures the outbound request and returns a fixed status code.</summary>
    private sealed class CapturingMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public HttpRequestMessage? CapturedRequest { get; private set; }

        public CapturingMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }
}
