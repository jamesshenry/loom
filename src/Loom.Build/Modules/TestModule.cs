using System.Text.Json.Nodes;
using Loom.Config;
using ModularPipelines.FileSystem;

namespace Loom.Modules;

public record TestResult(CommandResult? Result, string Path);

[ModuleCategory("Test")]
[DependsOn<BuildModule>(Optional = true)]
public class TestModule(LoomContext buildContext, IConfiguration configuration) : Module<TestResult>
{
    private readonly IConfiguration _configuration = configuration;

    protected override ModuleConfiguration Configure()
    {
        return ModuleConfiguration
            .Create()
            .WithSkipWhen(async ctx =>
            {
                var fsProvider = ctx.GetService<IFileSystemProvider>();
                var globalJsonPath = fsProvider.Combine(
                    buildContext.WorkingDirectory,
                    "global.json"
                );

                if (!fsProvider.FileExists(globalJsonPath))
                {
                    return SkipDecision.Skip(
                        "global.json not found. Add a global.json with test.runner set to \"Microsoft.Testing.Platform\" to use the Test target."
                    );
                }

                var content = await fsProvider.ReadAllTextAsync(
                    globalJsonPath,
                    CancellationToken.None
                );

                return ValidateMicrosoftTestingPlatform(content);
            })
            .Build();
    }

    protected override async Task<TestResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var fs = context.GetService<IFileSystemProvider>();
        var testResultsPath = fs.Combine(buildContext.WorkingDirectory, "TestResults");

        if (!fs.DirectoryExists(testResultsPath))
        {
            fs.CreateDirectory(testResultsPath);
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
                        "--coverage-output-format",
                        "xml",
                        "--ignore-exit-code",
                        "8",
                        "--results-directory",
                        testResultsPath,
                    ],
                },
                executionOptions: new CommandExecutionOptions
                {
                    WorkingDirectory = buildContext.WorkingDirectory,
                },
                cancellationToken: ct
            );

        return new TestResult(result, testResultsPath);
    }

    internal static SkipDecision ValidateMicrosoftTestingPlatform(string globalJsonContent)
    {
        var root = JsonNode.Parse(globalJsonContent);
        var runner = root?["test"]?["runner"]?.GetValue<string?>();
        if (!"Microsoft.Testing.Platform".Equals(runner, StringComparison.OrdinalIgnoreCase))
            return SkipDecision.Skip(
                $"global.json test.runner is \"{runner ?? "(not set)"}\". Set it to \"Microsoft.Testing.Platform\" to use the Test target."
            );

        return SkipDecision.DoNotSkip;
    }
}
