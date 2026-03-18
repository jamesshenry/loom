namespace Loom.Config;

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

public class LoomContext
{
    public LoomContext(LoomSettings settings)
    {
        // 1. Validate Core Workspace
        if (string.IsNullOrWhiteSpace(settings.Workspace.Solution))
            throw new ArgumentException("Workspace:Solution is required.");

        Solution = settings.Workspace.Solution;
        ArtifactsDirectory = settings.Workspace.ArtifactsPath;

        // 2. Bind Execution Options
        Target = settings.Run.Target;
        Version = settings.Run.Version ?? "1.0.0"; // Fallback, or read from MinVer later
        Rid = settings.Run.Rid ?? GetDefaultRid();

        Configuration =
            settings.Run.Configuration
            ?? (Target is BuildTarget.Release or BuildTarget.Publish ? "Release" : "Debug");

        // 3. Bind Artifacts
        // We wrap it in a ReadOnlyDictionary so modules can't accidentally mutate it during the build
        Artifacts = settings.Artifacts.AsReadOnly();

        // 4. Secrets
        // Since you removed PackagingSettings, get the API key from the environment securely
        NugetApiKey = Environment.GetEnvironmentVariable("LOOM_NUGET_APIKEY");
    }

    // Workspace
    public string Solution { get; }
    public string ArtifactsDirectory { get; } // Note: Removed 'set;' to keep it immutable

    // Artifacts
    public IReadOnlyDictionary<string, ArtifactSettings> Artifacts { get; }

    // Execution
    public BuildTarget Target { get; }
    public string Configuration { get; }
    public string Rid { get; }
    public string Version { get; }
    public string? NugetApiKey { get; }

    private static string GetDefaultRid()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "osx-arm64"
                : "osx-x64";
        return "linux-x64";
    }
}
