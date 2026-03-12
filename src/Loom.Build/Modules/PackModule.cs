using Loom.Config;
using File = ModularPipelines.FileSystem.File;

namespace Loom.Modules;

[ModuleCategory("Packaging")]
[DependsOn<BuildModule>] // Pack usually depends on Build
public class PackModule(LoomContext buildContext) : Module<List<File>>
{
    protected override async Task<List<File>?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var outputDir = Path.Combine(context.Environment.WorkingDirectory, "dist");

        context.Logger.LogInformation("Packing {Project}", buildContext.Project.EntryProject);
        return [];
        // return await context
        //     .DotNet()
        //     .Pack(
        //         new DotNetPackOptions
        //         {
        //             ProjectSolution = buildContext.Project.EntryProject,
        //             Configuration = buildContext.Configuration,
        //             Output = outputDir,
        //             NoBuild = true, // We already built `in BuildModule
        //             IncludeSymbols = true,
        //         },
        //         cancellationToken: ct
        //     );
    }
}
