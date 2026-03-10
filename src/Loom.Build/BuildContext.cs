namespace Loom;

public class BuildSettings
{
    public ProjectConfig Project { get; set; } = default!;
    public BuildConfig Build { get; set; } = default!;
    public NugetConfig Nuget { get; set; } = default!;
}

public class NugetConfig
{
    public string? ApiKey { get; set; }
}

public class BuildContext
{
    public BuildContext(BuildSettings settings)
    {
        Project = settings.Project;
        Rid = settings.Build.Rid ?? Win64;
        Quick = settings.Build.Quick ?? false;
        SkipPrep = settings.Build.SkipPreparation ?? false;
        SkipPkg = settings.Build.SkipPackaging ?? false;
        SkipDlv = settings.Build.SkipDelivery ?? false;
        Target = settings.Build.Target ?? BuildTarget.Build;
        NugetApiKey = settings.Nuget.ApiKey;
        Configuration =
            (Quick || settings.Build.Target is BuildTarget.Publish or BuildTarget.Release)
                ? "Release"
                : "Debug";
    }

    private const string Win64 = "win-x64";
    public ProjectConfig Project { get; }
    public string Rid { get; }
    public bool Quick { get; }
    public string Configuration { get; }
    public bool SkipPrep { get; }
    public bool SkipPkg { get; }
    public bool SkipDlv { get; }
    public BuildTarget Target { get; }
    public string? NugetApiKey { get; }

    public IEnumerable<string> GetIgnoredCategories()
    {
        if (SkipPrep || Quick)
            yield return "Preparation";
        if (SkipPkg || Quick || Target is BuildTarget.Test or BuildTarget.Build)
            yield return "Packaging";
        if (
            SkipDlv
            || Quick
            || Target is BuildTarget.Test or BuildTarget.Build or BuildTarget.Publish
        )
            yield return "Delivery";
    }
}
