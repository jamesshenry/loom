using Loom.Config;

namespace Loom.Modules;

public record RestoreResult(CommandResult CommandResult);

[ModuleCategory("Preparation")]
[DependsOn<RestoreToolsModule>(Optional = true)]
[DependsOn<CleanModule>(Optional = true)]
public class RestoreModule(LoomContext buildContext, IConfiguration configuration)
    : Module<RestoreResult>
{
    private readonly IConfiguration _configuration = configuration;

    protected override async Task<RestoreResult?> ExecuteAsync(
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
                    ProjectSolution = buildContext.Solution,
                },
                executionOptions: new CommandExecutionOptions
                {
                    WorkingDirectory = buildContext.WorkingDirectory,
                    ThrowOnNonZeroExitCode = true,
                },
                cancellationToken: ct
            );
        return new RestoreResult(result);
    }
}
