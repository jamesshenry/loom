using Loom.Config;
using ModularPipelines.GitHub.Extensions;
using ModularPipelines.GitHub.Options;
using Octokit;

namespace Loom.Modules;

[ModuleCategory("Delivery")]
[DependsOn<MinVerModule>]
[DependsOn<VelopackReleaseModule>] // Wait for Velopack to finish creating assets
public class GitHubReleaseModule(LoomContext loomContext) : Module<Release>
{
    protected override ModuleConfiguration Configure() =>
        ModuleConfiguration
            .Create()
            .WithSkipWhen(_ =>
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_TOKEN"))
                && string.IsNullOrWhiteSpace(loomContext.GitHubToken)
                    ? SkipDecision.Skip("No GitHub token provided.")
                    : SkipDecision.DoNotSkip
            )
            .WithSkipWhen(ctx =>
            {
                return !loomContext.Artifacts.Any(x => x.Value.Type == ArtifactType.Velopack)
                    ? SkipDecision.Skip("No velopack artifacts defined in loom.json")
                    : SkipDecision.DoNotSkip;
            })
            .Build();

    protected override async Task<Release?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var minVerModule = await context.GetModule<MinVerModule>();
        var version = minVerModule.ValueOrDefault ?? throw new Exception("Minver version is null");

        var velopackModule = await context.GetModule<VelopackReleaseModule>();
        var velopackArtifacts = velopackModule.ValueOrDefault ?? [];

        var gitHub = context.GitHub();
        var info = gitHub.RepositoryInfo;
        if (info == null)
            throw new Exception("Not in a Git repository.");

        var owner = info.Owner;
        var repo = info.RepositoryName!.EndsWith(".git")
            ? info.RepositoryName[..^4]
            : info.RepositoryName;
        var client = gitHub.Client;

        var tags = await client.Repository.GetAllTags(owner, repo);
        if (!tags.Any(t => t.Name.Equals(version, StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception(
                $"Aborting: Tag '{version}' must be created on GitHub before running the release target."
            );
        }

        Release? release;

        try
        {
            release = await client.Repository.Release.Get(owner, repo, version);
            context.Logger.LogInformation("Found existing release for tag {Version}", version);
        }
        catch (ApiException apiEx) when (apiEx.Message.Contains("Not Found"))
        {
            context.Logger.LogInformation("Creating new release for tag {Version}", version);
            var newRelease = new NewRelease(version)
            {
                Name = $"Release {version}",
                Body = "Automated release created by Loom Build.",
                Draft = true, // Create as draft initially
                Prerelease = version.Contains('-'),
            };
            release = await client.Repository.Release.Create(owner, repo, newRelease);
        }

        // Get all files recursively in the release directory
        foreach (var artifactDir in velopackArtifacts)
        {
            var folder = context.Files.GetFolder(artifactDir);
            if (!folder.Exists)
            {
                context.Logger.LogWarning("Directory {Dir} does not exist, skipping.", artifactDir);
                continue;
            }

            var files = folder.GetFiles(f => true);
            foreach (var file in files)
            {
                var fileName = file.Name;

                if (fileName.StartsWith("assets.", StringComparison.OrdinalIgnoreCase))
                    continue;

                var existingAsset = release.Assets.FirstOrDefault(a =>
                    a.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)
                );
                if (existingAsset != null)
                {
                    context.Logger.LogInformation(
                        "Asset {FileName} already exists. Deleting prior asset...",
                        fileName
                    );
                    await client.Repository.Release.DeleteAsset(owner, repo, existingAsset.Id);
                }

                context.Logger.LogInformation("Uploading asset {FileName}...", fileName);

                await using var stream = file.GetStream();
                var assetUpload = new ReleaseAssetUpload
                {
                    FileName = fileName,
                    ContentType = "application/octet-stream",
                    RawData = stream,
                };

                await client.Repository.Release.UploadAsset(release, assetUpload, ct);
            }
            context.Logger.LogInformation(
                "Successfully uploaded {Count} assets to GitHub Release.",
                files.Count()
            );
        }

        return release;
    }
}
