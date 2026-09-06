using Iridium.Resources.CurseForge;
using CurseForgeJsonContext = Iridium.Resources.CurseForge.CurseForgeJsonContext;

namespace Iridium.Resources.CurseForge;

public partial class CurseForgeClient {
    public async Task<CurseForgeFingerprintResult> GetFilesByFingerprintsAsync(IEnumerable<uint> fingerprints, CancellationToken cancellationToken = default) {
        var values = fingerprints.Distinct().ToArray();
        if (values.Length == 0)
            return new CurseForgeFingerprintResult { Data = new CurseForgeFingerprintData() };

        var url = BaseUrl.AppendPathSegments("fingerprints", MinecraftGameId);
        
        return await PostJsonAsync(url, 
            new CurseForgeFingerprintRequest { Fingerprints = values }, 
            CurseForgeJsonContext.Default.CurseForgeFingerprintRequest, 
            CurseForgeJsonContext.Default.CurseForgeFingerprintResult, cancellationToken) ?? new CurseForgeFingerprintResult();
    }
}
