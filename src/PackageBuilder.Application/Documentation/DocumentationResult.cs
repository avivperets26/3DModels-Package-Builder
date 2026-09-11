namespace PackageBuilder.Application.Documentation;

/// <summary>Expected documentation/configuration failures carry stable codes and no source paths or prose.</summary>
public sealed record DocumentationResult<T>(T? Value, string? Error) where T : class
{
    public bool IsSuccess => Value is not null && Error is null;

    internal static DocumentationResult<T> Success(T value) => new(value, null);
    internal static DocumentationResult<T> Failure(string code) => new(null, code);
}
