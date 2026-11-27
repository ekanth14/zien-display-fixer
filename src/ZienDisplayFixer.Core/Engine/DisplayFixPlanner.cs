namespace ZienDisplayFixer.Core.Engine;

using ZienDisplayFixer.Core.Models;

public sealed record DisplayFixPlan(IReadOnlyList<DisplayTarget> Targets)
{
    public bool HasChanges => Targets.Any(t => t.ModeChanged || t.PositionChanged);
    public int ModeChangeCount => Targets.Count(t => t.ModeChanged);
    public IEnumerable<DisplayTarget> ModeChanges => Targets.Where(t => t.ModeChanged);
}

public static class DisplayFixPlanner
{
    public static DisplayFixPlan Plan(
        IReadOnlyList<DisplayInfo> displays,
        IReadOnlyDictionary<string, IReadOnlyList<DisplayMode>> modesByDisplayId,
        IReadOnlyDictionary<string, DisplayPlacement>? savedPlacements = null)
    {
        var placements = ResolvePlacements(displays, savedPlacements);
        var planned = new List<(DisplayInfo Display, DisplayMode Mode, bool ModeChanged)>();

        foreach (var display in displays)
        {
            modesByDisplayId.TryGetValue(display.Id, out var modes);
            var best = BestModePicker.PickBestMode(modes, display);
            var target = best ?? display.CurrentMode;
            if (target == null)
            {
                continue;
            }

            bool modeChanged = best != null && !IsSameMode(display.CurrentMode, best);
            planned.Add((display, modeChanged ? best! : display.CurrentMode!, modeChanged));
        }

        var inputs = planned
            .Select(p => new ArrangeInput(
                p.Display.Id,
                p.Mode.Width,
                p.Mode.Height,
                p.Display.IsPrimary,
                p.Display.PositionX,
                p.Display.PositionY,
                placements.GetValueOrDefault(p.Display.Id, DisplayPlacement.Auto)))
            .ToList();

        bool rearrange = planned.Any(p => p.ModeChanged)
            || placements.Values.Any(p => p != DisplayPlacement.Auto)
            || !DisplayArranger.IsLayoutHealthy(inputs);

        var positions = rearrange
            ? DisplayArranger.Arrange(inputs)
            : inputs.ToDictionary(i => i.Id, i => (i.CurrentX, i.CurrentY), StringComparer.OrdinalIgnoreCase);

        var targets = planned
            .Select(p =>
            {
                var (x, y) = positions[p.Display.Id];
                bool moved = x != p.Display.PositionX || y != p.Display.PositionY;
                return new DisplayTarget(p.Display.Id, p.Mode, x, y, p.Display.IsPrimary, p.ModeChanged, moved);
            })
            .ToList();

        return new DisplayFixPlan(targets);
    }

    public static bool IsSameMode(DisplayMode? current, DisplayMode target) =>
        current != null &&
        current.Width == target.Width &&
        current.Height == target.Height &&
        Math.Abs(current.RefreshHz - target.RefreshHz) < 0.5;

    // Two identical monitors share a hardware id, so a saved placement can't tell them apart; leave those on Auto.
    private static Dictionary<string, DisplayPlacement> ResolvePlacements(
        IReadOnlyList<DisplayInfo> displays,
        IReadOnlyDictionary<string, DisplayPlacement>? savedPlacements)
    {
        var result = new Dictionary<string, DisplayPlacement>(StringComparer.OrdinalIgnoreCase);
        if (savedPlacements == null || savedPlacements.Count == 0)
        {
            return result;
        }

        var duplicateIds = displays
            .Where(d => !string.IsNullOrEmpty(d.HardwareId))
            .GroupBy(d => d.HardwareId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var display in displays)
        {
            if (display.IsPrimary ||
                string.IsNullOrEmpty(display.HardwareId) ||
                duplicateIds.Contains(display.HardwareId))
            {
                continue;
            }

            if (savedPlacements.TryGetValue(display.HardwareId, out var placement) && placement != DisplayPlacement.Auto)
            {
                result[display.Id] = placement;
            }
        }

        return result;
    }
}
