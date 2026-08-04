namespace PackageBuilder.Domain.Tools;

/// <summary>Represents discovered tool metadata while enforcing fail-closed selection containment.</summary>
public sealed class ToolInstallation : IEquatable<ToolInstallation>
{
    private ToolInstallation(
        ToolKind tool,
        ToolVersion version,
        string executablePath,
        string approvedToolsRoot,
        ToolInstallationContainment containment,
        bool isSelected)
    {
        Tool = tool;
        Version = version;
        ExecutablePath = executablePath;
        ApprovedToolsRoot = approvedToolsRoot;
        Containment = containment;
        IsSelected = isSelected;
    }

    /// <summary>Gets the supported tool identity.</summary>
    public ToolKind Tool { get; }

    /// <summary>Gets the validated vendor-aware version.</summary>
    public ToolVersion Version { get; }

    /// <summary>Gets the canonical executable path without asserting that it exists.</summary>
    public string ExecutablePath { get; }

    /// <summary>Gets the canonical configured tools root used for containment.</summary>
    public string ApprovedToolsRoot { get; }

    /// <summary>Gets whether this installation is contained or informational-only.</summary>
    public ToolInstallationContainment Containment { get; }

    /// <summary>Gets whether this contained installation is currently selected.</summary>
    public bool IsSelected { get; }

    /// <summary>Gets whether selection is permitted without changing external state.</summary>
    public bool CanBeSelected => Containment == ToolInstallationContainment.Contained;

    /// <summary>Creates installation metadata and rejects selection outside the approved tools root.</summary>
    public static ToolModelValidationResult<ToolInstallation> Create(
        ToolKind tool,
        ToolVersion? version,
        string? executablePath,
        string? approvedToolsRoot,
        bool isSelected)
    {
        if (!Enum.IsDefined(tool))
        {
            return ToolModelValidationResult<ToolInstallation>.Failure(ToolModelValidationError.UndefinedTool);
        }

        if (version is null)
        {
            return ToolModelValidationResult<ToolInstallation>.Failure(ToolModelValidationError.MissingVersion);
        }

        if (version.Tool != tool)
        {
            return ToolModelValidationResult<ToolInstallation>.Failure(ToolModelValidationError.VersionToolMismatch);
        }

        ToolModelValidationError pathError = ToolPathValidator.Validate(
            executablePath,
            approvedToolsRoot,
            out string normalizedExecutable,
            out string normalizedToolsRoot,
            out ToolInstallationContainment containment);
        return pathError != ToolModelValidationError.None
            ? ToolModelValidationResult<ToolInstallation>.Failure(pathError)
            : isSelected && containment != ToolInstallationContainment.Contained
            ? ToolModelValidationResult<ToolInstallation>.Failure(
                ToolModelValidationError.SelectionOutsideToolsRoot)
            : ToolModelValidationResult<ToolInstallation>.Success(
            new ToolInstallation(
                tool,
                version,
                normalizedExecutable,
                normalizedToolsRoot,
                containment,
                isSelected));
    }

    /// <summary>Returns a selected copy only when the installation is contained.</summary>
    public ToolModelValidationResult<ToolInstallation> Select() =>
        CanBeSelected
            ? ToolModelValidationResult<ToolInstallation>.Success(Copy(isSelected: true))
            : ToolModelValidationResult<ToolInstallation>.Failure(ToolModelValidationError.SelectionOutsideToolsRoot);

    /// <summary>Returns an unselected immutable copy without changing installation metadata.</summary>
    public ToolInstallation Deselect() => Copy(isSelected: false);

    /// <inheritdoc />
    public bool Equals(ToolInstallation? other) =>
        other is not null &&
        Tool == other.Tool &&
        Version.Equals(other.Version) &&
        string.Equals(ExecutablePath, other.ExecutablePath, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(ApprovedToolsRoot, other.ApprovedToolsRoot, StringComparison.OrdinalIgnoreCase) &&
        Containment == other.Containment &&
        IsSelected == other.IsSelected;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ToolInstallation other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StableToolHash.Create()
        .Add((int)Tool)
        .Add(Version.GetHashCode())
        .Add(ExecutablePath, ignoreCase: true)
        .Add(ApprovedToolsRoot, ignoreCase: true)
        .Add((int)Containment)
        .Add(IsSelected ? 1 : 0)
        .ToHashCode();

    private ToolInstallation Copy(bool isSelected) =>
        new(Tool, Version, ExecutablePath, ApprovedToolsRoot, Containment, isSelected);
}
