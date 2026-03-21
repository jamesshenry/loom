namespace Loom.Config;

using System.Runtime.InteropServices;

public class LoomContext
{
    public LoomContext(LoomSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Workspace.Solution))
            throw new ArgumentException("Workspace:Solution is required.");

        Solution = settings.Workspace.Solution;
        ArtifactsDirectory = settings.Workspace.ArtifactsPath;

        Target = settings.Run.Target;
        Version = settings.Run.Version ?? "1.0.0";
        Rid = settings.Run.Rid ?? GetDefaultRid();

        Configuration =
            settings.Run.Configuration
            ?? (Target is BuildTarget.Release or BuildTarget.Publish ? "Release" : "Debug");

        Artifacts = settings.Artifacts.AsReadOnly();

        RequiresMinVer = true;
        RequiresVelopack = settings.Artifacts.Values.Any(a => a.Type == ArtifactType.Velopack);

        NugetApiKey = settings.Nuget.ApiKey;
        GitHubToken = settings.GithubAccessToken;
    }

    public string Solution { get; }
    public string ArtifactsDirectory { get; }

    public IReadOnlyDictionary<string, ArtifactSettings> Artifacts { get; }

    public BuildTarget Target { get; }
    public string Configuration { get; }
    public string Rid { get; }
    public string Version { get; }

    public bool RequiresMinVer { get; }
    public bool RequiresVelopack { get; }

    public string NugetApiKey { get; }
    public string GitHubToken { get; set; } = string.Empty;

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
