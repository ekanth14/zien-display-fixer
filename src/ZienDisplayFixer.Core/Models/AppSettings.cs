namespace ZienDisplayFixer.Core.Models;

public class AppSettings
{
    public bool FixAutomaticallyOnConnect { get; set; } = true;
    public bool StartAtLogin { get; set; } = false;
    public bool IsAnimationEnabled { get; set; } = true;

    // Keyed by monitor hardware id (e.g. "HEC0030") so placement survives replugging.
    public Dictionary<string, DisplayPlacement> DisplayPlacements { get; set; } = new();
}
