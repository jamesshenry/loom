using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.FileSystem;
using ModularPipelines.Models;
using ModularPipelines.Options;
using Moq;

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

        // Default: nothing exists unless explicitly setup
        mockProvider.Setup(p => p.FileExists(It.IsAny<string>())).Returns(false);
        mockProvider.Setup(p => p.DirectoryExists(It.IsAny<string>())).Returns(false);
        mockProvider
            .Setup(p =>
                p.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())
            )
            .Returns(new List<string>());

        builder.Services.AddSingleton(mockProvider.Object);
        return mockProvider;
    }
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

public sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            System.IO.Path.GetRandomFileName()
        );
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, true);
        }
    }

    public static implicit operator string(TempDirectory d) => d.Path;

    public override string ToString() => Path;
}
