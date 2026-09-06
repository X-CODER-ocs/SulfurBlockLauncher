using System.Text.Json.Serialization;

namespace LiteSkinViewer3D.Shared.Models;

public record BedrockSkinInfo
{
    [JsonPropertyName("localization_name")]
    public string LocalizationName { get; set; } = "";

    [JsonPropertyName("geometry")]
    public string Geometry { get; set; } = "";

    [JsonPropertyName("texture")]
    public string Texture { get; set; } = "";

    public string? Cape { get; set; }

    public string? Type { get; set; }
}

public record BedrockSkinsFile
{
    [JsonPropertyName("skins")]
    public List<BedrockSkinInfo> Skins { get; set; } = [];
}

public record BedrockCube
{
    [JsonPropertyName("origin")]
    public float[] Origin { get; set; } = [0, 0, 0];

    [JsonPropertyName("size")]
    public float[] Size { get; set; } = [1, 1, 1];

    [JsonPropertyName("uv")]
    public float[] Uv { get; set; } = [0, 0];

    [JsonPropertyName("mirror")]
    public bool Mirror { get; set; }
}

public record BedrockBone
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("parent")]
    public string? Parent { get; set; }

    [JsonPropertyName("pivot")]
    public float[]? Pivot { get; set; }

    [JsonPropertyName("rotation")]
    public float[]? Rotation { get; set; }

    [JsonPropertyName("cubes")]
    public List<BedrockCube>? Cubes { get; set; }

    [JsonPropertyName("poly_mesh")]
    public object? PolyMesh { get; set; }
}

public record BedrockGeometry
{
    [JsonPropertyName("format_version")]
    public string FormatVersion { get; set; } = "";

    [JsonExtensionData]
    public Dictionary<string, object>? BonesData { get; set; }
}

public record BedrockGeometryBones
{
    [JsonPropertyName("bones")]
    public List<BedrockBone> Bones { get; set; } = [];

    [JsonPropertyName("texturewidth")]
    public int TextureWidth { get; set; } = 64;

    [JsonPropertyName("textureheight")]
    public int TextureHeight { get; set; } = 64;
}

public record PolyMeshData
{
    public List<float[]> Positions { get; set; } = [];
    public List<float[]> Normals { get; set; } = [];
    public List<float[]> Uvs { get; set; } = [];
    public List<int[][]> Polys { get; set; } = [];
    public bool NormalizedUvs { get; set; }
}

public record DynamicMeshData
{
    public float[] Vertices { get; set; } = [];
    public float[] Uvs { get; set; } = [];
    public float[] Normals { get; set; } = [];
    public uint[] Indices { get; set; } = [];
}

public record DynamicModel
{
    public List<DynamicMeshData> Parts { get; set; } = [];
    public bool IsDynamicModel { get; set; }
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float CenterZ { get; set; }
}
