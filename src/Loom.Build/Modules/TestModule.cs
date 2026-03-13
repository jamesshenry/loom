using Loom.Config;

namespace Loom.Modules;

[DependsOn<BuildModule>]
public class TestModule(LoomContext buildContext, IConfiguration configuration)
    : Module<CommandResult>
{
    private readonly IConfiguration _configuration = configuration;

    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var testResultsDir = Path.Combine(context.Environment.WorkingDirectory, "TestResults");
        var coverageFilePath = Path.Combine(testResultsDir, "coverage.xml");

        Directory.CreateDirectory(testResultsDir);

        context.Logger.LogInformation("Running tests for {Solution}", buildContext.Solution);

        return await context
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
                cancellationToken: ct
            );
    }
}
