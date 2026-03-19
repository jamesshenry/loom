using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Loom.Config;

public class ArtifactSettings
{
    [Required]
    [Description("Path to the project file.")]
    public string Project { get; set; } = "";

    [Required]
    [Description("Artifact type (nuget, dotnet-publish, velopack).")]
    public ArtifactType Type { get; set; } = ArtifactType.Nuget;

    [Description("Runtime identifier.")]
    public string? Rid { get; set; }

    public string? Version { get; set; }

    public string? VelopackId { get; set; }
}
