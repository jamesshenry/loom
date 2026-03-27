#pragma warning disable CA1822 // Mark members as static
using ConsoleAppFramework;
using Loom.Config;
using ModularPipelines;
using Spectre.Console;

namespace Loom;

public class Commands
{
    [Command("")]
    public async Task Root(
        CancellationToken ct,
        [HideDefaultValue] string? rid = null,
        [HideDefaultValue] BuildTarget? target = null
    )
    {
        var cliOptions = new ExecutionOptions
        {
            Rid = rid,
            Target = target ?? BuildTarget.Build,
        };

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
        builder.Options.RunOnlyCategories = LoomConfig.GetPipelineCategories(context.Target);

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
