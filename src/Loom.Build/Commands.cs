#pragma warning disable CA1822 // Mark members as static
using ConsoleAppFramework;
using Loom.Config;
using ModularPipelines;
using Spectre.Console;

namespace Loom;

public class Commands
{
    /// <summary>
    /// Default command runs loom against loom.json run.target or BuildTarget.Build
    /// </summary>
    /// <param name="rid">Override global rid set in loom.json</param>
    /// <param name="target">Build target to run</param>
    /// <param name="fresh">--clean|Prepend Clean target to start of pipeline</param>
    /// <returns></returns>
    [Command("")]
    public async Task Root(
        CancellationToken ct,
        [HideDefaultValue] string? rid = null,
        [HideDefaultValue, Argument] BuildTarget? target = null,
        bool fresh = false
    )
    {
        var cliOptions = new ExecutionOptions { Rid = rid, Target = target ?? BuildTarget.Build };

        var loomPath = LoomConfig.ResolveLoomJsonPath();

        if (loomPath == null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] loom.json not found.");
            AnsiConsole.MarkupLine("Run [yellow]dotnet loom init[/] to get started.");
            Environment.Exit(1);
        }

        var builder = Pipeline.CreateBuilder();
        var context = builder.Services.AddLoomContext(loomPath, cliOptions);

        builder.Services.AddModules();
        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = true;
        builder.Options.RunOnlyCategories = LoomConfig.GetPipelineCategories(context.Target, fresh);

        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();
    }

    [Command("init")]
    public async Task Init(bool force = false)
    {
        var currentDir = new DirectoryInfo(Environment.CurrentDirectory);

        try
        {
            string selectedSln = Setup.DiscoverSolution(currentDir);
            string selectedProj = Setup.DiscoverMainProject(currentDir);

            await Setup.InitializeWorkspace(selectedSln, selectedProj, force);

            AnsiConsole.MarkupLine(
                $"[green]Successfully initialized loom.json for {selectedSln} in .build/[/]"
            );
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }
}
