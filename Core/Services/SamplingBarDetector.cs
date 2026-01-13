using OpenCvSharp;

namespace ShineProCS.Core.Services;

/// <summary>
/// 采样点血条/蓝条检测器
/// 需求 3.1: 使用沿条中线的采样点代替全区域 HSV 扫描
/// 
/// 设计原理：
/// - 血条/蓝条通常是水平的矩形区域
/// - 沿着中线采样可以避免边缘干扰
/// - 采样点数量根据宽度动态计算（5-20个点）
/// </summary>
public class SamplingBarDetector
{
    // 采样点数量限制（需求 3.1: 5-20 个点）
    private const int MinSampleCount = 5;
    private const int MaxSampleCount = 20;
    
    // 默认颜色阈值（可通过配置覆盖）
    private readonly BarColorThresholds _thresholds;
    
    /// <summary>
    /// 创建采样点检测器
    /// </summary>
    /// <param name="thresholds">颜色阈值配置，为 null 时使用默认值</param>
    public SamplingBarDetector(BarColorThresholds? thresholds = null)
    {
        _thresholds = thresholds ?? BarColorThresholds.Default;
    }
    
    /// <summary>
    /// 检测血条/蓝条百分比
    /// 需求 3.1: 使用沿条中线的采样点代替全区域 HSV 扫描
    /// </summary>
    /// <param name="barImage">条形区域图像（BGR格式）</param>
    /// <param name="isHealth">是否为血条（true=血条，false=蓝条）</param>
    /// <returns>百分比 (0-100)</returns>
    public double DetectPercentage(Mat barImage, bool isHealth)
    {
        if (barImage == null || barImage.Empty())
            return 0.0;
        
        int width = barImage.Width;
        int height = barImage.Height;
        
        // 需求 3.1: 采样点位于中线
        int midY = height / 2;
        
        // 需求 3.1: 采样点数量计算（5-20 个点）
        // 根据宽度动态计算，每 10 像素一个采样点
        int sampleCount = CalculateSampleCount(width);
        int matchCount = 0;
        
        unsafe
        {
            var ptr = (byte*)barImage.DataPointer;
            int stride = (int)barImage.Step();
            int channels = barImage.Channels();
            
            // 确保是 BGR 格式（3 通道）
            if (channels < 3)
                return 0.0;
            
            for (int i = 0; i < sampleCount; i++)
            {
                // 计算采样点的 X 坐标（均匀分布）
                int x = CalculateSampleX(i, sampleCount, width);
                
                // 确保 X 坐标在有效范围内
                if (x < 0 || x >= width)
                    continue;
                
                // 计算像素偏移量
                int offset = midY * stride + x * channels;
                
                // 读取 BGR 值
                byte b = ptr[offset];
                byte g = ptr[offset + 1];
                byte r = ptr[offset + 2];
                
                // 根据类型检测颜色
                if (isHealth)
                {
                    // 需求 3.4: 同时识别红色（受伤）和绿色（治疗）配色方案
                    if (IsHealthColor(r, g, b))
                        matchCount++;
                }
                else
                {
                    // 蓝条检测
                    if (IsManaColor(r, g, b))
                        matchCount++;
                }
            }
        }
        
        // 计算百分比
        double percent = (double)matchCount / sampleCount * 100.0;
        return Math.Clamp(percent, 0.0, 100.0);
    }
    
    /// <summary>
    /// 获取采样点的 Y 坐标（中线位置）
    /// 需求 3.1: 所有采样点的 Y 坐标应该等于图像高度的一半
    /// </summary>
    /// <param name="imageHeight">图像高度</param>
    /// <returns>中线 Y 坐标</returns>
    public static int GetSampleY(int imageHeight)
    {
        return imageHeight / 2;
    }
    
    /// <summary>
    /// 计算采样点数量
    /// 需求 3.1: 采样点数量在 5-20 个之间
    /// </summary>
    /// <param name="width">图像宽度</param>
    /// <returns>采样点数量</returns>
    public static int CalculateSampleCount(int width)
    {
        // 每 10 像素一个采样点，但限制在 5-20 之间
        int count = width / 10;
        return Math.Clamp(count, MinSampleCount, MaxSampleCount);
    }
    
    /// <summary>
    /// 计算第 i 个采样点的 X 坐标
    /// 采样点均匀分布在图像宽度上
    /// </summary>
    /// <param name="index">采样点索引（从 0 开始）</param>
    /// <param name="totalSamples">总采样点数</param>
    /// <param name="width">图像宽度</param>
    /// <returns>X 坐标</returns>
    public static int CalculateSampleX(int index, int totalSamples, int width)
    {
        if (totalSamples <= 1)
            return width / 2;
        
        // 均匀分布：从 0 到 width-1
        return (index * (width - 1)) / (totalSamples - 1);
    }
    
    /// <summary>
    /// 获取所有采样点坐标（用于测试和调试）
    /// </summary>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <returns>采样点坐标列表 (x, y)</returns>
    public static List<(int x, int y)> GetSamplePoints(int width, int height)
    {
        var points = new List<(int x, int y)>();
        int midY = GetSampleY(height);
        int sampleCount = CalculateSampleCount(width);
        
        for (int i = 0; i < sampleCount; i++)
        {
            int x = CalculateSampleX(i, sampleCount, width);
            points.Add((x, midY));
        }
        
        return points;
    }
    
    /// <summary>
    /// 检测是否为血条颜色（红色或绿色）
    /// 需求 3.4: 同时识别红色（受伤）和绿色（治疗）配色方案
    /// </summary>
    private bool IsHealthColor(byte r, byte g, byte b)
    {
        // 红色血条检测：R 通道高，G/B 通道低
        bool isRed = r >= _thresholds.RedMinR && 
                     r > g + _thresholds.RedRGDiff && 
                     r > b + _thresholds.RedRBDiff;
        
        // 绿色血条检测：G 通道高，R/B 通道较低
        bool isGreen = g >= _thresholds.GreenMinG && 
                       g > r && 
                       g > b;
        
        return isRed || isGreen;
    }
    
    /// <summary>
    /// 检测是否为蓝条颜色
    /// </summary>
    private bool IsManaColor(byte r, byte g, byte b)
    {
        // 蓝色蓝条检测：B 通道高，R 通道低
        return b >= _thresholds.BlueMinB && 
               b > r && 
               b > g - _thresholds.BlueBGTolerance;
    }
}

/// <summary>
/// 血条/蓝条颜色阈值配置
/// 需求 3.3: 支持可配置的 HSV 阈值以适应不同游戏 UI 主题
/// 注意：这里使用 RGB 空间的简化检测，避免 HSV 转换开销
/// </summary>
public class BarColorThresholds
{
    // 红色血条阈值
    public int RedMinR { get; set; } = 150;      // R 通道最小值
    public int RedRGDiff { get; set; } = 30;     // R-G 最小差值
    public int RedRBDiff { get; set; } = 30;     // R-B 最小差值
    
    // 绿色血条阈值
    public int GreenMinG { get; set; } = 100;    // G 通道最小值
    
    // 蓝色蓝条阈值
    public int BlueMinB { get; set; } = 100;     // B 通道最小值
    public int BlueBGTolerance { get; set; } = 30; // B-G 容差
    
    /// <summary>
    /// 默认阈值配置
    /// </summary>
    public static BarColorThresholds Default => new();
    
    /// <summary>
    /// 从 AppSettings 创建阈值配置
    /// </summary>
    public static BarColorThresholds FromAppSettings(
        int redMinR = 150, int redRGDiff = 30, int redRBDiff = 30,
        int greenMinG = 100,
        int blueMinB = 100, int blueBGTolerance = 30)
    {
        return new BarColorThresholds
        {
            RedMinR = redMinR,
            RedRGDiff = redRGDiff,
            RedRBDiff = redRBDiff,
            GreenMinG = greenMinG,
            BlueMinB = blueMinB,
            BlueBGTolerance = blueBGTolerance
        };
    }
}
