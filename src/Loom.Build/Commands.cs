using System.Text.Json;
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
        [HideDefaultValue] string? version = null,
        [HideDefaultValue] BuildTarget? target = null
    )
    {
        var cliOptions = new ExecutionOptions
        {
            Rid = rid,
            Version = version,
            Target = target ?? BuildTarget.Build,
        };

        var loomPath = LoomConfig.ResolveLoomJsonPath();

        if (loomPath == null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] loom.json not found.");
            AnsiConsole.MarkupLine("Run [yellow]dotnet loom init[/] to get started.");
            Environment.Exit(1);
        }

        var (settings, config) = LoomConfig.Load(cliOptions);
        var context = new LoomContext(settings);

        var builder = Pipeline.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        builder.Services.AddServices(context);
        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = true;
        builder.Options.RunOnlyCategories = LoomConfig.GetPipelineCategories(context.Target);

        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();
    }

    [Command("init")]
    public async Task Init()
    {
        var currentDir = new DirectoryInfo(Environment.CurrentDirectory);

        try
        {
            var selectedSln = Setup.DiscoverSolution(currentDir);
            var selectedProj = Setup.DiscoverMainProject(currentDir);

            await Setup.InitializeWorkspace(selectedSln, selectedProj);

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
