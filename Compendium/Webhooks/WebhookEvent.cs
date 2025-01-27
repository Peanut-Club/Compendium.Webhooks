using System.Collections.Generic;
using Compendium.Webhooks.Discord;
using helpers.Time;

namespace Compendium.Webhooks;

public class WebhookEvent : WebhookData
{
	public static string Timestamp => TimeUtils.LocalTime.ToString("T");

	public IReadOnlyList<WebhookEventLog> AllowedEvents { get; }

	public WebhookEvent(string url, List<WebhookEventLog> allowed)
		: base(WebhookLog.Event, url)
	{
		AllowedEvents = allowed;
	}

	public void Event(string msg)
	{
		Send("[" + Timestamp + "] " + msg);
	}

	public void Event(Compendium.Webhooks.Discord.DiscordEmbed embed)
	{
		embed.WithField("\ud83d\udd52 Time", Timestamp);
		Send(null, embed);
	}
}
