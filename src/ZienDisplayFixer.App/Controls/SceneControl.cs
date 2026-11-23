namespace ZienDisplayFixer.App.Controls;

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ZienDisplayFixer.Core.Layout;
using ZienDisplayFixer.Core.Models;

public class SceneControl : Control
{
    public static readonly StyledProperty<DeviceType> DeviceTypeProperty =
        AvaloniaProperty.Register<SceneControl, DeviceType>(nameof(DeviceType), defaultValue: DeviceType.Laptop);

    public static readonly StyledProperty<double> LaptopScreenSizeProperty =
        AvaloniaProperty.Register<SceneControl, double>(nameof(LaptopScreenSize), defaultValue: 14.0);

    public static readonly StyledProperty<IEnumerable<DisplayInfo>?> DisplaysProperty =
        AvaloniaProperty.Register<SceneControl, IEnumerable<DisplayInfo>?>(nameof(Displays), defaultValue: null);

    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<SceneControl, bool>(nameof(IsAnimationEnabled), defaultValue: true);

    public static readonly StyledProperty<IBrush?> WallpaperBrushProperty =
        AvaloniaProperty.Register<SceneControl, IBrush?>(nameof(WallpaperBrush), defaultValue: null);

    public DeviceType DeviceType
    {
        get => GetValue(DeviceTypeProperty);
        set => SetValue(DeviceTypeProperty, value);
    }

    public double LaptopScreenSize
    {
        get => GetValue(LaptopScreenSizeProperty);
        set => SetValue(LaptopScreenSizeProperty, value);
    }

    public IEnumerable<DisplayInfo>? Displays
    {
        get => GetValue(DisplaysProperty);
        set => SetValue(DisplaysProperty, value);
    }

    public bool IsAnimationEnabled
    {
        get => GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    public IBrush? WallpaperBrush
    {
        get => GetValue(WallpaperBrushProperty);
        set => SetValue(WallpaperBrushProperty, value);
    }

    private readonly DispatcherTimer _animationTimer;
    private double _animationOffset;
    private bool _isAttached;
    private SceneGeometry? _cachedScene;
    private static readonly Pen SheathPen = new(CableSheathBrush, 6.0) { LineCap = PenLineCap.Round };
    private static readonly Pen CorePen = new(CableCoreBrush, 3.6) { LineCap = PenLineCap.Round };
    private static readonly Pen MonitorBorderPen = new(MonitorBorderBrush, 1.5);
    private static readonly Pen PillBorderPen = new(MonitorBorderBrush, 1.2);
    private static readonly Typeface TypefaceNormal = new("Inter", FontStyle.Normal, FontWeight.Normal);
    private static readonly Typeface TypefaceSemiBold = new("Inter", FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface TypefaceBold = new("Inter", FontStyle.Normal, FontWeight.Bold);

    // Palette Brushes & Pens
    private static readonly IBrush CableSheathBrush = new SolidColorBrush(Color.Parse("#3B3B43"));
    private static readonly IBrush CableCoreBrush = new SolidColorBrush(Color.Parse("#202026"));
    private static readonly IBrush AccentChampagneBrush = new SolidColorBrush(Color.Parse("#CDB78F"));
    private static readonly IBrush CardBgBrush = new SolidColorBrush(Color.Parse("#121215"));
    private static readonly IBrush MonitorBodyBrush = new SolidColorBrush(Color.Parse("#17171B"));
    private static readonly IBrush MonitorBorderBrush = new SolidColorBrush(Color.Parse("#3B3B43"));
    private static readonly IBrush StandBrush = new SolidColorBrush(Color.Parse("#27272D"));
    private static readonly IBrush BaseBrush = new SolidColorBrush(Color.Parse("#1E1E24"));
    private static readonly IBrush TextPrimaryBrush = new SolidColorBrush(Color.Parse("#F1EFEA"));
    private static readonly IBrush TextMutedBrush = new SolidColorBrush(Color.Parse("#9A978F"));
    private static readonly IBrush StatusOptimalBrush = new SolidColorBrush(Color.Parse("#86B894"));
    private static readonly IBrush StatusWarningBrush = new SolidColorBrush(Color.Parse("#D4A35F"));
    private static readonly IBrush LaptopLidBrush = new SolidColorBrush(Color.Parse("#1A1A20"));
    private static readonly IBrush LaptopBaseBrush = new SolidColorBrush(Color.Parse("#23232A"));
    private static readonly IBrush PcCaseBrush = new SolidColorBrush(Color.Parse("#18181C"));
    private static readonly IBrush DefaultScreenGradient = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops =
        [
            new GradientStop(Color.Parse("#12141A"), 0.0),
            new GradientStop(Color.Parse("#1D212A"), 1.0)
        ]
    };

    static SceneControl()
    {
        AffectsRender<SceneControl>(
            DeviceTypeProperty,
            LaptopScreenSizeProperty,
            DisplaysProperty,
            WallpaperBrushProperty);
    }

    public SceneControl()
    {
        ClipToBounds = true;

        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
        };
        _animationTimer.Tick += OnAnimationTick;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (IsAnimationEnabled && IsEffectivelyVisible)
        {
            _animationOffset = (_animationOffset + 2.5) % 274.0; // 14 + 260 pulse cycle
            InvalidateVisual();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DeviceTypeProperty ||
            change.Property == LaptopScreenSizeProperty ||
            change.Property == DisplaysProperty)
        {
            _cachedScene = null;
        }

        if (change.Property == IsAnimationEnabledProperty)
        {
            if (IsAnimationEnabled && _isAttached)
                _animationTimer.Start();
            else
                _animationTimer.Stop();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        if (IsAnimationEnabled)
            _animationTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAttached = false;
        _animationTimer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        if (_cachedScene == null)
        {
            var displayList = (Displays as IReadOnlyList<DisplayInfo>) ?? Displays?.ToList() ?? [];
            _cachedScene = SceneLayout.Calculate(DeviceType, LaptopScreenSize, displayList, SceneLayout.CanvasWidth, SceneLayout.CanvasHeight);
        }
        var scene = _cachedScene;

        // Uniform aspect fit to logical 1000 x 560 coordinate space
        double scale = Math.Min(Bounds.Width / SceneLayout.CanvasWidth, Bounds.Height / SceneLayout.CanvasHeight);
        double offsetX = (Bounds.Width - SceneLayout.CanvasWidth * scale) / 2.0;
        double offsetY = (Bounds.Height - SceneLayout.CanvasHeight * scale) / 2.0;

        var transform = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY);
        using (context.PushTransform(transform))
        {
            RenderCables(context, scene.Cables, _animationOffset);
            RenderDevice(context, scene.Device);
            RenderMonitors(context, scene.Monitors, WallpaperBrush);
        }
    }

    private void RenderCables(DrawingContext context, IReadOnlyList<CableGeometry> cables, double animationOffset)
    {
        foreach (var c in cables)
        {
            if (c.IsWireless)
            {
                // Dashed air waves with animated offset
                var wirelessPen = new Pen(AccentChampagneBrush, 1.8)
                {
                    DashStyle = new DashStyle([4.0, 7.0], animationOffset * 0.4)
                };
                context.DrawLine(wirelessPen, new Point(c.X0, c.Y0), new Point(c.Xe, c.Ye));

                // Centered wireless pill badge
                var pillRect = new Rect(c.PillX, c.PillY, c.PillWidth, c.PillHeight);
                context.DrawRectangle(CardBgBrush, PillBorderPen, pillRect, 10, 10);

                // Radio waves / Wi-Fi text
                DrawFormattedString(context, "Wi-Fi", 10.0, TextPrimaryBrush, pillRect, FontWeight.SemiBold);
            }
            else
            {
                // Cubic Bézier spline from host device to monitor base
                double dx = c.Xe - c.X0;
                double sg = dx >= 0 ? 1.0 : -1.0;
                double p1X = c.X0 + sg * Math.Min(Math.Abs(dx) * 0.5, 70.0);
                double p1Y = c.Y0;
                double p2X = c.Xe - sg * Math.Min(Math.Abs(dx) * 0.2, 40.0);
                double p2Y = c.Ye + 24.0;

                var wireGeom = new StreamGeometry();
                using (var sgc = wireGeom.Open())
                {
                    sgc.BeginFigure(new Point(c.X0, c.Y0), false);
                    sgc.CubicBezierTo(new Point(p1X, p1Y), new Point(p2X, p2Y), new Point(c.Xe, c.Ye));
                    sgc.EndFigure(false);
                }

                // 1. Sheath outer wire (stroke width 6, #3B3B43)
                context.DrawGeometry(null, SheathPen, wireGeom);

                // 2. Core wire (stroke width 3.6, #202026)
                context.DrawGeometry(null, CorePen, wireGeom);

                // 3. Light pulse with stroke dash array (14, 260) and animated offset (#CDB78F)
                var pulsePen = new Pen(AccentChampagneBrush, 3.6)
                {
                    DashStyle = new DashStyle([14.0, 260.0], -animationOffset),
                    LineCap = PenLineCap.Round
                };
                context.DrawGeometry(null, pulsePen, wireGeom);

                // 4. Labeled connector pill (HDMI, DP, USB-C, USB-4, VGA)
                var pillRect = new Rect(c.PillX, c.PillY, c.PillWidth, c.PillHeight);
                context.DrawRectangle(CardBgBrush, PillBorderPen, pillRect, 10, 10);
                DrawFormattedString(context, c.ConnectorText, 10.0, TextPrimaryBrush, pillRect, FontWeight.SemiBold);
            }
        }
    }

    private void RenderDevice(DrawingContext context, DeviceGeometry dev)
    {
        if (dev.IsLaptop)
        {
            // Laptop screen lid
            double lidH = dev.Height * 0.72;
            var lidRect = new Rect(dev.X, dev.Y, dev.Width, lidH);
            context.DrawRectangle(LaptopLidBrush, new Pen(MonitorBorderBrush, 1.5), lidRect, 8, 8);

            // Screen inner panel
            var innerScreen = lidRect.Deflate(4.0);
            context.DrawRectangle(WallpaperBrush ?? DefaultScreenGradient, null, innerScreen, 4, 4);

            // Host laptop display number "1"
            double fontSize = Math.Clamp(innerScreen.Height * 0.36, 16.0, 36.0);
            DrawFormattedString(context, "1", fontSize, TextPrimaryBrush, innerScreen, FontWeight.Bold);

            // Screen top label
            var topRect = new Rect(innerScreen.X, innerScreen.Y + 4.0, innerScreen.Width, 14.0);
            DrawFormattedString(context, "BUILT-IN", 8.0, TextMutedBrush, topRect, FontWeight.Normal);

            // Keyboard base deck
            double baseH = dev.Height * 0.30;
            double baseY = dev.Y + dev.Height * 0.70;
            var baseRect = new Rect(dev.X - 5.0, baseY, dev.Width + 10.0, baseH);
            context.DrawRectangle(LaptopBaseBrush, new Pen(MonitorBorderBrush, 1.2), baseRect, 6, 6);

            // Keyboard notch
            double notchW = dev.Width * 0.22;
            var notchRect = new Rect(dev.X + (dev.Width - notchW) / 2.0, baseY, notchW, 2.5);
            context.DrawRectangle(MonitorBodyBrush, null, notchRect, 1.5, 1.5);

            // Trackpad
            double padW = dev.Width * 0.32;
            double padH = baseH * 0.38;
            var padRect = new Rect(dev.X + (dev.Width - padW) / 2.0, baseY + baseH * 0.44, padW, padH);
            context.DrawRectangle(LaptopLidBrush, new Pen(MonitorBorderBrush, 1.0), padRect, 2, 2);
        }
        else
        {
            // PC Tower
            var towerRect = new Rect(dev.X, dev.Y, dev.Width, dev.Height);
            context.DrawRectangle(PcCaseBrush, new Pen(MonitorBorderBrush, 1.8), towerRect, 8, 8);

            // Power button with subtle accent ring
            double btnRadius = 5.0;
            var btnCenter = new Point(dev.X + dev.Width / 2.0, dev.Y + 16.0);
            context.DrawEllipse(CardBgBrush, new Pen(AccentChampagneBrush, 1.2), btnCenter, btnRadius, btnRadius);
            context.DrawEllipse(StatusOptimalBrush, null, btnCenter, 1.8, 1.8);

            // ZIEN logo debossed text
            var logoRect = new Rect(dev.X, dev.Y + dev.Height * 0.44, dev.Width, 20.0);
            DrawFormattedString(context, "ZIEN", 10.0, TextMutedBrush, logoRect, FontWeight.Normal);

            // Front ventilation slots
            double ventStartY = dev.Y + dev.Height * 0.65;
            for (int v = 0; v < 4; v++)
            {
                double vy = ventStartY + v * 6.0;
                context.DrawLine(
                    new Pen(MonitorBorderBrush, 1.2),
                    new Point(dev.X + 12.0, vy),
                    new Point(dev.X + dev.Width - 12.0, vy));
            }
        }
    }

    private void RenderMonitors(DrawingContext context, IReadOnlyList<MonitorGeometry> monitors, IBrush? wallpaper)
    {
        foreach (var m in monitors)
        {
            // Stand neck
            var standRect = new Rect(m.StandX, m.StandY, m.StandWidth, m.StandHeight);
            context.DrawRectangle(StandBrush, new Pen(MonitorBorderBrush, 1.0), standRect);

            // Stand base
            var baseRect = new Rect(m.BaseX, m.BaseY, m.BaseWidth, m.BaseHeight);
            context.DrawRectangle(BaseBrush, new Pen(MonitorBorderBrush, 1.2), baseRect, 4, 4);

            // Outer chassis body (dark rounded rect #17171B with border #3B3B43)
            var outerRect = new Rect(m.ScreenX, m.ScreenY, m.ScreenWidth, m.ScreenHeight);
            context.DrawRectangle(MonitorBodyBrush, new Pen(MonitorBorderBrush, 1.5), outerRect, 8, 8);

            // Inner screen
            var innerRect = outerRect.Deflate(5.0);
            using (context.PushClip(innerRect))
            {
                // Screen wallpaper / gradient
                if (wallpaper != null)
                    context.DrawRectangle(wallpaper, null, innerRect);
                else
                    context.DrawRectangle(DefaultScreenGradient, null, innerRect);

                // Large display number in center
                double numSize = Math.Clamp(innerRect.Height * 0.38, 18.0, 50.0);
                DrawFormattedString(context, m.DisplayNumber.ToString(), numSize, TextPrimaryBrush, innerRect, FontWeight.Bold);

                // Top header: model and size/aspect
                string headerText = $"{m.Name} • {m.SizeInches:0}\" ({m.AspectRatio})";
                double headerFontSize = Math.Clamp(innerRect.Height * 0.08, 8.5, 10.5);
                var headerRect = new Rect(innerRect.X + 8.0, innerRect.Y + 6.0, innerRect.Width - 28.0, 16.0);
                DrawFormattedString(context, headerText, headerFontSize, TextMutedBrush, headerRect, FontWeight.Normal, TextAlignment.Left);

                // Status dot (Green #86B894 if IsOptimal, Amber #D4A35F if can be improved)
                var dotCenter = new Point(innerRect.Right - 10.0, innerRect.Top + 14.0);
                var statusBrush = m.IsOptimal ? StatusOptimalBrush : StatusWarningBrush;
                context.DrawEllipse(statusBrush, null, dotCenter, 4.0, 4.0);

                // Bottom footer: current mode and refresh rate
                double footerFontSize = Math.Clamp(innerRect.Height * 0.08, 8.5, 10.5);
                var footerRect = new Rect(innerRect.X + 8.0, innerRect.Bottom - 18.0, innerRect.Width - 16.0, 14.0);
                DrawFormattedString(context, m.ModeText, footerFontSize, TextMutedBrush, footerRect, FontWeight.Normal, TextAlignment.Center);
            }
        }
    }

    private static void DrawFormattedString(
        DrawingContext context,
        string text,
        double fontSize,
        IBrush foreground,
        Rect targetRect,
        FontWeight weight = FontWeight.Normal,
        TextAlignment alignment = TextAlignment.Center)
    {
        if (string.IsNullOrEmpty(text) || targetRect.Width <= 0 || targetRect.Height <= 0)
            return;

        var typeface = weight switch
        {
            FontWeight.Bold => TypefaceBold,
            FontWeight.SemiBold => TypefaceSemiBold,
            _ => TypefaceNormal
        };
        var formattedText = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            foreground)
        {
            TextAlignment = alignment,
            MaxTextWidth = targetRect.Width,
            MaxTextHeight = targetRect.Height
        };

        double y = targetRect.Y + (targetRect.Height - formattedText.Height) / 2.0;
        double x = targetRect.X;

        context.DrawText(formattedText, new Point(x, y));
    }
}

