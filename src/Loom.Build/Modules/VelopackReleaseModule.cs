using Loom.Config;

namespace Loom.Modules;

[ModuleCategory("Packaging")]
[DependsOn<PublishModule>]
[DependsOn<MinVerModule>]
public class VelopackReleaseModule(LoomContext buildContext) : Module<CommandResult[]>
{
    protected override ModuleConfiguration Configure()
    {
        return ModuleConfiguration
            .Create()
            .WithSkipWhen(ctx =>
            {
                return !buildContext.Artifacts.Any(x => x.Value.Type == ArtifactType.Velopack)
                    ? SkipDecision.Skip("No velopack artifacts defined in loom.json")
                    : SkipDecision.DoNotSkip;
            })
            .Build();
    }

    protected override async Task<CommandResult[]?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var versionModule = await context.GetModule<MinVerModule>();
        var globalVersion = versionModule.ValueOrDefault;

        var publishModule = await context.GetModule<PublishModule>();
        var publishedArtifacts = publishModule.ValueOrDefault ?? new();

        var root = context.Environment.WorkingDirectory;
        var results = new List<CommandResult>();

        var velopackArtifacts = publishedArtifacts
            .Where(a => a.Type == ArtifactType.Velopack)
            .ToList();

        foreach (var artifact in velopackArtifacts)
        {
            var artifactSettings = buildContext.Artifacts[artifact.ArtifactName];

            var version = artifactSettings.Version ?? globalVersion;
            var packId = artifactSettings.VelopackId ?? artifact.ArtifactName;

            ArgumentException.ThrowIfNullOrWhiteSpace(version, nameof(version));

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
