namespace Loom.Modules;

[ModuleCategory("Packaging")]
[DependsOn<RestoreToolsModule>]
public class MinVerModule(MinVerCache cache) : Module<string>
{
    protected override Task<string?> ExecuteAsync(IModuleContext context, CancellationToken ct) =>
        cache
            .GetOrAddAsync(null, () => RunMinVerAsync(context, null, ct))
            .ContinueWith(t => (string?)t.Result, TaskContinuationOptions.ExecuteSynchronously);

    internal static async Task<string> RunMinVerAsync(
        IModuleContext context,
        string? tagPrefix,
        CancellationToken ct
    )
    {
        var options = new MinverOptions(tagPrefix);
        context.Logger.LogDebug("Minver Options:\n {Options}", options);
        var result = await context.Shell.Command.ExecuteCommandLineTool(
            options: options,
            executionOptions: new CommandExecutionOptions { ThrowOnNonZeroExitCode = true },
            cancellationToken: ct
        );

        var version = result.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new Exception(
                "MinVer output was empty. Check if Git is initialized and tags exist."
            );
        }

        context.Logger.LogInformation(
            "MinVer calculated version (prefix: '{Prefix}'): {Version}",
            tagPrefix ?? "(none)",
            version
        );
        return version;
    }
}
