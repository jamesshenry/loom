namespace Loom.Modules;

public record MinverOptions : CommandLineToolOptions
{
    public MinverOptions(bool useDnx)
    {
        var (tool, toolArgs) = useDnx switch
        {
            true => ("dnx", new[] { "minver-cli" }),
            _ => ("minver-cli", []),
        };
        Tool = tool;
        Arguments = [.. toolArgs, "--default-pre-release-identifiers", "preview.0"];
    }
}
