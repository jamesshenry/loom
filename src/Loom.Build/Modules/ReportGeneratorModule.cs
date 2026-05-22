using Loom.Config;
using ModularPipelines.FileSystem;

namespace Loom.Modules;

[ModuleCategory("Test")]
[DependsOn<TestModule>]
public class ReportGeneratorModule(LoomContext buildContext) : Module<CommandResult>
{
    protected override ModuleConfiguration Configure()
    {
        return ModuleConfiguration
            .Create()
            .WithSkipWhen(async ctx =>
            {
                // 1. Skip if the TestModule failed or was skipped
                var testModule = await ctx.GetModule<TestModule>();
                if (!testModule.IsSuccess)
                {
                    return SkipDecision.Skip("Tests did not run or failed.");
                }

                var testResults = testModule.ValueOrDefault;
                if (testResults is null)
                    return SkipDecision.Skip("TestResults is null");
                var fs = ctx.GetService<IFileSystemProvider>();

                if (!fs.DirectoryExists(testResults.Path))
                {
                    return SkipDecision.Skip("TestResults directory does not exist.");
                }

                // 3. Skip if no XML coverage files were produced
                var xmlFiles = fs.EnumerateFiles(
                        testResults.Path,
                        "*.xml",
                        SearchOption.AllDirectories
                    )
                    .ToList();
                if (xmlFiles.Count == 0)
                {
                    return SkipDecision.Skip(
                        "No XML coverage files were found in the TestResults directory."
                    );
                }
                // 4. Verify the reportgenerator tool is installed in the local manifest
                var toolsManifestPath = Path.Combine(
                    buildContext.WorkingDirectory,
                    ".config",
                    "dotnet-tools.json"
                );
                var legacyToolsManifestPath = Path.Combine(
                    buildContext.WorkingDirectory,
                    "dotnet-tools.json"
                );

                string? manifestContent = null;
                if (fs.FileExists(toolsManifestPath))
                {
                    manifestContent = await fs.ReadAllTextAsync(toolsManifestPath);
                }
                else if (fs.FileExists(legacyToolsManifestPath))
                {
                    manifestContent = await fs.ReadAllTextAsync(legacyToolsManifestPath);
                }

                if (
                    string.IsNullOrEmpty(manifestContent)
                    || !manifestContent.Contains(
                        "dotnet-reportgenerator-globaltool",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return SkipDecision.Skip(
                        "ReportGenerator tool not found. To enable HTML coverage reports, run: "
                            + "dotnet tool install dotnet-reportgenerator-globaltool"
                    );
                }
                return SkipDecision.DoNotSkip;
            })
            .Build();
    }

    protected override async Task<CommandResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    )
    {
        var testResultsPath = Path.Combine(buildContext.WorkingDirectory, "TestResults");
        var coverageReportPath = Path.Combine(testResultsPath, "CoverageReport");

        var fs = context.GetService<IFileSystemProvider>();

        // Clean the output directory if it already exists
        if (fs.DirectoryExists(coverageReportPath))
        {
            fs.DeleteDirectory(coverageReportPath, recursive: true);
        }

        context.Logger.LogInformation("Generating HTML Code Coverage Report...");

        // Execute the local tool
        var result = await context
            .DotNet()
            .Tool.Execute(
                new DotNetToolOptions
                {
                    Arguments =
                    [
                        "run",
                        "reportgenerator",
                        $"-reports:{Path.Combine(testResultsPath, "*.xml")}",
                        $"-targetdir:{coverageReportPath}",
                        "-reporttypes:HtmlInline",
                    ],
                },
                new CommandExecutionOptions { WorkingDirectory = buildContext.WorkingDirectory },
                ct
            );

        context.Logger.LogInformation(
            "Coverage report generated at: {ReportPath}",
            Path.Combine(coverageReportPath, "index.html")
        );

        return result;
    }
}
