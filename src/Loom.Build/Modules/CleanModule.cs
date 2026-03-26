using Loom.Config;
using ModularPipelines.DotNet.Options;

namespace Loom.Modules;

public record CleanResult(
    bool Success,
    string ArtifactsDirectory,
    bool DirectoryExisted,
    long? BytesDeleted
);

[ModuleCategory("Clean")]
public class CleanModule(LoomContext loomContext) : Module<CleanResult>
{
    protected override async Task<CleanResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        // 1. MSBuild clean target
        context.Logger.LogInformation(
            "Executing MSBuild Clean target for {Solution}",
            loomContext.Solution
        );
        await context
            .DotNet()
            .Clean(
                new DotNetCleanOptions { ProjectSolution = loomContext.Solution },
                executionOptions: new CommandExecutionOptions
                {
                    WorkingDirectory = loomContext.WorkingDirectory,
                },
                cancellationToken: ct
            );

        // 2. Clear known custom directories
        var deletionQueue = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add artifacts dir
        deletionQueue.Add(
            Path.GetFullPath(
                Path.Combine(loomContext.WorkingDirectory, loomContext.ArtifactsDirectory)
            )
        );

        // Add configured clean directories
        foreach (var dir in loomContext.CleanDirectories)
        {
            deletionQueue.Add(Path.GetFullPath(Path.Combine(loomContext.WorkingDirectory, dir)));
        }

        var artifactsRoot = context.Files.GetFolder(
            Path.Combine(loomContext.WorkingDirectory, loomContext.ArtifactsDirectory)
        );
        var existed = artifactsRoot.Exists;
        long? bytesDeleted = existed ? artifactsRoot.GetFiles(x => true).Sum(f => f.Length) : null;

        var orderedQueue = deletionQueue.OrderBy(x => x.Length).ToList(); // Sort so parents delete before children

        foreach (var path in orderedQueue)
        {
            var folder = context.Files.GetFolder(path);
            if (folder.Exists)
            {
                context.Logger.LogInformation("Deleting clean directory: {Path}", path);
                await folder.DeleteAsync(ct);
            }
        }

        context.Logger.LogInformation(
            "{artifacts} artifacts folder evaluated (Existed: {Existed}, Bytes: {Bytes}).",
            artifactsRoot,
            existed,
            bytesDeleted
        );

        return new CleanResult(true, loomContext.ArtifactsDirectory, existed, bytesDeleted);
    }
}
