namespace ZienDisplayFixer.Core.Models;

public class AppSettings
{
    public bool FixAutomaticallyOnConnect { get; set; } = true;
    public bool StartAtLogin { get; set; } = false;
    public bool IsAnimationEnabled { get; set; } = true;
    public bool IsDemoModeEnabled { get; set; } = true;
}

