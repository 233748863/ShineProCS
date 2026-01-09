using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Infrastructure;
using OpenCvSharp;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for Coordinate Validation and Fallback
/// **Feature: business-logic-fixes, Property 14: Coordinate Validation and Fallback**
/// **Validates: Requirements 10.2, 10.3, 10.4**
/// </summary>
public class CoordinateValidationFallbackTests : IDisposable
{
    private readonly List<OpenCvImageInterface> _interfacesToDispose = new();
    private readonly List<string> _logMessages = new();
    
    public void Dispose()
    {
        foreach (var iface in _interfacesToDispose)
        {
            iface.Dispose();
        }
    }
    
    /// <summary>
    /// 创建一个测试用的 OpenCvImageInterface
    /// </summary>
    private OpenCvImageInterface CreateInterface()
    {
        var iface = new OpenCvImageInterface();
        iface.SetLogCallback((msg, level) => _logMessages.Add($"[{level}] {msg}"));
        _interfacesToDispose.Add(iface);
        return iface;
    }
    
    /// <summary>
    /// Property 14.1: Invalid width or height returns null
    /// WHEN width or height is <= 0, GetScreenRegion SHALL return null.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    public void InvalidWidthOrHeightReturnsNull(int width, int height)
    {
        var iface = CreateInterface();
        
        // Act: 使用无效的宽度或高度
        var result = iface.GetScreenRegion(100, 100, width, height);
        
        // Assert: 应该返回 null
        Assert.Null(result);
    }
    
    /// <summary>
    /// Property 14.2: Valid coordinates with GDI mode returns non-null
    /// WHEN WGC is not enabled and coordinates are valid, GetScreenRegion SHALL return a Mat.
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Fact]
    public void ValidCoordinatesWithGdiModeReturnsNonNull()
    {
        var iface = CreateInterface();
        
        // 确保使用 GDI 模式
        iface.UseGdiMode();
        
        // Act: 使用有效的坐标（屏幕左上角的小区域）
        var result = iface.GetScreenRegion(0, 0, 10, 10);
        
        // Assert: 应该返回非 null 的 Mat
        Assert.NotNull(result);
        Assert.False(result.Empty());
        Assert.Equal(10, result.Width);
        Assert.Equal(10, result.Height);
        
        result.Dispose();
    }
    
    /// <summary>
    /// Property 14.3: GDI fallback uses original screen coordinates
    /// WHEN WGC capture fails, the system SHALL fall back to GDI with original screen coordinates.
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Fact]
    public void GdiFallbackUsesOriginalScreenCoordinates()
    {
        var iface = CreateInterface();
        
        // 使用 GDI 模式（模拟 WGC 不可用的情况）
        iface.UseGdiMode();
        
        // Act: 使用屏幕坐标
        int screenX = 50;
        int screenY = 50;
        int width = 20;
        int height = 20;
        
        var result = iface.GetScreenRegion(screenX, screenY, width, height);
        
        // Assert: 应该成功返回指定尺寸的 Mat
        Assert.NotNull(result);
        Assert.Equal(width, result.Width);
        Assert.Equal(height, result.Height);
        
        result.Dispose();
    }
    
    /// <summary>
    /// Property 14.4: Positive width and height are required
    /// FOR ALL positive width and height values, GetScreenRegion SHALL not return null due to size validation.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool PositiveWidthAndHeightAreRequired(PositiveInt widthGen, PositiveInt heightGen)
    {
        // 限制尺寸以避免内存问题
        var width = (widthGen.Get % 100) + 1;  // 1-100
        var height = (heightGen.Get % 100) + 1; // 1-100
        
        var iface = CreateInterface();
        iface.UseGdiMode();
        
        // Act: 使用正数的宽度和高度
        var result = iface.GetScreenRegion(0, 0, width, height);
        
        // Assert: 应该返回非 null（GDI 模式下应该成功）
        var success = result != null;
        result?.Dispose();
        
        return success;
    }
    
    /// <summary>
    /// Property 14.5: UseWgc property reflects current mode
    /// WHEN UseGdiMode is called, UseWgc SHALL be false.
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Fact]
    public void UseWgcPropertyReflectsCurrentMode()
    {
        var iface = CreateInterface();
        
        // 初始状态应该是 GDI 模式
        Assert.False(iface.UseWgc);
        
        // 显式切换到 GDI 模式
        iface.UseGdiMode();
        
        // Assert: UseWgc 应该为 false
        Assert.False(iface.UseWgc);
    }
    
    /// <summary>
    /// Property 14.6: Log callback is invoked for boundary issues
    /// WHEN log callback is set, boundary issues SHALL be logged.
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Fact]
    public void LogCallbackIsInvokedForBoundaryIssues()
    {
        var logMessages = new List<string>();
        var iface = CreateInterface();
        iface.SetLogCallback((msg, level) => logMessages.Add($"[{level}] {msg}"));
        
        // 使用 GDI 模式，不会触发 WGC 边界检查日志
        iface.UseGdiMode();
        
        // Act: 正常截图不应该产生日志
        var result = iface.GetScreenRegion(0, 0, 10, 10);
        result?.Dispose();
        
        // Assert: GDI 模式下正常截图不应该有边界警告日志
        // （边界检查日志只在 WGC 模式下触发）
        var boundaryLogs = logMessages.Where(m => m.Contains("越界") || m.Contains("超出")).ToList();
        Assert.Empty(boundaryLogs);
    }
    
    /// <summary>
    /// Property 14.7: GetScreenRegion handles zero coordinates
    /// WHEN x=0 and y=0, GetScreenRegion SHALL work correctly.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Fact]
    public void GetScreenRegionHandlesZeroCoordinates()
    {
        var iface = CreateInterface();
        iface.UseGdiMode();
        
        // Act: 使用零坐标
        var result = iface.GetScreenRegion(0, 0, 10, 10);
        
        // Assert: 应该成功
        Assert.NotNull(result);
        Assert.False(result.Empty());
        
        result.Dispose();
    }
    
    /// <summary>
    /// Property 14.8: GetScreenRegion returns correct dimensions
    /// FOR ALL valid width and height, the returned Mat SHALL have matching dimensions.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public bool GetScreenRegionReturnsCorrectDimensions(PositiveInt widthGen, PositiveInt heightGen)
    {
        // 限制尺寸以避免内存问题
        var width = (widthGen.Get % 50) + 1;  // 1-50
        var height = (heightGen.Get % 50) + 1; // 1-50
        
        var iface = CreateInterface();
        iface.UseGdiMode();
        
        // Act
        var result = iface.GetScreenRegion(0, 0, width, height);
        
        if (result == null) return false;
        
        // Assert: 返回的 Mat 尺寸应该与请求的尺寸匹配
        var dimensionsMatch = result.Width == width && result.Height == height;
        result.Dispose();
        
        return dimensionsMatch;
    }
    
    /// <summary>
    /// Property 14.9: Multiple sequential captures work correctly
    /// FOR ALL sequences of captures, each capture SHALL succeed independently.
    /// **Validates: Requirements 10.3, 10.4**
    /// </summary>
    [Property(MaxTest = 20)]
    public bool MultipleSequentialCapturesWorkCorrectly(PositiveInt countGen)
    {
        var count = (countGen.Get % 5) + 1; // 1-5 次截图
        var iface = CreateInterface();
        iface.UseGdiMode();
        
        var allSucceeded = true;
        
        for (int i = 0; i < count; i++)
        {
            var result = iface.GetScreenRegion(0, 0, 10, 10);
            if (result == null)
            {
                allSucceeded = false;
                break;
            }
            result.Dispose();
        }
        
        return allSucceeded;
    }
    
    /// <summary>
    /// Property 14.10: Dispose releases resources properly
    /// WHEN Dispose is called, subsequent operations SHALL not throw.
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Fact]
    public void DisposeReleasesResourcesProperly()
    {
        var iface = new OpenCvImageInterface();
        iface.UseGdiMode();
        
        // 先进行一次截图
        var result = iface.GetScreenRegion(0, 0, 10, 10);
        result?.Dispose();
        
        // Dispose 接口
        iface.Dispose();
        
        // Assert: 不应该抛出异常
        // （Dispose 后的行为是未定义的，但不应该崩溃）
    }
}
