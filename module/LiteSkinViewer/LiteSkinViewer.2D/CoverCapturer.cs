using LiteSkinViewer.Shared.Interfaces;
using LiteSkinViewer2D.Rendering;
using SkiaSharp;

namespace LiteSkinViewer2D;

public sealed class CoverCapturer : ICapturer
{
    public static readonly CoverCapturer Default = new();

    private readonly SkinRenderer _renderer = new();

    public SKBitmap Capture(SKBitmap skin, int scale = 8)
    {
        var width = 30 * scale;
        var height = 60 * scale;
        return _renderer.RenderCover(skin, width, height);
    }
}
