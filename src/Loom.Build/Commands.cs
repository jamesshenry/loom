#pragma warning disable CA1822 // Mark members as static
using ConsoleAppFramework;
using Loom.Config;
using ModularPipelines;
using Spectre.Console;

namespace Loom;

public class Commands
{
    /// <summary>
    /// Default command runs loom against BuildTarget.Build
    /// </summary>
    /// <param name="fresh">-f|--clean, Prepend Clean target to start of pipeline</param>
    /// <returns></returns>
    [Command("")]
    public async Task Root(CancellationToken ct, bool fresh = false)
    {
        await PipelineRunner.ExecuteAsync(new ExecutionRequest(BuildTarget.Build, null, fresh), ct);
    }

    /// <summary>
    /// Clean project and artifacts.
    /// </summary>
    /// <param name="rid">Override global rid set in loom.json</param>
    public Task Clean(CancellationToken ct, [HideDefaultValue] string? rid = null) =>
        PipelineRunner.ExecuteAsync(new ExecutionRequest(BuildTarget.Clean, rid), ct);

    /// <summary>
    /// Restore project dependencies.
    /// </summary>
    /// <param name="rid">Override global rid set in loom.json</param>
    /// <param name="fresh">-f|--clean, Prepend Clean target to start of pipeline</param>
    public Task Restore(
        CancellationToken ct,
        [HideDefaultValue] string? rid = null,
        bool fresh = false
    ) => PipelineRunner.ExecuteAsync(new ExecutionRequest(BuildTarget.Restore, rid, fresh), ct);

    /// <summary>
    /// Build project and artifacts.
    /// </summary>
    /// <param name="rid">Override global rid set in loom.json</param>
    /// <param name="fresh">-f|--clean, Prepend Clean target to start of pipeline</param>
    public Task Build(
        CancellationToken ct,
        [HideDefaultValue] string? rid = null,
        bool fresh = false
    ) => PipelineRunner.ExecuteAsync(new ExecutionRequest(BuildTarget.Build, rid, fresh), ct);

    /// <summary>
    /// Run tests.
    /// </summary>
    /// <param name="rid">Override global rid set in loom.json</param>
    /// <param name="fresh">-f|--clean, Prepend Clean target to start of pipeline</param>
    public Task Test(
        CancellationToken ct,
        [HideDefaultValue] string? rid = null,
        bool fresh = false
    ) => PipelineRunner.ExecuteAsync(new ExecutionRequest(BuildTarget.Test, rid, fresh), ct);

    /// <summary>
    /// Build and package project.
    /// </summary>
    /// <param name="rid">Override global rid set in loom.json</param>
    /// <param name="fresh">-f|--clean, Prepend Clean target to start of pipeline</param>
    public Task Publish(
        CancellationToken ct,
        [HideDefaultValue] string? rid = null,
        bool fresh = false
    ) => PipelineRunner.ExecuteAsync(new ExecutionRequest(BuildTarget.Publish, rid, fresh), ct);

    /// <summary>
    /// Build, package, and release project.
    /// </summary>
    /// <param name="rid">Override global rid set in loom.json</param>
    /// <param name="fresh">-f|--clean, Prepend Clean target to start of pipeline</param>
    public Task Release(
        CancellationToken ct,
        [HideDefaultValue] string? rid = null,
        bool fresh = false
    ) => PipelineRunner.ExecuteAsync(new ExecutionRequest(BuildTarget.Release, rid, fresh), ct);

    [Command("init")]
    public async Task Init(bool force = false)
    {
        var currentDir = new DirectoryInfo(Environment.CurrentDirectory);

        try
        {
            string selectedSln = Setup.DiscoverSolution(currentDir);
            string selectedProj = Setup.DiscoverMainProject(currentDir);

            await Setup.InitializeWorkspace(selectedSln, selectedProj, force);
            await Setup.InitializeWorkflows(force);
            await Setup.InitializeDependabot(force);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }
}
