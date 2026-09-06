using System.Numerics;
using LiteSkinViewer3D.Shared.Enums;
using LiteSkinViewer3D.Shared.Models;
using SkiaSharp;

namespace LiteSkinViewer3D.Shared;

public static class VoxelOverlayGenerator
{
    public static VoxelOverlayData Generate(SKBitmap skin, SkinType type)
    {
        var topTex = SteveTextureBuilder.GetSteveTextureTop(type);
        var data = new VoxelOverlayData();

        data.Head = GeneratePart(skin, topTex.Head, new Vector3(1.0f * 1.125f));

        if (type != SkinType.Legacy)
        {
            var armScaleX = type == SkinType.Slim ? 0.375f : 0.5f;
            data.Body = GeneratePart(skin, topTex.Body, new Vector3(0.5f * 1.125f, 1.5f * 1.125f, 0.5f * 1.125f));

            var armScale = new Vector3(armScaleX * 1.125f, 1.5f * 1.125f, 0.5f * 1.125f);
            data.LeftArm = GeneratePart(skin, topTex.LeftArm, armScale);
            data.RightArm = GeneratePart(skin, topTex.RightArm, armScale);

            var legScale = new Vector3(0.5f * 1.125f, 1.5f * 1.125f, 0.5f * 1.125f);
            data.LeftLeg = GeneratePart(skin, topTex.LeftLeg, legScale);
            data.RightLeg = GeneratePart(skin, topTex.RightLeg, legScale);
        }

        return data;
    }

    private static VoxelOverlayPart GeneratePart(SKBitmap skin, float[] uv, Vector3 cubeScale)
    {
        var vertices = new List<float>();
        var colors = new List<float>();
        var normals = new List<float>();
        var indices = new List<ushort>();
        var vertexOffset = 0;

        if (uv == null || uv.Length < 48)
            return new VoxelOverlayPart
            {
                Vertices = [], Colors = [], Normals = [], Indices = []
            };

        for (var face = 0; face < 6; face++)
        {
            var fi = face * 8;

            var u0 = uv[fi];
            var u1 = uv[fi + 2];
            var u2 = uv[fi + 4];
            var u3 = uv[fi + 6];
            var v0 = uv[fi + 1];
            var v1 = uv[fi + 3];
            var v2 = uv[fi + 5];
            var v3 = uv[fi + 7];

            var texW = skin.Width;
            var texH = skin.Height;

            var minUPixel = (int)MathF.Floor(MathF.Min(MathF.Min(u0, u1), MathF.Min(u2, u3)) * texW);
            var maxUPixel = (int)MathF.Ceiling(MathF.Max(MathF.Max(u0, u1), MathF.Max(u2, u3)) * texW);
            var minVPixel = (int)MathF.Floor(MathF.Min(MathF.Min(v0, v1), MathF.Min(v2, v3)) * texH);
            var maxVPixel = (int)MathF.Ceiling(MathF.Max(MathF.Max(v0, v1), MathF.Max(v2, v3)) * texH);

            var uRange = maxUPixel - minUPixel;
            var vRange = maxVPixel - minVPixel;
            if (uRange <= 0 || vRange <= 0) continue;

            var facePositions = new Vector3[4];
            for (var k = 0; k < 4; k++)
            {
                var vi = face * 4 + k;
                facePositions[k] = Cube.Positions[vi] * cubeScale;
            }

            var corner = facePositions[0];
            var uAxis = facePositions[3] - facePositions[0];
            var vAxis = facePositions[1] - facePositions[0];

            var normalIdx = face * 4;
            var rawNormal = new Vector3(
                Cube.Normals[normalIdx].X / cubeScale.X,
                Cube.Normals[normalIdx].Y / cubeScale.Y,
                Cube.Normals[normalIdx].Z / cubeScale.Z);
            var faceNormal = Vector3.Normalize(rawNormal);

            var voxelSizeU = uAxis.Length() / uRange;
            var voxelSizeV = vAxis.Length() / vRange;
            var voxelSize = MathF.Min(voxelSizeU, voxelSizeV);
            if (voxelSize <= 0) voxelSize = 0.1f;

            for (var py = minVPixel; py < maxVPixel; py++)
            {
                for (var px = minUPixel; px < maxUPixel; px++)
                {
                    if (px < 0 || px >= texW || py < 0 || py >= texH) continue;

                    var pixelColor = skin.GetPixel(px, py);
                    if (pixelColor.Alpha < 128) continue;

                    var uParam = (px - minUPixel + 0.5f) / uRange;
                    var vParam = (py - minVPixel + 0.5f) / vRange;

                    var center = corner + uAxis * uParam + vAxis * vParam;

                    var r = pixelColor.Red / 255f;
                    var g = pixelColor.Green / 255f;
                    var b = pixelColor.Blue / 255f;

                    var partPositions = Cube.GetTransformedVertices(new Vector3(voxelSize), center);

                    for (var i = 0; i < 24; i++)
                    {
                        vertices.Add(partPositions[i].X);
                        vertices.Add(partPositions[i].Y);
                        vertices.Add(partPositions[i].Z);
                        colors.Add(r);
                        colors.Add(g);
                        colors.Add(b);
                        normals.Add(Cube.Normals[i].X);
                        normals.Add(Cube.Normals[i].Y);
                        normals.Add(Cube.Normals[i].Z);
                    }

                    var baseIdx = Cube.GetIndices(0);
                    for (var i = 0; i < baseIdx.Length; i++)
                        indices.Add((ushort)(baseIdx[i] + vertexOffset));

                    vertexOffset += 24;
                }
            }
        }

        return new VoxelOverlayPart
        {
            Vertices = vertices.ToArray(),
            Colors = colors.ToArray(),
            Normals = normals.ToArray(),
            Indices = indices.ToArray()
        };
    }
}
