using Loom.Config;

namespace Loom.Modules;

public record VelopackArtifactResult(string ArtifactName, string ReleaseDir, string Version);

[ModuleCategory("Packaging")]
[DependsOn<PublishModule>]
[DependsOn<RestoreToolsModule>]
public class VelopackReleaseModule(LoomContext loomContext, MinVerCache minVerCache)
    : Module<List<VelopackArtifactResult>>
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

    protected override async Task<List<VelopackArtifactResult>?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var publishModule = await context.GetModule<PublishModule>();
        var publishedArtifacts = publishModule.ValueOrDefault ?? new();

        var root = context.Environment.WorkingDirectory;
        var results = new List<VelopackArtifactResult>();

        var velopackArtifacts = publishedArtifacts
            .Where(a => a.Type == ArtifactType.Velopack)
            .ToList();

        foreach (var artifact in velopackArtifacts)
        {
            var artifactSettings = loomContext.Artifacts[artifact.ArtifactName];

            var version = !string.IsNullOrWhiteSpace(artifactSettings.Version)
                ? artifactSettings.Version
                : await minVerCache.GetOrAddAsync(
                    artifactSettings.TagPrefix,
                    () => MinVerModule.RunMinVerAsync(context, artifactSettings.TagPrefix, ct)
                );

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

            await context.Shell.Command.ExecuteCommandLineTool(
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

            results.Add(new VelopackArtifactResult(artifact.ArtifactName, releaseDir, version));
        }

        return results;
    }
}
