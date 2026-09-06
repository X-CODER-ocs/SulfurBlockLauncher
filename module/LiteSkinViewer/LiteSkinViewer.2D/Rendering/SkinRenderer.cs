using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LiteSkinViewer3D.Shared.Enums;
using LiteSkinViewer3D.Shared.Helpers;
using SkiaSharp;

namespace LiteSkinViewer2D.Rendering;

public sealed class SkinRenderer
{
    public SKBitmap RenderHead(SKBitmap skin, float yawDeg = 0f, float pitchDeg = 0f, int size = 256)
    {
        var faces = SkinGeometry.BuildHead();
        return Draw(skin, faces, yawDeg, pitchDeg, size, size);
    }

    public SKBitmap RenderBody(SKBitmap skin, float yawDeg = 45f, float pitchDeg = 15f, int width = 210, int height = 420)
    {
        var type = DetectSkinType(skin);
        var faces = SkinGeometry.BuildBody(type);
        return Draw(skin, faces, yawDeg, pitchDeg, width, height);
    }

    public IReadOnlyList<SKBitmap> RenderBodyViews(SKBitmap skin, int width = 210, int height = 420)
    {
        var result = new List<SKBitmap>(4);
        foreach (var yaw in new[] { 0f, 90f, 180f, 270f })
            result.Add(RenderBody(skin, yaw, 0f, width, height));
        return result;
    }

    public SKBitmap Render(SKBitmap skin, SkinViewType view) => view switch
    {
        SkinViewType.Face => RenderHead(skin, 0f, 0f),
        SkinViewType.Body => RenderBody(skin, 45f, 15f),
        SkinViewType.Cover => RenderCover(skin),
        SkinViewType.Front => RenderBody(skin, 0f, 0f),
        SkinViewType.Right => RenderBody(skin, 90f, 0f),
        SkinViewType.Back => RenderBody(skin, 180f, 0f),
        SkinViewType.Left => RenderBody(skin, 270f, 0f),
        _ => throw new ArgumentOutOfRangeException(nameof(view)),
    };

    public SKBitmap RenderCover(SKBitmap skin, int width = 210, int height = 420)
    {
        var type = DetectSkinType(skin);
        var faces = SkinGeometry.BuildBody(type);
        var crop = new SKRectI(0, 0, width, (int)MathF.Round(height * 0.53f));
        return Draw(skin, faces, 45f, 15f, width, height, SkinCamera.VerticalAlign.Top, crop);
    }

    private static SkinType DetectSkinType(SKBitmap skin)
    {
        var type = SkinHelper.DetectSkin(skin);
        return type == LiteSkinViewer3D.Shared.Enums.SkinType.Unknown
            ? LiteSkinViewer3D.Shared.Enums.SkinType.Classic
            : type;
    }

    private static SKBitmap Draw(SKBitmap skin, IList<SkinFace> faces, float yaw, float pitch, int w, int h,
        SkinCamera.VerticalAlign valign = SkinCamera.VerticalAlign.Center, SKRectI? crop = null)
    {
        var cam = new SkinCamera { YawDeg = yaw, PitchDeg = pitch, Width = w, Height = h, Alignment = valign };
        var view = cam.BuildView();
        var (scale, tr) = cam.Fit(SkinGeometry.ComputeBounds(faces), view);

        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var visible = new List<ProjectedFace>(faces.Count);
        foreach (var f in faces)
        {
            var pf = ProjectFace(f, view, scale, tr);
            if (SignedArea(pf.Pts) < 0f)
                visible.Add(pf);
        }

        using var shader = skin.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, new SKSamplingOptions(SKFilterMode.Nearest));
        using var paint = new SKPaint { IsAntialias = true, Shader = shader };

        foreach (var pf in visible.Where(p => !p.Overlay).OrderBy(p => p.Depth))
            DrawFace(canvas, pf.Face, pf.Pts, paint);
        foreach (var pf in visible.Where(p => p.Overlay).OrderBy(p => p.Depth))
            DrawFace(canvas, pf.Face, pf.Pts, paint);

        using var snapshot = crop is { } rect ? surface.Snapshot(rect) : surface.Snapshot();
        return SKBitmap.FromImage(snapshot);
    }

    private static ProjectedFace ProjectFace(SkinFace f, Matrix4x4 view, float scale, Vector2 tr)
    {
        var pts = new SKPoint[4];
        var vs = new[] { f.V0, f.V1, f.V2, f.V3 };
        var depth = 0f;
        for (var i = 0; i < 4; i++)
        {
            var r = Vector3.Transform(vs[i], view);
            depth += r.Z;
            pts[i] = new SKPoint(r.X * scale + tr.X, tr.Y - r.Y * scale);
        }

        return new ProjectedFace(pts, depth * 0.25f, f.Overlay, f);
    }

    private static void DrawFace(SKCanvas canvas, SkinFace f, SKPoint[] pts, SKPaint paint)
    {
        var uvs = new[]
        {
            new SKPoint(f.U0.X, f.U0.Y),
            new SKPoint(f.U1.X, f.U1.Y),
            new SKPoint(f.U2.X, f.U2.Y),
            new SKPoint(f.U3.X, f.U3.Y),
        };
        using var verts = SKVertices.CreateCopy(SKVertexMode.TriangleFan, pts, uvs, null);
        canvas.DrawVertices(verts, SKBlendMode.SrcOver, paint);
    }

    private static float SignedArea(SKPoint[] p)
    {
        var a = 0f;
        for (var i = 0; i < 4; i++)
        {
            var j = (i + 1) % 4;
            a += p[i].X * p[j].Y - p[j].X * p[i].Y;
        }

        return a * 0.5f;
    }

    private sealed record ProjectedFace(SKPoint[] Pts, float Depth, bool Overlay, SkinFace Face);
}
