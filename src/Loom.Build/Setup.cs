using System.Text.Json;
using Loom.Config;
using NJsonSchema;
using NJsonSchema.Generation;
using Spectre.Console;

namespace Loom;

public static class Setup
{
    public static async Task GenerateSchema()
    {
        var settings = new SystemTextJsonSchemaGeneratorSettings
        {
            SerializerOptions = { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
        };

        var schema = JsonSchema.FromType<LoomSettings>(settings);

        schema.Properties["$schema"] = new JsonSchemaProperty
        {
            Type = JsonObjectType.String,
            Description = "The URL to the JSON schema for this file.",
        };

        var ridSchema = new JsonSchema
        {
            Type = JsonObjectType.String,
            Enumeration =
            {
                "win-x64",
                "win-arm64",
                "linux-x64",
                "linux-arm64",
                "osx-x64",
                "osx-arm64",
            },
        };

        schema.Definitions["RuntimeIdentifier"] = ridSchema;

        foreach (var def in schema.Definitions.Values)
        {
            foreach (var prop in def.Properties)
            {
                if (prop.Key.Equals("rid", StringComparison.OrdinalIgnoreCase))
                {
                    prop.Value.Reference = ridSchema;
                    prop.Value.Type = JsonObjectType.None; // remove inline type
                }
            }
        }
        foreach (var def in schema.Definitions.Values)
        {
            foreach (var prop in def.Properties.Values)
            {
                if ((prop.Type & JsonObjectType.Null) != 0)
                {
                    prop.Type &= ~JsonObjectType.Null;
                }
            }
        }

        var schemaJson = schema.ToJson();
        var repoRoot = Directory.GetRepoRoot(AppDomain.CurrentDomain.BaseDirectory);
        var path = Path.Combine(repoRoot, "src", "Loom.Build", "loom.schema.json.example");
        await File.WriteAllTextAsync(path, schemaJson);
    }

    public static string DiscoverSolution(DirectoryInfo currentDir)
    {
        var solutions = currentDir.GetFiles("*.sln*").Select(f => f.Name).ToList();

        if (solutions.Count == 0)
        {
            throw new Exception("No .sln or .slnx files found in the current directory.");
        }

        return solutions.Count == 1
            ? solutions[0]
            : AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Multiple solution files found. [green]Which one should Loom use?[/]")
                    .AddChoices(solutions)
            );
    }

    public static string DiscoverMainProject(DirectoryInfo currentDir)
    {
        var projFiles = currentDir.GetFiles("*.csproj", SearchOption.AllDirectories).ToList();

        return projFiles.Count == 0 ? "src/YourProject.csproj"
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
    }

    public static async Task InitializeWorkspace(
        string selectedSln,
        string selectedProj,
        bool force
    )
    {
        var buildDir = Path.Combine(Environment.CurrentDirectory, ".build");
        if (!Directory.Exists(buildDir))
            Directory.CreateDirectory(buildDir);

        var schemaExampleFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "loom.schema.json.example"
        );

        if (!File.Exists(schemaExampleFile))
        {
            throw new Exception("Example schema file not found");
        }

        var schemaJson = await File.ReadAllTextAsync(schemaExampleFile);
        var destinationSchemaFile = Path.Combine(buildDir, "loom.schema.json");

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
            Global = new GlobalSettings(),
        };

        var jsonContent = JsonSerializer.Serialize(loom, LoomSettingsContext.Default.LoomSettings);

        var finalJson = $$"""
{
  "$schema": "./loom.schema.json",{{jsonContent[1..]}}
""";

        var destinationLoomFile = Path.Combine(buildDir, "loom.json");

        if (!File.Exists(destinationLoomFile) || force)
        {
            await File.WriteAllTextAsync(destinationLoomFile, finalJson);
            AnsiConsole.MarkupLine(
                $"[green]Successfully initialized loom.json for {selectedSln} in .build/[/]"
            );
        }
        if (!File.Exists(destinationSchemaFile) || force)
        {
            await File.WriteAllTextAsync(destinationSchemaFile, schemaJson);
            AnsiConsole.MarkupLine(
                $"[green]Successfully initialized loom.schema.json for {selectedSln} in .build/[/]"
            );
        }
    }

    public static async Task InitializeWorkflows(bool force)
    {
        var workflowsDir = Path.Combine(Environment.CurrentDirectory, ".github", "workflows");
        var assetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".github", "workflows");

        if (!Directory.Exists(assetsDir))
            throw new Exception("Workflow template assets not found.");

        var templateFiles = Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories);

        if (!Directory.Exists(workflowsDir))
        {
            Directory.CreateDirectory(workflowsDir);
        }

        foreach (var templateFile in templateFiles)
        {
            var fileName = Path.GetFileName(templateFile);
            var destination = Path.Combine(workflowsDir, fileName);

            if (force || !File.Exists(destination))
            {
                var content = await File.ReadAllTextAsync(templateFile);
                await File.WriteAllTextAsync(destination, content);

                AnsiConsole.MarkupLine(
                    $"[green]Successfully initialized {fileName} in .github/workflows[/]"
                );
            }
        }
    }

    internal static async Task InitializeDependabot(bool force)
    {
        var dotGithub = Path.Combine(Environment.CurrentDirectory, ".github");
        var sourceFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            ".github",
            "dependabot.yml"
        );

        if (!Directory.Exists(dotGithub))
        {
            Directory.CreateDirectory(dotGithub);
        }

        var destination = Path.Combine(dotGithub, "dependabot.yml");

        if (force || !File.Exists(sourceFile))
        {
            var content = await File.ReadAllTextAsync(sourceFile);
            await File.WriteAllTextAsync(destination, content);

            AnsiConsole.MarkupLine("[green]dependabot.yml created in .github[/]");
        }
    }
}
