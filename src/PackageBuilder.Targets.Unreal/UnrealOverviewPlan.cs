using System.Text.Json;
using PackageBuilder.Domain.Preview;
using PackageBuilder.Domain.Products;

namespace PackageBuilder.Targets.Unreal;

/// <summary>Translates validated static presentation intent into a bounded native overview request.</summary>
public static class UnrealOverviewPlan
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Preserves view order and shared studio tokens; rejects unsupported product cases before engine work.</summary>
    public static string Create(UnrealSurfacePlan surfaces, PreviewPresentationSpecification presentation, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        ArgumentNullException.ThrowIfNull(presentation);
        if (presentation.ProductCase != ProductCase.Static || surfaces.Meshes.Count != 1 ||
            presentation.Views.Count is < 1 or > 32 || presentation.Views.Any(v =>
                v.Kind == PreviewViewKind.Detail || v.Visibility != PreviewVisibility.EntireProduct) ||
            label is not null && (label.Length is < 1 or > 120 || label.Any(char.IsControl)))
        { throw new ArgumentException("Static overview requires one product mesh, standard views and an optional bounded label."); }
        foreach (PreviewViewDefinition view in presentation.Views)
        { UnrealImportPlan.ValidateName(view.Id.Value); }
        if (presentation.Views.Select(v => v.Id.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() != presentation.Views.Count)
        { throw new ArgumentException("View output identities collide on Windows.", nameof(presentation)); }
        PreviewBackground background = presentation.Background;
        return background.Radius is < .01 or > 10 || background.HorizontalScale is < .01 or > 10 ||
            presentation.Lighting.KeyLight.Intensity > 100 || presentation.Lighting.FillLight.Intensity > 100
            ? throw new ArgumentException("Presentation exceeds the native overview bounds.", nameof(presentation))
            : JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                profile = "unreal-overview-v1",
                projectName = surfaces.ProjectName,
                meshId = surfaces.Meshes[0].Id,
                label,
                width = 1920,
                height = 1080,
                fieldOfView = PreviewPresentationDefaults.PerspectiveFieldOfViewDegrees,
                padding = PreviewPresentationDefaults.FramingPadding,
                views = presentation.Views.Select(v => new { id = v.Id.Value, kind = v.Kind.CanonicalIdentifier }).ToArray(),
                background = new
                {
                    outer = Colour(background.OuterColour),
                    centre = Colour(background.CentreColour),
                    background.CentreX,
                    background.CentreY,
                    background.Radius,
                    background.HorizontalScale
                },
                lights = new[] { Light(presentation.Lighting.KeyLight), Light(presentation.Lighting.FillLight) }
            }, _jsonOptions);
    }

    private static double[] Colour(PreviewColour colour) => [colour.Red, colour.Green, colour.Blue];
    private static object Light(PreviewDirectionalLight light) => new
    { light.YawDegrees, light.PitchDegrees, light.Intensity, colour = Colour(light.Colour) };
}
