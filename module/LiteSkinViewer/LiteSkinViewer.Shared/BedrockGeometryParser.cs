using System.Text.Json;
using LiteSkinViewer3D.Shared.Models;

namespace LiteSkinViewer3D.Shared;

public static class BedrockGeometryParser
{
    public static BedrockSkinInfo? FindSkinByTexture(string skinsJsonPath, string textureFileName)
    {
        if (!File.Exists(skinsJsonPath))
            return null;

        var json = File.ReadAllText(skinsJsonPath);
        var skinsFile = JsonSerializer.Deserialize<BedrockSkinsFile>(json);
        if (skinsFile?.Skins == null)
            return null;

        var targetName = Path.GetFileName(textureFileName);
        return skinsFile.Skins.FirstOrDefault(s =>
            string.Equals(s.Texture, targetName, StringComparison.OrdinalIgnoreCase));
    }

    public static BedrockGeometryBones? LoadGeometry(string geometryJsonPath, string geometryId)
    {
        if (!File.Exists(geometryJsonPath))
            return null;

        var json = File.ReadAllBytes(geometryJsonPath);
        var reader = new Utf8JsonReader(json);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName &&
                reader.ValueTextEquals(geometryId.AsSpan()))
            {
                reader.Read();
                using var doc = JsonDocument.ParseValue(ref reader);
                return JsonSerializer.Deserialize<BedrockGeometryBones>(doc.RootElement.GetRawText());
            }
        }

        return null;
    }

    public static string? ResolveGeometry(string skinPath)
    {
        var dir = Path.GetDirectoryName(skinPath);
        if (string.IsNullOrEmpty(dir))
            return null;

        var skinsJsonPath = Path.Combine(dir, "skins.json");
        var skinInfo = FindSkinByTexture(skinsJsonPath, skinPath);
        if (skinInfo == null || string.IsNullOrEmpty(skinInfo.Geometry))
            return null;

        var geometryJsonPath = Path.Combine(dir, "geometry.json");
        var geometry = LoadGeometry(geometryJsonPath, skinInfo.Geometry);
        if (geometry == null)
            return null;

        return skinInfo.Geometry;
    }

    public static BedrockGeometryBones? LoadGeometryFromSkinPath(string skinPath)
    {
        var dir = Path.GetDirectoryName(skinPath);
        if (string.IsNullOrEmpty(dir))
            return null;

        var skinsJsonPath = Path.Combine(dir, "skins.json");
        var skinInfo = FindSkinByTexture(skinsJsonPath, skinPath);
        if (skinInfo == null || string.IsNullOrEmpty(skinInfo.Geometry))
            return null;

        var geometryJsonPath = Path.Combine(dir, "geometry.json");
        return LoadGeometry(geometryJsonPath, skinInfo.Geometry);
    }
}
