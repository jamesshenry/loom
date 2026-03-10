using Loom.Config;

namespace Loom.Modules;

[ModuleCategory("Packaging")]
[DependsOn<RestoreModule>]
public class PublishModule(BuildContext buildContext, IConfiguration configuration)
    : Module<CommandResult>
{
    private readonly IConfiguration _configuration = configuration;

    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildContext.Rid, nameof(buildContext.Rid));

        var publishDir = Path.Combine(
            context.Environment.WorkingDirectory,
            "dist",
            "publish",
            buildContext.Rid
        );

        if (Directory.Exists(publishDir))
        {
            context.Logger.LogInformation(
                "Cleaning existing publish directory: {Path}",
                publishDir
            );
            Directory.Delete(publishDir, true);
        }

        context.Logger.LogInformation(
            "Publishing {Project} for {Rid} in {Config} mode",
            buildContext.Project.EntryProject,
            buildContext.Rid,
            buildContext.Configuration
        );

        return await context
            .DotNet()
            .Publish(
                new DotNetPublishOptions
                {
                    ProjectSolution = buildContext.Project.EntryProject,
                    Configuration = buildContext.Configuration,
                    Output = publishDir,
                    Runtime = buildContext.Rid,
                    NoRestore = true,
                },
                cancellationToken: ct
            );
    }
}
