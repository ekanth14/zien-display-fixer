namespace ZienDisplayFixer.Core.Layout;

public readonly record struct SceneSize(double Width, double Height);

public record DeviceGeometry(
    double X,
    double Y,
    double Width,
    double Height,
    bool IsLaptop,
    string Label,
    double PortY);

public record MonitorGeometry(
    int Index,
    int DisplayNumber,
    string Name,
    double SizeInches,
    string AspectRatio,
    string ModeText,
    bool IsOptimal,
    double X,
    double Y,
    double Width,
    double Height,
    double ScreenX,
    double ScreenY,
    double ScreenWidth,
    double ScreenHeight,
    double StandX,
    double StandY,
    double StandWidth,
    double StandHeight,
    double BaseX,
    double BaseY,
    double BaseWidth,
    double BaseHeight);

public record CableGeometry(
    int Index,
    double X0,
    double Y0,
    double Xe,
    double Ye,
    bool IsWireless,
    string ConnectorText,
    double PillX,
    double PillY,
    double PillWidth,
    double PillHeight);

public record SceneGeometry(
    double ViewportWidth,
    double ViewportHeight,
    DeviceGeometry Device,
    IReadOnlyList<MonitorGeometry> Monitors,
    IReadOnlyList<CableGeometry> Cables);

