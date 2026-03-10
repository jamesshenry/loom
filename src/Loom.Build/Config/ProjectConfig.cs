namespace Loom.Config;

public record ProjectConfig
{
    public string Solution { get; set; } = null!;
    public string EntryProject { get; set; } = null!;
    public string? VelopackId { get; set; } = null;
}
