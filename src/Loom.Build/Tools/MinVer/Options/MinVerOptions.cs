using System.Diagnostics.CodeAnalysis;

namespace Loom.MinVer.Options;

[CliTool("minver")]
[ExcludeFromCodeCoverage]
public partial record MinVerOptions : MinVerBaseOptions;

[CliTool("dotnet")]
[CliSubCommand("minver")]
[ExcludeFromCodeCoverage]
public partial record DotNetMinVerOptions : MinVerBaseOptions;

[ExcludeFromCodeCoverage]
public abstract record MinVerBaseOptions : CommandLineToolOptions
{
    [CliOption("--auto-increment", ShortForm = "-a")]
    public string? AutoIncrement { get; set; } // major, minor, patch (default)

    [CliOption("--build-metadata", ShortForm = "-b")]
    public string? BuildMetadata { get; set; }

    [CliOption("--default-pre-release-identifiers", ShortForm = "-i")]
    public string? DefaultPreReleaseIdentifiers { get; set; }

    [CliOption("--ignore-height", ShortForm = "-m")]
    public bool? IgnoreHeight { get; set; }

    [CliOption("--minimum-major-minor", ShortForm = "-m")]
    public string? MinimumMajorMinor { get; set; }

    [CliOption("--tag-prefix", ShortForm = "-t")]
    public string? TagPrefix { get; set; }

    [CliOption("--verbosity", ShortForm = "-v")]
    public string? Verbosity { get; set; } // error, warn, info, debug, trace
}
