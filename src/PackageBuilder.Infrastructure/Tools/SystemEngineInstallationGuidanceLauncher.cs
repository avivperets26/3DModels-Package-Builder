using System.ComponentModel;
using System.Diagnostics;
using PackageBuilder.Contracts.Tools;

namespace PackageBuilder.Infrastructure.Tools;

/// <summary>Opens only the three approved vendor installation guides in the system browser.</summary>
public sealed class SystemEngineInstallationGuidanceLauncher : IEngineInstallationGuidanceLauncher
{
    private static readonly HashSet<string> _approvedGuides = new(StringComparer.Ordinal)
    {
        "https://www.blender.org/download/",
        "https://docs.unity3d.com/Manual/GettingStartedInstallingUnity.html",
        "https://dev.epicgames.com/documentation/en-us/unreal-engine/install-unreal-engine",
    };

    public Task<bool> OpenAsync(
        Uri officialGuidanceUri,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested
            || officialGuidanceUri is null
            || !officialGuidanceUri.IsAbsoluteUri
            || officialGuidanceUri.Scheme != Uri.UriSchemeHttps
            || !_approvedGuides.Contains(officialGuidanceUri.AbsoluteUri))
        {
            return Task.FromResult(false);
        }

        try
        {
            using var process = Process.Start(
                new ProcessStartInfo(officialGuidanceUri.AbsoluteUri)
                {
                    UseShellExecute = true,
                });
            return Task.FromResult(process is not null);
        }
        catch (Exception exception) when (
            exception is Win32Exception
                or InvalidOperationException
                or NotSupportedException)
        {
            return Task.FromResult(false);
        }
    }
}
