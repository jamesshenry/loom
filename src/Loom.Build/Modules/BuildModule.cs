using Loom.Config;

namespace Loom.Modules;

public record BuildResult(string? Output);

[ModuleCategory("Build")]
[DependsOn<RestoreModule>(Optional = true)]
[DependsOn<MinVerModule>(Optional = true)]
public class BuildModule(LoomContext buildContext) : Module<BuildResult>
{
    protected override async Task<BuildResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var minVerModule = await context.GetModule<MinVerModule>();
        var minVerResult = minVerModule.ValueOrDefault;
        var versionProperties = PublishHelpers.CreateVersionProperties(
            PublishHelpers.ResolveVersion(minVerResult)
        );

        var result = await context
            .DotNet()
            .Build(
                new DotNetBuildOptions
                {
                    ProjectSolution = buildContext.Solution,
                    NoRestore = true,
                    Configuration = buildContext.Configuration,
                    Properties = versionProperties,
                },
                executionOptions: new CommandExecutionOptions
                {
                    WorkingDirectory = buildContext.WorkingDirectory,
                },
                cancellationToken: ct
            );
        return new BuildResult(result.StandardOutput);
    }
}
