namespace LiteSkinViewer3D.Shared.Models;

/// <summary>
///     表示模型网格的一个部
/// </summary>
public record CubeItemModel
{
    public ushort[] Indices;
    public float[] Vertices;
}

/// <summary>
///     史蒂夫样式模型
/// </summary>
public record SteveMeshModel
{
    public CubeItemModel Body;
    public CubeItemModel? Cape;

    public CubeItemModel Head;
    public CubeItemModel LeftArm;
    public CubeItemModel LeftLeg;
    public CubeItemModel RightArm;
    public CubeItemModel RightLeg;
}

/// <summary>
///     史蒂夫皮肤贴图的 UV 坐标布局
/// </summary>
public record SteveTextureLayout
{
    public float[] Body;
    public float[]? Cape;

    public float[] Head;
    public float[] LeftArm;
    public float[] LeftLeg;
    public float[] RightArm;
    public float[] RightLeg;
}

/// <summary>
///     单个 Voxel 叠加层部件（顶点 + 颜色 + 法线 + 索引）
/// </summary>
public record VoxelOverlayPart
{
    public float[] Vertices;
    public float[] Colors;
    public float[] Normals;
    public ushort[] Indices;
}

/// <summary>
///     Voxel 叠加层数据，按身体部件分组
/// </summary>
public record VoxelOverlayData
{
    public VoxelOverlayPart Head;
    public VoxelOverlayPart Body;
    public VoxelOverlayPart LeftArm;
    public VoxelOverlayPart RightArm;
    public VoxelOverlayPart LeftLeg;
    public VoxelOverlayPart RightLeg;
}