using System.Text.Json.Serialization;

namespace Compendium.Webhooks.Discord;

public struct DiscordEmbedFooter
{
	[JsonPropertyName("text")]
	public string Text { get; set; }

	[JsonPropertyName("icon_url")]
	public string IconUrl { get; set; }

	[JsonPropertyName("proxy_icon_url")]
	public string ProxyIconUrl { get; set; }

	public DiscordEmbedFooter WithText(string text)
	{
		Text = text;
		return this;
	}

	public DiscordEmbedFooter WithIcon(string iconUrl, string proxyIconUrl = null)
	{
		IconUrl = iconUrl;
		ProxyIconUrl = proxyIconUrl;
		return this;
	}

	public static DiscordEmbedFooter Create(string text, string icon = null, string proxyIcon = null)
	{
		DiscordEmbedFooter result = default(DiscordEmbedFooter);
		result.Text = text;
		result.IconUrl = icon;
		result.ProxyIconUrl = proxyIcon;
		return result;
	}
}
