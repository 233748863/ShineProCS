using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace ShineProCS.Views;

public partial class RegionPreviewWindow : FluentWindow
{
    public int[] Region { get; private set; }

    public RegionPreviewWindow(int[] region)
    {
        InitializeComponent();
        Region = (int[])region.Clone();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        RegionInfo.Text = $"区域: X={Region[0]}, Y={Region[1]}, W={Region[2]}, H={Region[3]}";
        
        if (Region[2] <= 0 || Region[3] <= 0)
        {
            PreviewImage.Source = null;
            return;
        }

        try
        {
            using var bitmap = CaptureRegion(Region[0], Region[1], Region[2], Region[3]);
            if (bitmap != null)
            {
                PreviewImage.Source = BitmapToImageSource(bitmap);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"截图失败: {ex.Message}");
        }
    }

    private static Bitmap? CaptureRegion(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0) return null;
        
        // 确保不超出屏幕范围
        var screenWidth = (int)SystemParameters.PrimaryScreenWidth;
        var screenHeight = (int)SystemParameters.PrimaryScreenHeight;
        
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        if (x + width > screenWidth) width = screenWidth - x;
        if (y + height > screenHeight) height = screenHeight - y;
        
        if (width <= 0 || height <= 0) return null;

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
        return bitmap;
    }

    private static BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0;
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memory;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        return bitmapImage;
    }

    // 位置微调
    private void MoveLeft_Click(object sender, RoutedEventArgs e) { Region[0] = Math.Max(0, Region[0] - 1); UpdatePreview(); }
    private void MoveRight_Click(object sender, RoutedEventArgs e) { Region[0]++; UpdatePreview(); }
    private void MoveUp_Click(object sender, RoutedEventArgs e) { Region[1] = Math.Max(0, Region[1] - 1); UpdatePreview(); }
    private void MoveDown_Click(object sender, RoutedEventArgs e) { Region[1]++; UpdatePreview(); }

    // 大小微调
    private void ShrinkWidth_Click(object sender, RoutedEventArgs e) { Region[2] = Math.Max(1, Region[2] - 1); UpdatePreview(); }
    private void ExpandWidth_Click(object sender, RoutedEventArgs e) { Region[2]++; UpdatePreview(); }
    private void ShrinkHeight_Click(object sender, RoutedEventArgs e) { Region[3] = Math.Max(1, Region[3] - 1); UpdatePreview(); }
    private void ExpandHeight_Click(object sender, RoutedEventArgs e) { Region[3]++; UpdatePreview(); }

    private void Refresh_Click(object sender, RoutedEventArgs e) => UpdatePreview();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
