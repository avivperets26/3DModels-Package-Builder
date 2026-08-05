using System.Net.Http.Headers;
using PackageBuilder.Contracts.Tools;

namespace PackageBuilder.Infrastructure.Tools;

/// <summary>Bounded HTTP transport for caller-consented official metadata refreshes.</summary>
public sealed class HttpReleaseMetadataTransport(HttpClient httpClient) : IOfficialReleaseMetadataTransport
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<ReleaseCatalogResult<OfficialReleaseMetadataPayload>> FetchAsync(
        Uri source,
        int maximumBytes,
        CancellationToken cancellationToken = default)
    {
        if (source is null || !source.IsAbsoluteUri || source.Scheme != Uri.UriSchemeHttps || maximumBytes <= 0)
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseMetadataPayload>(
                "RELEASE_TRANSPORT_REQUEST_INVALID",
                "The official metadata transport request is invalid.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, source);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PackageBuilder", "1.0"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html", 0.8));
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ReleaseCatalogResult.Failure<OfficialReleaseMetadataPayload>(
                    "RELEASE_TRANSPORT_HTTP_FAILURE",
                    "The official metadata source returned an unsuccessful response.");
            }

            Uri effectiveSource = response.RequestMessage?.RequestUri ?? source;
            if (effectiveSource.Scheme != Uri.UriSchemeHttps
                || !string.Equals(effectiveSource.Host, source.Host, StringComparison.OrdinalIgnoreCase)
                || effectiveSource.Port != source.Port)
            {
                return ReleaseCatalogResult.Failure<OfficialReleaseMetadataPayload>(
                    "RELEASE_TRANSPORT_REDIRECT_REJECTED",
                    "The official metadata source redirected outside its approved HTTPS authority.");
            }

            if (response.Content.Headers.ContentLength is > 0 and var contentLength
                && contentLength > maximumBytes)
            {
                return ReleaseCatalogResult.Failure<OfficialReleaseMetadataPayload>(
                    "RELEASE_TRANSPORT_RESPONSE_TOO_LARGE",
                    "The official metadata response exceeds the bounded size.");
            }

            await using Stream sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var destination = new MemoryStream(Math.Min(maximumBytes, 81_920));
            byte[] buffer = new byte[81_920];
            int remaining = maximumBytes + 1;
            while (remaining > 0)
            {
                int read = await sourceStream.ReadAsync(
                    buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                destination.Write(buffer, 0, read);
                remaining -= read;
            }

            return destination.Length > maximumBytes
                ? ReleaseCatalogResult.Failure<OfficialReleaseMetadataPayload>(
                    "RELEASE_TRANSPORT_RESPONSE_TOO_LARGE",
                    "The official metadata response exceeds the bounded size.")
                : ReleaseCatalogResult.Success(
                new OfficialReleaseMetadataPayload(effectiveSource, destination.ToArray()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseMetadataPayload>(
                "RELEASE_CATALOG_CANCELLED",
                "The release metadata refresh was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseMetadataPayload>(
                "RELEASE_TRANSPORT_TIMEOUT",
                "The official metadata request exceeded its configured timeout.");
        }
        catch (HttpRequestException)
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseMetadataPayload>(
                "RELEASE_TRANSPORT_UNAVAILABLE",
                "The official metadata source is unavailable.");
        }
        catch (IOException)
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseMetadataPayload>(
                "RELEASE_TRANSPORT_READ_FAILURE",
                "The official metadata response could not be read safely.");
        }
    }
}
