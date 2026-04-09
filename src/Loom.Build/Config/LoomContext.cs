namespace Loom.Config;

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

public record LoomContext
{
    public LoomContext() { }

    public LoomContext(LoomSettings settings, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(settings.Workspace.Solution))
            throw new ArgumentException("Workspace:Solution is required.");

        WorkingDirectory = workingDirectory;
        Solution = settings.Workspace.Solution;
        ArtifactsDirectory = settings.Workspace.ArtifactsPath;
        CleanDirectories = settings.Workspace.CleanDirectories;

        Target = settings.Global.Target ?? BuildTarget.Build;
        Version = settings.Global.Version ?? "1.0.0";
        Rid = settings.Global.Rid ?? GetDefaultRid();

        Configuration =
            settings.Global.Configuration
            ?? ((Target is BuildTarget.Release or BuildTarget.Publish) ? "Release" : "Debug");

        Artifacts = settings.Artifacts.AsReadOnly();

        RequiresMinVer = LoomConfig.GetPipelineCategories(Target).Contains("Packaging");

        NugetApiKey = settings.Nuget.ApiKey;
        EnableVelopack =
            settings.Artifacts.Values.Any(a => a.Type == ArtifactType.Velopack)
            || (settings.Workspace.EnableVelopackRelease ?? false);
        EnableNugetUpload = settings.Workspace.EnableNugetUpload ?? false;
        EnableGithubRelease = settings.Workspace.EnableGithubRelease ?? false;
    }

    public string WorkingDirectory { get; init; } = string.Empty;
    public string Solution { get; init; } = string.Empty;
    public string ArtifactsDirectory { get; init; } = ".artifacts";
    public IReadOnlyList<string> CleanDirectories { get; init; } = ["dist"];

    public IReadOnlyDictionary<string, ArtifactSettings> Artifacts { get; init; } =
        ReadOnlyDictionary<string, ArtifactSettings>.Empty;

    public BuildTarget Target { get; init; } = BuildTarget.Build;
    public string Configuration { get; init; } = "Release";
    public string Rid { get; init; } = "win-x64";
    public string? Version { get; init; }

    public bool RequiresMinVer { get; init; } = true;
    public bool EnableVelopack { get; init; } = false;

    public string? NugetApiKey { get; init; }
    public bool? EnableNugetUpload { get; init; }
    public bool EnableGithubRelease { get; init; } = false;

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
