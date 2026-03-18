using Loom.Config;

namespace Loom.Modules;

[ModuleCategory("Packaging")]
[DependsOn<PublishModule>]
[DependsOn<MinVerModule>]
public class VelopackReleaseModule(LoomContext buildContext) : Module<CommandResult[]>
{
    protected override async Task<CommandResult[]?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var versionModule = await context.GetModule<MinVerModule>();
        var globalVersion = versionModule.ValueOrDefault;

        // 1. Get the exact outputs from the PublishModule!
        var publishModule = await context.GetModule<PublishModule>();
        var publishedArtifacts = publishModule.ValueOrDefault ?? new();

        var root = context.Environment.WorkingDirectory;
        var results = new List<CommandResult>();

        // 2. Filter ONLY for the ones marked for Velopack
        var velopackArtifacts = publishedArtifacts
            .Where(a => a.Type.Equals("Velopack", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (velopackArtifacts.Count == 0)
        {
            return [];
        }

        foreach (var artifact in velopackArtifacts)
        {
            // Fetch the specific settings for this artifact from the context
            var artifactSettings = buildContext.Artifacts[artifact.ArtifactName];

            var version = artifactSettings.Version ?? globalVersion;
            var packId = artifactSettings.VelopackId ?? artifact.ArtifactName;

            ArgumentException.ThrowIfNullOrWhiteSpace(version, nameof(version));

            // We don't have to guess the publish path anymore, the previous module gave it to us!
            var publishDir = artifact.PublishDirectory.Path;
            var releaseDir = Path.Combine(
                root,
                buildContext.ArtifactsDirectory,
                "release",
                artifact.ArtifactName,
                artifact.Rid
            );

            string directive = artifact.Rid.ToLower() switch
            {
                var r when r.StartsWith("win") => "[win]",
                var r when r.StartsWith("osx") => "[osx]",
                var r when r.StartsWith("linux") => "[linux]",
                _ => throw new NotSupportedException(
                    $"RID {artifact.Rid} is not supported by Velopack."
                ),
            };

            var result = await context.Shell.Command.ExecuteCommandLineTool(
                new VelopackOptions
                {
                    Arguments =
                    [
                        "vpk",
                        directive,
                        "pack",
                        "--packId",
                        packId,
                        "--packVersion",
                        version,
                        "--packDir",
                        publishDir,
                        "--outputDir",
                        releaseDir,
                        "--yes",
                    ],
                },
                cancellationToken: ct
            );

            results.Add(result);
        }

        return [.. results];
    }
}
