using System.Text.Json;
using ConsoleAppFramework;
using Loom;
using Loom.Config;
using ModularPipelines;
using Newtonsoft.Json.Serialization;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using Spectre.Console;
#pragma warning disable ConsoleUse // Use of Console detected

string repoRoot = Directory.GetRepoRoot(Directory.GetCurrentDirectory());

Directory.SetCurrentDirectory(repoRoot);

var app = ConsoleApp.Create();
app.Add<Commands>();
await app.RunAsync(args);

#if DEBUG
Console.ReadLine();
#pragma warning restore ConsoleUse // Use of Console detected
#endif

public class Commands
{
    [Command("")]
    public async Task Root(
        CancellationToken ct,
        [HideDefaultValue] string? rid = null,
        [HideDefaultValue] string? version = null,
        [HideDefaultValue] BuildTarget? target = null,
        [HideDefaultValue] bool? quick = null,
        [HideDefaultValue] bool? skipPreparation = null,
        [HideDefaultValue] bool? skipPackaging = null,
        [HideDefaultValue] bool? skipDelivery = null
    )
    {
        var cliOptions = new BuildConfig
        {
            Rid = rid,
            Version = version,
            Target = target,
            Quick = quick,
            SkipPreparation = skipPreparation,
            SkipPackaging = skipPackaging,
            SkipDelivery = skipDelivery,
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
        builder.Options.IgnoreCategories = [.. context.GetIgnoredCategories()];
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

        // Build the Loom Context
        var loom = new LoomSettings
        {
            Project = new ProjectConfig
            {
                Solution = selectedSln,
                EntryProject = selectedProj,
                VelopackId = Path.GetFileNameWithoutExtension(selectedSln),
            },
            Build = new BuildConfig { Rid = "win-x64", SkipDelivery = false },
            Nuget = new NugetConfig { ApiKey = "" },
        };

        var settings = new NewtonsoftJsonSchemaGeneratorSettings() { };
        settings.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        var schema = JsonSchema.FromType<LoomSettings>(settings);

        schema.Properties["$schema"] = new JsonSchemaProperty
        {
            Type = JsonObjectType.String,
            Description = "The URL to the JSON schema for this file.",
        };

        var schemaJson = schema.ToJson(Newtonsoft.Json.Formatting.Indented);
        await File.WriteAllTextAsync("loom.schema.json", schemaJson);

        var jsonContent = JsonSerializer.Serialize(loom, LoomSettingsContext.Default.LoomSettings);

        var finalJson = $$"""
{
  "$schema": "./loom.schema.json",{{jsonContent[1..]}}
""";

        await File.WriteAllTextAsync("loom.json", finalJson);
        AnsiConsole.MarkupLine($"[green]Successfully initialized loom.json for {selectedSln}[/]");
    }
}
