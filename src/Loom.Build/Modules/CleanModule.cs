using Loom.Config;
using ModularPipelines.FileSystem;
using SearchOption = System.IO.SearchOption;

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
        var fs = context.GetService<IFileSystemProvider>();

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

        var deletionQueue = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        deletionQueue.Add(
            Path.GetFullPath(
                fs.Combine(loomContext.WorkingDirectory, loomContext.ArtifactsDirectory)
            )
        );

        // Add configured clean directories
        foreach (var dir in loomContext.CleanDirectories)
        {
            deletionQueue.Add(Path.GetFullPath(fs.Combine(loomContext.WorkingDirectory, dir)));
        }

        var artifactsRoot = fs.Combine(
            loomContext.WorkingDirectory,
            loomContext.ArtifactsDirectory
        );
        var existed = fs.DirectoryExists(artifactsRoot);
        long? bytesDeleted = null;

        if (existed)
        {
            bytesDeleted = 0;
            foreach (
                var filePath in fs.EnumerateFiles(artifactsRoot, "*", SearchOption.AllDirectories)
            )
            {
                bytesDeleted += (await fs.ReadAllBytesAsync(filePath, ct)).LongLength;
            }
        }

        var orderedQueue = deletionQueue.OrderBy(x => x.Length).ToList(); // Sort so parents delete before children

        foreach (var path in orderedQueue)
        {
            if (fs.DirectoryExists(path))
            {
                context.Logger.LogInformation("Deleting clean directory: {Path}", path);
                fs.DeleteDirectory(path, true);
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
