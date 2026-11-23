namespace ZienDisplayFixer.App.Services.MacOS.Native;

using System;
using System.Runtime.InteropServices;

public static class CoreGraphics
{
    public const string CoreGraphicsLib = "CoreGraphics";
    public const string CoreFoundationLib = "CoreFoundation";

    public const int kCGErrorSuccess = 0;

    // Configuration options
    public const uint kCGConfigureForCommandLine = 0;
    public const uint kCGConfigurePermanently = 1;
    public const uint kCGConfigureForSession = 2;

    // Reconfiguration flags
    public const uint kCGDisplayBeginConfigurationFlag = 1 << 0;
    public const uint kCGDisplayMovedFlag = 1 << 1;
    public const uint kCGDisplaySetMainFlag = 1 << 2;
    public const uint kCGDisplaySetModeFlag = 1 << 3;
    public const uint kCGDisplayAddFlag = 1 << 4;
    public const uint kCGDisplayRemoveFlag = 1 << 5;
    public const uint kCGDisplayEnabledFlag = 1 << 8;
    public const uint kCGDisplayDisabledFlag = 1 << 9;

    // Display mode flags
    public const uint kDisplayModeInterlacedFlag = 0x00000040;

    [StructLayout(LayoutKind.Sequential)]
    public struct CGPoint
    {
        public double X;
        public double Y;

        public CGPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGSize
    {
        public double Width;
        public double Height;

        public CGSize(double width, double height)
        {
            Width = width;
            Height = height;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;

        public CGRect(CGPoint origin, CGSize size)
        {
            Origin = origin;
            Size = size;
        }

        public CGRect(double x, double y, double width, double height)
        {
            Origin = new CGPoint(x, y);
            Size = new CGSize(width, height);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CGDisplayReconfigurationCallBack(uint display, uint flags, IntPtr userInfo);

    [DllImport(CoreGraphicsLib)]
    public static extern int CGGetActiveDisplayList(uint maxDisplays, [Out] uint[]? activeDisplays, out uint displayCount);

    [DllImport(CoreGraphicsLib)]
    public static extern uint CGMainDisplayID();

    [DllImport(CoreGraphicsLib)]
    public static extern CGRect CGDisplayBounds(uint display);

    [DllImport(CoreGraphicsLib)]
    public static extern CGSize CGDisplayScreenSize(uint display);

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGDisplayCopyDisplayMode(uint display);

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGDisplayCopyAllDisplayModes(uint display, IntPtr options);

    [DllImport(CoreGraphicsLib)]
    public static extern nint CGDisplayModeGetWidth(IntPtr mode);

    [DllImport(CoreGraphicsLib)]
    public static extern nint CGDisplayModeGetHeight(IntPtr mode);

    [DllImport(CoreGraphicsLib)]
    public static extern double CGDisplayModeGetRefreshRate(IntPtr mode);

    [DllImport(CoreGraphicsLib)]
    public static extern uint CGDisplayModeGetIOFlags(IntPtr mode);

    [DllImport(CoreGraphicsLib)]
    public static extern void CGDisplayModeRelease(IntPtr mode);

    [DllImport(CoreFoundationLib)]
    public static extern nint CFArrayGetCount(IntPtr theArray);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, nint idx);

    [DllImport(CoreFoundationLib)]
    public static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFDictionaryCreate(
        IntPtr allocator,
        [In] IntPtr[] keys,
        [In] IntPtr[] values,
        nint numValues,
        IntPtr keyCallBacks,
        IntPtr valueCallBacks);

    [DllImport(CoreGraphicsLib)]
    public static extern int CGBeginDisplayConfiguration(out IntPtr configRef);

    [DllImport(CoreGraphicsLib)]
    public static extern int CGConfigureDisplayWithDisplayMode(IntPtr configRef, uint display, IntPtr mode, IntPtr options);

    [DllImport(CoreGraphicsLib)]
    public static extern int CGCompleteDisplayConfiguration(IntPtr configRef, uint option);

    [DllImport(CoreGraphicsLib)]
    public static extern int CGCancelDisplayConfiguration(IntPtr configRef);

    [DllImport(CoreGraphicsLib)]
    public static extern int CGDisplayRegisterReconfigurationCallback(CGDisplayReconfigurationCallBack callback, IntPtr userInfo);

    [DllImport(CoreGraphicsLib)]
    public static extern int CGDisplayRemoveReconfigurationCallback(CGDisplayReconfigurationCallBack callback, IntPtr userInfo);

    private static IntPtr _kCGDisplayShowDuplicateLowResolutionModes;
    private static IntPtr _kCFBooleanTrue;
    private static IntPtr _kCFTypeDictionaryKeyCallBacks;
    private static IntPtr _kCFTypeDictionaryValueCallBacks;
    private static bool _symbolsLoaded;
    private static readonly object _symbolLock = new();

    public static IntPtr CreateAllModesOptionsDictionary()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return IntPtr.Zero;
        }

        lock (_symbolLock)
        {
            if (!_symbolsLoaded)
            {
                _symbolsLoaded = true;

                IntPtr cgHandle = LoadLibrary(CoreGraphicsLib);
                if (cgHandle != IntPtr.Zero)
                {
                    if (NativeLibrary.TryGetExport(cgHandle, "kCGDisplayShowDuplicateLowResolutionModes", out IntPtr symbolAddr))
                    {
                        _kCGDisplayShowDuplicateLowResolutionModes = Marshal.ReadIntPtr(symbolAddr);
                    }
                }

                IntPtr cfHandle = LoadLibrary(CoreFoundationLib);
                if (cfHandle != IntPtr.Zero)
                {
                    if (NativeLibrary.TryGetExport(cfHandle, "kCFBooleanTrue", out IntPtr boolAddr))
                    {
                        _kCFBooleanTrue = Marshal.ReadIntPtr(boolAddr);
                    }
                    NativeLibrary.TryGetExport(cfHandle, "kCFTypeDictionaryKeyCallBacks", out _kCFTypeDictionaryKeyCallBacks);
                    NativeLibrary.TryGetExport(cfHandle, "kCFTypeDictionaryValueCallBacks", out _kCFTypeDictionaryValueCallBacks);
                }
            }
        }

        if (_kCGDisplayShowDuplicateLowResolutionModes == IntPtr.Zero || _kCFBooleanTrue == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            IntPtr[] keys = [_kCGDisplayShowDuplicateLowResolutionModes];
            IntPtr[] values = [_kCFBooleanTrue];

            return CFDictionaryCreate(
                IntPtr.Zero,
                keys,
                values,
                1,
                _kCFTypeDictionaryKeyCallBacks,
                _kCFTypeDictionaryValueCallBacks);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static IntPtr LoadLibrary(string libName)
    {
        if (NativeLibrary.TryLoad(libName, typeof(CoreGraphics).Assembly, null, out IntPtr handle))
        {
            return handle;
        }
        if (NativeLibrary.TryLoad(libName, out handle))
        {
            return handle;
        }
        return IntPtr.Zero;
    }
}
