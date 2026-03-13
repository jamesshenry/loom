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
        var outputDir = Path.Combine(context.Environment.WorkingDirectory, "dist");

        context.Logger.LogInformation("Packing {Project}", buildContext.MainProject);

        string? version;
        try
        {
            var versionModule = await context.GetModule<MinVerModule>();
            version = versionModule.ValueOrDefault;
        }
        catch
        {
            // Fallback for tests or when MinVer isn't registered
            version = "1.0.0";
        }

        if (buildContext.Target == BuildTarget.Test)
        {
            return [];
        }

        await context
            .DotNet()
            .Pack(
                new DotNetPackOptions
                {
                    ProjectSolution = buildContext.MainProject,
                    Configuration = buildContext.Configuration,
                    Output = outputDir,
                    NoBuild = true, // We already built in BuildModule
                    IncludeSymbols = true,
                    Properties = [new("Version", version!)],
                },
                cancellationToken: ct
            );

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
