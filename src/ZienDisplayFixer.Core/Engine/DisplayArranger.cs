namespace ZienDisplayFixer.Core.Engine;

using ZienDisplayFixer.Core.Models;

// Width/Height are the size the display will have after the fix; CurrentWidth/CurrentHeight are its size
// right now (0 = unchanged). Where a display sits today must be judged from its current rectangle.
public sealed record ArrangeInput(
    string Id,
    int Width,
    int Height,
    bool IsPrimary,
    int CurrentX,
    int CurrentY,
    DisplayPlacement Placement = DisplayPlacement.Auto,
    int CurrentWidth = 0,
    int CurrentHeight = 0)
{
    public int NowWidth => CurrentWidth > 0 ? CurrentWidth : Width;
    public int NowHeight => CurrentHeight > 0 ? CurrentHeight : Height;
}

public static class DisplayArranger
{
    // Primary goes at the origin; every other display is snapped flush against it on its side
    // (side displays top-aligned, displays above/below centred), so there are no gaps or overlaps.
    public static IReadOnlyDictionary<string, (int X, int Y)> Arrange(IReadOnlyList<ArrangeInput> displays)
    {
        var result = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);
        if (displays.Count == 0)
        {
            return result;
        }

        var primary = displays.FirstOrDefault(d => d.IsPrimary) ?? displays[0];
        result[primary.Id] = (0, 0);

        var others = displays
            .Where(d => !ReferenceEquals(d, primary))
            .GroupBy(d => d.Placement == DisplayPlacement.Auto ? InferPlacement(d, primary) : d.Placement)
            .ToDictionary(g => g.Key, g => g.ToList());

        if (others.TryGetValue(DisplayPlacement.Right, out var right))
        {
            int x = primary.Width;
            foreach (var d in right.OrderBy(d => d.CurrentX))
            {
                result[d.Id] = (x, 0);
                x += d.Width;
            }
        }

        if (others.TryGetValue(DisplayPlacement.Left, out var left))
        {
            int x = 0;
            foreach (var d in left.OrderByDescending(d => d.CurrentX))
            {
                x -= d.Width;
                result[d.Id] = (x, 0);
            }
        }

        if (others.TryGetValue(DisplayPlacement.Above, out var above))
        {
            PlaceRow(above, primary.Width, d => -d.Height, result);
        }

        if (others.TryGetValue(DisplayPlacement.Below, out var below))
        {
            PlaceRow(below, primary.Width, _ => primary.Height, result);
        }

        return result;
    }

    public static DisplayPlacement InferPlacement(ArrangeInput display, ArrangeInput primary)
    {
        int pLeft = primary.CurrentX, pTop = primary.CurrentY;
        int pRight = pLeft + primary.NowWidth, pBottom = pTop + primary.NowHeight;
        int dLeft = display.CurrentX, dTop = display.CurrentY;
        int dRight = dLeft + display.NowWidth, dBottom = dTop + display.NowHeight;

        bool overlapsHorizontally = dLeft < pRight && dRight > pLeft;
        if (overlapsHorizontally && dBottom <= pTop) return DisplayPlacement.Above;
        if (overlapsHorizontally && dTop >= pBottom) return DisplayPlacement.Below;

        double displayCenter = dLeft + display.NowWidth / 2.0;
        double primaryCenter = pLeft + primary.NowWidth / 2.0;
        return displayCenter < primaryCenter ? DisplayPlacement.Left : DisplayPlacement.Right;
    }

    // Healthy = no two displays overlap and every display is reachable from the primary through shared edges.
    public static bool IsLayoutHealthy(IReadOnlyList<ArrangeInput> displays)
    {
        if (displays.Count <= 1)
        {
            return true;
        }

        for (int i = 0; i < displays.Count; i++)
        {
            for (int j = i + 1; j < displays.Count; j++)
            {
                if (Overlaps(displays[i], displays[j]))
                {
                    return false;
                }
            }
        }

        var primary = displays.FirstOrDefault(d => d.IsPrimary) ?? displays[0];
        var visited = new HashSet<ArrangeInput> { primary };
        var queue = new Queue<ArrangeInput>();
        queue.Enqueue(primary);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var other in displays)
            {
                if (!visited.Contains(other) && Touches(current, other))
                {
                    visited.Add(other);
                    queue.Enqueue(other);
                }
            }
        }

        return visited.Count == displays.Count;
    }

    private static void PlaceRow(
        List<ArrangeInput> row,
        int primaryWidth,
        Func<ArrangeInput, int> y,
        Dictionary<string, (int X, int Y)> result)
    {
        int totalWidth = row.Sum(d => d.Width);
        int x = (primaryWidth - totalWidth) / 2;
        foreach (var d in row.OrderBy(d => d.CurrentX))
        {
            result[d.Id] = (x, y(d));
            x += d.Width;
        }
    }

    private static bool Overlaps(ArrangeInput a, ArrangeInput b) =>
        a.CurrentX < b.CurrentX + b.NowWidth && a.CurrentX + a.NowWidth > b.CurrentX &&
        a.CurrentY < b.CurrentY + b.NowHeight && a.CurrentY + a.NowHeight > b.CurrentY;

    private static bool Touches(ArrangeInput a, ArrangeInput b)
    {
        bool verticalSpansOverlap = a.CurrentY < b.CurrentY + b.NowHeight && a.CurrentY + a.NowHeight > b.CurrentY;
        bool horizontalSpansOverlap = a.CurrentX < b.CurrentX + b.NowWidth && a.CurrentX + a.NowWidth > b.CurrentX;
        bool sideBySide = a.CurrentX + a.NowWidth == b.CurrentX || b.CurrentX + b.NowWidth == a.CurrentX;
        bool stacked = a.CurrentY + a.NowHeight == b.CurrentY || b.CurrentY + b.NowHeight == a.CurrentY;
        return (sideBySide && verticalSpansOverlap) || (stacked && horizontalSpansOverlap);
    }
}
