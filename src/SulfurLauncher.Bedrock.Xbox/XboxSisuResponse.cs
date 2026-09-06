using System.Text.Json.Serialization;

namespace SulfurLauncher.Bedrock.Xbox;

public sealed class XboxSisuResponse
{
	[JsonPropertyName("AuthorizationToken")]
	public XboxTokenResponse AuthorizationToken { get; init; } = new XboxTokenResponse();
}
