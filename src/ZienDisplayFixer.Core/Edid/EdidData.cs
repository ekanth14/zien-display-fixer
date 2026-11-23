namespace ZienDisplayFixer.Core.Edid;

using ZienDisplayFixer.Core.Models;

public record EdidData
{
    public string ManufacturerId { get; init; } = string.Empty;
    public string? ModelName { get; init; }
    public string? SerialNumber { get; init; }
    public int PhysicalWidthCm { get; init; }
    public int PhysicalHeightCm { get; init; }
    public double DiagonalInches { get; init; }
    public string AspectRatio { get; init; } = string.Empty;
    public DisplayMode? PreferredMode { get; init; }

    public EdidData() { }

    public EdidData(
        string manufacturerId,
        string? modelName,
        string? serialNumber,
        int physicalWidthCm,
        int physicalHeightCm,
        double diagonalInches,
        string aspectRatio,
        DisplayMode? preferredMode)
    {
        ManufacturerId = manufacturerId;
        ModelName = modelName;
        SerialNumber = serialNumber;
        PhysicalWidthCm = physicalWidthCm;
        PhysicalHeightCm = physicalHeightCm;
        DiagonalInches = diagonalInches;
        AspectRatio = aspectRatio;
        PreferredMode = preferredMode;
    }
}
