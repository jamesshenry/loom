using Loom.Config;
using Loom.MinVer;
using ModularPipelines.FileSystem;
using File = ModularPipelines.FileSystem.File;
using SearchOption = System.IO.SearchOption;

namespace Loom.Modules;

public record PackResult(List<File> Artifacts);

[ModuleCategory("Packaging")]
[DependsOn<BuildModule>(Optional = true)]
[DependsOn<MinVerModule>(Optional = true)]
public class PackModule(LoomContext buildContext) : Module<PackResult>
{
    protected override ModuleConfiguration Configure() =>
        ModuleConfiguration
            .Create()
            .WithSkipWhen(ctx =>
                !buildContext.Artifacts.Any(x => x.Value.Type == ArtifactType.Nuget)
                    ? SkipDecision.Skip("No nuget artifacts defined in loom.json")
                    : SkipDecision.DoNotSkip
            )
            .Build();

    protected override async Task<PackResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var nugetArtifacts = buildContext
            .Artifacts.Where(a => a.Value.Type == ArtifactType.Nuget)
            .ToList();

        var outputDir = Path.Combine(
            buildContext.WorkingDirectory,
            buildContext.ArtifactsDirectory,
            "nuget"
        );

        var minVerModule = await context.GetModule<MinVerModule>();
        var minVerResult = minVerModule.ValueOrDefault;

        foreach (var (artifactName, artifactSettings) in nugetArtifacts)
        {
            context.Logger.LogInformation(
                "Packing {ArtifactName} ({Project})",
                artifactName,
                artifactSettings.Project
            );

            var version = !string.IsNullOrWhiteSpace(artifactSettings.Version)
                ? MinVerVersion.From(artifactSettings.Version)
                : minVerResult?.GetVersion(artifactSettings.TagPrefix);

            var properties = new List<KeyValue>();
            if (!string.IsNullOrWhiteSpace(version!.ToString()))
            {
                properties.Add(new("Version", version.ToString()));
            }

            await context
                .DotNet()
                .Pack(
                    new DotNetPackOptions
                    {
                        ProjectSolution = artifactSettings.Project,
                        Configuration = buildContext.Configuration,
                        Output = outputDir,
                        NoBuild = true,
                        Properties = properties.Count > 0 ? properties : null,
                    },
                    executionOptions: new CommandExecutionOptions
                    {
                        WorkingDirectory = buildContext.WorkingDirectory,
                    },
                    cancellationToken: ct
                );
        }

        var provider = context.Services.Get<IFileSystemProvider>();

        var nupkgs = provider
            .EnumerateFiles(outputDir, "*", SearchOption.TopDirectoryOnly)
            .Where(f =>
                f.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase)
            );
        var files = nupkgs.Select(f => context.Files.GetFile(f)).ToList();

        return new PackResult(files);
    }
}
