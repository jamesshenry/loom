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
            .Append(string.Empty)
            .Distinct();

        var tasks = prefixes.Select(async prefix =>
        {
            var tagPrefix = string.IsNullOrEmpty(prefix) ? null : prefix;
            var version = await context
                .MinVer()
                .Run(
                    new DotNetMinVerOptions
                    {
                        TagPrefix = tagPrefix,
                        DefaultPreReleaseIdentifiers = loomContext.DefaultPreReleaseIdentifiers,
                    }
                );
            return KeyValuePair.Create(prefix, version);
        });
        var results = await Task.WhenAll(tasks);

        return new MinVerResult(results.ToDictionary());
    }
}
