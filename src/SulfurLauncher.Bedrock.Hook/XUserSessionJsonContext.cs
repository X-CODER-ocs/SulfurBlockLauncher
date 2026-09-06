using System.Text.Json.Serialization;

namespace SulfurLauncher.Bedrock.Hook;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(XUserSessionDocument))]
internal sealed partial class XUserSessionJsonContext : JsonSerializerContext
{
}
