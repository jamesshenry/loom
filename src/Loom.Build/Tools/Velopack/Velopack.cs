using Loom.Velopack.Options;
using ModularPipelines.Context;
using ModularPipelines.Options;

namespace Loom.Velopack;

public class Velopack(ICommand command) : IVelopack
{
    public Task ExecuteAsync(
        VelopackBaseOptions options,
        CommandExecutionOptions? executionOptions = null,
        CancellationToken ct = default
    )
    {
        return command.ExecuteCommandLineTool(options, executionOptions, ct);
    }
}
