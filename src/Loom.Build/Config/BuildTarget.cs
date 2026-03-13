using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Loom.Build.Tests")]

namespace Loom;

public enum BuildTarget
{
    Build,
    Test,
    Publish,
    Restore,
    Release,
    NugetUpload,
    Clean,
}
