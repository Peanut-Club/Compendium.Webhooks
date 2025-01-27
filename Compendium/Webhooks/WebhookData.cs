using System;
using System.Collections.Concurrent;
using Compendium.Features;
using Compendium.Webhooks.Discord;

namespace Compendium.Webhooks;

public class WebhookData
{
	public WebhookLog Type { get; }

	public Uri Url { get; }

	public string Content { get; }

	public ConcurrentQueue<DiscordMessage> Queue { get; } = new ConcurrentQueue<DiscordMessage>();


	public DateTime? Next { get; set; }

	public WebhookData(WebhookLog type, string url, string content = null)
	{
		Type = type;
		Content = content;
		if (string.IsNullOrWhiteSpace(Content) || Content == "empty")
		{
			Content = null;
		}
		if (Uri.TryCreate(url, UriKind.Absolute, out var result))
		{
			Url = result;
			return;
		}
		Url = null;
		FLog.Warn("Failed to parse URL: " + url);
	}

	public void Send(string content = null, Compendium.Webhooks.Discord.DiscordEmbed? embed = null)
	{
		if ((object)Url == null)
		{
			FLog.Warn("Attempted to send message on an invalid webhook!");
			return;
		}
		if (content == null && Content != null)
		{
			content = Content;
		}
		if (content == null && !embed.HasValue)
		{
			FLog.Warn("Attempted to send an empty message!");
			return;
		}
		DiscordMessage item = default(DiscordMessage);
		if (!string.IsNullOrWhiteSpace(content))
		{
			item.WithContent(content);
		}
		if (embed.HasValue)
		{
			item.WithEmbeds(embed.Value);
		}
		Queue.Enqueue(item);
	}
}
