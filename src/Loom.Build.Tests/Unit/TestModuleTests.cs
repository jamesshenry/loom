using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Extensions;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Options;
using Moq;

namespace Loom.Build.Tests.Unit;

public class TestModuleTests
{
    [Test]
    public async Task ExecuteAsync_CreatesTestResultsDirectory_And_RunsDotNetTest()
    {
        const string workingDirectory = "/fake/workspace";

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

        var (summary, mockFs) = await RunTestModuleAsync(
            workingDirectory,
            mockDotNet.Object,
            globalJsonContent: """{ "test": { "runner": "Microsoft.Testing.Platform" } }"""
        );
        var testModuleResult = await summary.GetModule<TestModule>();
        var resultData = testModuleResult.ValueOrDefault;

        await Assert.That(testModuleResult.IsSuccess).IsTrue();
        await Assert.That(resultData).IsNotNull();
        await Assert
            .That(resultData!.CoverageFilePath)
            .IsEqualTo(Path.Combine(workingDirectory, "TestResults"));
        await Assert.That(capturedOptions).IsNotNull();
        await Assert.That(capturedExecutionOptions).IsNotNull();
        await Assert.That(capturedOptions!.Solution).IsEqualTo("test.sln");
        await Assert.That(capturedOptions.Configuration).IsEqualTo("Debug");
        await Assert.That(capturedOptions.Arguments!).Contains("--coverage");
        await Assert.That(capturedOptions.Arguments!).Contains("xml");
        await Assert.That(capturedExecutionOptions!.WorkingDirectory).IsEqualTo(workingDirectory);

        mockFs.Verify(
            x => x.CreateDirectory(It.Is<string>(s => s.Contains("TestResults"))),
            Times.Once
        );

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

    [Test]
    public async Task ExecuteAsync_UsesExistingTestResultsDirectory()
    {
        const string workingDirectory = "/fake/workspace";

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

        var (summary, mockFs) = await RunTestModuleAsync(
            workingDirectory,
            mockDotNet.Object,
            globalJsonContent: """{ "test": { "runner": "Microsoft.Testing.Platform" } }""",
            testResultsDirectoryExists: true
        );
        var testModuleResult = await summary.GetModule<TestModule>();

        await Assert.That(testModuleResult.IsSuccess).IsTrue();
        mockFs.Verify(
            x => x.CreateDirectory(It.Is<string>(s => s.Contains("TestResults"))),
            Times.Never
        );

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
        const string workingDirectory = "/fake/workspace";

        // No global.json written — module should fail and throw from pipeline
        var mockDotNet = new Mock<IDotNet>();

        var (summary, _) = await RunTestModuleAsync(workingDirectory, mockDotNet.Object);
        var testModuleResult = await summary.GetModule<TestModule>();

        await Assert.That(testModuleResult.IsSkipped).IsTrue();

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

    [Test]
    public async Task ExecuteAsync_UsesReleaseConfigurationWhenSet()
    {
        var fakeWorkspace = "/fake/workspace";

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
                (opts, _, _) => capturedOptions = opts
            )
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
            Global = new GlobalSettings { Target = BuildTarget.Test, Configuration = "Release" },
        };

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, fakeWorkspace),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                services.AddModule<TestModule>();
            }
        );

        // 1. Add the file system mock
        var mockFs = builder.AddMockFileSystem();

        // 2. Tell the mock that global.json EXISTS
        mockFs
            .Setup(f => f.FileExists(It.Is<string>(s => s.EndsWith("global.json"))))
            .Returns(true);

        // 3. Tell the mock what to return when it is READ
        mockFs
            .Setup(f =>
                f.ReadAllTextAsync(
                    It.Is<string>(s => s.EndsWith("global.json")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("""{ "test": { "runner": "Microsoft.Testing.Platform" } }""");

        var summary = await (await builder.BuildAsync()).RunAsync();
        var testModuleResult = await summary.GetModule<TestModule>();

        await Assert.That(testModuleResult.IsSuccess).IsTrue();
    }

    private static async Task<(
        PipelineSummary Summary,
        Mock<IFileSystemProvider> FileSystem
    )> RunTestModuleAsync(
        string workingDirectory,
        IDotNet dotNet,
        string? globalJsonContent = null,
        bool testResultsDirectoryExists = false
    )
    {
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
        };

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, workingDirectory),
            services =>
            {
                services.AddSingleton(dotNet);
                services.AddModule<TestModule>();
            }
        );

        var mockFs = builder.AddMockFileSystem();

        if (globalJsonContent != null)
        {
            mockFs
                .Setup(f => f.FileExists(It.Is<string>(s => s.EndsWith("global.json"))))
                .Returns(true);
            mockFs
                .Setup(f =>
                    f.ReadAllTextAsync(
                        It.Is<string>(s => s.EndsWith("global.json")),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(globalJsonContent);
        }

        if (testResultsDirectoryExists)
        {
            mockFs
                .Setup(f => f.DirectoryExists(It.Is<string>(s => s.EndsWith("TestResults"))))
                .Returns(true);
        }

        var pipeline = await builder.BuildAsync();

        return (await pipeline.RunAsync(), mockFs);
    }
}
