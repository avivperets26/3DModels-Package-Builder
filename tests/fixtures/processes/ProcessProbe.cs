using System.Text.Json;

string? mode = ReadValue("--mode=");
string? pidFile = ReadValue("--pid-file=");
if (string.Equals(mode, "child", StringComparison.Ordinal))
{
    if (pidFile is not null)
    {
        await File.WriteAllTextAsync(pidFile, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}

if (mode is not null)
{
    if (args.Contains("--spawn-child", StringComparer.Ordinal))
    {
        var childStart = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Environment.ProcessPath,
            UseShellExecute = false,
        };
        childStart.ArgumentList.Add("--mode=child");
        if (pidFile is not null)
        {
            childStart.ArgumentList.Add($"--pid-file={pidFile}");
        }

        _ = System.Diagnostics.Process.Start(childStart);
    }

    if (string.Equals(mode, "startup-delay", StringComparison.Ordinal))
    {
        await Task.Delay(Timeout.InfiniteTimeSpan);
        return 0;
    }

    Console.Out.WriteLine("probe-started");
    Console.Out.Flush();
    if (string.Equals(mode, "heartbeat", StringComparison.Ordinal))
    {
        while (true)
        {
            await Task.Delay(25);
            Console.Out.WriteLine("heartbeat");
            Console.Out.Flush();
        }
    }

    if (string.Equals(mode, "graceful", StringComparison.Ordinal))
    {
        string? cancellationFile = Environment.GetEnvironmentVariable("PACKAGEBUILDER_CANCELLATION_FILE");
        while (cancellationFile is null || !File.Exists(cancellationFile))
        {
            await Task.Delay(10);
        }

        Console.Error.Write("probe-cancelled");
        Console.Error.Flush();
        return 42;
    }

    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}

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
    string? value = ReadValue(prefix);
    return value is null ? 0 : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}

string? ReadValue(string prefix)
{
    string? argument = args.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
    return argument is null ? null : argument[prefix.Length..];
}
