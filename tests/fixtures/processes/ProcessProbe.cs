using System.Text.Json;

int outputCount = ReadCount("--stdout-count=");
int errorCount = ReadCount("--stderr-count=");
int exitCode = ReadCount("--exit-code=");

if (outputCount > 0 || errorCount > 0)
{
    Console.Out.Write(new string('O', outputCount));
    Console.Error.Write(new string('E', errorCount));
}
else
{
    var observation = new
    {
        arguments = args,
        workingDirectory = Environment.CurrentDirectory,
        probeValue = Environment.GetEnvironmentVariable("PROBE_VALUE"),
        path = Environment.GetEnvironmentVariable("PATH"),
        temporary = Environment.GetEnvironmentVariable("TEMP"),
        temporaryAlternative = Environment.GetEnvironmentVariable("TMP"),
        temporaryPortable = Environment.GetEnvironmentVariable("TMPDIR"),
        home = Environment.GetEnvironmentVariable("HOME"),
        userProfile = Environment.GetEnvironmentVariable("USERPROFILE"),
        applicationData = Environment.GetEnvironmentVariable("APPDATA"),
        localApplicationData = Environment.GetEnvironmentVariable("LOCALAPPDATA"),
        cache = Environment.GetEnvironmentVariable("PACKAGEBUILDER_CACHE_ROOT"),
        xdgCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME"),
        dotnetHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME"),
        nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES"),
        logs = Environment.GetEnvironmentVariable("PACKAGEBUILDER_LOG_ROOT"),
    };

    Console.Out.Write(JsonSerializer.Serialize(observation));
    Console.Error.Write("probe-stderr");
}

return exitCode;

int ReadCount(string prefix)
{
    string? value = args.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.Ordinal));
    return value is null ? 0 : int.Parse(value[prefix.Length..], System.Globalization.CultureInfo.InvariantCulture);
}
