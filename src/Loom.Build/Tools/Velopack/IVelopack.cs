using Loom.Velopack.Options;
using ModularPipelines.Options;

namespace Loom.Velopack;

public interface IVelopack
{
    Task ExecuteAsync(
        VelopackBaseOptions options,
        CommandExecutionOptions? executionOptions = null,
        CancellationToken ct = default
    );
}
