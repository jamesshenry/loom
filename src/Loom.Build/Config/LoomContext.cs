namespace Loom.Config;

using System.Runtime.InteropServices;

public class LoomContext
{
    public LoomContext(LoomSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Workspace.Solution))
            throw new ArgumentException("Workspace:Solution is required.");
        if (string.IsNullOrWhiteSpace(settings.Workspace.MainProject))
            throw new ArgumentException("Workspace:MainProject is required.");

        Solution = settings.Workspace.Solution;
        MainProject = settings.Workspace.MainProject;
        VelopackId = settings.Packaging.VelopackId;
        NugetApiKey = settings.Packaging.NugetApiKey;

        Target = settings.Run.Target;
        Version = settings.Run.Version ?? settings.Workspace.DefaultVersionPrefix;
        Rid = settings.Run.Rid ?? GetDefaultRid();

        // Simple default configuration
        Configuration =
            settings.Run.Configuration
            ?? (Target is BuildTarget.Release or BuildTarget.Publish ? "Release" : "Debug");
        ArtifactsDirectory = settings.Workspace.ArtifactsPath;
    }

    public string Solution { get; }
    public string MainProject { get; }
    public string? VelopackId { get; }
    public string? NugetApiKey { get; }

    public BuildTarget Target { get; }
    public string Configuration { get; }
    public string Rid { get; }
    public string Version { get; }
    public string ArtifactsDirectory { get; set; }

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
