namespace ZienDisplayFixer.App.Services;

using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using ZienDisplayFixer.Core.Models;

public interface IDisplaySetupState
{
    DeviceType DeviceType { get; set; }
    double LaptopScreenSize { get; set; }
    IBrush? WallpaperBrush { get; set; }
    string SelectedWallpaperPreset { get; set; }
    bool IsAnimationEnabled { get; set; }
    ObservableCollection<DisplayInfo> Displays { get; }
    event EventHandler? SetupChanged;
}
