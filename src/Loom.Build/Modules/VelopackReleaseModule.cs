using Loom.Config;
using Loom.MinVer;
using Loom.Velopack;
using Loom.Velopack.Options;

namespace Loom.Modules;

public record VelopackArtifactResult(string ArtifactName, string ReleaseDir, string Version);

[ModuleCategory("Packaging")]
[DependsOn<PublishModule>(Optional = true)]
[DependsOn<MinVerModule>(Optional = true)]
[DependsOn<RestoreToolsModule>(Optional = true)]
public class VelopackReleaseModule(LoomContext loomContext) : Module<List<VelopackArtifactResult>>
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
        var publishedArtifactsInfo = publishModule.ValueOrDefault;
        var publishedArtifacts = publishedArtifactsInfo?.Artifacts ?? [];

        var minVerModule = await context.GetModule<MinVerModule>();
        var minVerResult = minVerModule.ValueOrDefault;

        var root = loomContext.WorkingDirectory;
        var results = new List<VelopackArtifactResult>();

        var velopackArtifacts = publishedArtifacts
            .Where(a => a.Type == ArtifactType.Velopack)
            .ToList();

        foreach (var artifact in velopackArtifacts)
        {
            var artifactSettings = loomContext.Artifacts[artifact.ArtifactName];

            var version = !string.IsNullOrWhiteSpace(artifactSettings.Version)
                ? MinVerVersion.From(artifactSettings.Version)
                : minVerResult?.GetVersion(artifactSettings.TagPrefix);

            var packId = artifactSettings.VelopackId ?? artifact.ArtifactName;
            ArgumentNullException.ThrowIfNull(version, nameof(version));

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
                var r when r.StartsWith("win") => "win",
                var r when r.StartsWith("osx") => "osx",
                var r when r.StartsWith("linux") => "linux",
                _ => throw new NotSupportedException(
                    $"RID {artifact.Rid} is not supported by Velopack."
                ),
            };

            await context
                .Velopack()
                .ExecuteAsync(
                    new DotNetVelopackOptions
                    {
                        PackId = packId,
                        PackVersion = version.ToString(),
                        PackDir = publishDir,
                        OutputDir = releaseDir,
                        Channel = directive,
                    },
                    executionOptions: new CommandExecutionOptions
                    {
                        WorkingDirectory = loomContext.WorkingDirectory,
                    },
                    ct: ct
                );

            results.Add(
                new VelopackArtifactResult(artifact.ArtifactName, releaseDir, version.ToString())
            );
        }

        return results;
    }
}
