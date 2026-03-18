using Loom.Config;
using ModularPipelines.FileSystem;
using File = ModularPipelines.FileSystem.File;
using SearchOption = System.IO.SearchOption;

namespace Loom.Modules;

[ModuleCategory("Packaging")]
[DependsOn<BuildModule>(Optional = true)] // Runs after Build when present, but can run standalone
public class PackModule(LoomContext buildContext) : Module<List<File>>
{
    protected override async Task<List<File>?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        // 1. Find all artifacts marked as NuGet
        var nugetArtifacts = buildContext
            .Artifacts.Where(a => a.Value.Type.Equals("NuGet", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (nugetArtifacts.Count == 0)
        {
            context.Logger.LogInformation("No NuGet artifacts defined. Skipping pack.");
            return [];
        }

        // 2. Use the new ArtifactsDirectory instead of hardcoded "dist"
        var outputDir = Path.Combine(
            context.Environment.WorkingDirectory,
            buildContext.ArtifactsDirectory,
            "nuget"
        );

        // 3. Resolve global version
        string globalVersion;
        try
        {
            var versionModule = await context.GetModule<MinVerModule>();
            globalVersion = versionModule.ValueOrDefault ?? buildContext.Version;
        }
        catch
        {
            // Fallback to the Context version (which falls back to the DefaultVersionPrefix in loom.json)
            globalVersion = buildContext.Version;
        }

        // 4. Iterate and pack each NuGet artifact
        foreach (var (artifactName, artifactSettings) in nugetArtifacts)
        {
            context.Logger.LogInformation(
                "Packing {ArtifactName} ({Project})",
                artifactName,
                artifactSettings.Project
            );

            // Allow artifact-specific version override if provided in loom.json
            var packVersion = artifactSettings.Version ?? globalVersion;

            await context
                .DotNet()
                .Pack(
                    new DotNetPackOptions
                    {
                        ProjectSolution = artifactSettings.Project, // Only pack this specific .csproj
                        Configuration = buildContext.Configuration,
                        Output = outputDir,
                        NoBuild = true, // We already built the whole solution in BuildModule
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
