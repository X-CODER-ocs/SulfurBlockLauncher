using Iridium.Enums;
using Iridium.Extensions;
using Iridium.Models.Resources;
using CurseForgeJsonContext = Iridium.Resources.CurseForge.CurseForgeJsonContext;

namespace Iridium.Resources.CurseForge;

public partial class CurseForgeClient {
    public async Task<IReadOnlyList<string>> GetGameVersionsAsync(CancellationToken cancellationToken = default) {
        var url = BaseUrl.AppendPathSegments("games", MinecraftGameId, "versions");
        var response = await GetJsonAsync(url,
            CurseForgeJsonContext.Default.CurseForgeResponseListCurseForgeGameVersion, cancellationToken);
        
        return response?.Data?
            .Select(version => version.VersionString)
            .Where(version => version is not null)
            .Cast<string>()
            .Distinct()
            .ToArray() ?? [];
    }
    
    public async Task<IReadOnlyList<ResourceCategory>> GetCategoriesAsync(ResourceType type, CancellationToken cancellationToken = default) {
        var classId = type.ToCurseForgeClassId();
        var url = BaseUrl.AppendPathSegments("categories").SetQueryParam("gameId", MinecraftGameId);
        var response = await GetJsonAsync(url,
            CurseForgeJsonContext.Default.CurseForgeResponseListCurseForgeCategory, cancellationToken);
        
        return response?.Data?
            .Where(category => classId is null || category.ClassId == classId || category.Id == classId)
            .Select(category => category.ToResourceCategory(type)).ToArray() ?? [];
    }
}
