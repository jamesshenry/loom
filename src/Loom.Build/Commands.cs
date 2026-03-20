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

        var loomPath = Path.Combine(Environment.CurrentDirectory, "loom.json");

        if (!File.Exists(loomPath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] loom.json not found.");
            AnsiConsole.MarkupLine("Run [yellow]dotnet loom init[/] to get started.");
            Environment.Exit(1);
        }
        var config = new ConfigurationBuilder()
            .SetBasePath(Environment.CurrentDirectory)
            .AddJsonFile("loom.json", optional: false)
            .AddEnvironmentVariables(prefix: "LOOM_")
            .AddInMemoryCollection(cliOptions.ToInMemoryCollection())
            .AddEnvironmentVariables()
            .Build();

        var settings = new LoomSettings();
        config.Bind(settings);
        var context = new LoomContext(settings);

        var builder = Pipeline.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        builder.Services.AddServices(context);
        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = true;
        builder.Options.RunOnlyCategories = context.Target switch
        {
            BuildTarget.Build => ["Preparation", "Build"],
            BuildTarget.Test => ["Preparation", "Build", "Test"],
            BuildTarget.Publish => ["Preparation", "Packaging"],
            BuildTarget.Release => ["Preparation", "Build", "Packaging"],
            BuildTarget.NugetUpload => ["Preparation", "Build", "Packaging", "Delivery"],
            BuildTarget.Clean => ["Preparation"],
            BuildTarget.Restore => ["Preparation"],
            _ => [],
        };

        var pipeline = await builder.BuildAsync();
        await pipeline.RunAsync();
    }

    [Command("init")]
    public async Task Init()
    {
        var currentDir = new DirectoryInfo(Environment.CurrentDirectory);

        var solutions = currentDir.GetFiles("*.sln*").Select(f => f.Name).ToList();

        if (solutions.Count == 0)
        {
            AnsiConsole.MarkupLine(
                "[red]Error:[/] No .sln or .slnx files found in the current directory."
            );
            return;
        }

        string selectedSln =
            solutions.Count == 1
                ? solutions[0]
                : AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title(
                            "Multiple solution files found. [green]Which one should Loom use?[/]"
                        )
                        .AddChoices(solutions)
                );

        var projFiles = currentDir
            .GetFiles("*.csproj", SearchOption.AllDirectories)
            .Where(f =>
                !f.FullName.Contains("test", StringComparison.OrdinalIgnoreCase)
                && !f.FullName.Contains("build", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        string selectedProj =
            projFiles.Count == 0 ? "src/YourProject.csproj"
            : projFiles.Count == 1
                ? Path.GetRelativePath(Environment.CurrentDirectory, projFiles[0].FullName)
            : AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Multiple projects found. [green]Which is the main entry project?[/]")
                    .AddChoices(
                        projFiles.Select(f =>
                            Path.GetRelativePath(Environment.CurrentDirectory, f.FullName)
                        )
                    )
            );

        var loom = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = selectedSln },
            Artifacts = new Dictionary<string, ArtifactSettings>
            {
                [Path.GetFileNameWithoutExtension(selectedSln)] = new ArtifactSettings
                {
                    Project = selectedProj,
                    Type = ArtifactType.Executable,
                },
            },
            Run = new ExecutionOptions(),
        };

        var jsonContent = JsonSerializer.Serialize(loom, LoomSettingsContext.Default.LoomSettings);

        var finalJson = $$"""
{
  "$schema": "./loom.schema.json",{{jsonContent[1..]}}
""";

        await File.WriteAllTextAsync("loom.json", finalJson);
        AnsiConsole.MarkupLine($"[green]Successfully initialized loom.json for {selectedSln}[/]");
    }
}
