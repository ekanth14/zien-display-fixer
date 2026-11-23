namespace ZienDisplayFixer.App.ViewModels;

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZienDisplayFixer.Core.Models;

public partial class DisplayItemViewModel : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayNumberText))]
    private int _displayIndex = 1;

    public string DisplayNumberText => $"#{DisplayIndex}";

    [ObservableProperty]
    private string _name = "Display";

    [ObservableProperty]
    private double _diagonalInches = 27.0;

    [ObservableProperty]
    private string _aspectRatio = "16:9";

    [ObservableProperty]
    private ConnectionType _connection = ConnectionType.Hdmi;

    [ObservableProperty]
    private string _nativeModeText = "2560 × 1440 @ 144Hz";

    public DisplayMode? CurrentMode { get; set; }
    public bool IsPrimary { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    public IReadOnlyList<string> AspectRatioOptions => DisplaysViewModel.DefaultAspectRatioOptions;
    public IReadOnlyList<ConnectionType> ConnectionOptions => DisplaysViewModel.DefaultConnectionOptions;

    public IRelayCommand? RemoveCommand { get; set; }

    public event EventHandler? Changed;

    partial void OnNameChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);
    partial void OnDiagonalInchesChanged(double value) => Changed?.Invoke(this, EventArgs.Empty);
    partial void OnAspectRatioChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);
    partial void OnConnectionChanged(ConnectionType value) => Changed?.Invoke(this, EventArgs.Empty);
    partial void OnNativeModeTextChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);

    public static (double WidthMm, double HeightMm) CalculateDimensions(double diagonalInches, string aspectRatio)
    {
        double ratio = aspectRatio switch
        {
            "16:10" => 16.0 / 10.0,
            "21:9" => 21.0 / 9.0,
            "32:9" => 32.0 / 9.0,
            "4:3" => 4.0 / 3.0,
            "5:4" => 5.0 / 4.0,
            _ => 16.0 / 9.0
        };

        double diagMm = diagonalInches * 25.4;
        double heightMm = diagMm / Math.Sqrt(ratio * ratio + 1.0);
        double widthMm = heightMm * ratio;

        return (Math.Round(widthMm, 1), Math.Round(heightMm, 1));
    }

    public static string GetClosestAspectRatio(double ratio)
    {
        (string Name, double R)[] standard =
        [
            ("16:9", 16.0 / 9.0),
            ("16:10", 16.0 / 10.0),
            ("21:9", 21.0 / 9.0),
            ("32:9", 32.0 / 9.0),
            ("4:3", 4.0 / 3.0),
            ("5:4", 5.0 / 4.0)
        ];

        string best = "16:9";
        double minDiff = double.MaxValue;
        foreach (var (name, r) in standard)
        {
            double diff = Math.Abs(ratio - r);
            if (diff < minDiff)
            {
                minDiff = diff;
                best = name;
            }
        }
        return best;
    }

    public DisplayInfo ToDisplayInfo()
    {
        var (wMm, hMm) = CalculateDimensions(DiagonalInches, AspectRatio);
        return new DisplayInfo(
            id: Id,
            name: Name,
            model: Name,
            isPrimary: IsPrimary,
            positionX: PositionX,
            positionY: PositionY,
            currentMode: CurrentMode ?? new DisplayMode(2560, 1440, 144.0, false),
            physicalWidthMm: wMm,
            physicalHeightMm: hMm,
            connection: Connection);
    }

    public static DisplayItemViewModel FromDisplayInfo(
        DisplayInfo info,
        Action<DisplayItemViewModel>? onRemove = null,
        int displayIndex = 1)
    {
        double diag = 27.0;
        string ar = "16:9";
        if (info.PhysicalWidthMm > 0 && info.PhysicalHeightMm > 0)
        {
            diag = Math.Round(Math.Sqrt(info.PhysicalWidthMm * info.PhysicalWidthMm + info.PhysicalHeightMm * info.PhysicalHeightMm) / 25.4, 0);
            double r = info.PhysicalWidthMm / info.PhysicalHeightMm;
            ar = GetClosestAspectRatio(r);
        }

        string nativeText = info.CurrentMode != null
            ? $"{info.CurrentMode.Width} × {info.CurrentMode.Height} @ {info.CurrentMode.RefreshHz:F0}Hz"
            : "2560 × 1440 @ 144Hz";

        var vm = new DisplayItemViewModel
        {
            Id = !string.IsNullOrEmpty(info.Id) ? info.Id : Guid.NewGuid().ToString(),
            DisplayIndex = displayIndex,
            Name = !string.IsNullOrWhiteSpace(info.Name) ? info.Name : (!string.IsNullOrWhiteSpace(info.Model) ? info.Model : "Display"),
            DiagonalInches = diag > 0 ? diag : 27.0,
            AspectRatio = ar,
            Connection = info.Connection != ConnectionType.Unknown ? info.Connection : ConnectionType.Hdmi,
            NativeModeText = nativeText,
            CurrentMode = info.CurrentMode,
            IsPrimary = info.IsPrimary,
            PositionX = info.PositionX,
            PositionY = info.PositionY
        };

        if (onRemove != null)
        {
            vm.RemoveCommand = new RelayCommand(() => onRemove(vm));
        }

        return vm;
    }
}
