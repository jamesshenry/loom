using System.Collections.Concurrent;
using Loom.Config;
using Loom.MinVer;
using Loom.MinVer.Options;

namespace Loom.Modules;

public record MinVerResult(IReadOnlyDictionary<string, MinVerVersion> Versions)
{
    public MinVerVersion GetVersion(string? tagPrefix) =>
        Versions.GetValueOrDefault(tagPrefix ?? string.Empty, MinVerVersion.V1);
}

[ModuleCategory("Packaging")]
[DependsOn<RestoreToolsModule>(Optional = true)]
public class MinVerModule(LoomContext loomContext) : Module<MinVerResult>
{
    protected override async Task<MinVerResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var prefixes = loomContext
            .Artifacts.Values.Select(a => a.TagPrefix ?? string.Empty)
            .Distinct()
            .ToList();

        if (!prefixes.Contains(string.Empty))
        {
            prefixes.Add(string.Empty);
        }

        var results = new ConcurrentDictionary<string, MinVerVersion>();

        await Task.WhenAll(
            prefixes.Select(async prefix =>
            {
                var tagPrefix = string.IsNullOrEmpty(prefix) ? null : prefix;
                var options = new DotNetMinVerOptions() { TagPrefix = tagPrefix };
                var version = await context.MinVer().Run(options);

                results[prefix] = version;
            })
        );

        return new MinVerResult(results);
    }

    // private static async Task<string> RunMinVerAsync(
    //     IModuleContext context,
    //     string? tagPrefix,
    //     string workingDirectory,
    //     CancellationToken ct
    // )
    // {
    //     var options = new MinVerOptions();
    //     context.Logger.LogDebug("Minver Options:\n {Options}", options);
    //     var result = await context.Shell.Command.ExecuteCommandLineTool(
    //         options: options,
    //         executionOptions: new CommandExecutionOptions
    //         {
    //             ThrowOnNonZeroExitCode = true,
    //             WorkingDirectory = workingDirectory,
    //         },
    //         cancellationToken: ct
    //     );

    //     var version = result.StandardOutput.Trim();
    //     if (string.IsNullOrWhiteSpace(version))
    //     {
    //         throw new Exception(
    //             "MinVer output was empty. Check if Git is initialized and tags exist."
    //         );
    //     }

    //     context.Logger.LogInformation(
    //         "MinVer calculated version (prefix: '{Prefix}'): {Version}",
    //         tagPrefix ?? "(none)",
    //         version
    //     );
    //     return version;
    // }
}
