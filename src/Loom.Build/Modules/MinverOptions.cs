namespace Loom.Modules;

public record MinverOptions : CommandLineToolOptions
{
    public MinverOptions()
    {
        Tool = "dotnet";
        Arguments = ["minver", "--default-pre-release-identifiers", "preview.0"];
    }
}
