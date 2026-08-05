using System.Net;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Infrastructure.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class HttpReleaseMetadataTransportTests
{
    private static readonly Uri _source = new("https://official.example/releases");

    [Fact]
    public void ConstructorRejectsNullClient() =>
        _ = Assert.Throws<ArgumentNullException>(() => new HttpReleaseMetadataTransport(null!));

    [Fact]
    public async Task StreamsSuccessfulResponseWithinBound()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(static request =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent([1, 2, 3]),
            }));
        var transport = new HttpReleaseMetadataTransport(client);

        ReleaseCatalogResult<OfficialReleaseMetadataPayload> result = await transport.FetchAsync(
            _source,
            maximumBytes: 3,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2, 3], result.Value!.Contents.ToArray());
        Assert.Equal(_source, result.Value.EffectiveSource);
    }

    [Fact]
    public async Task RejectsDeclaredAndStreamedOversizedResponses()
    {
        using var declaredClient = new HttpClient(new StubHttpMessageHandler(static request =>
        {
            var content = new ByteArrayContent(new byte[4]);
            content.Headers.ContentLength = 4;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = content,
            };
        }));
        using var streamedClient = new HttpClient(new StubHttpMessageHandler(static request =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new UnknownLengthContent([1, 2, 3, 4]),
            }));

        ReleaseCatalogResult<OfficialReleaseMetadataPayload> declared =
            await new HttpReleaseMetadataTransport(declaredClient).FetchAsync(
                _source,
                maximumBytes: 3,
                TestContext.Current.CancellationToken);
        ReleaseCatalogResult<OfficialReleaseMetadataPayload> streamed =
            await new HttpReleaseMetadataTransport(streamedClient).FetchAsync(
                _source,
                maximumBytes: 3,
                TestContext.Current.CancellationToken);

        Assert.Equal("RELEASE_TRANSPORT_RESPONSE_TOO_LARGE", declared.Error!.Code);
        Assert.Equal("RELEASE_TRANSPORT_RESPONSE_TOO_LARGE", streamed.Error!.Code);
    }

    [Fact]
    public async Task RejectsCrossAuthorityRedirectAndUnsuccessfulStatus()
    {
        using var redirectClient = new HttpClient(new StubHttpMessageHandler(static _ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://attacker.invalid/releases"),
                Content = new ByteArrayContent([1]),
            }));
        using var failureClient = new HttpClient(new StubHttpMessageHandler(static request =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                RequestMessage = request,
            }));

        ReleaseCatalogResult<OfficialReleaseMetadataPayload> redirect =
            await new HttpReleaseMetadataTransport(redirectClient).FetchAsync(
                _source,
                maximumBytes: 10,
                TestContext.Current.CancellationToken);
        ReleaseCatalogResult<OfficialReleaseMetadataPayload> failure =
            await new HttpReleaseMetadataTransport(failureClient).FetchAsync(
                _source,
                maximumBytes: 10,
                TestContext.Current.CancellationToken);

        Assert.Equal("RELEASE_TRANSPORT_REDIRECT_REJECTED", redirect.Error!.Code);
        Assert.Equal("RELEASE_TRANSPORT_HTTP_FAILURE", failure.Error!.Code);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("relative")]
    [InlineData("size")]
    public async Task RejectsInvalidRequestsWithoutSending(string scenario)
    {
        var handler = new StubHttpMessageHandler(static _ => throw new InvalidOperationException("Must not send."));
        using var client = new HttpClient(handler);
        var transport = new HttpReleaseMetadataTransport(client);
        Uri source = scenario switch
        {
            "http" => new Uri("http://official.example/releases"),
            "relative" => new Uri("releases", UriKind.Relative),
            _ => _source,
        };

        ReleaseCatalogResult<OfficialReleaseMetadataPayload> result = await transport.FetchAsync(
            source,
            scenario == "size" ? 0 : 10,
            TestContext.Current.CancellationToken);

        Assert.Equal("RELEASE_TRANSPORT_REQUEST_INVALID", result.Error!.Code);
        Assert.Equal(0, handler.CallCount);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class UnknownLengthContent(byte[] contents) : HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) =>
            stream.WriteAsync(contents).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
