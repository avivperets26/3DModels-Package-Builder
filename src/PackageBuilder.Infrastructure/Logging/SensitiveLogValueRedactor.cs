using PackageBuilder.Contracts.Logging;

namespace PackageBuilder.Infrastructure.Logging;

/// <summary>Compatibility boundary for the shared persistent-diagnostic redaction policy.</summary>
internal static class SensitiveLogValueRedactor
{
    public const string RedactedValue = SensitiveDiagnosticValueRedactor.RedactedValue;
    public static string Redact(string name, string value) => SensitiveDiagnosticValueRedactor.Redact(name, value);
}
