using Loom.Config;

namespace Loom.Modules;

[ModuleCategory("Packaging")]
[DependsOn<PublishModule>]
[DependsOn<MinVerModule>]
public class VelopackReleaseModule(LoomContext buildContext) : Module<CommandResult>
{
    // protected override ModuleConfiguration Configure()
    // {
    //     return ModuleConfiguration
    //         .Create()
    //         .WithSkipWhen(() =>
    //             buildContext.SkipPkg
    //                 ? SkipDecision.Skip("Packaging explicitly skipped")
    //                 : SkipDecision.DoNotSkip
    //         )
    //         .Build();
    // }

    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var versionModule = await context.GetModule<MinVerModule>();
        var version = versionModule.ValueOrDefault;

        ArgumentException.ThrowIfNullOrWhiteSpace(version, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(buildContext.Rid, nameof(buildContext.Rid));
        ArgumentException.ThrowIfNullOrWhiteSpace(
            buildContext.VelopackId,
            nameof(buildContext.VelopackId)
        );

        var root = context.Environment.WorkingDirectory;
        var publishDir = Path.Combine(root, "dist", "publish", buildContext.Rid);
        var releaseDir = Path.Combine(root, "dist", "release", buildContext.Rid);

        string directive = buildContext.Rid.ToLower() switch
        {
            var r when r.StartsWith("win") => "[win]",
            var r when r.StartsWith("osx") => "[osx]",
            var r when r.StartsWith("linux") => "[linux]",
            _ => throw new NotSupportedException(
                $"RID {buildContext.Rid} is not supported by Velopack."
            ),
        };

        context.Logger.LogInformation(
            "Packaging {Id} {Version} for {Rid} using directive {Directive}",
            buildContext.VelopackId,
            version,
            buildContext.Rid,
            directive
        );

        return await context.Shell.Command.ExecuteCommandLineTool(
            new VelopackOptions
            {
                Arguments =
                [
                    "vpk",
                    directive,
                    "pack",
                    "--packId",
                    buildContext.VelopackId,
                    "--packVersion",
                    version,
                    "--packDir",
                    publishDir,
                    "--outputDir",
                    releaseDir,
                    "--yes",
                ],
            },
            cancellationToken: ct
        );
    }
}
