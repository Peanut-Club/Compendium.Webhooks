using System.Text.Json.Serialization;

namespace Compendium.Webhooks.Discord;

public struct DiscordEmbedAuthor
{
	[JsonPropertyName("name")]
	public string Name { get; set; }

	[JsonPropertyName("url")]
	public string Url { get; set; }

	[JsonPropertyName("icon_url")]
	public string IconUrl { get; set; }

	[JsonPropertyName("proxy_icon_url")]
	public string ProxyIconUrl { get; set; }

	public DiscordEmbedAuthor WithName(string name)
	{
		Name = name;
		return this;
	}

	public DiscordEmbedAuthor WithUrl(string url)
	{
		Url = url;
		return this;
	}

	public DiscordEmbedAuthor WithIcon(string iconUrl, string proxyIconUrl = null)
	{
		IconUrl = iconUrl;
		ProxyIconUrl = proxyIconUrl;
		return this;
	}

	public static DiscordEmbedAuthor Create(string name, string url = null, string iconUrl = null, string proxyUrl = null)
	{
		DiscordEmbedAuthor result = default(DiscordEmbedAuthor);
		result.Name = name;
		result.Url = url;
		result.IconUrl = iconUrl;
		result.ProxyIconUrl = proxyUrl;
		return result;
	}
}
