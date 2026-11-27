namespace ZienDisplayFixer.Core.Models;

public record DisplayInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string HardwareId { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public DisplayMode? CurrentMode { get; init; }
    public double PhysicalWidthMm { get; init; }
    public double PhysicalHeightMm { get; init; }
    public ConnectionType Connection { get; init; } = ConnectionType.Unknown;

    public DisplayInfo() { }

    public DisplayInfo(
        string id,
        string name,
        string model,
        bool isPrimary,
        int positionX,
        int positionY,
        DisplayMode? currentMode,
        double physicalWidthMm,
        double physicalHeightMm,
        ConnectionType connection)
    {
        Id = id;
        Name = name;
        Model = model;
        IsPrimary = isPrimary;
        PositionX = positionX;
        PositionY = positionY;
        CurrentMode = currentMode;
        PhysicalWidthMm = physicalWidthMm;
        PhysicalHeightMm = physicalHeightMm;
        Connection = connection;
    }
}
