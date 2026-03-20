namespace Loom.Modules;

[ModuleCategory("Packaging")]
[DependsOn<RestoreModule>(Optional = true)]
public class MinVerModule : Module<string>
{
    protected override async Task<string?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var options = new MinverOptions() { };
        context.Logger.LogDebug("Minver Options:\n {Options}", options);
        var result = await context.Shell.Command.ExecuteCommandLineTool(
            options: options,
            executionOptions: new CommandExecutionOptions { ThrowOnNonZeroExitCode = true },
            cancellationToken: ct
        );

        string version = result.StandardOutput.Trim();
        context.Logger.LogDebug(version);
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new Exception(
                "MinVer output was empty. Check if Git is initialized and tags exist."
            );
        }

        context.Logger.LogInformation("MinVer calculated version: {Version}", version);
        return version;
    }
}
