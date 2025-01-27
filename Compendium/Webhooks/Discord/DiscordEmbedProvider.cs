using System.Text.Json.Serialization;

namespace Compendium.Webhooks.Discord;

public struct DiscordEmbedProvider
{
	[JsonPropertyName("name")]
	public string Name { get; set; }

	[JsonPropertyName("url")]
	public string Url { get; set; }

	public static DiscordEmbedProvider Create(string name, string url)
	{
		DiscordEmbedProvider result = default(DiscordEmbedProvider);
		result.Name = name;
		result.Url = url;
		return result;
	}
}
