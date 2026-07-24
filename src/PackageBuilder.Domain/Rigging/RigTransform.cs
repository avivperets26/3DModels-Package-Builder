namespace PackageBuilder.Domain.Rigging;

/// <summary>
/// Represents a renderer-independent local transform with finite translation and scale and a
/// canonical unit rotation quaternion.
/// </summary>
public sealed class RigTransform : IEquatable<RigTransform>
{
    private RigTransform(
        double translationX,
        double translationY,
        double translationZ,
        double rotationX,
        double rotationY,
        double rotationZ,
        double rotationW,
        double scaleX,
        double scaleY,
        double scaleZ)
    {
        TranslationX = translationX;
        TranslationY = translationY;
        TranslationZ = translationZ;
        RotationX = rotationX;
        RotationY = rotationY;
        RotationZ = rotationZ;
        RotationW = rotationW;
        ScaleX = scaleX;
        ScaleY = scaleY;
        ScaleZ = scaleZ;
    }

    public double TranslationX { get; }

    public double TranslationY { get; }

    public double TranslationZ { get; }

    public double RotationX { get; }

    public double RotationY { get; }

    public double RotationZ { get; }

    public double RotationW { get; }

    public double ScaleX { get; }

    public double ScaleY { get; }

    public double ScaleZ { get; }

    /// <summary>
    /// Validates and creates a transform. Any finite signed translation or scale is retained.
    /// A non-zero finite quaternion is normalized robustly and its sign is canonicalized, so
    /// <c>q</c> and <c>-q</c> store the same rotation deterministically.
    /// </summary>
    public static RigTransformValidationResult Create(
        double translationX,
        double translationY,
        double translationZ,
        double rotationX,
        double rotationY,
        double rotationZ,
        double rotationW,
        double scaleX,
        double scaleY,
        double scaleZ)
    {
        double[] values =
        [
            translationX,
            translationY,
            translationZ,
            rotationX,
            rotationY,
            rotationZ,
            rotationW,
            scaleX,
            scaleY,
            scaleZ,
        ];

        for (int index = 0; index < values.Length; index++)
        {
            if (!double.IsFinite(values[index]))
            {
                return RigTransformValidationResult.Failure(
                    (RigTransformValidationError)(index + 1));
            }
        }

        double maximum = Math.Max(
            Math.Max(Math.Abs(rotationX), Math.Abs(rotationY)),
            Math.Max(Math.Abs(rotationZ), Math.Abs(rotationW)));
        if (maximum == 0d)
        {
            return RigTransformValidationResult.Failure(
                RigTransformValidationError.ZeroLengthRotation);
        }

        // Scale before measuring length to avoid overflow and underflow at finite boundaries.
        double scaledX = rotationX / maximum;
        double scaledY = rotationY / maximum;
        double scaledZ = rotationZ / maximum;
        double scaledW = rotationW / maximum;
        double inverseLength =
            1d /
            Math.Sqrt(
                (scaledX * scaledX) +
                (scaledY * scaledY) +
                (scaledZ * scaledZ) +
                (scaledW * scaledW));

        double normalizedX = scaledX * inverseLength;
        double normalizedY = scaledY * inverseLength;
        double normalizedZ = scaledZ * inverseLength;
        double normalizedW = scaledW * inverseLength;

        // Quaternion signs encode the same rotation. Prefer positive W, then the first non-zero
        // vector component, to make ordering, equality, and hashing independent of source sign.
        bool negate =
            normalizedW < 0d ||
            (normalizedW == 0d &&
                (normalizedX < 0d ||
                    (normalizedX == 0d &&
                        (normalizedY < 0d ||
                            (normalizedY == 0d && normalizedZ < 0d)))));
        if (negate)
        {
            normalizedX = -normalizedX;
            normalizedY = -normalizedY;
            normalizedZ = -normalizedZ;
            normalizedW = -normalizedW;
        }

        return RigTransformValidationResult.Success(
            new RigTransform(
                translationX,
                translationY,
                translationZ,
                normalizedX,
                normalizedY,
                normalizedZ,
                normalizedW,
                scaleX,
                scaleY,
                scaleZ));
    }

    /// <inheritdoc />
    public bool Equals(RigTransform? other) =>
        other is not null &&
        TranslationX.Equals(other.TranslationX) &&
        TranslationY.Equals(other.TranslationY) &&
        TranslationZ.Equals(other.TranslationZ) &&
        RotationX.Equals(other.RotationX) &&
        RotationY.Equals(other.RotationY) &&
        RotationZ.Equals(other.RotationZ) &&
        RotationW.Equals(other.RotationW) &&
        ScaleX.Equals(other.ScaleX) &&
        ScaleY.Equals(other.ScaleY) &&
        ScaleZ.Equals(other.ScaleZ);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RigTransform other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        AddToHash(StableRigHash.Create()).ToHashCode();

    internal StableRigHash AddToHash(StableRigHash hash) =>
        hash.Add(TranslationX)
            .Add(TranslationY)
            .Add(TranslationZ)
            .Add(RotationX)
            .Add(RotationY)
            .Add(RotationZ)
            .Add(RotationW)
            .Add(ScaleX)
            .Add(ScaleY)
            .Add(ScaleZ);
}
