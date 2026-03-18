using Loom.Config;
using ModularPipelines.FileSystem;
using File = ModularPipelines.FileSystem.File;
using SearchOption = System.IO.SearchOption;

namespace Loom.Modules;

[ModuleCategory("Packaging")]
[DependsOn<BuildModule>(Optional = true)]
public class PackModule(LoomContext buildContext) : Module<List<File>>
{
    protected override async Task<List<File>?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var nugetArtifacts = buildContext
            .Artifacts.Where(a => a.Value.Type.Equals("NuGet", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (nugetArtifacts.Count == 0)
        {
            context.Logger.LogInformation("No NuGet artifacts defined. Skipping pack.");
            return [];
        }

        var outputDir = Path.Combine(
            context.Environment.WorkingDirectory,
            buildContext.ArtifactsDirectory,
            "nuget"
        );

        string globalVersion;
        try
        {
            var versionModule = await context.GetModule<MinVerModule>();
            globalVersion = versionModule.ValueOrDefault ?? buildContext.Version;
        }
        catch
        {
            globalVersion = buildContext.Version;
        }

        foreach (var (artifactName, artifactSettings) in nugetArtifacts)
        {
            context.Logger.LogInformation(
                "Packing {ArtifactName} ({Project})",
                artifactName,
                artifactSettings.Project
            );

            var packVersion = artifactSettings.Version ?? globalVersion;

            await context
                .DotNet()
                .Pack(
                    new DotNetPackOptions
                    {
                        ProjectSolution = artifactSettings.Project,
                        Configuration = buildContext.Configuration,
                        Output = outputDir,
                        NoBuild = true,
                        IncludeSymbols = true,
                        Properties = [new("Version", packVersion)],
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
