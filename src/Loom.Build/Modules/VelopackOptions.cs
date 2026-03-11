using System.Text;

namespace Loom.Modules;

public record VelopackOptions : CommandLineToolOptions
{
    protected override bool PrintMembers(StringBuilder builder)
    {
        return base.PrintMembers(builder);
    }

    public VelopackOptions()
    {
        Tool = "dotnet";
    }
}
