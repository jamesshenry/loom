using System.Diagnostics.CodeAnalysis;

namespace Loom.Velopack.Options;

[CliTool("vpk")]
[ExcludeFromCodeCoverage]
public partial record VelopackOptions : VelopackBaseOptions;

[CliTool("dotnet")]
[CliSubCommand("vpk")]
[ExcludeFromCodeCoverage]
public partial record DotNetVelopackOptions : VelopackBaseOptions;

[ExcludeFromCodeCoverage]
public abstract record VelopackBaseOptions : CommandLineToolOptions
{
    [CliOption("--outputDir", ShortForm = "-o")]
    public string? OutputDir { get; set; }

    [CliOption("--channel", ShortForm = "-c")]
    public string? Channel { get; set; }

    [CliOption("--runtime", ShortForm = "-r")]
    public string? Runtime { get; set; }

    [CliOption("--packId", ShortForm = "-u")]
    public string? PackId { get; set; }

    [CliOption("--packVersion", ShortForm = "-v")]
    public string? PackVersion { get; set; }

    [CliOption("--packDir", ShortForm = "-p")]
    public string? PackDir { get; set; }

    [CliOption("--packAuthors")]
    public string? PackAuthors { get; set; }

    [CliOption("--packTitle")]
    public string? PackTitle { get; set; }

    [CliOption("--releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [CliOption("--delta")]
    public string? Delta { get; set; }

    [CliOption("--icon", ShortForm = "-i")]
    public string? Icon { get; set; }

    [CliOption("--mainExe", ShortForm = "-e")]
    public string? MainExe { get; set; }

    [CliOption("--exclude")]
    public string? Exclude { get; set; }

    [CliOption("--noPortable")]
    public bool NoPortable { get; set; }

    [CliOption("--noInst")]
    public bool NoInst { get; set; }

    [CliOption("--framework", ShortForm = "-f")]
    public string? Framework { get; set; }

    [CliOption("--splashImage", ShortForm = "-s")]
    public string? SplashImage { get; set; }

    [CliOption("--skipVeloAppCheck")]
    public bool SkipVeloAppCheck { get; set; }

    [CliOption("--signTemplate")]
    public string? SignTemplate { get; set; }

    [CliOption("--signExclude")]
    public string? SignExclude { get; set; }

    [CliOption("--signParallel")]
    public int? SignParallel { get; set; }

    [CliOption("--shortcuts")]
    public string? Shortcuts { get; set; }

    [CliOption("--signParams", ShortForm = "-n")]
    public string? SignParams { get; set; }

    [CliOption("--azureTrustedSignFile")]
    public string? AzureTrustedSignFile { get; set; }

    [CliOption("--msiDeploymentTool")]
    public bool MsiDeploymentTool { get; set; }

    [CliOption("--msiDeploymentToolVersion")]
    public string? MsiDeploymentToolVersion { get; set; }
}
