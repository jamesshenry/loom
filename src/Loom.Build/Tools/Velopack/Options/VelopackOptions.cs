using System.Diagnostics.CodeAnalysis;

namespace Loom.Velopack.Options;

[CliTool("dotnet")]
[CliSubCommand("vpk", "[linux]", "pack")]
[ExcludeFromCodeCoverage]
public partial record DotNetVelopackPackLinuxOptions : VelopackPackBaseOptions
{
    [CliOption("--categories")]
    public string? Categories { get; set; }

    [CliOption("--compression")]
    public string? Compression { get; set; }
}

[CliTool("dotnet")]
[CliSubCommand("vpk", "[win]", "pack")]
[ExcludeFromCodeCoverage]
public partial record DotNetVelopackPackWinOptions : VelopackPackBaseOptions
{
    [CliOption("--noPortable")]
    public bool? NoPortable { get; set; }

    [CliOption("--noInst")]
    public bool? NoInst { get; set; }

    [CliOption("--framework", ShortForm = "-f")]
    public string? Framework { get; set; }

    [CliOption("--splashProgressColor")]
    public string? SplashProgressColor { get; set; }

    [CliOption("--skipVeloAppCheck")]
    public bool? SkipVeloAppCheck { get; set; }

    [CliOption("--signTemplate")]
    public string? SignTemplate { get; set; }

    [CliOption("--signExclude")]
    public string? SignExclude { get; set; }

    [CliOption("--signParallel")]
    public int? SignParallel { get; set; }

    [CliOption("--aumid")]
    public int? ApplicationUserModelId { get; set; }

    [CliOption("--shortcuts")]
    public string? Shortcuts { get; set; } = "None";

    [CliOption("--signParams", ShortForm = "-n")]
    public string? SignParams { get; set; }

    [CliOption("--azureTrustedSignFile")]
    public string? AzureTrustedSignFile { get; set; }

    [CliOption("--msi")]
    public bool? Msi { get; set; }

    [CliOption("--msiVersion")]
    public string? MsiVersion { get; set; }

    [CliOption("--instWelcome")]
    public string? InstallerWelcomePath { get; set; }

    [CliOption("--instLicense")]
    public string? InstallerLicensePath { get; set; }

    [CliOption("--instReadme")]
    public string? InstallerReadmePath { get; set; }

    [CliOption("--instConclusion")]
    public string? InstallerConclusionPath { get; set; }

    [CliOption("--instLocation")]
    public string? InstallerLocation { get; set; }

    [CliOption("--msiBanner")]
    public string? MsiBannerPath { get; set; }

    [CliOption("--msiLogo")]
    public string? MsiLogoPath { get; set; }
}

[ExcludeFromCodeCoverage]
public record VelopackPackBaseOptions : CommandLineToolOptions
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
}
