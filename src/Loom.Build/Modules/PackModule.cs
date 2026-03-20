using Loom.Config;
using ModularPipelines.FileSystem;
using File = ModularPipelines.FileSystem.File;
using SearchOption = System.IO.SearchOption;

namespace Loom.Modules;

[ModuleCategory("Packaging")]
[DependsOn<BuildModule>(Optional = true)]
public class PackModule(LoomContext buildContext) : Module<List<File>>
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

    protected override async Task<List<File>?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var nugetArtifacts = buildContext
            .Artifacts.Where(a => a.Value.Type == ArtifactType.Nuget)
            .ToList();

        var outputDir = Path.Combine(
            context.Environment.WorkingDirectory,
            buildContext.ArtifactsDirectory,
            "nuget"
        );

        foreach (var (artifactName, artifactSettings) in nugetArtifacts)
        {
            context.Logger.LogInformation(
                "Packing {ArtifactName} ({Project})",
                artifactName,
                artifactSettings.Project
            );

            await context
                .DotNet()
                .Pack(
                    new DotNetPackOptions
                    {
                        ProjectSolution = artifactSettings.Project,
                        Configuration = buildContext.Configuration,
                        Output = outputDir,
                        NoBuild = true,
                    },
                    cancellationToken: ct
                );
        }

        var provider = context.Services.Get<IFileSystemProvider>();

        return provider
            .EnumerateFiles(outputDir, "*", SearchOption.TopDirectoryOnly)
            .Where(f =>
                f.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase)
            )
            .Select(f => context.Files.GetFile(f))
            .ToList();
    }
}
