using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.Enums;
using ModularPipelines.Extensions;
using ModularPipelines.FileSystem;
using ModularPipelines.Modules;
using Moq;

public class MyModuleTests
{
    [Test]
    public async Task MyModule_ReadsConfigFile()
    {
        // Create a mock provider
        var mockProvider = new Mock<IFileSystemProvider>();
        mockProvider
            .Setup(p => p.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"setting\": \"value\"}");

        // Run pipeline with mock
        var builder = Pipeline.CreateBuilder();

        builder.Services.AddSingleton<IFileSystemProvider>(mockProvider.Object);
        builder.Services.AddModule<MyModule>();

        var result = await builder.Build().RunAsync();

        // Assert results
        await Assert.That(result.Status).IsEqualTo(Status.Successful);
        mockProvider.Verify(
            fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtMostOnce()
        );
    }
}

public class MyModule : Module<bool>
{
    protected override async Task<bool> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken
    )
    {
        var file = context.Files.GetFile("path/to/file.Json");
        var text = await file.ReadAsync(cancellationToken);
        return true;
    }
}
