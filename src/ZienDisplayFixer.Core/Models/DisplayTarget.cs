namespace ZienDisplayFixer.Core.Models;

public sealed record DisplayTarget(
    string DisplayId,
    DisplayMode Mode,
    int PositionX,
    int PositionY,
    bool IsPrimary,
    bool ModeChanged,
    bool PositionChanged);
