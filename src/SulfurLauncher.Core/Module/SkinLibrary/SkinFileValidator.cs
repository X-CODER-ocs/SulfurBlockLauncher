using SkiaSharp;

namespace SulfurLauncher.Core.Module.SkinLibrary;

/// <summary>
/// Minecraft 本地皮肤文件校验：必须是 PNG，且尺寸为 64×64 或 64×32。
/// </summary>
public static class SkinFileValidator
{
    public static (bool IsValid, int Width, int Height) Validate(byte[] pngBytes)
    {
        if (pngBytes.Length == 0)
            return (false, 0, 0);

        try
        {
            using var bitmap = SKBitmap.Decode(pngBytes);
            if (bitmap == null)
                return (false, 0, 0);

            var valid = bitmap.Width == 64 && (bitmap.Height == 64 || bitmap.Height == 32);
            return (valid, bitmap.Width, bitmap.Height);
        }
        catch
        {
            return (false, 0, 0);
        }
    }

    public static (bool IsValid, int Width, int Height) ValidateFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return (false, 0, 0);

        try
        {
            return Validate(File.ReadAllBytes(filePath));
        }
        catch
        {
            return (false, 0, 0);
        }
    }
}