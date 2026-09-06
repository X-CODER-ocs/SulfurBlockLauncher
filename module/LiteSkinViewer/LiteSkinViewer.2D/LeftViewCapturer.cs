using LiteSkinViewer.Shared.Interfaces;
using LiteSkinViewer2D.Rendering;
using SkiaSharp;

namespace LiteSkinViewer2D;

public sealed class LeftViewCapturer : ICapturer
{
    public static readonly LeftViewCapturer Default = new();

    private readonly SkinRenderer _renderer = new();

    public SKBitmap Capture(SKBitmap skin, int scale = 8)
    {
        var width = 30 * scale;
        var height = 60 * scale;
        return _renderer.RenderBody(skin, 270f, 0f, width, height);
    }
}
