using System.Numerics;
using System.Text.Json;
using LiteSkinViewer3D.Shared.Models;

namespace LiteSkinViewer3D.Shared;

public static class DynamicModelBuilder
{
    private const float BedrockToModel = 1f / 8f;

    private static readonly Vector3[] CubeCorners =
    [
        new(-0.5f,  0.5f, -0.5f),
        new(-0.5f, -0.5f, -0.5f),
        new( 0.5f, -0.5f, -0.5f),
        new( 0.5f,  0.5f, -0.5f),
        new( 0.5f,  0.5f,  0.5f),
        new( 0.5f, -0.5f,  0.5f),
        new(-0.5f, -0.5f,  0.5f),
        new(-0.5f,  0.5f,  0.5f),
    ];

    private static readonly int[] FaceVertexIndices =
    [
        3, 2, 1, 0,
        7, 6, 5, 4,
        0, 1, 6, 7,
        4, 5, 2, 3,
        0, 7, 4, 3,
        1, 2, 5, 6
    ];

    public static DynamicModel Build(List<BedrockBone> bones, float texWidth, float texHeight)
    {
        var model = new DynamicModel();

        if (bones == null || bones.Count == 0)
            return model;

        var boneDict = bones.ToDictionary(b => b.Name, b => b);
        var boneTransforms = new Dictionary<string, Matrix4x4>();

        foreach (var bone in bones)
        {
            ComputeBoneTransform(bone, boneDict, boneTransforms);
        }

        var allVertices = new List<float>();
        var allUvs = new List<float>();
        var allNormals = new List<float>();
        var allIndices = new List<uint>();
        int vertexOffset = 0;

        foreach (var bone in bones)
        {
            var boneTransform = boneTransforms.GetValueOrDefault(bone.Name, Matrix4x4.Identity);

            if (bone.Cubes is { Count: > 0 })
            {
                foreach (var cube in bone.Cubes)
                {
                    BuildCube(cube, boneTransform, texWidth, texHeight,
                        allVertices, allUvs, allNormals, allIndices, ref vertexOffset);
                }
            }

            if (bone.PolyMesh != null)
            {
                BuildPolyMesh(bone.PolyMesh, boneTransform, texWidth, texHeight,
                    allVertices, allUvs, allNormals, allIndices, ref vertexOffset);
            }
        }

        if (allVertices.Count > 0)
        {
            model.Parts.Add(new DynamicMeshData
            {
                Vertices = allVertices.ToArray(),
                Uvs = allUvs.ToArray(),
                Normals = allNormals.ToArray(),
                Indices = allIndices.ToArray()
            });
        }

        if (model.Parts.Count > 0)
        {
            model.IsDynamicModel = true;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var part in model.Parts)
            {
                for (var i = 0; i < part.Vertices.Length; i += 3)
                {
                    if (part.Vertices[i] < minX) minX = part.Vertices[i];
                    if (part.Vertices[i] > maxX) maxX = part.Vertices[i];
                    if (part.Vertices[i + 1] < minY) minY = part.Vertices[i + 1];
                    if (part.Vertices[i + 1] > maxY) maxY = part.Vertices[i + 1];
                    if (part.Vertices[i + 2] < minZ) minZ = part.Vertices[i + 2];
                    if (part.Vertices[i + 2] > maxZ) maxZ = part.Vertices[i + 2];
                }
            }
            model.CenterX = (minX + maxX) * 0.5f;
            model.CenterY = (minY + maxY) * 0.5f;
            model.CenterZ = (minZ + maxZ) * 0.5f;
        }

        return model;
    }

    private static void ComputeBoneTransform(BedrockBone bone,
        Dictionary<string, BedrockBone> boneDict,
        Dictionary<string, Matrix4x4> transforms)
    {
        if (transforms.ContainsKey(bone.Name))
            return;

        var pivot = bone.Pivot != null && bone.Pivot.Length == 3
            ? new Vector3(bone.Pivot[0], bone.Pivot[1], bone.Pivot[2]) * BedrockToModel
            : Vector3.Zero;

        var rotation = bone.Rotation != null && bone.Rotation.Length == 3
            ? new Vector3(bone.Rotation[0], bone.Rotation[1], bone.Rotation[2])
            : Vector3.Zero;

        var localTransform =
            Matrix4x4.CreateTranslation(-pivot) *
            Matrix4x4.CreateRotationZ(MathF.PI / 180f * rotation.Z) *
            Matrix4x4.CreateRotationY(MathF.PI / 180f * rotation.Y) *
            Matrix4x4.CreateRotationX(MathF.PI / 180f * rotation.X) *
            Matrix4x4.CreateTranslation(pivot);

        if (!string.IsNullOrEmpty(bone.Parent) && boneDict.TryGetValue(bone.Parent, out var parentBone))
        {
            ComputeBoneTransform(parentBone, boneDict, transforms);
            var parentTransform = transforms[bone.Parent];
            localTransform = localTransform * parentTransform;
        }

        transforms[bone.Name] = localTransform;
    }

    private static readonly Vector3[] FaceNormals =
    [
        new(0, 0, -1),
        new(0, 0, 1),
        new(-1, 0, 0),
        new(1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0)
    ];

    private static void BuildCube(BedrockCube cube, Matrix4x4 boneTransform,
        float texWidth, float texHeight,
        List<float> vertices, List<float> uvs, List<float> normals, List<uint> indices,
        ref int vertexOffset)
    {
        var origin = new Vector3(cube.Origin[0], cube.Origin[1], cube.Origin[2]) * BedrockToModel;
        var size = new Vector3(cube.Size[0], cube.Size[1], cube.Size[2]) * BedrockToModel;
        var u = cube.Uv[0];
        var v = cube.Uv[1];
        var w = cube.Size[0];
        var h = cube.Size[1];
        var d = cube.Size[2];
        var mirror = cube.Mirror;

        for (var face = 0; face < 6; face++)
        {
            var faceStart = face * 4;
            var localNormal = FaceNormals[face];
            var worldNormal = Vector3.Normalize(Vector3.TransformNormal(localNormal, boneTransform));

            for (var vert = 0; vert < 4; vert++)
            {
                var cornerIdx = FaceVertexIndices[faceStart + vert];
                var cornerOffset = CubeCorners[cornerIdx];

                var localPos = origin + size * (cornerOffset + new Vector3(0.5f));
                var worldPos = Vector3.Transform(localPos, boneTransform);

                vertices.Add(worldPos.X);
                vertices.Add(worldPos.Y);
                vertices.Add(-worldPos.Z);

                normals.Add(worldNormal.X);
                normals.Add(worldNormal.Y);
                normals.Add(-worldNormal.Z);

                float texU, texV;
                (texU, texV) = ComputeUv(face, vert, cornerIdx, u, v, w, h, d, mirror);

                uvs.Add(texU / texWidth);
                uvs.Add(texV / texHeight);
            }

            var baseIdx = (uint)(face * 4 + vertexOffset);
            indices.Add(baseIdx);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 3);
        }

        vertexOffset += 24;
    }

    private static PolyMeshData? ParsePolyMesh(object? polyMesh)
    {
        if (polyMesh is not JsonElement elem)
            return null;

        var result = new PolyMeshData();

        if (elem.TryGetProperty("normalized_uvs", out var normUvs))
            result.NormalizedUvs = normUvs.GetBoolean();

        if (elem.TryGetProperty("positions", out var positions))
        {
            foreach (var pos in positions.EnumerateArray())
            {
                var arr = new float[3];
                var i = 0;
                foreach (var p in pos.EnumerateArray())
                    arr[i++] = p.GetSingle();
                result.Positions.Add(arr);
            }
        }

        if (elem.TryGetProperty("normals", out var normals))
        {
            foreach (var n in normals.EnumerateArray())
            {
                var arr = new float[3];
                var i = 0;
                foreach (var val in n.EnumerateArray())
                    arr[i++] = val.GetSingle();
                result.Normals.Add(arr);
            }
        }

        if (elem.TryGetProperty("uvs", out var uvs))
        {
            foreach (var uv in uvs.EnumerateArray())
            {
                var arr = new float[2];
                var i = 0;
                foreach (var val in uv.EnumerateArray())
                    arr[i++] = val.GetSingle();
                result.Uvs.Add(arr);
            }
        }

        if (elem.TryGetProperty("polys", out var polys))
        {
            foreach (var poly in polys.EnumerateArray())
            {
                var verts = new List<int[]>();
                foreach (var vert in poly.EnumerateArray())
                {
                    var idxArr = new int[3];
                    var j = 0;
                    foreach (var idx in vert.EnumerateArray())
                        idxArr[j++] = idx.GetInt32();
                    verts.Add(idxArr);
                }

                if (verts.Count == 3)
                {
                    result.Polys.Add([verts[0], verts[1], verts[2]]);
                }
                else if (verts.Count == 4)
                {
                    result.Polys.Add([verts[0], verts[1], verts[2]]);
                    result.Polys.Add([verts[0], verts[2], verts[3]]);
                }
            }
        }

        return result;
    }

    private static void BuildPolyMesh(object? polyMeshObj, Matrix4x4 boneTransform,
        float texWidth, float texHeight,
        List<float> vertices, List<float> uvs, List<float> normals, List<uint> indices,
        ref int vertexOffset)
    {
        var mesh = ParsePolyMesh(polyMeshObj);
        if (mesh == null || mesh.Polys.Count == 0)
            return;

        var polyVertices = new List<PolyVertex>();
        var polyIndices = new List<int>();

        foreach (var poly in mesh.Polys)
        {
            for (var triVert = 0; triVert < 3; triVert++)
            {
                var vi = triVert == 1 ? 2 : triVert == 2 ? 1 : 0;
                var posIdx = poly[vi][0];
                var normIdx = poly[vi][1];
                var uvIdx = poly[vi][2];

                var pv = new PolyVertex
                {
                    PosIdx = posIdx,
                    NormIdx = normIdx,
                    UvIdx = uvIdx
                };

                var existingIdx = polyVertices.IndexOf(pv);
                if (existingIdx >= 0)
                {
                    polyIndices.Add(existingIdx);
                }
                else
                {
                    polyIndices.Add(polyVertices.Count);
                    polyVertices.Add(pv);
                }
            }
        }

        for (var i = 0; i < polyVertices.Count; i++)
        {
            var pv = polyVertices[i];

            var posArr = mesh.Positions[pv.PosIdx];
            var localPos = new Vector3(posArr[0], posArr[1], posArr[2]) * BedrockToModel;
            var worldPos = Vector3.Transform(localPos, boneTransform);
            vertices.Add(worldPos.X);
            vertices.Add(worldPos.Y);
            vertices.Add(-worldPos.Z);

            var normArr = mesh.Normals[pv.NormIdx];
            var localNorm = new Vector3(normArr[0], normArr[1], normArr[2]);
            var worldNorm = Vector3.Normalize(Vector3.TransformNormal(localNorm, boneTransform));
            normals.Add(worldNorm.X);
            normals.Add(worldNorm.Y);
            normals.Add(-worldNorm.Z);

            var uvArr = mesh.Uvs[pv.UvIdx];
            if (mesh.NormalizedUvs)
            {
                uvs.Add(uvArr[0]);
                uvs.Add(1f - uvArr[1]);
            }
            else
            {
                uvs.Add(uvArr[0] / texWidth);
                uvs.Add((texHeight - uvArr[1]) / texHeight);
            }
        }

        foreach (var idx in polyIndices)
        {
            indices.Add((uint)(idx + vertexOffset));
        }

        vertexOffset += polyVertices.Count;
    }

    private struct PolyVertex : IEquatable<PolyVertex>
    {
        public int PosIdx;
        public int NormIdx;
        public int UvIdx;

        public readonly bool Equals(PolyVertex other) =>
            PosIdx == other.PosIdx && NormIdx == other.NormIdx && UvIdx == other.UvIdx;

        public override readonly bool Equals(object? obj) =>
            obj is PolyVertex other && Equals(other);

        public override readonly int GetHashCode() =>
            HashCode.Combine(PosIdx, NormIdx, UvIdx);
    }

    private static (float u, float v) ComputeUv(int face, int vertInFace, int cornerIdx,
        float u, float v, float w, float h, float d, bool mirror)
    {
        float pu, pv;

        switch (face)
        {
            case 0:
                pu = u + d + w + d + (vertInFace is 0 or 1 ? w : 0);
                pv = v + d + (vertInFace is 0 or 3 ? 0 : h);
                break;
            case 1:
                pu = u + d + (vertInFace is 2 or 3 ? w : 0);
                pv = v + d + (vertInFace is 1 or 2 ? h : 0);
                break;
            case 2:
                pu = u + (vertInFace is 2 or 3 ? d : 0);
                pv = v + d + (vertInFace is 0 or 3 ? 0 : h);
                break;
            case 3:
                pu = u + d + w + (vertInFace is 2 or 3 ? d : 0);
                pv = v + d + (vertInFace is 0 or 3 ? 0 : h);
                break;
            case 4:
                pu = u + d + (vertInFace is 2 or 3 ? w : 0);
                pv = v + (vertInFace is 0 or 3 ? 0 : d);
                break;
            case 5:
                pu = u + d + w + (vertInFace is 0 or 3 ? 0 : w);
                pv = v + (vertInFace is 0 or 1 ? 0 : d);
                break;
            default:
                pu = 0;
                pv = 0;
                break;
        }

        if (mirror)
        {
            var faceLeft = face switch
            {
                0 => u + d + w + d,
                1 => u + d,
                2 => u,
                3 => u + d + w,
                4 => u + d,
                5 => u + d + w,
                _ => 0
            };
            var faceRight = face switch
            {
                0 => u + d + w + d + w,
                1 => u + d + w,
                2 => u + d,
                3 => u + d + w + d,
                4 => u + d + w,
                5 => u + d + w + w,
                _ => 0
            };
            pu = faceLeft + faceRight - pu;
        }

        return (pu, pv);
    }
}
