using System.Security.Cryptography;
using SkiaSharp;

namespace SulfurLauncher.Core.Module.SkinLibrary;

/// <summary>
/// 皮肤内容哈希：对皮肤解码后的像素（宽度 + 高度 + RGBA 像素）计算 SHA-256。
/// 两份编码不同但画面相同的皮肤会得到同一哈希，实现跨来源的内容去重；
/// 当无法解码（非法图片）时退化为对原始字节哈希。
/// </summary>
public static class SkinContentHasher
{
    public static string Compute(byte[] pngBytes)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(pngBytes);
            if (bitmap != null && bitmap.Width > 0 && bitmap.Height > 0)
            {
                var pixels = new byte[bitmap.Width * bitmap.Height * 4];
                var offset = 0;
                for (var y = 0; y < bitmap.Height; y++)
                {
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var color = bitmap.GetPixel(x, y);
                        pixels[offset++] = color.Red;
                        pixels[offset++] = color.Green;
                        pixels[offset++] = color.Blue;
                        pixels[offset++] = color.Alpha;
                    }
                }

                var input = new byte[8 + pixels.Length];
                BitConverter.GetBytes(bitmap.Width).CopyTo(input, 0);
                BitConverter.GetBytes(bitmap.Height).CopyTo(input, 4);
                pixels.CopyTo(input, 8);
                return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
            }
        }
        catch
        {
            // 解码失败时退回原始字节哈希。
        }

        return Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant();
    }
}