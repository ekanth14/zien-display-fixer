namespace ZienDisplayFixer.Core.Layout;

using ZienDisplayFixer.Core.Models;

public static class SceneLayout
{
    public const double CanvasWidth = 1000.0;
    public const double CanvasHeight = 560.0;
    public const double CenterX = 500.0;
    public const double CenterY = 265.0;
    public const double MaxScale = 11.0;
    public const double AvailableWidth = 940.0;
    public const double AvailableHeight = 470.0;
    private const double GapX = 4.0;
    private const double ColGapX = 3.5;
    private const double GapY = 2.0;

    public static SceneGeometry Calculate(
        DeviceType device,
        double laptopInches,
        IReadOnlyList<DisplayInfo>? displays,
        SceneSize viewportSize)
    {
        return Calculate(device, laptopInches, displays, viewportSize.Width, viewportSize.Height);
    }

    public static SceneGeometry Calculate(
        DeviceType device,
        double laptopInches,
        IReadOnlyList<DisplayInfo>? displays,
        double viewportWidth = CanvasWidth,
        double viewportHeight = CanvasHeight,
        Func<DisplayInfo, bool>? isOptimalEvaluator = null)
    {
        if (viewportWidth <= 0)
            viewportWidth = CanvasWidth;
        if (viewportHeight <= 0)
            viewportHeight = CanvasHeight;

        bool isLaptop = device == DeviceType.Laptop;
        if (laptopInches <= 0)
            laptopInches = 14.0;

        double dw = isLaptop ? (laptopInches * 0.88) : 8.0;
        double dh = isLaptop ? (dw * 0.7) : 17.0;
        string deviceLabel = isLaptop ? "Laptop" : "PC";

        displays ??= [];

        // Precompute display items
        var items = new List<DisplayLayoutItem>();
        for (int i = 0; i < displays.Count; i++)
        {
            var d = displays[i];
            int sg = (i % 2 != 0) ? 1 : -1; // 0 -> -1 (Left), 1 -> +1 (Right), alternating
            int displayNumber = i + 1 + (isLaptop ? 1 : 0);

            string aspectStr = DetectAspectRatio(d);
            var (a, b) = LookupAspectRatio(aspectStr);
            double r = Math.Sqrt(a * a + b * b);

            double sizeInches = CalculateDiagonalInches(d);
            double w = sizeInches * a / r;
            double sh = sizeInches * b / r;
            double h = sh * 1.2;

            string name = !string.IsNullOrWhiteSpace(d.Model)
                ? d.Model
                : (!string.IsNullOrWhiteSpace(d.Name) ? d.Name : $"Display {displayNumber}");

            string modeText = d.CurrentMode != null
                ? $"{d.CurrentMode.Width}x{d.CurrentMode.Height} @ {Math.Round(d.CurrentMode.RefreshHz)}Hz"
                : "Unknown";

            bool isOptimal = isOptimalEvaluator?.Invoke(d) ?? CheckIsOptimal(d);

            items.Add(new DisplayLayoutItem(
                Index: i,
                DisplayNumber: displayNumber,
                Display: d,
                Side: sg,
                SizeInches: sizeInches,
                AspectRatio: aspectStr,
                ModeText: modeText,
                IsOptimal: isOptimal,
                Name: name,
                W: w,
                Sh: sh,
                H: h));
        }

        // Group by side into columns of up to 3 monitors
        var leftDisplays = items.Where(x => x.Side == -1).ToList();
        var rightDisplays = items.Where(x => x.Side == 1).ToList();

        var leftColumns = GroupIntoColumns(leftDisplays);
        var rightColumns = GroupIntoColumns(rightDisplays);

        // Calculate total widths and maximum heights
        double leftTotalWidthInches = CalculateSideWidthInches(leftColumns);
        double rightTotalWidthInches = CalculateSideWidthInches(rightColumns);

        double maxSideWidthInches = Math.Max(leftTotalWidthInches, rightTotalWidthInches);
        double totalWidthInches = dw + 2.0 * maxSideWidthInches;

        double maxColumnHeightInches = dh;
        foreach (var col in leftColumns)
            maxColumnHeightInches = Math.Max(maxColumnHeightInches, col.Height);
        foreach (var col in rightColumns)
            maxColumnHeightInches = Math.Max(maxColumnHeightInches, col.Height);

        // Scale factor: fit within 940 x 470, max 11.0
        double s = Math.Min(AvailableWidth / totalWidthInches, Math.Min(AvailableHeight / maxColumnHeightInches, MaxScale));

        // Position Host Device
        double scaledDevW = dw * s;
        double scaledDevH = dh * s;
        double devX = CenterX - scaledDevW / 2.0;
        double devY = CenterY - scaledDevH / 2.0;
        double devPortY = isLaptop ? (devY + scaledDevH * 0.65) : (devY + scaledDevH * 0.5);

        var deviceGeom = new DeviceGeometry(
            X: devX,
            Y: devY,
            Width: scaledDevW,
            Height: scaledDevH,
            IsLaptop: isLaptop,
            Label: deviceLabel,
            PortY: devPortY);

        var placedMonitors = new List<MonitorGeometry>();
        var placedCables = new List<CableGeometry>();

        // Layout Left Columns (outward from host device to the left)
        double curLeftBoundary = devX - GapX * s;
        for (int c = 0; c < leftColumns.Count; c++)
        {
            var col = leftColumns[c];
            double colPixelW = col.Width * s;
            double colCenterX = curLeftBoundary - colPixelW / 2.0;

            LayoutColumn(
                col,
                colCenterX,
                CenterY,
                s,
                deviceGeom,
                placedMonitors,
                placedCables);

            curLeftBoundary -= (colPixelW + ColGapX * s);
        }

        // Layout Right Columns (outward from host device to the right)
        double curRightBoundary = devX + scaledDevW + GapX * s;
        for (int c = 0; c < rightColumns.Count; c++)
        {
            var col = rightColumns[c];
            double colPixelW = col.Width * s;
            double colCenterX = curRightBoundary + colPixelW / 2.0;

            LayoutColumn(
                col,
                colCenterX,
                CenterY,
                s,
                deviceGeom,
                placedMonitors,
                placedCables);

            curRightBoundary += (colPixelW + ColGapX * s);
        }

        // Sort monitors and cables by original Index
        var orderedMonitors = placedMonitors.OrderBy(m => m.Index).ToList();
        var orderedCables = placedCables.OrderBy(c => c.Index).ToList();

        return new SceneGeometry(
            ViewportWidth: viewportWidth,
            ViewportHeight: viewportHeight,
            Device: deviceGeom,
            Monitors: orderedMonitors,
            Cables: orderedCables);
    }

    private static void LayoutColumn(
        ColumnLayout col,
        double colCenterX,
        double centerY,
        double s,
        DeviceGeometry device,
        List<MonitorGeometry> outMonitors,
        List<CableGeometry> outCables)
    {
        double colPixelH = col.Height * s;
        double currentY = centerY - colPixelH / 2.0;

        for (int j = 0; j < col.Items.Count; j++)
        {
            var item = col.Items[j];
            double monW = item.W * s;
            double monScreenH = item.Sh * s;
            double monTotalH = item.H * s;

            double monX = colCenterX - monW / 2.0;
            double monY = currentY;

            // Stand and base geometry
            double extraH = monTotalH - monScreenH;
            double baseH = Math.Max(5.0, extraH * 0.35);
            double standH = extraH - baseH;

            double standW = Math.Clamp(monW * 0.08, 10.0, 24.0);
            double standX = colCenterX - standW / 2.0;
            double standY = monY + monScreenH;

            double baseW = Math.Clamp(monW * 0.35, 30.0, 100.0);
            double baseX = colCenterX - baseW / 2.0;
            double baseY = standY + standH;

            var monitor = new MonitorGeometry(
                Index: item.Index,
                DisplayNumber: item.DisplayNumber,
                Name: item.Name,
                SizeInches: item.SizeInches,
                AspectRatio: item.AspectRatio,
                ModeText: item.ModeText,
                IsOptimal: item.IsOptimal,
                X: monX,
                Y: monY,
                Width: monW,
                Height: monTotalH,
                ScreenX: monX,
                ScreenY: monY,
                ScreenWidth: monW,
                ScreenHeight: monScreenH,
                StandX: standX,
                StandY: standY,
                StandWidth: standW,
                StandHeight: standH,
                BaseX: baseX,
                BaseY: baseY,
                BaseWidth: baseW,
                BaseHeight: baseH);

            outMonitors.Add(monitor);

            // Cable geometry
            double x0 = item.Side == -1 ? device.X : (device.X + device.Width);
            double y0 = device.PortY;
            double xe = colCenterX;
            double ye = baseY + baseH / 2.0;

            bool isWireless = item.Display.Connection == ConnectionType.WiFi;
            string connectorText = FormatConnector(item.Display.Connection);

            double midX = (x0 + xe) / 2.0;
            double midY = (y0 + ye) / 2.0 + (isWireless ? 0.0 : 10.0);
            double pillW = Math.Max(42.0, connectorText.Length * 8.0 + 16.0);
            double pillH = 20.0;
            double pillX = midX - pillW / 2.0;
            double pillY = midY - pillH / 2.0;

            outCables.Add(new CableGeometry(
                Index: item.Index,
                X0: x0,
                Y0: y0,
                Xe: xe,
                Ye: ye,
                IsWireless: isWireless,
                ConnectorText: connectorText,
                PillX: pillX,
                PillY: pillY,
                PillWidth: pillW,
                PillHeight: pillH));

            currentY += (monTotalH + GapY * s);
        }
    }

    private static List<ColumnLayout> GroupIntoColumns(List<DisplayLayoutItem> items)
    {
        var cols = new List<ColumnLayout>();
        for (int i = 0; i < items.Count; i += 3)
        {
            var chunk = items.Skip(i).Take(3).ToList();
            double colW = chunk.Max(x => x.W);
            double colH = chunk.Sum(x => x.H) + (chunk.Count - 1) * GapY;
            cols.Add(new ColumnLayout(chunk, colW, colH));
        }
        return cols;
    }

    private static double CalculateSideWidthInches(List<ColumnLayout> columns)
    {
        if (columns.Count == 0)
            return 0.0;

        return GapX + columns.Sum(c => c.Width) + (columns.Count - 1) * ColGapX;
    }

    private static double CalculateDiagonalInches(DisplayInfo d)
    {
        if (d.PhysicalWidthMm > 0 && d.PhysicalHeightMm > 0)
        {
            double diagMm = Math.Sqrt(d.PhysicalWidthMm * d.PhysicalWidthMm + d.PhysicalHeightMm * d.PhysicalHeightMm);
            double inches = Math.Round(diagMm / 25.4, 1);
            if (inches > 0)
                return inches;
        }

        return 27.0;
    }

    public static string DetectAspectRatio(DisplayInfo d)
    {
        double ratio;
        if (d.PhysicalWidthMm > 0 && d.PhysicalHeightMm > 0)
        {
            ratio = d.PhysicalWidthMm / d.PhysicalHeightMm;
        }
        else if (d.CurrentMode != null && d.CurrentMode.Width > 0 && d.CurrentMode.Height > 0)
        {
            ratio = (double)d.CurrentMode.Width / d.CurrentMode.Height;
        }
        else
        {
            return "16:9";
        }

        var candidates = new (string Name, double Ratio)[]
        {
            ("16:9", 16.0 / 9.0),
            ("16:10", 16.0 / 10.0),
            ("21:9", 21.0 / 9.0),
            ("32:9", 32.0 / 9.0),
            ("4:3", 4.0 / 3.0),
            ("5:4", 5.0 / 4.0)
        };

        var closest = candidates.OrderBy(c => Math.Abs(c.Ratio - ratio)).First();
        return closest.Name;
    }

    public static (double A, double B) LookupAspectRatio(string aspectStr)
    {
        return aspectStr switch
        {
            "16:9" => (16.0, 9.0),
            "16:10" => (16.0, 10.0),
            "21:9" => (21.0, 9.0),
            "32:9" => (32.0, 9.0),
            "4:3" => (4.0, 3.0),
            "5:4" => (5.0, 4.0),
            _ => (16.0, 9.0)
        };
    }

    public static string FormatConnector(ConnectionType conn)
    {
        return conn switch
        {
            ConnectionType.Hdmi => "HDMI",
            ConnectionType.DisplayPort => "DP",
            ConnectionType.UsbC => "USB-C",
            ConnectionType.Usb4 => "USB-4",
            ConnectionType.Vga => "VGA",
            ConnectionType.WiFi => "Wi-Fi",
            _ => "HDMI"
        };
    }

    private static bool CheckIsOptimal(DisplayInfo d)
    {
        if (d.CurrentMode == null)
            return false;

        if (d.CurrentMode.IsInterlaced)
            return false;

        if (d.CurrentMode.RefreshHz < 59.0)
            return false;

        if (d.CurrentMode.Width < 1280 || d.CurrentMode.Height < 720)
            return false;

        return true;
    }

    private record DisplayLayoutItem(
        int Index,
        int DisplayNumber,
        DisplayInfo Display,
        int Side,
        double SizeInches,
        string AspectRatio,
        string ModeText,
        bool IsOptimal,
        string Name,
        double W,
        double Sh,
        double H);

    private record ColumnLayout(
        List<DisplayLayoutItem> Items,
        double Width,
        double Height);
}
