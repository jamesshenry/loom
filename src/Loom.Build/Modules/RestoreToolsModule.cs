using Loom.Config;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace Loom.Modules;

[ModuleCategory("Preparation")]
public class RestoreToolsModule : Module<CommandResult>
{
    private readonly LoomContext _loom;

    public RestoreToolsModule(LoomContext loom)
    {
        _loom = loom;
    }

    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var rootManifest = Path.Combine(context.Environment.WorkingDirectory, "dotnet-tools.json");
        var configManifest = Path.Combine(
            context.Environment.WorkingDirectory,
            ".config",
            "dotnet-tools.json"
        );

        var manifestExists = File.Exists(rootManifest) || File.Exists(configManifest);

        if (!manifestExists)
        {
            context.Logger.LogInformation("No dotnet tool manifest found. Creating one...");
            await context
                .DotNet()
                .New.Execute(
                    new DotNetNewOptions() { Arguments = ["tool-manifest"] },
                    cancellationToken: ct
                );
        }

        if (_loom.RequiresMinVer)
        {
            await EnsureToolInstalled(context, "minver-cli", ct);
        }

        if (_loom.RequiresVelopack)
        {
            await EnsureToolInstalled(context, "vpk", ct);
        }

        context.Logger.LogInformation("Restoring dotnet local tools...");
        return await context
            .DotNet()
            .Tool.Restore(new DotNetToolRestoreOptions(), cancellationToken: ct);
    }

    private async Task EnsureToolInstalled(
        IModuleContext context,
        string toolId,
        CancellationToken ct
    )
    {
        var listResult = await context
            .DotNet()
            .Tool.Execute(new DotNetToolOptions() { Arguments = ["list"] }, cancellationToken: ct);

        if (!listResult.StandardOutput.Contains(toolId, StringComparison.OrdinalIgnoreCase))
        {
            context.Logger.LogInformation(
                "Tool {ToolId} not found in manifest. Installing...",
                toolId
            );
            await context
                .DotNet()
                .Tool.Execute(
                    new DotNetToolOptions() { Arguments = ["install", toolId] },
                    cancellationToken: ct
                );
        }
    }
}
