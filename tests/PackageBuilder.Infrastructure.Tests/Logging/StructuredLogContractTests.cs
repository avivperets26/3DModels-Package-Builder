using PackageBuilder.Contracts.Logging;

namespace PackageBuilder.Infrastructure.Tests.Logging;

public sealed class StructuredLogContractTests
{
    private static DateTimeOffset Timestamp { get; } =
        new(2026, 8, 4, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ValidEventIsImmutableAndPropertiesUseOrdinalOrder()
    {
        var input = new List<StructuredLogProperty?>
        {
            new("zeta", "last"),
            new("Alpha", "first"),
        };

        StructuredLogResult<StructuredLogEvent> result = StructuredLogEvent.Create(
            Timestamp,
            "Correlation-01",
            "orchestrator",
            "preflight",
            StructuredLogSeverity.Information,
            "Started preflight.",
            input);
        input.Clear();

        Assert.True(result.IsSuccess);
        Assert.Equal(["Alpha", "zeta"], result.Value!.Properties.Select(property => property.Name));
        Assert.Equal(Timestamp, result.Value.TimestampUtc);
    }

    [Theory]
    [InlineData("timestamp")]
    [InlineData("correlation")]
    [InlineData("component")]
    [InlineData("step")]
    [InlineData("severity")]
    [InlineData("message")]
    [InlineData("property")]
    [InlineData("duplicate")]
    [InlineData("property-count")]
    public void InvalidEventsReturnStructuredFailures(string scenario)
    {
        DateTimeOffset timestamp = scenario == "timestamp" ? Timestamp.ToOffset(TimeSpan.FromHours(1)) : Timestamp;
        string correlation = scenario == "correlation" ? "unsafe/id" : "Correlation-01";
        string component = scenario == "component" ? "bad component" : "orchestrator";
        string? step = scenario == "step" ? "bad/step" : "preflight";
        StructuredLogSeverity severity = scenario == "severity" ? (StructuredLogSeverity)999 : StructuredLogSeverity.Warning;
        string message = scenario == "message" ? "line\nbreak" : "Validation warning.";
        IEnumerable<StructuredLogProperty?>? properties = scenario switch
        {
            "property" => [new StructuredLogProperty("bad name", "value")],
            "duplicate" => [new StructuredLogProperty("name", "a"), new StructuredLogProperty("name", "b")],
            "property-count" => Enumerable.Range(0, 65).Select(index => new StructuredLogProperty($"p{index}", "v")),
            _ => null,
        };

        StructuredLogResult<StructuredLogEvent> result = StructuredLogEvent.Create(
            timestamp,
            correlation,
            component,
            step,
            severity,
            message,
            properties);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("LOG_", result.Error!.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe/id", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("_invalid-start")]
    [InlineData("@invalid-start")]
    [InlineData("[invalid-start")]
    [InlineData("`invalid-start")]
    [InlineData("{invalid-start")]
    public void MalformedCorrelationTokensAreRejected(string? correlationId)
    {
        StructuredLogResult<StructuredLogEvent> result = StructuredLogEvent.Create(
            Timestamp,
            correlationId,
            "component",
            "step",
            StructuredLogSeverity.Information,
            "Message.");

        Assert.False(result.IsSuccess);
        Assert.Equal("LOG_CORRELATION_ID_INVALID", result.Error!.Code);
    }

    [Fact]
    public void TokenBoundsAndAllowedPunctuationAreEnforced()
    {
        Assert.True(StructuredLogEvent.Create(
            Timestamp,
            "A0-valid_token.value",
            "component",
            "step",
            StructuredLogSeverity.Information,
            "Message.").IsSuccess);
        Assert.Equal(
            "LOG_CORRELATION_ID_INVALID",
            StructuredLogEvent.Create(
                Timestamp,
                new string('a', 129),
                "component",
                "step",
                StructuredLogSeverity.Information,
                "Message.").Error!.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("line\nbreak")]
    public void MissingWhitespaceOrControlledMessagesAreRejected(string? message)
    {
        StructuredLogResult<StructuredLogEvent> result = StructuredLogEvent.Create(
            Timestamp,
            "Correlation-01",
            "component",
            "step",
            StructuredLogSeverity.Information,
            message);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOG_MESSAGE_INVALID", result.Error!.Code);
    }

    [Fact]
    public void OversizedMessagesAndPropertyValuesAreRejected()
    {
        Assert.Equal(
            "LOG_MESSAGE_INVALID",
            StructuredLogEvent.Create(
                Timestamp,
                "Correlation-01",
                "component",
                "step",
                StructuredLogSeverity.Information,
                new string('m', 16_385)).Error!.Code);
        Assert.Equal(
            "LOG_PROPERTY_INVALID",
            StructuredLogEvent.Create(
                Timestamp,
                "Correlation-01",
                "component",
                "step",
                StructuredLogSeverity.Information,
                "Message.",
                [new StructuredLogProperty("value", new string('v', 8_193))]).Error!.Code);
    }
}
