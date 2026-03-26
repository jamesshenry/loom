using Loom.MinVer.Options;

namespace Loom.MinVer;

internal class MinVer : IMinVer
{
    private readonly ICommand _command;

    public MinVer(ICommand command)
    {
        _command = command;
    }

    public virtual async Task<MinVerVersion> Run(
        MinVerBaseOptions? options = default,
        CommandExecutionOptions? executionOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _command
            .ExecuteCommandLineTool(
                options ?? new MinVerOptions(),
                executionOptions,
                cancellationToken
            )
            .ConfigureAwait(false);

        MinVerVersion version = new MinVerVersion(result.StandardOutput.Trim());
        return version;
    }
}
