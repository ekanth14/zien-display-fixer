namespace ZienDisplayFixer.Core.Models;

public enum ConnectionType
{
    Hdmi,
    DisplayPort,
    UsbC,
    Usb4,
    Vga,
    WiFi,
    Unknown
}

public enum DeviceType
{
    Laptop,
    Pc
}

public enum DisplayPlacement
{
    Auto,
    Left,
    Right,
    Above,
    Below
}

public enum ScreenTopology
{
    Extend,
    Duplicate,
    InternalOnly,
    ExternalOnly
}
