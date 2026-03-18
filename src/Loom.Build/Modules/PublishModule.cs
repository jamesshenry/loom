using Loom.Config;
using ModularPipelines.FileSystem;

namespace Loom.Modules;

public record PublishedArtifact(
    string ArtifactName,
    Folder PublishDirectory,
    string Rid,
    string Type
);

[ModuleCategory("Packaging")]
[DependsOn<RestoreModule>(Optional = true)]
public class PublishModule(LoomContext buildContext) : Module<List<PublishedArtifact>>
{
    protected override async Task<List<PublishedArtifact>?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var publishableArtifacts = buildContext
            .Artifacts.Where(a =>
                a.Value.Type.Equals("Executable", StringComparison.OrdinalIgnoreCase)
                || a.Value.Type.Equals("Velopack", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        if (publishableArtifacts.Count == 0)
        {
            context.Logger.LogInformation("No Executable or Velopack artifacts defined. Skipping.");
            return [];
        }

        var results = new List<PublishedArtifact>();

        foreach (var (artifactName, artifactSettings) in publishableArtifacts)
        {
            var rid = artifactSettings.Rid ?? buildContext.Rid;
            ArgumentException.ThrowIfNullOrWhiteSpace(rid, nameof(rid));

            var publishDirPath = Path.Combine(
                context.Environment.WorkingDirectory,
                buildContext.ArtifactsDirectory,
                "publish",
                artifactName,
                rid
            );

            var publishFolder = context.Files.GetFolder(publishDirPath);

            if (publishFolder.Exists)
            {
                context.Logger.LogInformation(
                    "Cleaning existing publish directory: {Path}",
                    publishFolder.Path
                );
                await publishFolder.DeleteAsync(ct);
            }

            context.Logger.LogInformation(
                "Publishing {ArtifactName} ({Project}) for {Rid} in {Config} mode",
                artifactName,
                artifactSettings.Project,
                rid,
                buildContext.Configuration
            );

            await context
                .DotNet()
                .Publish(
                    new DotNetPublishOptions
                    {
                        ProjectSolution = artifactSettings.Project,
                        Configuration = buildContext.Configuration,
                        Output = publishFolder.Path,
                        Runtime = rid,
                        NoRestore = true,
                    },
                    cancellationToken: ct
                );

            results.Add(
                new PublishedArtifact(
                    ArtifactName: artifactName,
                    PublishDirectory: publishFolder,
                    Rid: rid,
                    Type: artifactSettings.Type
                )
            );
        }

        return results;
    }
}
