using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Compendium.Webhooks.Discord;

public struct DiscordEmbed
{
	[JsonPropertyName("title")]
	public string Title { get; set; }

	[JsonPropertyName("description")]
	public string Description { get; set; }

	[JsonPropertyName("url")]
	public string Url { get; set; }

	[JsonPropertyName("color")]
	public int? Color { get; set; }

	[JsonPropertyName("author")]
	public DiscordEmbedAuthor? Author { get; set; }

	[JsonPropertyName("footer")]
	public DiscordEmbedFooter? Footer { get; set; }

	[JsonPropertyName("image")]
	public DiscordEmbedImage? Image { get; set; }

	[JsonPropertyName("thumbnail")]
	public DiscordEmbedThumbnail? Thumbnail { get; set; }

	[JsonPropertyName("video")]
	public DiscordEmbedVideo? Video { get; set; }

	[JsonPropertyName("fields")]
	public DiscordEmbedField[] Fields { get; set; }

	public DiscordEmbedColor? GetColor()
	{
		if (Color.HasValue)
		{
			return new DiscordEmbedColor(Color.Value);
		}
		return null;
	}

	public DiscordEmbed WithAuthor(DiscordEmbedAuthor? author)
	{
		Author = author;
		return this;
	}

	public DiscordEmbed WithAuthor(string name, string url = null, string iconUrl = null, string proxyIconUrl = null)
	{
		Author = DiscordEmbedAuthor.Create(name, url, iconUrl, proxyIconUrl);
		return this;
	}

	public DiscordEmbed WithTitle(string title, string url = null)
	{
		Title = title;
		if (url != null)
		{
			Url = url;
		}
		return this;
	}

	public DiscordEmbed WithColor(Color color)
	{
		Color = new DiscordEmbedColor(color).ToHexRgb();
		return this;
	}

	public DiscordEmbed WithDescription(string description)
	{
		Description = description;
		return this;
	}

	public DiscordEmbed WithFooter(DiscordEmbedFooter? footer)
	{
		Footer = footer;
		return this;
	}

	public DiscordEmbed WithFooter(string text, string iconUrl = null, string proxyIconUrl = null)
	{
		Footer = DiscordEmbedFooter.Create(text, iconUrl, proxyIconUrl);
		return this;
	}

	public DiscordEmbed WithImage(DiscordEmbedImage? image)
	{
		Image = image;
		return this;
	}

	public DiscordEmbed WithImage(string imageUrl, string proxyImageUrl = null, int? height = null, int? width = null)
	{
		Image = DiscordEmbedImage.Create(imageUrl, proxyImageUrl, height, width);
		return this;
	}

	public DiscordEmbed WithThumbnail(DiscordEmbedThumbnail? thumbnail)
	{
		Thumbnail = thumbnail;
		return this;
	}

	public DiscordEmbed WithThumbnail(string imageUrl, string proxyImageUrl = null, int? height = null, int? width = null)
	{
		Thumbnail = DiscordEmbedThumbnail.Create(imageUrl, proxyImageUrl, height, width);
		return this;
	}

	public DiscordEmbed WithVideo(DiscordEmbedVideo? video)
	{
		Video = video;
		return this;
	}

	public DiscordEmbed WithVideo(string videoUrl, string proxyVideoUrl = null, int? height = null, int? width = null)
	{
		Video = DiscordEmbedVideo.Create(videoUrl, proxyVideoUrl, height, width);
		return this;
	}

	public DiscordEmbed WithFields(params DiscordEmbedField[] fields)
	{
		if (Fields != null)
		{
			List<DiscordEmbedField> list = new List<DiscordEmbedField>(Fields);
			list.AddRange(fields);
			Fields = list.ToArray();
			return this;
		}
		Fields = fields;
		return this;
	}

	public DiscordEmbed WithField(string name, object value, bool inline = true)
	{
		return WithFields(DiscordEmbedField.Create(name, value, inline));
	}

	public DiscordEmbed ResetFields()
	{
		Fields = null;
		return this;
	}
}
