namespace Loom.Modules;

[ModuleCategory("Delivery")]
[DependsOn<PackModule>]
public class NuGetUploadModule(BuildContext buildContext) : Module<CommandResult[]>
{
    protected override async Task<CommandResult[]?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var nupkgs = context.Files.Glob("dist/**.nupkg");
        if (!nupkgs.Any())
        {
            context.Logger.LogWarning("No NuGet packages found to upload.");
            return null;
        }

        var results = new List<CommandResult>();
        var apiKey = buildContext.NugetApiKey;
        var count = 0;

        foreach (var package in nupkgs)
        {
            count++;

            context.Logger.LogInformation(
                "Preparing to push package: {Package} {count}/{totalCount}",
                package.Name,
                count,
                nupkgs.Count()
            );

            if (string.IsNullOrEmpty(apiKey))
            {
                context.Logger.LogWarning(
                    "Skipping NuGet push for {Package} (No API Key found).",
                    package.Name
                );
                continue;
            }

            var result = await context
                .DotNet()
                .Nuget.Push(
                    new DotNetNugetPushOptions
                    {
                        Path = package,
                        Source = "https://api.nuget.org/v3/index.json",
                        ApiKey = apiKey,
                    },
                    cancellationToken: ct
                );

            results.Add(result);
        }

        return [.. results];
    }
}

[ModuleCategory("Packaging")]
[DependsOn<BuildModule>] // Pack usually depends on Build
public class PackModule(BuildContext buildContext) : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var outputDir = Path.Combine(context.Environment.WorkingDirectory, "dist");

        context.Logger.LogInformation("Packing {Project}", buildContext.Project.EntryProject);

        return await context
            .DotNet()
            .Pack(
                new DotNetPackOptions
                {
                    ProjectSolution = buildContext.Project.EntryProject,
                    Configuration = buildContext.Configuration,
                    Output = outputDir,
                    NoBuild = true, // We already built in BuildModule
                    IncludeSymbols = true,
                },
                cancellationToken: ct
            );
    }
}
