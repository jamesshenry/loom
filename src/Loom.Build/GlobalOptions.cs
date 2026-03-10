// File: .build\GlobalOptions.cs
namespace Loom;

public class BuildConfig
{
    public string? Rid { get; set; }
    public string? Version { get; set; }
    public BuildTarget? Target { get; set; }
    public bool? Quick { get; set; }
    public bool? SkipPreparation { get; set; }
    public bool? SkipPackaging { get; set; }
    public bool? SkipDelivery { get; set; }

    public IEnumerable<KeyValuePair<string, string?>> ToInMemoryCollection()
    {
        var dict = new Dictionary<string, string?>();

        if (Rid != null)
            dict[$"{nameof(BuildSettings.Build)}:Rid"] = Rid;
        if (Version != null)
            dict[$"{nameof(BuildSettings.Build)}:Version"] = Version;
        if (Target.HasValue)
            dict[$"{nameof(BuildSettings.Build)}:Target"] = Target.ToString();
        if (Quick.HasValue)
            dict[$"{nameof(BuildSettings.Build)}:Quick"] = Quick.Value.ToString();
        if (SkipPreparation.HasValue)
            dict[$"{nameof(BuildSettings.Build)}:SkipPreparation"] =
                SkipPreparation.Value.ToString();
        if (SkipPackaging.HasValue)
            dict[$"{nameof(BuildSettings.Build)}:SkipPackaging"] = SkipPackaging.Value.ToString();
        if (SkipDelivery.HasValue)
            dict[$"{nameof(BuildSettings.Build)}:SkipDelivery"] = SkipDelivery.Value.ToString();

        return dict;
    }
}
