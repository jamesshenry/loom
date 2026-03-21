using Loom.Config;
using File = ModularPipelines.FileSystem.File;

namespace Loom.Modules;

[ModuleCategory("Delivery")]
[DependsOn<PackModule>]
public class NugetUploadModule(LoomContext loomContext, IEnumerable<File>? overridePackages = null)
    : Module<CommandResult[]>
{
    public bool IsDryRun { get; set; }

    protected override async Task<CommandResult[]?> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken
    )
    {
        var packages = overridePackages;

        if (packages == null)
        {
            var packModule = await context.GetModule<PackModule>();
            packages = packModule.ValueOrDefault;
        }
        packages ??= [];

        var results = new List<CommandResult>();
        foreach (var package in packages)
        {
            var apiKey = loomContext.NugetApiKey;
            var nugetPushOptions = new DotNetNugetPushOptions
            {
                Path = package.Path,
                ApiKey = apiKey,
                Source = "https://api.nuget.org/v3/index.json",
                SkipDuplicate = true,
            };
            if (IsDryRun)
            {
                context.Logger.LogInformation(
                    "Nuget options: {NugetPushOptions}",
                    nugetPushOptions
                );
                continue;
            }

            var result = await context
                .DotNet()
                .Nuget.Push(nugetPushOptions, cancellationToken: cancellationToken);

            results.Add(result);
        }

        return [.. results];
    }
}
