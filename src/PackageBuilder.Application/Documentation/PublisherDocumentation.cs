using System.Globalization;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Application.Documentation;

/// <summary>Canonical publisher prose. Uses the configured year policy, never the ambient clock.</summary>
public static class PublisherDocumentation
{
    public static string AiText(AiDisclosure disclosure)
    {
        string state = disclosure.State.Equals(AiDisclosureState.Undeclared) ? "Undeclared" :
            disclosure.State.Equals(AiDisclosureState.NoAiAssistance) ? "No AI assistance declared" : "AI-assisted";
        return disclosure.Text is null ? state : $"{state}: {disclosure.Text}";
    }

    public static string CopyrightYears(CopyrightYearPolicy policy) => policy.StartYear.HasValue ?
        $"{policy.StartYear.Value.ToString(CultureInfo.InvariantCulture)}-{policy.Year.ToString(CultureInfo.InvariantCulture)}" :
        policy.Year.ToString(CultureInfo.InvariantCulture);

    internal static IEnumerable<(string Heading, string[] Lines)> Sections(PublisherProfile publisher)
    {
        yield return ("AI disclosure", [AiText(publisher.AiDisclosure)]);
        yield return ("Support", [$"{(publisher.SupportContact.Kind == SupportContactKind.Email ? "Email" : "URL")}: {publisher.SupportContact.Value}"]);
        yield return ("Copyright", [$"Copyright © {CopyrightYears(publisher.Copyright.YearPolicy)} {publisher.Copyright.Holder.Value}"]);
    }
}
