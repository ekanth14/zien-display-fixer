namespace ZienDisplayFixer.Core.Models;

public record WirelessDisplayDevice(string Id, string Name, bool IsConnected = false, double DiagonalInches = 55.0);
