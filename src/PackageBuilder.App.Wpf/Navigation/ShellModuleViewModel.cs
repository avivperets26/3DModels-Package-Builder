using System.Collections.ObjectModel;

namespace PackageBuilder.App.Wpf.Navigation;

/// <summary>Describes one safe navigation destination without containing build behavior.</summary>
public sealed class ShellModuleViewModel
{
    public ShellModuleViewModel(
        string route,
        string navigationLabel,
        string accessKeyLabel,
        string glyph,
        string eyebrow,
        string title,
        string description,
        string readiness,
        string nextStep,
        IEnumerable<ShellHighlight> highlights)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentException.ThrowIfNullOrWhiteSpace(navigationLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKeyLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(glyph);
        ArgumentException.ThrowIfNullOrWhiteSpace(eyebrow);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(readiness);
        ArgumentException.ThrowIfNullOrWhiteSpace(nextStep);
        ArgumentNullException.ThrowIfNull(highlights);

        Route = route;
        NavigationLabel = navigationLabel;
        AccessKeyLabel = accessKeyLabel;
        Glyph = glyph;
        Eyebrow = eyebrow;
        Title = title;
        Description = description;
        Readiness = readiness;
        NextStep = nextStep;
        Highlights = new ReadOnlyCollection<ShellHighlight>(highlights.ToArray());
    }

    public string Route { get; }

    public string NavigationLabel { get; }

    public string AccessKeyLabel { get; }

    public string Glyph { get; }

    public string Eyebrow { get; }

    public string Title { get; }

    public string Description { get; }

    public string Readiness { get; }

    public string NextStep { get; }

    public IReadOnlyList<ShellHighlight> Highlights { get; }
}

/// <summary>Provides one concise capability or progress card in a shell module.</summary>
public sealed record ShellHighlight(string Label, string Value, string Detail);
