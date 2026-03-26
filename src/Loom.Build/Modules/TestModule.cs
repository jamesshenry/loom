using System.Text.Json.Nodes;
using Loom.Config;

namespace Loom.Modules;

public record TestResult(CommandResult? Result, string CoverageFilePath);

[ModuleCategory("Test")]
[DependsOn<BuildModule>(Optional = true)]
public class TestModule(LoomContext buildContext, IConfiguration configuration) : Module<TestResult>
{
    private readonly IConfiguration _configuration = configuration;

    protected override async Task<TestResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var globalJsonPath = Path.Combine(buildContext.WorkingDirectory, "global.json");
        var globalJsonFile = context.Files.GetFile(globalJsonPath);

        if (!globalJsonFile.Exists)
            throw new InvalidOperationException(
                "global.json not found. Add a global.json with test.runner set to \"Microsoft.Testing.Platform\" to use the Test target."
            );

        var content = await globalJsonFile.ReadAsync();
        ValidateMicrosoftTestingPlatform(content);

        var testResultsDir = Path.Combine(buildContext.WorkingDirectory, "TestResults");
        var coverageFilePath = Path.Combine(testResultsDir, "coverage.xml");

        var testResultsFolder = context.Files.GetFolder(testResultsDir);
        if (!testResultsFolder.Exists)
        {
            testResultsFolder.Create();
        }

        context.Logger.LogInformation("Running tests for {Solution}", buildContext.Solution);

        var result = await context
            .DotNet()
            .Test(
                new DotNetTestOptions
                {
                    Solution = buildContext.Solution,
                    Configuration = buildContext.Configuration,
                    NoBuild = true,
                    Arguments =
                    [
                        "--coverage",
                        "--coverage-output",
                        coverageFilePath,
                        "--coverage-output-format",
                        "xml",
                        "--ignore-exit-code",
                        "8",
                    ],
                },
                executionOptions: new CommandExecutionOptions
                {
                    WorkingDirectory = buildContext.WorkingDirectory,
                },
                cancellationToken: ct
            );
        return new TestResult(result, coverageFilePath);
    }

    internal static void ValidateMicrosoftTestingPlatform(string globalJsonContent)
    {
        var root = JsonNode.Parse(globalJsonContent);
        var runner = root?["test"]?["runner"]?.GetValue<string>();

        if (
            !string.Equals(runner, "Microsoft.Testing.Platform", StringComparison.OrdinalIgnoreCase)
        )
            throw new InvalidOperationException(
                $"global.json test.runner is \"{runner ?? "(not set)"}\". Set it to \"Microsoft.Testing.Platform\" to use the Test target."
            );
    }
}
