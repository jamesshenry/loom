using Loom.Config;
using File = ModularPipelines.FileSystem.File;

namespace Loom.Modules;

[ModuleCategory("Delivery")]
[DependsOn<PackModule>]
public class NugetUploadModule(LoomContext loomContext) : Module<CommandResult[]>
{
    public NugetUploadModule(LoomContext loomContext, IEnumerable<File>? overridePackages = null)
        : this(loomContext) { }

    public bool IsDryRun { get; set; }

    protected override ModuleConfiguration Configure() =>
        ModuleConfiguration
            .Create()
            .WithSkipWhen(ctx =>
                !loomContext.Artifacts.Any(x => x.Value.Type == ArtifactType.Nuget)
                    ? SkipDecision.Skip("No nuget artifacts defined in loom.json")
                    : SkipDecision.DoNotSkip
            )
            .WithSkipWhen(ctx =>
                !loomContext.EnableNugetUpload
                    ? SkipDecision.Skip("NuGet upload disabled in workspace settings.")
                    : SkipDecision.DoNotSkip
            )
            .WithSkipWhen(ctx =>
                ctx.IsRunningLocally()
                    ? SkipDecision.Skip("Should not be run locally.")
                    : SkipDecision.DoNotSkip
            )
            .Build();

    protected override async Task<CommandResult[]?> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken
    )
    {
        var packModule = await context.GetModule<PackModule>();
        var packages = packModule.ValueOrDefault ?? [];

        var results = new List<CommandResult>();
        foreach (var package in packages)
        {
            var nugetPushOptions = new DotNetNugetPushOptions
            {
                Path = package.Path,
                ApiKey = loomContext.NugetApiKey,
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
