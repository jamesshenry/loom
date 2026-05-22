using System.Runtime.CompilerServices;
using CliWrap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Options;
using Moq;
using CommandResult = ModularPipelines.Models.CommandResult;

namespace Loom.Build.Tests.Unit;

public static class TestHelpers
{
    public static FakeTimeProvider DefaultFakeTimeProvider { get; } = new();

    public static CommandResult EmptyCommandResult(FakeTimeProvider? timeProvider = null)
    {
        var provider = timeProvider ?? DefaultFakeTimeProvider;
        var now = provider.GetUtcNow();

        return new CommandResult(
            "",
            "",
            "",
            "",
            new Dictionary<string, string?>(),
            now,
            now,
            TimeSpan.Zero,
            0
        );
    }

    public static PipelineBuilder CreateSilentPipelineBuilder(
        LoomContext context,
        Action<IServiceCollection>? configureServices = null
    )
    {
        var builder = Pipeline.CreateBuilder();
        builder.Services.AddSingleton(context);
        builder.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        configureServices?.Invoke(builder.Services);

        builder.Options.PrintLogo = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.PrintResults = false;
        builder.Options.PrintDependencyChains = false;
        builder.Options.DefaultLoggingOptions = CommandLoggingOptions.Silent;

        return builder;
    }

    public static Mock<IFileSystemProvider> AddMockFileSystem(this PipelineBuilder builder)
    {
        var mockProvider = new Mock<IFileSystemProvider>();

        mockProvider
            .Setup(p => p.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        mockProvider
            .Setup(p => p.ReadLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable());
        mockProvider
            .Setup(p => p.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        mockProvider.Setup(p => p.OpenRead(It.IsAny<string>())).Returns(() => new MemoryStream());
        mockProvider.Setup(p => p.Create(It.IsAny<string>())).Returns(() => new MemoryStream());
        mockProvider
            .Setup(p => p.Open(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>()))
            .Returns(() => new MemoryStream());

        mockProvider.Setup(p => p.FileExists(It.IsAny<string>())).Returns(false);
        mockProvider.Setup(p => p.DirectoryExists(It.IsAny<string>())).Returns(false);
        mockProvider
            .Setup(p =>
                p.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())
            )
            .Returns([]);
        mockProvider
            .Setup(p =>
                p.EnumerateDirectories(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchOption>()
                )
            )
            .Returns([]);

        mockProvider.Setup(p => p.GetTempPath()).Returns(Path.GetTempPath());
        mockProvider.Setup(p => p.GetRandomFileName()).Returns(Path.GetRandomFileName());
        mockProvider.Setup(p => p.Combine(It.IsAny<string[]>())).Returns<string[]>(Path.Combine);
        mockProvider
            .Setup(p => p.GetRelativePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>(Path.GetRelativePath);

        builder.Services.AddSingleton(mockProvider.Object);
        return mockProvider;
    }

#pragma warning disable CS1998
    private static async IAsyncEnumerable<string> EmptyAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        yield break;
    }
#pragma warning restore CS1998
}

public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset? initialUtcNow = null)
    {
        _utcNow = initialUtcNow ?? new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        _utcNow = value;
    }

    public void Advance(TimeSpan delta)
    {
        _utcNow = _utcNow.Add(delta);
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }
}
