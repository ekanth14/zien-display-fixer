namespace ZienDisplayFixer.Tests.Services;

using System.Runtime.InteropServices;
using Xunit;
using ZienDisplayFixer.App.Services.Windows.Native;

public class DevModeWTests
{
    [Fact]
    public void DevModeW_StructSize_MustBeExactly220Bytes()
    {
        int actualSize = Marshal.SizeOf<DEVMODEW>();
        Assert.Equal(220, actualSize);
    }
}

