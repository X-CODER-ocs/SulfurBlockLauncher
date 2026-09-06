using System;
using System.Numerics;
using SkiaSharp;

namespace LiteSkinViewer2D.Rendering;

public sealed class SkinCamera
{
    private const float Deg = MathF.PI / 180f;

    public float YawDeg { get; set; }

    public float PitchDeg { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public float FitScale { get; set; } = 1f;

    public VerticalAlign Alignment { get; set; } = VerticalAlign.Center;

    public enum VerticalAlign
    {
        Center,
        Top,
    }

    public Matrix4x4 BuildView() =>
        Matrix4x4.CreateRotationY(YawDeg * Deg) * Matrix4x4.CreateRotationX(PitchDeg * Deg);

    public (float Scale, Vector2 Translate) Fit(BoundingBox box, Matrix4x4 view)
    {
        float minx = float.MaxValue, maxx = float.MinValue;
        float miny = float.MaxValue, maxy = float.MinValue;
        foreach (var c in box.Corners())
        {
            var r = Vector3.Transform(c, view);
            minx = MathF.Min(minx, r.X);
            maxx = MathF.Max(maxx, r.X);
            miny = MathF.Min(miny, r.Y);
            maxy = MathF.Max(maxy, r.Y);
        }

        var sizeX = maxx - minx;
        var sizeY = maxy - miny;
        var sx = sizeX > 0 ? Width / sizeX : 1f;
        var sy = sizeY > 0 ? Height / sizeY : 1f;
        var scale = MathF.Min(sx, sy) * FitScale;

        var cx = (minx + maxx) * 0.5f;
        var cy = (miny + maxy) * 0.5f;
        var tx = Width * 0.5f - cx * scale;
        var ty = Alignment == VerticalAlign.Top
            ? maxy * scale
            : Height * 0.5f + cy * scale;
        return (scale, new Vector2(tx, ty));
    }

    public (SKPoint Point, float Depth) Project(Vector3 v, Matrix4x4 view, float scale, Vector2 tr)
    {
        var r = Vector3.Transform(v, view);
        var pt = new SKPoint(r.X * scale + tr.X, tr.Y - r.Y * scale);
        return (pt, r.Z);
    }
}
