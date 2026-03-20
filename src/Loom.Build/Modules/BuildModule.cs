using Loom.Config;

namespace Loom.Modules;

[ModuleCategory("Build")]
[DependsOn<RestoreModule>(Optional = true)]
public class BuildModule(LoomContext buildContext, IConfiguration configuration)
    : Module<CommandResult>
{
    private readonly IConfiguration _configuration = configuration;

    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        return await context
            .DotNet()
            .Build(
                new DotNetBuildOptions
                {
                    ProjectSolution = buildContext.Solution,
                    NoRestore = true,
                    Configuration = buildContext.Configuration,
                },
                cancellationToken: ct
            );
    }
}
