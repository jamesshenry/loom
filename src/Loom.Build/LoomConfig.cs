using Loom.Config;

namespace Loom;

public static class LoomConfig
{
    private static readonly string[] PossibleLoomPaths =
    [
        Path.Combine(Environment.CurrentDirectory, "loom.json"),
        Path.Combine(Environment.CurrentDirectory, ".build", "loom.json"),
    ];

    public static string? ResolveLoomJsonPath() => PossibleLoomPaths.FirstOrDefault(File.Exists);

    public static string[] GetPipelineCategories(BuildTarget target) =>
        target switch
        {
            BuildTarget.Build => ["Preparation", "Build"],
            BuildTarget.Test => ["Preparation", "Build", "Test"],
            BuildTarget.Publish => ["Preparation", "Packaging"],
            BuildTarget.Release => ["Preparation", "Build", "Packaging"],
            BuildTarget.NugetUpload => ["Preparation", "Build", "Packaging", "Delivery"],
            BuildTarget.Clean => ["Preparation"],
            BuildTarget.Restore => ["Preparation"],
            _ => [],
        };
}
