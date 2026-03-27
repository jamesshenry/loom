using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests.Unit;

public class TestModuleTests
{
    [Test]
    public async Task ExecuteAsync_CreatesTestResultsDirectory_And_RunsDotNetTest()
    {
        var workingDirectory = CreateTemporaryDirectory();

        try
        {
            await WriteGlobalJsonAsync(workingDirectory, "Microsoft.Testing.Platform");

            var mockDotNet = new Mock<IDotNet>();
            DotNetTestOptions? capturedOptions = null;
            CommandExecutionOptions? capturedExecutionOptions = null;

            mockDotNet
                .Setup(x =>
                    x.Test(
                        It.IsAny<DotNetTestOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<DotNetTestOptions, CommandExecutionOptions, CancellationToken>(
                    (options, executionOptions, _) =>
                    {
                        capturedOptions = options;
                        capturedExecutionOptions = executionOptions;
                    }
                )
                .ReturnsAsync((CommandResult)null!);

            var summary = await RunTestModuleAsync(workingDirectory, mockDotNet.Object);
            var testModuleResult = await summary.GetModule<TestModule>();
            var resultData = testModuleResult.ValueOrDefault;
            var coverageFilePath = Path.Combine(workingDirectory, "TestResults", "coverage.xml");

            await Assert.That(testModuleResult.IsSuccess).IsTrue();
            await Assert
                .That(Directory.Exists(Path.Combine(workingDirectory, "TestResults")))
                .IsTrue();
            await Assert.That(resultData).IsNotNull();
            await Assert.That(resultData!.CoverageFilePath).IsEqualTo(coverageFilePath);
            await Assert.That(capturedOptions).IsNotNull();
            await Assert.That(capturedExecutionOptions).IsNotNull();
            await Assert.That(capturedOptions!.Solution).IsEqualTo("test.sln");
            await Assert.That(capturedOptions.Configuration).IsEqualTo("Debug");
            await Assert.That(capturedOptions.NoBuild).IsTrue();
            await Assert.That(capturedOptions.Arguments!).Contains("--coverage");
            await Assert.That(capturedOptions.Arguments!).Contains(coverageFilePath);
            await Assert.That(capturedOptions.Arguments!).Contains("xml");
            await Assert
                .That(capturedExecutionOptions!.WorkingDirectory)
                .IsEqualTo(workingDirectory);

            mockDotNet.Verify(
                x =>
                    x.Test(
                        It.IsAny<DotNetTestOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Test]
    public async Task ExecuteAsync_UsesExistingTestResultsDirectory()
    {
        var workingDirectory = CreateTemporaryDirectory();

        try
        {
            await WriteGlobalJsonAsync(workingDirectory, "Microsoft.Testing.Platform");
            Directory.CreateDirectory(Path.Combine(workingDirectory, "TestResults"));

            var mockDotNet = new Mock<IDotNet>();
            mockDotNet
                .Setup(x =>
                    x.Test(
                        It.IsAny<DotNetTestOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((CommandResult)null!);

            var summary = await RunTestModuleAsync(workingDirectory, mockDotNet.Object);
            var testModuleResult = await summary.GetModule<TestModule>();

            await Assert.That(testModuleResult.IsSuccess).IsTrue();

            mockDotNet.Verify(
                x =>
                    x.Test(
                        It.IsAny<DotNetTestOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Test]
    public async Task ValidateMicrosoftTestingPlatform_Throws_WhenRunnerMissing()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            TestModule.ValidateMicrosoftTestingPlatform("""{ "sdk": { "version": "10.0.104" } }""")
        );

        await Assert.That(exception!.Message).Contains("(not set)");
    }

    [Test]
    public async Task ValidateMicrosoftTestingPlatform_Throws_WhenRunnerIsWrongValue()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            TestModule.ValidateMicrosoftTestingPlatform(
                """{ "sdk": { "version": "10.0.104" }, "test": { "runner": "VSTest" } }"""
            )
        );

        await Assert.That(exception!.Message).Contains("VSTest");
    }

    [Test]
    public void ValidateMicrosoftTestingPlatform_DoesNotThrow_WhenConfiguredCorrectly()
    {
        TestModule.ValidateMicrosoftTestingPlatform(
            """{ "sdk": { "version": "10.0.104" }, "test": { "runner": "Microsoft.Testing.Platform" } }"""
        );
    }

    [Test]
    public void ValidateMicrosoftTestingPlatform_DoesNotThrow_WhenRunnerIsCaseInsensitive()
    {
        TestModule.ValidateMicrosoftTestingPlatform(
            """{ "test": { "runner": "microsoft.testing.platform" } }"""
        );
    }

    [Test]
    public async Task ExecuteAsync_Fails_WhenGlobalJsonMissing()
    {
        var workingDirectory = CreateTemporaryDirectory();

        try
        {
            // No global.json written — module should fail and throw from pipeline
            var mockDotNet = new Mock<IDotNet>();

            var exception = Assert.Throws<Exception>(() =>
                RunTestModuleAsync(workingDirectory, mockDotNet.Object).GetAwaiter().GetResult()
            );

            await Assert.That(exception!.Message).Contains("global.json not found");

            mockDotNet.Verify(
                x =>
                    x.Test(
                        It.IsAny<DotNetTestOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Test]
    public async Task ExecuteAsync_UsesReleaseConfigurationWhenSet()
    {
        var workingDirectory = CreateTemporaryDirectory();

        try
        {
            await WriteGlobalJsonAsync(workingDirectory, "Microsoft.Testing.Platform");

            var mockDotNet = new Mock<IDotNet>();
            DotNetTestOptions? capturedOptions = null;

            mockDotNet
                .Setup(x =>
                    x.Test(
                        It.IsAny<DotNetTestOptions>(),
                        It.IsAny<CommandExecutionOptions>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<DotNetTestOptions, CommandExecutionOptions, CancellationToken>(
                    (options, _, _) => capturedOptions = options
                )
                .ReturnsAsync((CommandResult)null!);

            var settings = new LoomSettings
            {
                Workspace = new WorkspaceSettings { Solution = "test.sln" },
                Global = new GlobalSettings
                {
                    Target = BuildTarget.Test,
                    Configuration = "Release",
                },
            };

            var builder = Pipeline.CreateBuilder();
            builder.Services.AddSingleton(new LoomContext(settings, workingDirectory));
            builder.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            builder.Services.AddSingleton(mockDotNet.Object);
            builder.Services.AddModule<TestModule>();
            builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;

            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();
            var testModuleResult = await summary.GetModule<TestModule>();

            await Assert.That(testModuleResult.IsSuccess).IsTrue();
            await Assert.That(capturedOptions).IsNotNull();
            await Assert.That(capturedOptions!.Configuration).IsEqualTo("Release");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static async Task<PipelineSummary> RunTestModuleAsync(
        string workingDirectory,
        IDotNet dotNet
    )
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
        };

        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(new LoomContext(settings, workingDirectory));
        builder.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        builder.Services.AddSingleton(dotNet);
        builder.Services.AddModule<TestModule>();
        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.PrintResults = false;
        builder.Options.PrintDependencyChains = false;
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;

        var pipeline = await builder.BuildAsync();

        return await pipeline.RunAsync();
    }

    private static async Task WriteGlobalJsonAsync(string workingDirectory, string runner)
    {
        await File.WriteAllTextAsync(
            Path.Combine(workingDirectory, "global.json"),
            $$"""
            {
              "sdk": {
                "version": "10.0.104"
              },
              "test": {
                "runner": "{{runner}}"
              }
            }
            """
        );
    }

    private static string CreateTemporaryDirectory()
    {
        return Directory.CreateTempSubdirectory("loom-test-module-").FullName;
    }
}
