using Loom.MinVer.Options;

namespace Loom.MinVer;

public interface IMinVer
{
    Task<MinVerVersion> Run(
        MinVerBaseOptions? options = default,
        CommandExecutionOptions? executionOptions = null,
        CancellationToken cancellationToken = default
    );
}
