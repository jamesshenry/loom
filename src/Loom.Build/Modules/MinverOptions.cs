namespace Loom.Modules;

public record MinverOptions : CommandLineToolOptions
{
    public MinverOptions(string? tagPrefix = null)
    {
        Tool = "dotnet";
        var args = new List<string> { "minver", "--default-pre-release-identifiers", "preview.0" };
        if (!string.IsNullOrWhiteSpace(tagPrefix))
            args.AddRange(["--tag-prefix", tagPrefix]);
        Arguments = [.. args];
    }
}
