using Loom.Config;

namespace Loom.Modules;

[ModuleCategory("Preparation")]
public class CleanModule(LoomContext loomContext) : Module<bool>
{
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken ct)
    {
        var dir = await context
            .Git()
            .Commands.RevParse(new GitRevParseOptions() { ShowToplevel = true }, token: ct);

        var repoRoot = dir.StandardOutput.Trim();
        context.Logger.LogInformation("Repo Root: {Directory}", repoRoot);

        var artifacts = context.Files.GetFolder(Path.Combine(repoRoot, ".artifacts"));
        if (artifacts.Exists)
        {
            await artifacts.DeleteAsync(ct);
        }
        context.Logger.LogInformation("{artifacts} folder deleted.", artifacts);
        var dist = context.Files.GetFolder(Path.Combine(repoRoot, loomContext.ArtifactsDirectory));
        if (dist.Exists)
        {
            await dist.DeleteAsync(ct);
        }
        context.Logger.LogInformation("{dist} folder deleted.", dist);

        return true;
    }
}
