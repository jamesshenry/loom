using Loom.MinVer;
using Loom.Modules;
using ModularPipelines.Context;
using File = ModularPipelines.FileSystem.File;

namespace Loom.Build.Tests.Unit;

public class FakeBuildMinVerModule : MinVerModule
{
    public FakeBuildMinVerModule(LoomContext loomContext)
        : base(loomContext) { }

    protected override Task<MinVerResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    ) =>
        Task.FromResult<MinVerResult?>(
            new MinVerResult(
                new Dictionary<string, MinVerVersion>
                {
                    [string.Empty] = new MinVerVersion("1.2.3"),
                }
            )
        );
}

public class FakePublishMinVerModule : MinVerModule
{
    public FakePublishMinVerModule(LoomContext loomContext)
        : base(loomContext) { }

    protected override Task<MinVerResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    ) =>
        Task.FromResult<MinVerResult?>(
            new MinVerResult(
                new Dictionary<string, MinVerVersion>
                {
                    [string.Empty] = new MinVerVersion("1.2.3"),
                    ["v"] = new MinVerVersion("1.2.4"),
                }
            )
        );
}

public class FakeMinVerModule : MinVerModule
{
    public static readonly MinVerVersion MinVer123 = new("1.2.3");
    public static readonly MinVerVersion MinVer124 = new("1.2.4");

    public FakeMinVerModule(LoomContext loomContext)
        : base(loomContext) { }

    protected override Task<MinVerResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken ct
    ) =>
        Task.FromResult<MinVerResult?>(
            new MinVerResult(
                new Dictionary<string, MinVerVersion>
                {
                    [string.Empty] = MinVer123,
                    ["v"] = MinVer124,
                }
            )
        );
}

public class FakeBuildModule : BuildModule
{
    public FakeBuildModule(LoomContext loomContext)
        : base(loomContext) { }

    protected override Task<BuildResult?> ExecuteAsync(IModuleContext context, CancellationToken ct)
    {
        return Task.FromResult<BuildResult?>(new BuildResult("success"));
    }
}

public class FakePackModule : PackModule
{
    public FakePackModule(LoomContext buildContext)
        : base(buildContext) { }

    protected override Task<PackResult?> ExecuteAsync(IModuleContext context, CancellationToken ct)
    {
        return Task.FromResult<PackResult?>(
            new PackResult(
                new List<File> { new File("package1.nupkg"), new File("package2.nupkg") }
            )
        );
    }
}
