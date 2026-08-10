using System.Buffers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Json.Schema;
using PackageBuilder.Contracts.Json;
using PackageBuilder.Domain.Preview;

namespace PackageBuilder.Contracts.Preview;

/// <summary>Strict canonical JSON for the engine-neutral interactive preview contract.</summary>
public static class PreviewExperienceJson
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumInputCharacters = JsonInputSafeguards.MaximumInputCharacters;
    public const string SchemaIdentifier =
        "https://schemas.packagebuilder.dev/preview-experience/v1";
    private const string SchemaResource =
        "PackageBuilder.Contracts.Schemas.preview-experience.schema.json";
    private static readonly Lazy<string> _schemaText = new(ReadSchemaText);
    private static readonly Lazy<JsonSchema> _schema = new(() => JsonSchema.FromText(
        _schemaText.Value,
        new BuildOptions
        {
            Dialect = Dialect.Draft202012,
            SchemaRegistry = new SchemaRegistry(),
        }));

    public static string SchemaText => _schemaText.Value;

    /// <summary>Checks the embedded schema identifier, dialect, and parseability.</summary>
    public static PreviewExperienceSchemaValidationResult ValidateSchemaDefinition()
    {
        try
        {
            using var document = JsonDocument.Parse(SchemaText);
            JsonElement root = document.RootElement;
            bool valid = root.GetProperty("$id").GetString() == SchemaIdentifier &&
                root.GetProperty("$schema").GetString() ==
                "https://json-schema.org/draft/2020-12/schema";
            _ = _schema.Value;
            return new(valid, valid ? null : "The schema identifier or dialect is not approved.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return new(false, exception.Message);
        }
    }

    /// <summary>Serializes properties and ordered collections deterministically.</summary>
    public static PreviewExperienceJsonResult Serialize(PreviewExperienceContract? value)
    {
        if (value is null)
        {
            return PreviewExperienceJsonResult.Failure(PreviewExperienceJsonError.NullValue);
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(writer, value);
        }

        string json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        using var document = JsonDocument.Parse(json);
        EvaluationResults evaluated = _schema.Value.Evaluate(
            document.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.Flag });
        return evaluated.IsValid
            ? PreviewExperienceJsonResult.Success(value, json)
            : PreviewExperienceJsonResult.Failure(PreviewExperienceJsonError.DomainViolation);
    }

    /// <summary>Rejects malformed, duplicate, unknown, or domain-invalid input and canonicalizes success.</summary>
    public static PreviewExperienceJsonResult Deserialize(string? json)
    {
        JsonInputError inputError = JsonInputSafeguards.TryParseObject(
            json,
            MaximumInputCharacters,
            out JsonDocument? document);
        PreviewExperienceJsonError error = inputError switch
        {
            JsonInputError.None => PreviewExperienceJsonError.None,
            JsonInputError.Null => PreviewExperienceJsonError.NullJson,
            JsonInputError.Empty => PreviewExperienceJsonError.EmptyJson,
            JsonInputError.TooLarge => PreviewExperienceJsonError.InputTooLarge,
            JsonInputError.Malformed => PreviewExperienceJsonError.MalformedJson,
            JsonInputError.RootMustBeObject => PreviewExperienceJsonError.RootMustBeObject,
            JsonInputError.DuplicateProperty => PreviewExperienceJsonError.DuplicateProperty,
            _ => PreviewExperienceJsonError.MalformedJson,
        };
        if (error != PreviewExperienceJsonError.None)
        {
            return PreviewExperienceJsonResult.Failure(error);
        }

        using (document!)
        {
            EvaluationResults evaluated = _schema.Value.Evaluate(
                document!.RootElement,
                new EvaluationOptions { OutputFormat = OutputFormat.Flag });
            if (!evaluated.IsValid)
            {
                return PreviewExperienceJsonResult.Failure(
                    PreviewExperienceJsonError.SchemaViolation);
            }

            PreviewExperienceContract? value = Read(document.RootElement);
            return value is null
                ? PreviewExperienceJsonResult.Failure(PreviewExperienceJsonError.DomainViolation)
                : Serialize(value);
        }
    }

    private static PreviewExperienceContract? Read(JsonElement root)
    {
        JsonElement presentation = root.GetProperty("presentation");
        PreviewBackground? background = ReadBackground(presentation.GetProperty("background"));
        PreviewLighting? lighting = ReadLighting(presentation.GetProperty("lighting"));
        JsonElement navigation = root.GetProperty("navigation");
        PreviewNavigationPolicy? navigationPolicy = PreviewNavigationPolicy.Create(
            navigation.GetProperty("minimumPitchDegrees").GetDouble(),
            navigation.GetProperty("maximumPitchDegrees").GetDouble(),
            navigation.GetProperty("minimumDistanceMultiplier").GetDouble(),
            navigation.GetProperty("maximumDistanceMultiplier").GetDouble(),
            navigation.GetProperty("pointerOrbitDegreesPerUnit").GetDouble(),
            navigation.GetProperty("keyboardOrbitStepDegrees").GetDouble(),
            navigation.GetProperty("pointerZoomStep").GetDouble(),
            navigation.GetProperty("keyboardZoomStep").GetDouble()).Value;
        JsonElement lightControls = root.GetProperty("lightControls");
        PreviewLightControlPolicy? lightPolicy = PreviewLightControlPolicy.Create(
            lightControls.GetProperty("minimumPitchDegrees").GetDouble(),
            lightControls.GetProperty("maximumPitchDegrees").GetDouble(),
            lightControls.GetProperty("pointerStepDegrees").GetDouble(),
            lightControls.GetProperty("keyboardStepDegrees").GetDouble()).Value;
        JsonElement overlay = root.GetProperty("overlay");
        var overlayPolicy = new PreviewOverlayPolicy(
            overlay.GetProperty("initiallyVisible").GetBoolean(),
            overlay.GetProperty("restoreControlId").GetString()!);
        JsonElement items = root.GetProperty("itemSelection");
        var itemPolicy = new PreviewItemSelectionPolicy(
            items.GetProperty("wrapPreviousNext").GetBoolean(),
            items.GetProperty("initiallyShowAll").GetBoolean());
        JsonElement animation = root.GetProperty("animationTransport");
        PreviewAnimationTransportPolicy? animationPolicy =
            PreviewAnimationTransportPolicy.Create(
                animation.GetProperty("selectFirstAnimation").GetBoolean(),
                animation.GetProperty("initiallyPlaying").GetBoolean(),
                animation.GetProperty("keyboardScrubSeconds").GetDouble()).Value;
        PreviewInputBinding[] bindings =
        [
            .. root.GetProperty("bindings").EnumerateArray().Select(ReadBinding),
        ];
        PreviewAccessibilityPolicy? accessibility = PreviewAccessibilityPolicy.Create(
            root.GetProperty("accessibility").GetProperty("controls").EnumerateArray().Select(
                item => new PreviewAccessibleControl(
                    item.GetProperty("id").GetString()!,
                    item.GetProperty("label").GetString()!,
                    item.GetProperty("accessibleName").GetString()!,
                    item.GetProperty("accessibleHelp").GetString()!,
                    item.GetProperty("focusOrder").GetInt32()))).Value;
        return PreviewExperienceContract.Create(
            root.GetProperty("contractVersion").GetInt32(),
            background,
            lighting,
            navigationPolicy,
            lightPolicy,
            overlayPolicy,
            itemPolicy,
            animationPolicy,
            bindings,
            accessibility).Value;
    }

    private static PreviewInputBinding ReadBinding(JsonElement item) => new(
        Enum.Parse<PreviewAction>(item.GetProperty("action").GetString()!, false),
        item.GetProperty("input").GetString()!);

    private static PreviewBackground? ReadBackground(JsonElement value) => PreviewBackground.Create(
        ReadColour(value.GetProperty("outerColour")),
        ReadColour(value.GetProperty("centreColour")),
        value.GetProperty("centreX").GetDouble(),
        value.GetProperty("centreY").GetDouble(),
        value.GetProperty("radius").GetDouble(),
        value.GetProperty("horizontalScale").GetDouble()).Value;

    private static PreviewLighting? ReadLighting(JsonElement value) => PreviewLighting.Create(
        ReadLight(value.GetProperty("keyLight")),
        ReadLight(value.GetProperty("fillLight"))).Value;

    private static PreviewDirectionalLight? ReadLight(JsonElement value) =>
        PreviewDirectionalLight.Create(
            value.GetProperty("yawDegrees").GetDouble(),
            value.GetProperty("pitchDegrees").GetDouble(),
            value.GetProperty("intensity").GetDouble(),
            ReadColour(value.GetProperty("colour"))).Value;

    private static PreviewColour? ReadColour(JsonElement value) => PreviewColour.Create(
        value.GetProperty("red").GetDouble(),
        value.GetProperty("green").GetDouble(),
        value.GetProperty("blue").GetDouble()).Value;

    private static void Write(Utf8JsonWriter writer, PreviewExperienceContract value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
        writer.WriteNumber("contractVersion", value.ContractVersion);
        writer.WriteStartObject("presentation");
        writer.WriteStartObject("background");
        WriteColour(writer, "outerColour", value.Background.OuterColour);
        WriteColour(writer, "centreColour", value.Background.CentreColour);
        writer.WriteNumber("centreX", value.Background.CentreX);
        writer.WriteNumber("centreY", value.Background.CentreY);
        writer.WriteNumber("radius", value.Background.Radius);
        writer.WriteNumber("horizontalScale", value.Background.HorizontalScale);
        writer.WriteEndObject();
        writer.WriteStartObject("lighting");
        WriteLight(writer, "keyLight", value.Lighting.KeyLight);
        WriteLight(writer, "fillLight", value.Lighting.FillLight);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteStartObject("navigation");
        writer.WriteNumber("minimumPitchDegrees", value.Navigation.MinimumPitchDegrees);
        writer.WriteNumber("maximumPitchDegrees", value.Navigation.MaximumPitchDegrees);
        writer.WriteNumber("minimumDistanceMultiplier", value.Navigation.MinimumDistanceMultiplier);
        writer.WriteNumber("maximumDistanceMultiplier", value.Navigation.MaximumDistanceMultiplier);
        writer.WriteNumber("pointerOrbitDegreesPerUnit", value.Navigation.PointerOrbitDegreesPerUnit);
        writer.WriteNumber("keyboardOrbitStepDegrees", value.Navigation.KeyboardOrbitStepDegrees);
        writer.WriteNumber("pointerZoomStep", value.Navigation.PointerZoomStep);
        writer.WriteNumber("keyboardZoomStep", value.Navigation.KeyboardZoomStep);
        writer.WriteEndObject();
        writer.WriteStartObject("lightControls");
        writer.WriteNumber("minimumPitchDegrees", value.LightControls.MinimumPitchDegrees);
        writer.WriteNumber("maximumPitchDegrees", value.LightControls.MaximumPitchDegrees);
        writer.WriteNumber("pointerStepDegrees", value.LightControls.PointerStepDegrees);
        writer.WriteNumber("keyboardStepDegrees", value.LightControls.KeyboardStepDegrees);
        writer.WriteEndObject();
        writer.WriteStartObject("overlay");
        writer.WriteBoolean("initiallyVisible", value.Overlay.InitiallyVisible);
        writer.WriteString("restoreControlId", value.Overlay.RestoreControlId);
        writer.WriteEndObject();
        writer.WriteStartObject("itemSelection");
        writer.WriteBoolean("wrapPreviousNext", value.ItemSelection.WrapPreviousNext);
        writer.WriteBoolean("initiallyShowAll", value.ItemSelection.InitiallyShowAll);
        writer.WriteEndObject();
        writer.WriteStartObject("animationTransport");
        writer.WriteBoolean("selectFirstAnimation", value.AnimationTransport.SelectFirstAnimation);
        writer.WriteBoolean("initiallyPlaying", value.AnimationTransport.InitiallyPlaying);
        writer.WriteNumber("keyboardScrubSeconds", value.AnimationTransport.KeyboardScrubSeconds);
        writer.WriteEndObject();
        writer.WriteStartArray("bindings");
        foreach (PreviewInputBinding binding in value.Bindings)
        {
            writer.WriteStartObject();
            writer.WriteString("action", binding.Action.ToString());
            writer.WriteString("input", binding.Input);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteStartObject("accessibility");
        writer.WriteBoolean("visibleFocusRequired", PreviewAccessibilityPolicy.VisibleFocusRequired);
        writer.WriteStartArray("controls");
        foreach (PreviewAccessibleControl control in value.Accessibility.Controls)
        {
            writer.WriteStartObject();
            writer.WriteString("id", control.Id);
            writer.WriteString("label", control.Label);
            writer.WriteString("accessibleName", control.AccessibleName);
            writer.WriteString("accessibleHelp", control.AccessibleHelp);
            writer.WriteNumber("focusOrder", control.FocusOrder);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteLight(
        Utf8JsonWriter writer,
        string name,
        PreviewDirectionalLight value)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("yawDegrees", value.YawDegrees);
        writer.WriteNumber("pitchDegrees", value.PitchDegrees);
        writer.WriteNumber("intensity", value.Intensity);
        WriteColour(writer, "colour", value.Colour);
        writer.WriteEndObject();
    }

    private static void WriteColour(Utf8JsonWriter writer, string name, PreviewColour value)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("red", value.Red);
        writer.WriteNumber("green", value.Green);
        writer.WriteNumber("blue", value.Blue);
        writer.WriteEndObject();
    }

    private static string ReadSchemaText()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(SchemaResource)!;
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }
}
