using Loom.Config;

namespace Loom.Modules;

[ModuleCategory("Packaging")]
[DependsOn<PublishModule>]
[DependsOn<MinVerModule>]
[DependsOn<RestoreToolsModule>]
public class VelopackReleaseModule(LoomContext loomContext) : Module<string[]>
{
    protected override ModuleConfiguration Configure()
    {
        return ModuleConfiguration
            .Create()
            .WithSkipWhen(ctx =>
            {
                return !loomContext.Artifacts.Any(x => x.Value.Type == ArtifactType.Velopack)
                    ? SkipDecision.Skip("No velopack artifacts defined in loom.json")
                    : SkipDecision.DoNotSkip;
            })
            .Build();
    }

    protected override async Task<string[]?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var versionModule = await context.GetModule<MinVerModule>();
        var globalVersion = versionModule.ValueOrDefault;

        var publishModule = await context.GetModule<PublishModule>();
        var publishedArtifacts = publishModule.ValueOrDefault ?? new();

        var root = context.Environment.WorkingDirectory;
        var results = new List<string>();

        var velopackArtifacts = publishedArtifacts
            .Where(a => a.Type == ArtifactType.Velopack)
            .ToList();

        foreach (var artifact in velopackArtifacts)
        {
            var artifactSettings = loomContext.Artifacts[artifact.ArtifactName];

            var version = artifactSettings.Version ?? globalVersion;
            var packId = artifactSettings.VelopackId ?? artifact.ArtifactName;
            ArgumentException.ThrowIfNullOrWhiteSpace(version, nameof(version));

            var publishDir = artifact.PublishDirectory.Path;
            var releaseDir = Path.Combine(
                root,
                loomContext.ArtifactsDirectory,
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

            results.Add(releaseDir);
        }

        return [.. results];
    }
}
