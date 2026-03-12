using Loom.Config;

namespace Loom.Modules;

[ModuleCategory("Preparation")]
[DependsOn<RestoreToolsModule>]
public class RestoreModule(LoomContext buildContext, IConfiguration configuration)
    : Module<CommandResult>
{
    private readonly IConfiguration _configuration = configuration;

    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        context.Logger.LogDebug("Restoring slnx");
        var result = await context
            .DotNet()
            .Restore(
                new DotNetRestoreOptions
                {
                    ProjectSolution = buildContext.Project.Solution,
                    Runtime = buildContext.Rid,
                },
                executionOptions: new CommandExecutionOptions() { ThrowOnNonZeroExitCode = true },
                cancellationToken: ct
            );
        return result;
    }
}
