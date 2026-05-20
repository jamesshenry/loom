using Loom.Config;
using Loom.MinVer;
using Loom.Velopack;
using Loom.Velopack.Options;

using static Loom.Modules.PublishHelpers;

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
                // Only run if there are Velopack artifacts that the current host is capable of building
                var hasCompatibleArtifacts = loomContext.ResolvedArtifacts
                    .Any(a => a.CanBuildOnHost && a.Settings.Type == ArtifactType.Velopack);

                return !hasCompatibleArtifacts
                    ? SkipDecision.Skip("No compatible Velopack artifacts for this host.")
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
        var publishedArtifacts = publishModule.ValueOrDefault?.Artifacts ?? [];

        var minVerModule = await context.GetModule<MinVerModule>();
        var minVerResult = minVerModule.ValueOrDefault;

        var results = new List<VelopackArtifactResult>();

        // Filter for artifacts that are marked as Velopack and were compatible with this host
        var targetArtifacts = loomContext.ResolvedArtifacts
            .Where(a => a.CanBuildOnHost && a.Settings.Type == ArtifactType.Velopack);

        foreach (var artifact in targetArtifacts)
        {
            var publishedInfo = publishedArtifacts.FirstOrDefault(p =>
                p.ArtifactName.Equals(artifact.Name, StringComparison.OrdinalIgnoreCase));

            if (publishedInfo == null)
            {
                context.Logger.LogWarning("Expected published output for {Name} was not found. Skipping Velopack packaging.", artifact.Name);
                continue;
            }

            var version = !string.IsNullOrWhiteSpace(artifact.Settings.Version)
                ? MinVerVersion.From(artifact.Settings.Version)
                : minVerResult?.GetVersion(artifact.Settings.TagPrefix);

            ArgumentNullException.ThrowIfNull(version, nameof(version));

            var packId = artifact.Settings.VelopackId ?? artifact.Name;
            var publishDir = publishedInfo.PublishDirectory.Path;
            var releaseDir = Path.Combine(
                loomContext.WorkingDirectory,
                loomContext.ArtifactsDirectory,
                "release",
                artifact.Name,
                artifact.Rid
            );

            VelopackPackBaseOptions velopackPackOptions = new()
            {
                PackId = packId,
                PackVersion = version.ToString(),
                PackDir = publishDir,
                OutputDir = releaseDir,
                Runtime = artifact.Rid,
            };

            velopackPackOptions = artifact.Rid.ToLower() switch
            {
                var r when r.StartsWith("win") => new DotNetVelopackPackWinOptions() with
                {
                    PackId = velopackPackOptions.PackId,
                    PackVersion = velopackPackOptions.PackVersion,
                    PackDir = velopackPackOptions.PackDir,
                    OutputDir = velopackPackOptions.OutputDir,
                    Runtime = velopackPackOptions.Runtime,
                    Shortcuts = "None",
                },
                var r when r.StartsWith("linux") => velopackPackOptions,
                _ => throw new NotSupportedException("Switch case not supported"),
            };
            await context
                .Velopack()
                .ExecuteAsync(
                    velopackPackOptions,
                    executionOptions: new CommandExecutionOptions
                    {
                        WorkingDirectory = loomContext.WorkingDirectory,
                    },
                    ct: ct
                );

            results.Add(new VelopackArtifactResult(artifact.Name, releaseDir, version.ToString()));
        }

        return results;
    }
}
