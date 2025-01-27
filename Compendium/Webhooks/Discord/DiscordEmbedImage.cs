using System.Text.Json.Serialization;

namespace Compendium.Webhooks.Discord;

public struct DiscordEmbedImage
{
	[JsonPropertyName("url")]
	public string Url { get; set; }

	[JsonPropertyName("proxy_url")]
	public string ProxyUrl { get; set; }

	[JsonPropertyName("height")]
	public int? Height { get; set; }

	[JsonPropertyName("width")]
	public int? Width { get; set; }

	public static DiscordEmbedImage Create(string url, string proxy = null, int? height = null, int? width = null)
	{
		DiscordEmbedImage result = default(DiscordEmbedImage);
		result.Url = url;
		result.ProxyUrl = proxy;
		result.Height = height;
		result.Width = width;
		return result;
	}
}
