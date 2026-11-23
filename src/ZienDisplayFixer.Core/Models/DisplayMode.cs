namespace ZienDisplayFixer.Core.Models;

public record DisplayMode
{
    public int Width { get; init; }
    public int Height { get; init; }
    public double RefreshHz { get; init; }
    public bool IsInterlaced { get; init; }

    public DisplayMode() { }

    public DisplayMode(int width, int height, double refreshHz, bool isInterlaced = false)
    {
        Width = width;
        Height = height;
        RefreshHz = refreshHz;
        IsInterlaced = isInterlaced;
    }
}
