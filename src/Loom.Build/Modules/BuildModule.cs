using Loom.Config;

namespace Loom.Modules;

[DependsOn<RestoreModule>(Optional = true)]
[DependsOn<MinVerModule>]
public class BuildModule(LoomContext buildContext, IConfiguration configuration)
    : Module<CommandResult>
{
    private readonly IConfiguration _configuration = configuration;

    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var version = await context.GetModule<MinVerModule>();

        return await context
            .DotNet()
            .Build(
                new DotNetBuildOptions
                {
                    ProjectSolution = buildContext.Solution,
                    NoRestore = true,
                    Configuration = buildContext.Configuration,
                    Properties = [new("Version", version.ValueOrDefault!)],
                },
                cancellationToken: ct
            );
    }
}
