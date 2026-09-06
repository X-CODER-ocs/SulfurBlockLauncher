using LiteSkinViewer.Shared.Interfaces;
using LiteSkinViewer2D.Rendering;
using SkiaSharp;

namespace LiteSkinViewer2D;

public sealed class Head3DCapturer : ICapturer
{
    public static readonly Head3DCapturer Default = new();

    private readonly SkinRenderer _renderer = new();

    public SKBitmap Capture(SKBitmap skin, int scale = 8)
    {
        var size = 32 * scale;
        return _renderer.RenderHead(skin, 0f, 0f, size);
    }
}
