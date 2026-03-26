using Loom.Config;
using ModularPipelines.FileSystem;

namespace Loom.Modules;

public record PublishedArtifact(
    string ArtifactName,
    Folder PublishDirectory,
    string Rid,
    ArtifactType Type
);

public record PublishResult(List<PublishedArtifact> Artifacts);

[ModuleCategory("Packaging")]
[DependsOn<RestoreModule>(Optional = true)]
[DependsOn<BuildModule>(Optional = true)]
public class PublishModule(LoomContext buildContext) : Module<PublishResult>
{
    protected override ModuleConfiguration Configure() =>
        ModuleConfiguration
            .Create()
            .WithSkipWhen(ctx =>
                !buildContext.Artifacts.Any(a =>
                    a.Value.Type == ArtifactType.Executable || a.Value.Type == ArtifactType.Velopack
                )
                    ? SkipDecision.Skip("No velopack or executable artifacts defined in loom.json")
                    : SkipDecision.DoNotSkip
            )
            .Build();

    protected override async Task<PublishResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var publishableArtifacts = buildContext
            .Artifacts.Where(a =>
                a.Value.Type == ArtifactType.Executable || a.Value.Type == ArtifactType.Velopack
            )
            .ToList();

        var results = new List<PublishedArtifact>();

        foreach (var (artifactName, artifactSettings) in publishableArtifacts)
        {
            var rid = artifactSettings.Rid ?? buildContext.Rid;
            ArgumentException.ThrowIfNullOrWhiteSpace(rid, nameof(rid));

            var publishDirPath = Path.Combine(
                buildContext.WorkingDirectory,
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
                    executionOptions: new CommandExecutionOptions
                    {
                        WorkingDirectory = buildContext.WorkingDirectory,
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

        return new PublishResult(results);
    }
}
