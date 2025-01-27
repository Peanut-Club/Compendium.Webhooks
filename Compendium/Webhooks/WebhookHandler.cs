using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Timers;
using Compendium.Attributes;
using Compendium.Enums;
using Compendium.Features;
using Compendium.Guard;
using Compendium.PlayerData;
using Compendium.Warns;
using Compendium.Webhooks.Discord;
using GameCore;
using helpers;
using helpers.Attributes;
using helpers.Configuration;
using helpers.Extensions;
using helpers.Pooling.Pools;
using helpers.Time;
using MapGeneration.Distributors;
using Mirror;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp939;
using PlayerStatsSystem;
using PluginAPI.Core;
using PluginAPI.Loader;
using Respawning;
using UnityEngine;

namespace Compendium.Webhooks;

public static class WebhookHandler
{
	private static readonly List<WebhookData> _webhooks;

	private static List<string> _plugins;

	private static Timer _infoTimer;

	private static bool _warnReg;

	private static string _ip;

	private static AlphaWarheadOutsitePanel _outsite;

	private static Scp079Generator[] _gens;

	public static IReadOnlyList<WebhookData> Webhooks => _webhooks;

	[Config(Name = "Webhooks", Description = "A list of webhooks.")]
	public static Dictionary<WebhookLog, List<WebhookConfigData>> WebhookList { get; set; }

	[Config(Name = "Info Data", Description = "The data to include in Info webhooks.")]
	public static List<WebhookInfoData> InfoData { get; set; }

	[Config(Name = "Event Log", Description = "A list of webhooks with their respective in-game events.")]
	public static Dictionary<string, List<WebhookEventLog>> EventLog { get; set; }

	[Config(Name = "Private Bans Include IP", Description = "Whether or not to show user's IP address in a private ban log.")]
	public static bool PrivateBansIncludeIp { get; set; }

	[Config(Name = "Reports Include IP", Description = "Whether or not to show user's IP address in reports.")]
	public static bool ReportsIncludeIp { get; set; }

	[Config(Name = "Cheater Reports Include IP", Description = "Whether or not to show user's IP address in cheater reports.")]
	public static bool CheaterReportsIncludeIp { get; set; }

	[Config(Name = "Send Time", Description = "The amount of milliseconds between each queue pull.")]
	public static int SendTime { get; set; }

	[Config(Name = "Info Time", Description = "The amount of milliseconds between each info pull.")]
	public static int InfoTime { get; set; }

	[Config(Name = "Announce Reports", Description = "Whether or not to announce reports in-game.")]
	public static bool AnnounceReportsInGame { get; set; }

	[Load]
	[Reload]
	public static void Reload()
	{
		if (!_warnReg)
		{
			WarnSystem.OnWarnIssued.Register(new Action<WarnData, PlayerDataRecord, PlayerDataRecord>(OnWarned));
			_warnReg = true;
		}
		_webhooks.Clear();
		foreach (KeyValuePair<WebhookLog, List<WebhookConfigData>> webhook in WebhookList)
		{
			foreach (WebhookConfigData item in webhook.Value)
			{
				if (!string.IsNullOrWhiteSpace(item.Url) && item.Url != "empty")
				{
					_webhooks.Add(new WebhookData(webhook.Key, item.Url, item.Content));
				}
				else
				{
					FLog.Warn($"Invalid webhook URL: {item.Url} ({webhook.Key})");
				}
			}
		}
		foreach (KeyValuePair<string, List<WebhookEventLog>> item2 in EventLog)
		{
			if (!string.IsNullOrWhiteSpace(item2.Key) && item2.Key != "empty")
			{
				_webhooks.Add(new WebhookEvent(item2.Key, item2.Value));
			}
			else
			{
				FLog.Warn("Invalid event webhook URL: " + item2.Key);
			}
		}
		if (_webhooks.Any((WebhookData w) => w.Type == WebhookLog.Info))
		{
			if (_infoTimer == null)
			{
				_infoTimer = new Timer(InfoTime);
				_infoTimer.Elapsed += OnElapsed;
				_infoTimer.Enabled = true;
				_infoTimer.Start();
				FLog.Info("Started the info timer.");
			}
		}
		else if (_infoTimer != null)
		{
			_infoTimer.Elapsed -= OnElapsed;
			_infoTimer.Enabled = false;
			_infoTimer.Stop();
			_infoTimer.Dispose();
			_infoTimer = null;
			FLog.Info("Stopped the info timer.");
		}
		FLog.Info($"Loaded {_webhooks.Count} webhooks.");
	}

	public static string GetDamageName(DamageHandlerBase damageHandler)
	{
		if (damageHandler is WarheadDamageHandler)
		{
			return "Alpha Warhead";
		}
		if (damageHandler is Scp018DamageHandler)
		{
			return "SCP-018";
		}
		if (damageHandler is Scp049DamageHandler)
		{
			return "SCP-049";
		}
		if (damageHandler is Scp096DamageHandler)
		{
			return "SCP-096";
		}
		if (damageHandler is Scp939DamageHandler)
		{
			return "SCP-939";
		}
		if (damageHandler is DisruptorDamageHandler)
		{
			return "3-X Particle Disruptor";
		}
		if (damageHandler is JailbirdDamageHandler)
		{
			return "Jailbird";
		}
		if (damageHandler is MicroHidDamageHandler)
		{
			return "Micro-HID";
		}
		if (damageHandler is RecontainmentDamageHandler)
		{
			return "Recontainment";
		}
		if (damageHandler is ExplosionDamageHandler)
		{
			return "Grenade";
		}
		if (damageHandler is FirearmDamageHandler firearmDamageHandler)
		{
			return firearmDamageHandler.WeaponType.ToString().SpaceByPascalCase();
		}
		if (damageHandler is UniversalDamageHandler universalDamageHandler && DeathTranslations.TranslationsById.TryGetValue(universalDamageHandler.TranslationId, out var value))
		{
			return value.LogLabel;
		}
		if (damageHandler is AttackerDamageHandler attackerDamageHandler)
		{
			if (attackerDamageHandler.Attacker.Hub != null)
			{
				if (attackerDamageHandler.Attacker.Hub.IsSCP())
				{
					return attackerDamageHandler.Attacker.Role.ToString().SpaceByPascalCase();
				}
				if (attackerDamageHandler.Attacker.Hub.inventory.CurInstance != null)
				{
					return attackerDamageHandler.Attacker.Hub.inventory.CurInstance.ItemTypeId.ToString().SpaceByPascalCase();
				}
			}
			return attackerDamageHandler.Attacker.Role.ToString().SpaceByPascalCase();
		}
		if (DamageHandlers.ConstructorsById.TryGetFirst((KeyValuePair<byte, Func<DamageHandlerBase>> p) => p.Value.Method.ReturnType == damageHandler.GetType(), out var value2))
		{
			int stableHashCode = value2.Value.GetType().FullName.GetStableHashCode();
			if (DamageHandlers.IdsByTypeHash.TryGetValue(stableHashCode, out var value3) && DeathTranslations.TranslationsById.TryGetValue(value3, out value))
			{
				return value.LogLabel;
			}
		}
		return damageHandler.GetType().Name;
	}

	[RoundStateChanged(new RoundState[] { RoundState.InProgress })]
	private static void OnRoundStarted()
	{
		foreach (WebhookData webhook in _webhooks)
		{
			if (webhook is WebhookEvent webhookEvent && webhookEvent.AllowedEvents != null && webhookEvent.AllowedEvents.Contains(WebhookEventLog.RoundStarted))
			{
				webhookEvent.Event("⚡ The round has started!");
			}
		}
		_gens = UnityEngine.Object.FindObjectsOfType<Scp079Generator>();
		GameObject gameObject = GameObject.Find("OutsitePanelScript");
		if ((object)gameObject != null)
		{
			_outsite = gameObject.GetComponentInParent<AlphaWarheadOutsitePanel>();
		}
	}

	[RoundStateChanged(new RoundState[] { RoundState.WaitingForPlayers })]
	private static void OnWaiting()
	{
		foreach (WebhookData webhook in _webhooks)
		{
			if (webhook is WebhookEvent webhookEvent && webhookEvent.AllowedEvents != null && webhookEvent.AllowedEvents.Contains(WebhookEventLog.RoundWaiting))
			{
				webhookEvent.Event("⏳ Waiting for players ..");
			}
		}
	}

	[RoundStateChanged(new RoundState[] { RoundState.Ending })]
	private static void OnRoundEnded()
	{
		_gens = null;
		foreach (WebhookData webhook in _webhooks)
		{
			if (webhook is WebhookEvent webhookEvent && webhookEvent.AllowedEvents != null && webhookEvent.AllowedEvents.Contains(WebhookEventLog.RoundEnded))
			{
				webhookEvent.Event("\ud83d\uded1 The round has ended!");
			}
		}
	}

	private static void OnWarned(WarnData warn, PlayerDataRecord issuer, PlayerDataRecord target)
	{
		if (!_webhooks.Any((WebhookData w) => w.Type == WebhookLog.Warn))
		{
			return;
		}
		Compendium.Webhooks.Discord.DiscordEmbed value = default(Compendium.Webhooks.Discord.DiscordEmbed);
		value.WithColor(System.Drawing.Color.Red);
		value.WithTitle("⚠\ufe0f " + World.CurrentClearOrAlternativeServerName);
		value.WithField("\ud83d\udd17 Udělil", "**" + issuer.NameTracking.LastValue + "** *(" + issuer.UserId.Split(new char[1] { '@' })[0] + ")*", inline: false);
		value.WithField("\ud83d\udd17 Hráč", "**" + target.NameTracking.LastValue + "** *(" + target.UserId.Split(new char[1] { '@' })[0] + " | " + target.Ip + ")*", inline: false);
		value.WithField("❓ Důvod", warn.Reason, inline: false);
		value.WithFooter("\ud83d\udcdd " + warn.Id + " | \ud83d\udd52 " + warn.IssuedAt.ToString("F"));
		foreach (WebhookData webhook in _webhooks)
		{
			if (webhook.Type == WebhookLog.Warn)
			{
				webhook.Send(null, value);
			}
		}
	}

	private static void OnElapsed(object sender, ElapsedEventArgs e)
	{
		if (!InfoData.Any())
		{
			return;
		}
		Compendium.Webhooks.Discord.DiscordEmbed value = default(Compendium.Webhooks.Discord.DiscordEmbed);
		value.WithTitle("ℹ\ufe0f " + World.CurrentClearOrAlternativeServerName);
		value.WithFooter("\ud83d\udd52 Poslední aktualizace: " + TimeUtils.LocalStringFull + " (interval: " + TimeSpan.FromMilliseconds(InfoTime).UserFriendlySpan() + ")");
		if (InfoData.Contains(WebhookInfoData.ServerAddress))
		{
			if (_ip == null)
			{
				_ip = string.Format("**{0}:{1}**", ConfigFile.ServerConfig.GetString("server_ip", "auto"), ServerStatic.ServerPort);
			}
			value.WithField("\ud83c\udf10 IP Adresa", _ip, inline: false);
		}
		if (InfoData.Contains(WebhookInfoData.RoundStatus))
		{
			value.WithField("\ud83d\udd52 Status kola", GetRoundStatus(), inline: false);
		}
		if (InfoData.Contains(WebhookInfoData.WarheadStatus))
		{
			value.WithField("☣\ufe0f Status hlavice Alpha", GetWarheadStatus(), inline: false);
		}
		if (InfoData.Contains(WebhookInfoData.GeneratorStatus))
		{
			value.WithField("⚡ Status generátorů", GetGeneratorStatus(), inline: false);
		}
		if (InfoData.Contains(WebhookInfoData.RespawnTime))
		{
			value.WithField("\ud83d\udd52 Čas do respawnu", GetRespawnStatus(), inline: false);
		}
		if (InfoData.Contains(WebhookInfoData.TotalPlayers))
		{
			value.WithField($"\ud83e\uddd1\ud83c\udffb\u200d\ud83e\udd1d\u200d\ud83e\uddd1\ud83c\udffb Počet hráčů ({Hub.Count})", GetPlayerList(), inline: false);
		}
		if (InfoData.Contains(WebhookInfoData.PluginList))
		{
			if (!_plugins.Any())
			{
				AssemblyLoader.InstalledPlugins.ForEach(delegate(PluginHandler pl)
				{
					_plugins.Add("*[" + ((pl.PluginName == "Compendium API" || pl.PluginName == "BetterCommands") ? "CUSTOM" : "NW API") + "]* **" + pl.PluginName + "**");
				});
				FeatureManager.LoadedFeatures.ForEach(delegate(IFeature f)
				{
					if (f.IsEnabled)
					{
						_plugins.Add("*[CUSTOM]* **" + f.Name + "**");
					}
				});
				_plugins = _plugins.OrderBy((string p) => p).ToList();
			}
			value.WithField($"\ud83d\udcdd Seznam pluginů ({_plugins.Count})", GetPluginList(), inline: false);
		}
		foreach (WebhookData webhook in _webhooks)
		{
			if (webhook.Type == WebhookLog.Info)
			{
				webhook.Send(null, value);
			}
		}
	}

	private static string GetPluginList()
	{
		StringBuilder sb = StringBuilderPool.Pool.Get();
		_plugins.For(delegate(int i, string p)
		{
			sb.AppendLine("- " + p);
		});
		return StringBuilderPool.Pool.PushReturn(sb);
	}

	private static string GetPlayerList()
	{
		StringBuilder stringBuilder = StringBuilderPool.Pool.Get();
		if (InfoData.Contains(WebhookInfoData.AliveCi))
		{
			stringBuilder.AppendLine($"- \ud83d\udfe2 **Chaos Insurgency**: {Hub.Hubs.Count((ReferenceHub h) => h.GetTeam() == Team.ChaosInsurgency)}");
		}
		if (InfoData.Contains(WebhookInfoData.AliveNtf))
		{
			stringBuilder.AppendLine($"- \ud83d\udd35 **Nine-Tailed Fox**: {Hub.Hubs.Count((ReferenceHub h) => h.GetTeam() == Team.FoundationForces && HubRoleExtensions.RoleId(h) != RoleTypeId.FacilityGuard)}");
		}
		if (InfoData.Contains(WebhookInfoData.AliveScps))
		{
			stringBuilder.AppendLine($"- \ud83d\udd34 **SCP**: {Hub.Hubs.Count((ReferenceHub h) => h.GetTeam() == Team.SCPs)}");
		}
		if (InfoData.Contains(WebhookInfoData.AliveSpectators))
		{
			stringBuilder.AppendLine($"- \ud83d\udc80 **Diváci**: {Hub.Hubs.Count((ReferenceHub h) => h.GetRoleId() == RoleTypeId.Spectator || h.GetRoleId() == RoleTypeId.Overwatch)}");
		}
		if (InfoData.Contains(WebhookInfoData.TotalStaff))
		{
			stringBuilder.AppendLine($"- \ud83e\uddf0 **Administrátoři**: {Hub.Hubs.Count((ReferenceHub h) => h.serverRoles.RemoteAdmin)}");
		}
		return StringBuilderPool.Pool.PushReturn(stringBuilder);
	}

	private static string GetRespawnStatus() {
        return "Nelze zjistit!";
		/*
        if (RespawnManager.Singleton == null || !RoundHelper.IsStarted)
		{
			return "Nelze zjistit!";
		}
		if (InfoData.Contains(WebhookInfoData.RespawnTeam))
		{
			if ((int)RespawnManager.Singleton._curSequence == 1 && RespawnManager.Singleton.NextKnownTeam != 0)
			{
				return ((RespawnManager.Singleton.NextKnownTeam == SpawnableTeamType.NineTailedFox) ? "\ud83d\udc6e" : "\ud83d\ude94") + " Tým **" + ((RespawnManager.Singleton.NextKnownTeam == SpawnableTeamType.NineTailedFox) ? "Nine-Tailed Fox" : "Chaos Insurgency") + "** se spawne za **" + TimeSpan.FromSeconds(RespawnManager.Singleton.TimeTillRespawn).UserFriendlySpan() + "**";
			}
			if ((int)RespawnManager.Singleton._curSequence == 3)
			{
				return ((RespawnManager.Singleton.NextKnownTeam == SpawnableTeamType.NineTailedFox) ? "\ud83d\udc6e" : "\ud83d\ude94") + " Probíhá spawn týmu **" + ((RespawnManager.Singleton.NextKnownTeam == SpawnableTeamType.NineTailedFox) ? "Nine-Tailed Fox" : "Chaos Insurgency") + "**";
			}
		}
		return "⏳ Zbývá **" + TimeSpan.FromSeconds(RespawnManager.Singleton.TimeTillRespawn).UserFriendlySpan() + "** do respawnu.";
		*/
	}

	private static string GetGeneratorStatus()
	{
		if (_gens == null)
		{
			return "Neznámý počet.";
		}
		int num = _gens.Count((Scp079Generator g) => g.HasFlag(g.Network_flags, Scp079Generator.GeneratorFlags.Engaged));
		int num2 = _gens.Length;
		return $"**{num} / {num2}**";
	}

	private static string GetWarheadStatus()
	{
		if (AlphaWarheadController.Detonated)
		{
			return "\ud83d\udca5 **Detonována**";
		}
		if (AlphaWarheadController.InProgress)
		{
			return $"⚠\ufe0f **Probíhá** *({Mathf.CeilToInt(AlphaWarheadController.TimeUntilDetonation)} sekund do detonace)*";
		}
		if (AlphaWarheadOutsitePanel.nukeside != null && AlphaWarheadOutsitePanel.nukeside.Networkenabled && _outsite != null && _outsite.NetworkkeycardEntered)
		{
			return "✅ **Připravena** k detonaci";
		}
		if (AlphaWarheadOutsitePanel.nukeside != null && AlphaWarheadOutsitePanel.nukeside.Networkenabled)
		{
			return "✅ **Páčka povolena, karta není vložena**";
		}
		if (_outsite != null && _outsite.NetworkkeycardEntered)
		{
			return "✅ **Karta vložena, páčka není povolena**";
		}
		return "❎ **Není vložena karta ani povolena páčka**";
	}

	private static string GetRoundStatus()
	{
		return RoundHelper.State switch
		{
			RoundState.Ending => "⭕ **Konec**", 
			RoundState.InProgress => "\ud83d\udfe2 **Probíhá** *(" + RoundStart.RoundStartTimer.Elapsed.UserFriendlySpan() + ")*", 
			RoundState.Restarting => "\ud83d\udfe2 **Restartuje se** ..", 
			RoundState.WaitingForPlayers => "⏳ **Čeká se na hráče** ..", 
			_ => "Neznámý status kola!", 
		};
	}

	static WebhookHandler()
	{
		_webhooks = new List<WebhookData>();
		_plugins = new List<string>();
		WebhookList = new Dictionary<WebhookLog, List<WebhookConfigData>>
		{
			[WebhookLog.Console] = new List<WebhookConfigData>
			{
				new WebhookConfigData()
			},
			[WebhookLog.Server] = new List<WebhookConfigData>
			{
				new WebhookConfigData()
			},
			[WebhookLog.Report] = new List<WebhookConfigData>
			{
				new WebhookConfigData()
			},
			[WebhookLog.CheaterReport] = new List<WebhookConfigData>
			{
				new WebhookConfigData()
			},
			[WebhookLog.BanPrivate] = new List<WebhookConfigData>
			{
				new WebhookConfigData()
			},
			[WebhookLog.BanPublic] = new List<WebhookConfigData>
			{
				new WebhookConfigData()
			}
		};
		InfoData = new List<WebhookInfoData>
		{
			WebhookInfoData.RoundStatus,
			WebhookInfoData.RespawnTime,
			WebhookInfoData.RespawnTeam,
			WebhookInfoData.TotalPlayers,
			WebhookInfoData.TotalStaff,
			WebhookInfoData.PluginList,
			WebhookInfoData.AliveCi,
			WebhookInfoData.AliveNtf,
			WebhookInfoData.AliveScps,
			WebhookInfoData.AliveSpectators,
			WebhookInfoData.GeneratorStatus,
			WebhookInfoData.WarheadStatus,
			WebhookInfoData.ServerAddress
		};
		EventLog = new Dictionary<string, List<WebhookEventLog>> { ["empty"] = new List<WebhookEventLog>
		{
			WebhookEventLog.GrenadeExploded,
			WebhookEventLog.GrenadeThrown,
			WebhookEventLog.PlayerCuff,
			WebhookEventLog.PlayerDamage,
			WebhookEventLog.PlayerSelfDamage,
			WebhookEventLog.PlayerSuicide,
			WebhookEventLog.PlayerAuth,
			WebhookEventLog.PlayerFriendlyDamage,
			WebhookEventLog.PlayerFriendlyKill,
			WebhookEventLog.PlayerJoined,
			WebhookEventLog.PlayerKill,
			WebhookEventLog.PlayerLeft,
			WebhookEventLog.PlayerUncuff,
			WebhookEventLog.RoundEnded,
			WebhookEventLog.RoundStarted,
			WebhookEventLog.RoundWaiting
		} };
		PrivateBansIncludeIp = true;
		ReportsIncludeIp = true;
		CheaterReportsIncludeIp = true;
		SendTime = 500;
		InfoTime = 1000;
		AnnounceReportsInGame = true;
		ServerGuard.OnKicking += OnRejecting;
		ServerGuard.OnRejecting += OnRejectingAuth;
	}

	public static void OnRejectingAuth(string ip, string id, ServerGuardReason reason)
	{
		if (PlayerDataRecorder.TryQuery(ip, queryNick: false, out var record))
		{
			SendGuard("Authentification Rejection", id, ip, record.NameTracking.LastValue, reason);
		}
		else
		{
			SendGuard("Authentification Rejection", id, ip, null, reason);
		}
	}

	internal static void SendGuard(string type, string id, string ip, string name, ServerGuardReason reason)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			name = "Unknown name";
		}
		Compendium.Webhooks.Discord.DiscordEmbed value = default(Compendium.Webhooks.Discord.DiscordEmbed);
		value.WithColor(System.Drawing.Color.Red);
		value.WithTitle("\ud83d\udeab | " + type);
		value.WithField("\ud83c\udf10 | ID", id, inline: false);
		value.WithField("\ud83c\udf10 | IP", ip, inline: false);
		value.WithField("\ud83c\udf10 | Name", name, inline: false);
		value.WithField("❔ | Reason", reason, inline: false);
		foreach (WebhookData webhook in Webhooks)
		{
			if (webhook.Type == WebhookLog.ServerGuard)
			{
				webhook.Send(null, value);
			}
		}
	}

	public static void OnRejecting(IServerGuardProcessor _, ReferenceHub player, ServerGuardReason reason)
	{
		SendGuard("Connection Rejection", HubDataExtensions.UserId(player), HubDataExtensions.Ip(player), HubDataExtensions.Nick(player), reason);
	}
}
