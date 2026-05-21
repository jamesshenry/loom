using Loom.Config;

using ModularPipelines;

using Spectre.Console;

namespace Loom;

public static class PipelineRunner
{
    public static async Task ExecuteAsync(ExecutionRequest request, CancellationToken ct)
    {
        var cliOptions = new GlobalSettings { Rid = request.Rid, Target = request.Target };

        var loomPath = LoomConfig.ResolveLoomJsonPath();

        if (loomPath is null)
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
        builder.Options.RunOnlyCategories = LoomConfig.GetPipelineCategories(context.Target, request.Fresh);

        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync(ct);
    }
}
