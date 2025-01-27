using System.Text.Json.Serialization;

namespace Compendium.Webhooks.Discord;

public struct DiscordEmbedField
{
	[JsonPropertyName("name")]
	public string Name { get; set; }

	[JsonPropertyName("value")]
	public string Value { get; set; }

	[JsonPropertyName("inline")]
	public bool? IsInline { get; set; }

	public DiscordEmbedField WithName(string name)
	{
		Name = name;
		return this;
	}

	public DiscordEmbedField WithValue(object value, bool inline = true)
	{
		Value = value?.ToString();
		IsInline = inline;
		return this;
	}

	public static DiscordEmbedField Create(string name, object value, bool isInline = false)
	{
		DiscordEmbedField result = default(DiscordEmbedField);
		result.Name = name;
		result.Value = value?.ToString();
		result.IsInline = isInline;
		return result;
	}
}
