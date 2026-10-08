using P7SUniversalViewer;
using Xunit;

namespace P7SUniversalViewer.Tests;

public class ImagePreviewTests
{
    [Fact]
    public void PngPreviewDecodesAfterHeaderInspectionAndOwnsItsPixels()
    {
        var image = ImagePreviewLoader.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "preview.png")));
        Assert.Equal(256, image.PixelWidth);
        Assert.Equal(256, image.PixelHeight);
        Assert.True(image.IsFrozen);
        var pixels = new byte[256 * 256 * 4];
        image.CopyPixels(pixels, 256 * 4, 0);
        Assert.Contains(pixels, value => value != 0);
    }
}
