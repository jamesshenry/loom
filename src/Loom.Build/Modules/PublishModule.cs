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
[DependsOn<MinVerModule>(Optional = true)]
public class PublishModule(LoomContext buildContext) : Module<PublishResult>
{
    private async Task<bool> IsAotEnabled(string projectPath, IModuleContext context)
    {
        // Simple robust check: does the csproj text contain <PublishAot>true
        // You can use context.Files to read it
        var file = context.Files.GetFile(projectPath);
        if (!file.Exists) return false;

        var content = await file.ReadAsync();
        return content.Contains("<PublishAot>true", StringComparison.OrdinalIgnoreCase);
    }
    protected override ModuleConfiguration Configure()
    {
        return ModuleConfiguration
            .Create()
            .WithSkipWhen(ctx =>
            {
                return !buildContext.ResolvedArtifacts.Any(a => a.CanBuildOnHost && a.Settings.Type != ArtifactType.Nuget)
                    ? SkipDecision.Skip("No compatible artifacts for this host.")
                    : SkipDecision.DoNotSkip;
            })
            .Build();
    }

    protected override async Task<PublishResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var minVerModule = await context.GetModule<MinVerModule>();
        var minVerResult = minVerModule.ValueOrDefault;

        var publishableArtifacts = buildContext.ResolvedArtifacts
            .Where(a => a.CanBuildOnHost && (a.Settings.Type == ArtifactType.Executable || a.Settings.Type == ArtifactType.Velopack));


        var results = new List<PublishedArtifact>();

        foreach (var artifact in publishableArtifacts)
        {

            var publishDirPath = Path.Combine(
                buildContext.WorkingDirectory,
                buildContext.ArtifactsDirectory,
                "publish",
                artifact.Name,
                artifact.Rid
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
                artifact.Name,
                artifact.Settings.Project,
                artifact.Rid,
                buildContext.Configuration
            );

            var versionProperties = PublishHelpers.CreateVersionProperties(
                PublishHelpers.ResolveVersion(artifact.Settings, minVerResult)
            );

            await context
                .DotNet()
                .Publish(
                    new DotNetPublishOptions
                    {
                        ProjectSolution = artifact.Settings.Project,
                        Configuration = buildContext.Configuration,
                        Output = publishFolder.Path,
                        Runtime = artifact.Rid,
                        Properties = versionProperties,
                    },
                    executionOptions: new CommandExecutionOptions
                    {
                        WorkingDirectory = buildContext.WorkingDirectory,
                    },
                    cancellationToken: ct
                );

            results.Add(
                new PublishedArtifact(
                    ArtifactName: artifact.Name,
                    PublishDirectory: publishFolder,
                    Rid: artifact.Rid,
                    Type: artifact.Settings.Type
                )
            );
        }

        return new PublishResult(results);
    }
}
