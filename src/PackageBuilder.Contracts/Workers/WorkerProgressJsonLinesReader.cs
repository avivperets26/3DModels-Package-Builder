using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace PackageBuilder.Contracts.Workers;

/// <summary>
/// Frames and parses worker progress events from a bounded JSON Lines character stream.
/// </summary>
public static class WorkerProgressJsonLinesReader
{
    private const int ReadBufferCharacters = 4_096;

    /// <summary>
    /// Reads worker events incrementally and returns a result for every physical line.
    /// </summary>
    /// <remarks>
    /// Malformed and oversized lines produce structured failures, are not retained, and do not
    /// prevent later lines from being parsed. Both LF and CRLF delimiters are supported, and a
    /// final unterminated line is processed at end of stream.
    /// </remarks>
    /// <param name="reader">The character stream containing one JSON object per line.</param>
    /// <param name="cancellationToken">A token that cancels incremental stream consumption.</param>
    /// <returns>An asynchronous sequence of line-level parse results.</returns>
    public static async IAsyncEnumerable<WorkerProgressJsonLineReadResult> ReadAsync(
        TextReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        char[] buffer = ArrayPool<char>.Shared.Rent(ReadBufferCharacters);
        var line = new StringBuilder();
        long lineNumber = 1;
        bool hasLineContent = false;
        bool lineExceededBound = false;

        try
        {
            while (true)
            {
                int count = await reader.ReadAsync(
                    buffer.AsMemory(0, ReadBufferCharacters),
                    cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
                for (int index = 0; index < count; index++)
                {
                    char character = buffer[index];
                    if (character == '\n')
                    {
                        yield return ParseLine(
                            lineNumber,
                            line,
                            lineExceededBound,
                            isLineFeedTerminated: true);
                        lineNumber++;
                        _ = line.Clear();
                        hasLineContent = false;
                        lineExceededBound = false;
                        continue;
                    }

                    hasLineContent = true;
                    if (lineExceededBound)
                    {
                        continue;
                    }

                    // One extra character is retained so an exact-limit record followed by the
                    // CR in a CRLF delimiter remains distinguishable from an oversized record.
                    if (line.Length <= WorkerProgressEventJson.MaximumInputCharacters)
                    {
                        _ = line.Append(character);
                    }
                    else
                    {
                        lineExceededBound = true;
                    }
                }
            }

            if (hasLineContent || lineExceededBound)
            {
                yield return ParseLine(
                    lineNumber,
                    line,
                    lineExceededBound,
                    isLineFeedTerminated: false);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static WorkerProgressJsonLineReadResult ParseLine(
        long lineNumber,
        StringBuilder line,
        bool lineExceededBound,
        bool isLineFeedTerminated)
    {
        int parsedLength = line.Length;
        if (isLineFeedTerminated && parsedLength > 0 && line[parsedLength - 1] == '\r')
        {
            parsedLength--;
        }

        if (lineExceededBound || parsedLength > WorkerProgressEventJson.MaximumInputCharacters)
        {
            return WorkerProgressJsonLineReadResult.Failure(
                lineNumber,
                WorkerJsonError.LineTooLarge);
        }

        string json = line.ToString(0, parsedLength);
        WorkerJsonDeserializationResult<WorkerProgressEvent> parsed =
            WorkerProgressEventJson.Deserialize(json);
        return parsed.IsSuccessful
            ? WorkerProgressJsonLineReadResult.Success(lineNumber, parsed.Value!)
            : WorkerProgressJsonLineReadResult.Failure(
                lineNumber,
                parsed.Error,
                parsed.Details);
    }
}
