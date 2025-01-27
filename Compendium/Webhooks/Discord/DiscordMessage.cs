using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using helpers.Json;

namespace Compendium.Webhooks.Discord;

public struct DiscordMessage
{
	[JsonPropertyName("content")]
	public string Content { get; set; }

	[JsonPropertyName("tts")]
	public bool IsTextToSpeech { get; set; }

	[JsonPropertyName("embeds")]
	public DiscordEmbed[] Embeds { get; set; }

	[JsonPropertyName("allowed_mentions")]
	public DiscordMessageAllowedMentions? Mentions { get; set; }

	public DiscordMessage WithContent(string content, bool isTts = false)
	{
		Content = content;
		IsTextToSpeech = isTts;
		return this;
	}

	public DiscordMessage WithTextToSpeech(bool isTts = true)
	{
		IsTextToSpeech = isTts;
		return this;
	}

	public DiscordMessage WithEmbeds(params DiscordEmbed[] embeds)
	{
		if (Embeds != null && Embeds.Any())
		{
			List<DiscordEmbed> list = new List<DiscordEmbed>(Embeds);
			list.AddRange(embeds);
			Embeds = list.ToArray();
			return this;
		}
		Embeds = embeds;
		return this;
	}

	public DiscordMessage WithMentions(DiscordMessageAllowedMentions? discordMessageAllowedMentions)
	{
		Mentions = discordMessageAllowedMentions;
		return this;
	}

	public override string ToString()
	{
		if (!string.IsNullOrWhiteSpace(Content) && Content.Length >= 1900)
		{
			Content = Content.Substring(0, 1900) + " ...";
		}
		if (Embeds != null && Embeds.Any())
		{
			DiscordEmbed[] embeds = Embeds;
			for (int i = 0; i < embeds.Length; i++)
			{
				DiscordEmbed discordEmbed = embeds[i];
				if (!string.IsNullOrWhiteSpace(discordEmbed.Description) && discordEmbed.Description.Length >= 1900)
				{
					string description = discordEmbed.Description;
					description = description.Substring(0, 1900) + " ...";
					discordEmbed.WithDescription(description);
				}
			}
		}
		return JsonSerializer.Serialize(this);
	}

	public static DiscordMessage FromJson(string json)
	{
		return json.FromJson<DiscordMessage>();
	}
}
