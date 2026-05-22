using Loom.Config;
using Loom.Modules;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.DotNet.Services;
using ModularPipelines.Models;
using ModularPipelines.Options;
using Moq;
using TestResult = Loom.Modules.TestResult;

namespace Loom.Build.Tests.Unit;

public class FakeTestModule : TestModule
{
    public FakeTestModule(LoomContext buildContext)
        : base(buildContext, null!) { }

    protected override Task<TestResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    ) => Task.FromResult<TestResult?>(new TestResult(TestHelpers.EmptyCommandResult(), null!));
}

public class ReportGeneratorModuleTests
{
    [Test]
    public async Task Configure_SkipsExecution_WhenToolNotInstalled()
    {
        var fakeWorkspace = "/fake/workspace";
        var settings = new LoomSettings
        {
            Workspace = new WorkspaceSettings { Solution = "test.sln" },
        };

        // 1. Mock IDotNet so the real TestModule succeeds instantly
        var mockDotNet = new Mock<IDotNet>();
        mockDotNet
            .Setup(x =>
                x.Test(
                    It.IsAny<DotNetTestOptions>(),
                    It.IsAny<CommandExecutionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(TestHelpers.EmptyCommandResult());

        var builder = TestHelpers.CreateSilentPipelineBuilder(
            new LoomContext(settings, fakeWorkspace),
            services =>
            {
                services.AddSingleton(mockDotNet.Object);
                // Register the real TestModule instead of a fake
                services.AddModule<TestModule>();
                services.AddModule<ReportGeneratorModule>();
            }
        );

        var mockFs = builder.AddMockFileSystem();

        // 2. Setup mocks so TestModule thinks it CAN run
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
            .ReturnsAsync("""{ "test": { "runner": "Microsoft.Testing.Platform" } }""");

        // 3. Setup mocks for ReportGeneratorModule's checks
        mockFs
            .Setup(f => f.DirectoryExists(It.Is<string>(s => s.EndsWith("TestResults"))))
            .Returns(true);
        mockFs
            .Setup(f => f.EnumerateFiles(It.IsAny<string>(), "*.xml", SearchOption.AllDirectories))
            .Returns(["coverage.xml"]);

        // 4. Return an empty manifest to simulate the tool NOT being installed
        mockFs
            .Setup(f => f.FileExists(It.Is<string>(s => s.EndsWith("dotnet-tools.json"))))
            .Returns(true);
        mockFs
            .Setup(f =>
                f.ReadAllTextAsync(
                    It.Is<string>(s => s.EndsWith("dotnet-tools.json")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("{}");

        // Run the pipeline
        var summary = await (await builder.BuildAsync()).RunAsync();
        var result = await summary.GetModule<ReportGeneratorModule>();

        // Assert
        await Assert.That(result.SkipDecisionOrDefault!.ShouldSkip).IsTrue();
        await Assert
            .That(result.SkipDecisionOrDefault.Reason)
            .Contains("ReportGenerator tool not found");
    }
}
