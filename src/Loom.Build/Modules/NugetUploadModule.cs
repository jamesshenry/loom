using Loom.Config;
using File = ModularPipelines.FileSystem.File;

namespace Loom.Modules;

public record NugetUploadResult(CommandResult[] Results);

[ModuleCategory("Delivery")]
[DependsOn<PackModule>(Optional = true)]
public class NugetUploadModule(LoomContext loomContext) : Module<NugetUploadResult>
{
    public NugetUploadModule(LoomContext loomContext, IEnumerable<File>? overridePackages = null)
        : this(loomContext) { }

    public bool IsDryRun { get; set; }

    protected override ModuleConfiguration Configure() =>
        ModuleConfiguration
            .Create()
            .WithSkipWhen(ctx =>
            {
                if (!loomContext.Artifacts.Any(x => x.Value.Type == ArtifactType.Nuget))
                    return SkipDecision.Skip("No nuget artifacts defined in loom.json");
                if (!loomContext.EnableNugetUpload)
                    return SkipDecision.Skip("NuGet upload disabled in workspace settings.");
                if (
                    Environment.GetEnvironmentVariable("LOOM_IGNORE_LOCAL_CHECK") != "true"
                    && ctx.IsRunningLocally()
                )
                    return SkipDecision.Skip("Should not be run locally.");
                return SkipDecision.DoNotSkip;
            })
            .Build();

    protected override async Task<NugetUploadResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken
    )
    {
        var packModule = await context.GetModule<PackModule>();

        var packages = packModule.ValueOrDefault?.Artifacts ?? [];

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

        return new NugetUploadResult([.. results]);
    }
}
