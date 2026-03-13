using Loom.Config;

namespace Loom.Modules;

[ModuleCategory("Preparation")]
[DependsOn<CleanModule>(Optional = true)]
public class RestoreToolsModule : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        if (
            !File.Exists(
                Path.Combine(context.Environment.WorkingDirectory, ".config", "dotnet-tools.json")
            )
        )
        {
            context.Logger.LogInformation(
                "No dotnet-tools.json found in .config directory. Skipping tool restore."
            );
            return null;
        }

        context.Logger.LogInformation("Restoring dotnet local tools...");

        return await context
            .DotNet()
            .Tool.Restore(new DotNetToolRestoreOptions(), cancellationToken: ct);
    }
}
