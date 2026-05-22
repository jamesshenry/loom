using Loom.Config;
using Loom.MinVer;

namespace Loom.Modules;

public static class PublishHelpers
{
    public static MinVerVersion ResolveVersion(
        ArtifactSettings artifact,
        MinVerResult? minVerResult
    )
    {
        if (!string.IsNullOrWhiteSpace(artifact.Version))
        {
            return MinVerVersion.From(artifact.Version)!;
        }

        return minVerResult?.GetVersion(artifact.TagPrefix) ?? MinVerVersion.V1;
    }

    public static MinVerVersion ResolveVersion(MinVerResult? minVerResult) =>
        minVerResult?.GetVersion(null) ?? MinVerVersion.V1;

    public static List<KeyValue> CreateVersionProperties(MinVerVersion version) =>
        [
            new("AssemblyVersion", version.AssemblyVersion),
            new("FileVersion", version.FileVersion),
            new("InformationalVersion", version.Version),
            new("PackageVersion", version.PackageVersion),
            new("Version", version.Version),
        ];
}
