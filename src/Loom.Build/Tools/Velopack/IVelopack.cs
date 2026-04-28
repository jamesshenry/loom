using Loom.Velopack.Options;
using ModularPipelines.Options;

namespace Loom.Velopack;

public interface IVelopackPack
{
    Task ExecuteAsync(
        VelopackPackBaseOptions options,
        CommandExecutionOptions? executionOptions = null,
        CancellationToken ct = default
    );
}
