using System.Numerics;
using System.Runtime.InteropServices;

namespace LiteSkinViewer3D.OpenGL;

/// <summary>
///    OpenGL顶点结构：包含位置、UV坐标与法线，用于传给GPU
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct VertexDataGL
{
    public Vector3 Position;
    public Vector2 UV;
    public Vector3 Normal;
}

/// <summary>
///    带顶点颜色的 OpenGL 顶点结构，用于 Voxel 叠加层
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct ColoredVertexDataGL
{
    public Vector3 Position;
    public Vector3 Color;
    public Vector3 Normal;
}