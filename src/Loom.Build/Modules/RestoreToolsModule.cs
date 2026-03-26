using Loom.Config;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace Loom.Modules;

public record RestoreToolsResult(CommandResult CommandResult);

[ModuleCategory("Preparation")]
public class RestoreToolsModule : Module<RestoreToolsResult>
{
    private readonly LoomContext _loom;

    public RestoreToolsModule(LoomContext loom)
    {
        _loom = loom;
    }

    protected override ModuleConfiguration Configure() =>
        ModuleConfiguration
            .Create()
            .WithSkipWhen(ctx =>
                !_loom.RequiresMinVer && !_loom.RequiresVelopack
                    ? SkipDecision.Skip("No dotnet tools required for this build target")
                    : SkipDecision.DoNotSkip
            )
            .Build();

    protected override async Task<RestoreToolsResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var rootManifest = Path.Combine(_loom.WorkingDirectory, "dotnet-tools.json");
        var configManifest = Path.Combine(_loom.WorkingDirectory, ".config", "dotnet-tools.json");

        var manifestExists = File.Exists(rootManifest) || File.Exists(configManifest);

        if (!manifestExists)
        {
            context.Logger.LogInformation("No dotnet tool manifest found. Creating one...");
            await context
                .DotNet()
                .New.Execute(
                    new DotNetNewOptions() { Arguments = ["tool-manifest", "--force"] },
                    executionOptions: new CommandExecutionOptions
                    {
                        WorkingDirectory = _loom.WorkingDirectory,
                    },
                    cancellationToken: ct
                );
        }

        if (_loom.RequiresMinVer)
        {
            await context
                .DotNet()
                .Tool.Execute(
                    new DotNetToolOptions() { Arguments = ["install", "minver-cli"] },
                    executionOptions: new CommandExecutionOptions
                    {
                        WorkingDirectory = _loom.WorkingDirectory,
                    },
                    cancellationToken: ct
                );
        }

        if (_loom.RequiresVelopack)
        {
            await context
                .DotNet()
                .Tool.Execute(
                    new DotNetToolOptions() { Arguments = ["install", "vpk"] },
                    executionOptions: new CommandExecutionOptions
                    {
                        WorkingDirectory = _loom.WorkingDirectory,
                    },
                    cancellationToken: ct
                );
        }

        context.Logger.LogInformation("Restoring dotnet local tools...");
        var result = await context
            .DotNet()
            .Tool.Restore(
                new DotNetToolRestoreOptions(),
                executionOptions: new CommandExecutionOptions
                {
                    WorkingDirectory = _loom.WorkingDirectory,
                },
                cancellationToken: ct
            );

        return new RestoreToolsResult(result);
    }
}
