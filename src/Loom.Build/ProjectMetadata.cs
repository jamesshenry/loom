namespace Loom;

public record ProjectConfig
{
    public string Solution { get; set; }
    public string EntryProject { get; set; }
    public string VelopackId { get; set; }
}
