using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Compendium.Features;
using Compendium.Logging;
using Compendium.Updating;
using helpers.Attributes;
using helpers.Json;

namespace Compendium.Webhooks.Discord;

public static class DiscordClient
{
	private static MediaTypeHeaderValue _jsonHeader = MediaTypeHeaderValue.Parse("application/json");

	private static HttpClient _client;

	private static DateTime? _lastCheck;

	[Load]
	public static void Load()
	{
		_client = new HttpClient();
	}

	[Unload]
	public static void Unload()
	{
		_client.Dispose();
		_client = null;
	}

	[Update(Delay = 10, IsUnity = false, PauseRestarting = false, PauseWaiting = false)]
	public static void Update()
	{
		if (_lastCheck.HasValue)
		{
			if ((DateTime.Now - _lastCheck.Value).Milliseconds <= WebhookHandler.SendTime)
			{
				return;
			}
			_lastCheck = DateTime.Now;
		}
		else
		{
			_lastCheck = DateTime.Now;
		}
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook.Next.HasValue)
			{
				if (!(DateTime.Now >= webhook.Next.Value))
				{
					continue;
				}
				webhook.Next = null;
			}
			if (!webhook.Queue.TryDequeue(out var message))
			{
				continue;
			}
			Task.Run(async delegate
			{
				try
				{
					string boundary = "------------------------" + DateTime.Now.Ticks.ToString("x");
					MultipartFormDataContent multipartFormDataContent = new MultipartFormDataContent(boundary);
					StringContent stringContent = new StringContent(JsonSerializer.Serialize(message));
					stringContent.Headers.ContentType = _jsonHeader;
					multipartFormDataContent.Add(stringContent, "payload_json");
					using HttpResponseMessage httpResponseMessage = await _client.PostAsync(webhook.Url, multipartFormDataContent);
					if (!httpResponseMessage.IsSuccessStatusCode)
					{
						webhook.Next = DateTime.Now + TimeSpan.FromSeconds(2.0);
					}
					else
					{
						webhook.Next = null;
					}
				}
				catch (Exception arg)
				{
					FLog.Error($"Failed to send payload:\n{arg}", new LogParameter("payload", message.ToJson()), new LogParameter("destination", webhook.Url.ToString()));
				}
			});
		}
	}
}
