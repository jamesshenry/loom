namespace Loom.Modules;

public record VelopackOptions : CommandLineToolOptions
{
    public VelopackOptions(string rid, bool useDnx = false)
    {
        string directive =
            rid.StartsWith("linux", StringComparison.OrdinalIgnoreCase) ? "[linux]"
            : rid.StartsWith("osx", StringComparison.OrdinalIgnoreCase) ? "[osx]"
            : "[win]";
        var (tool, toolArgs) = useDnx switch
        {
            true => ("dnx", new[] { "vpk" }),
            _ => ("vpk", []),
        };
        Tool = tool;
        Arguments = [.. toolArgs, "--default-pre-release-identifiers", "preview.0"];
    }
}
