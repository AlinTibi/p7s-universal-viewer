using System.IO;
using System.Windows.Media.Imaging;

namespace P7SUniversalViewer;

public static class ImagePreviewLoader
{
    public static BitmapImage Load(byte[] bytes)
    {
        using var probe = new MemoryStream(bytes);
        var decoder = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > 40_000_000)
            throw new InvalidDataException("Image dimensions exceed the 40 megapixel preview limit.");

        // The header decoder consumes its stream. Decode from a fresh stream so
        // the preview starts at the image signature, regardless of codec behavior.
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = Math.Min(frame.PixelWidth, 1600);
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
