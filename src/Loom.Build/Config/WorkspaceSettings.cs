using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Loom.Config;

public class WorkspaceSettings
{
    [Required]
    [Description("Path to the solution file.")]
    public string Solution { get; set; } = string.Empty;
    public string ArtifactsPath { get; set; } = ".artifacts";

    [Description("Additional directories to clean during the Clean target.")]
    public string[] CleanDirectories { get; set; } = [];

    [Description("Whether to upload NuGet packages during a release.")]
    public bool EnableNugetUpload { get; set; } = false;

    [Description("Whether to create a GitHub release during a release.")]
    public bool EnableGithubRelease { get; set; } = false;
}
