using System.Globalization;

namespace PackageBuilder.Domain.Tools;

/// <summary>Represents a canonical vendor-aware version with deterministic semantic precedence.</summary>
public sealed class ToolVersion : IComparable<ToolVersion>, IEquatable<ToolVersion>
{
    private readonly int[] _numbers;
    private readonly string[] _prereleaseIdentifiers;
    private readonly int _unityStage;
    private readonly int _unityRevision;

    private ToolVersion(ToolKind tool, string value, ToolVersionParts parts)
    {
        Tool = tool;
        Value = value;
        Channel = parts.Channel;
        _numbers = (int[])parts.Numbers.Clone();
        _prereleaseIdentifiers = (string[])parts.PrereleaseIdentifiers.Clone();
        _unityStage = parts.UnityStage;
        _unityRevision = parts.UnityRevision;
    }

    /// <summary>Gets the tool whose grammar and precedence rules apply.</summary>
    public ToolKind Tool { get; }

    /// <summary>Gets the canonical version exactly as supplied.</summary>
    public string Value { get; }

    /// <summary>Gets the release channel derived from the vendor version marker.</summary>
    public ToolReleaseChannel Channel { get; }

    /// <summary>Validates and parses a canonical tool version without naive text ordering.</summary>
    public static ToolModelValidationResult<ToolVersion> Create(ToolKind tool, string? value)
    {
        ToolModelValidationError error = ToolVersionParser.Parse(tool, value, out ToolVersionParts parts);
        return error == ToolModelValidationError.None
            ? ToolModelValidationResult<ToolVersion>.Success(new ToolVersion(tool, value!, parts))
            : ToolModelValidationResult<ToolVersion>.Failure(error);
    }

    /// <inheritdoc />
    public int CompareTo(ToolVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int toolComparison = Tool.CompareTo(other.Tool);
        if (toolComparison != 0)
        {
            return toolComparison;
        }

        int numericComparison = CompareNumbers(_numbers, other._numbers);
        return numericComparison != 0
            ? numericComparison
            : Tool == ToolKind.Unity
            ? CompareUnity(other)
            : CompareSemanticPrerelease(other);
    }

    /// <inheritdoc />
    public bool Equals(ToolVersion? other) =>
        other is not null && Tool == other.Tool && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ToolVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StableToolHash.Create().Add((int)Tool).Add(Value).ToHashCode();

    /// <summary>Determines whether two versions have identical tool and canonical version values.</summary>
    public static bool operator ==(ToolVersion? left, ToolVersion? right) => Equals(left, right);

    /// <summary>Determines whether two versions differ.</summary>
    public static bool operator !=(ToolVersion? left, ToolVersion? right) => !Equals(left, right);

    /// <summary>Determines whether the left version sorts before the right version.</summary>
    public static bool operator <(ToolVersion? left, ToolVersion? right) => Compare(left, right) < 0;

    /// <summary>Determines whether the left version sorts after the right version.</summary>
    public static bool operator >(ToolVersion? left, ToolVersion? right) => Compare(left, right) > 0;

    /// <summary>Determines whether the left version does not sort after the right version.</summary>
    public static bool operator <=(ToolVersion? left, ToolVersion? right) => Compare(left, right) <= 0;

    /// <summary>Determines whether the left version does not sort before the right version.</summary>
    public static bool operator >=(ToolVersion? left, ToolVersion? right) => Compare(left, right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value;

    private int CompareUnity(ToolVersion other)
    {
        int stageComparison = _unityStage.CompareTo(other._unityStage);
        return stageComparison != 0 ? stageComparison : _unityRevision.CompareTo(other._unityRevision);
    }

    private int CompareSemanticPrerelease(ToolVersion other)
    {
        if (_prereleaseIdentifiers.Length == 0 || other._prereleaseIdentifiers.Length == 0)
        {
            return _prereleaseIdentifiers.Length == other._prereleaseIdentifiers.Length
                ? 0
                : _prereleaseIdentifiers.Length == 0 ? 1 : -1;
        }

        int sharedLength = Math.Min(_prereleaseIdentifiers.Length, other._prereleaseIdentifiers.Length);
        for (int index = 0; index < sharedLength; index++)
        {
            int comparison = CompareIdentifier(
                _prereleaseIdentifiers[index],
                other._prereleaseIdentifiers[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return _prereleaseIdentifiers.Length.CompareTo(other._prereleaseIdentifiers.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        if (int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out int leftNumber))
        {
            _ = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out int rightNumber);
            return leftNumber.CompareTo(rightNumber);
        }

        // Supported vendor grammars compare labels at index zero and numeric identifiers thereafter.
        return string.Compare(left, right, StringComparison.Ordinal);
    }

    private static int CompareNumbers(int[] left, int[] right)
    {
        for (int index = 0; index < left.Length; index++)
        {
            int comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static int Compare(ToolVersion? left, ToolVersion? right) =>
        Comparer<ToolVersion>.Default.Compare(left, right);
}
