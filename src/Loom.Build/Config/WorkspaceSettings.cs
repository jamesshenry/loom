using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Loom.Config;

public class WorkspaceSettings
{
    [Required]
    [Description("Path to the solution file.")]
    public string Solution { get; set; } = string.Empty;
    public string ArtifactsPath { get; set; } = ".artifacts";
}
