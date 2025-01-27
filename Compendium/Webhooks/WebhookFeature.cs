using System;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Compendium.Events;
using Compendium.Extensions;
using Compendium.Features;
using Compendium.PlayerData;
using Compendium.Staff;
using Compendium.Webhooks.Discord;
using helpers;
using helpers.Extensions;
using helpers.Pooling.Pools;
using helpers.Time;
using LiteNetLib;
using PlayerRoles;
using PlayerStatsSystem;
using PluginAPI.Core;
using PluginAPI.Events;
using UnityEngine;

namespace Compendium.Webhooks;

public class WebhookFeature : ConfigFeatureBase
{
	public static string[] ExceptionSeparator = new string[1] { "at" };

	public override string Name => "Webhooks";

	public override bool IsPatch => true;

	public override bool CanBeShared => false;

	public override void Load()
	{
		base.Load();
		PlayerStats.OnAnyPlayerDamaged += OnDamage;
		PlayerStats.OnAnyPlayerDied += OnDeath;
		NetworkHelper.OnDisconnecting += OnLeaving;
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs ev)
		{
			if (ev.ExceptionObject == null || !(ev.ExceptionObject is Exception ex))
			{
				return;
			}
			Compendium.Webhooks.Discord.DiscordEmbed value = default(Compendium.Webhooks.Discord.DiscordEmbed);
			Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField = default(Compendium.Webhooks.Discord.DiscordEmbedField);
			Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField2 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
			Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField3 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
			discordEmbedField2.WithName("❓ Method");
			discordEmbedField2.WithValue("```csharp\n" + (ex.StackTrace.Split(ExceptionSeparator, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Unknown") + "\n```", inline: false);
			discordEmbedField3.WithName("\ud83d\uddd2\ufe0f Message");
			discordEmbedField3.WithValue("```" + ex.Message + "```", inline: false);
			discordEmbedField.WithName("❓ Type");
			discordEmbedField.WithValue(ex.GetType().Name, inline: false);
			value.WithAuthor("⛔ Caught an unhandled exception");
			value.WithColor(System.Drawing.Color.Red);
			value.WithFields(discordEmbedField, discordEmbedField2, discordEmbedField3);
			value.WithFooter("\ud83d\udd57 " + DateTime.Now.ToString("G"));
			foreach (WebhookData webhook in WebhookHandler.Webhooks)
			{
				if (webhook.Type == WebhookLog.UnhandledExceptions)
				{
					webhook.Send(null, value);
				}
			}
		};
	}

	public static void SendEvent(string msg, WebhookEventLog eventLog)
	{
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook is WebhookEvent webhookEvent && webhookEvent.AllowedEvents != null && webhookEvent.AllowedEvents.Contains(eventLog))
			{
				webhookEvent.Event(msg);
			}
		}
	}

	public static void SendEvent(Compendium.Webhooks.Discord.DiscordEmbed msg, WebhookEventLog eventLog)
	{
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook is WebhookEvent webhookEvent && webhookEvent.AllowedEvents != null && webhookEvent.AllowedEvents.Contains(eventLog))
			{
				webhookEvent.Event(msg);
			}
		}
	}

	private static string PositionSummary(ReferenceHub target, ReferenceHub attacker)
	{
		ReferenceHub target2 = target;
		string arg = HubWorldExtensions.RoomId(target2).ToString().SpaceByPascalCase();
		IOrderedEnumerable<ReferenceHub> values = from h in Hub.Hubs
			where h.IsSCP()
			orderby UnityExtensions.DistanceSquared(h, target2)
			select h;
		StringBuilder sb = StringBuilderPool.Pool.Get();
        sb.AppendLine($"**{HubDataExtensions.Nick(target2)}**: [{HubWorldExtensions.Zone(target2)}] {arg}");
        if (attacker != null && attacker != target2) {
            var distance = Mathf.CeilToInt(Mathf.Sqrt(UnityExtensions.DistanceSquared(attacker, target2)));
            sb.AppendLine($"**{HubDataExtensions.Nick(attacker)}**: [{HubWorldExtensions.Zone(attacker)}] {HubWorldExtensions.RoomId(attacker).ToString().SpaceByPascalCase()} *({distance} units away)*\"");
		}
		values.ForEach(delegate(ReferenceHub h)
		{
			var distance = Mathf.CeilToInt(Mathf.Sqrt(UnityExtensions.DistanceSquared(h, target2)));

            sb.AppendLine($"**{HubRoleExtensions.RoleId(h).ToString().SpaceByPascalCase()}**: [{HubWorldExtensions.Zone(h)}] {HubWorldExtensions.RoomId(h)} *({distance} units away)*");
		});
		return StringBuilderPool.Pool.PushReturn(sb);
	}

	[Event]
	private static void OnConnection(PlayerPreauthEvent ev)
	{
		SendEvent($"\ud83c\udf10 **{ev.UserId}** is preauthentificating from **{ev.IpAddress}** ({ev.Region}) with flags: {ev.CentralFlags}", WebhookEventLog.PlayerAuth);
	}

	private static void OnDamage(ReferenceHub target, DamageHandlerBase damageHandler)
	{
		if (!RoundHelper.IsStarted)
		{
			return;
		}
		ReferenceHub referenceHub = null;
		AttackerDamageHandler attackerDamageHandler = null;
		if (damageHandler is AttackerDamageHandler)
		{
			attackerDamageHandler = damageHandler as AttackerDamageHandler;
			referenceHub = attackerDamageHandler.Attacker.Hub;
		}
		try
		{
			if ((object)referenceHub == null || referenceHub == target)
			{
				SendEvent("\ud83d\udd2b [" + HubRoleExtensions.RoleId(target).ToString().SpaceByPascalCase() + "] " + HubDataExtensions.Nick(target) + " (" + HubDataExtensions.UserId(target) + ") damaged himself using " + WebhookHandler.GetDamageName(damageHandler).TrimEnd(new char[1] { '.' }) + ".", WebhookEventLog.PlayerSelfDamage);
			}
			else if (attackerDamageHandler != null && referenceHub != null && referenceHub != target && referenceHub.GetFaction() == target.GetFaction())
			{
				SendEvent(default(Compendium.Webhooks.Discord.DiscordEmbed).WithTitle("⚠\ufe0f Team Damage").WithField("\ud83d\udd17 Attacker", HubRoleExtensions.RoleId(referenceHub).ToString().SpaceByPascalCase() + " **" + HubDataExtensions.Nick(referenceHub) + "** (" + HubDataExtensions.UserId(referenceHub) + ")", inline: false).WithField("\ud83d\udd17 Target", HubRoleExtensions.RoleId(target).ToString().SpaceByPascalCase() + " **" + HubDataExtensions.Nick(target) + "** (" + HubDataExtensions.UserId(target) + ")", inline: false)
					.WithField("\ud83d\udd17 Damage", $"**{Mathf.CeilToInt(attackerDamageHandler.Damage)} HP** ({WebhookHandler.GetDamageName(damageHandler).TrimEnd(new char[1] { '.' })})", inline: false)
					.WithField("\ud83c\udf10 Position Summary", PositionSummary(target, referenceHub), inline: false)
					.WithColor(System.Drawing.Color.Orange), WebhookEventLog.PlayerFriendlyDamage);
			}
			else if (referenceHub != null)
			{
				SendEvent("\ud83d\udd2b [" + HubRoleExtensions.RoleId(referenceHub).ToString().SpaceByPascalCase() + "] " + HubDataExtensions.Nick(referenceHub) + " (" + HubDataExtensions.UserId(referenceHub) + ") damaged player " + HubRoleExtensions.RoleId(target).ToString().SpaceByPascalCase() + " " + HubDataExtensions.Nick(target) + " (" + HubDataExtensions.UserId(target) + ") with " + WebhookHandler.GetDamageName(damageHandler).TrimEnd(new char[1] { '.' }), WebhookEventLog.PlayerDamage);
			}
			else
			{
				SendEvent("\ud83d\udd2b [" + HubRoleExtensions.RoleId(target).ToString().SpaceByPascalCase() + "] " + HubDataExtensions.Nick(target) + " (" + HubDataExtensions.UserId(target) + ") damaged himself using " + WebhookHandler.GetDamageName(damageHandler).TrimEnd(new char[1] { '.' }) + ".", WebhookEventLog.PlayerSelfDamage);
			}
		}
		catch (Exception ex)
		{
			FLog.Error("OnDamage caught exception: " + ex.Message);
		}
	}

	private static void OnDeath(ReferenceHub target, DamageHandlerBase damageHandler)
	{
		if (!RoundHelper.IsStarted)
		{
			return;
		}
		ReferenceHub referenceHub = null;
		AttackerDamageHandler attackerDamageHandler = null;
		if (damageHandler is AttackerDamageHandler)
		{
			attackerDamageHandler = damageHandler as AttackerDamageHandler;
			referenceHub = attackerDamageHandler.Attacker.Hub;
		}
		try
		{
			if ((object)referenceHub == null || referenceHub == target)
			{
				SendEvent("\ud83e\udea6 [" + HubRoleExtensions.RoleId(target).ToString().SpaceByPascalCase() + "] " + HubDataExtensions.Nick(target) + " (" + HubDataExtensions.UserId(target) + ") killed himself using " + WebhookHandler.GetDamageName(damageHandler).TrimEnd(new char[1] { '.' }) + ".", WebhookEventLog.PlayerSuicide);
			}
			else if (attackerDamageHandler != null && referenceHub != null && referenceHub != target && referenceHub.GetFaction() == target.GetFaction())
			{
				SendEvent(default(Compendium.Webhooks.Discord.DiscordEmbed).WithTitle("☠\ufe0f Team Kill").WithField("\ud83d\udd17 Attacker", HubRoleExtensions.RoleId(referenceHub).ToString().SpaceByPascalCase() + " **" + HubDataExtensions.Nick(referenceHub) + "** (" + HubDataExtensions.UserId(referenceHub) + ")", inline: false).WithField("\ud83d\udd17 Target", HubRoleExtensions.RoleId(target).ToString().SpaceByPascalCase() + " **" + HubDataExtensions.Nick(target) + "** (" + HubDataExtensions.UserId(target) + ")", inline: false)
					.WithField("\ud83d\udd17 Damage", $"**{Mathf.CeilToInt(attackerDamageHandler.Damage)} HP** ({WebhookHandler.GetDamageName(damageHandler).TrimEnd(new char[1] { '.' })})", inline: false)
					.WithField("\ud83c\udf10 Position Summary", PositionSummary(target, referenceHub), inline: false)
					.WithColor(System.Drawing.Color.Red), WebhookEventLog.PlayerFriendlyKill);
			}
			else if (referenceHub != null)
			{
				SendEvent("\ud83d\udc80 [" + HubRoleExtensions.RoleId(referenceHub).ToString().SpaceByPascalCase() + "] " + HubDataExtensions.Nick(referenceHub) + " (" + HubDataExtensions.UserId(referenceHub) + ") killed player " + HubRoleExtensions.RoleId(target).ToString().SpaceByPascalCase() + " " + HubDataExtensions.Nick(target) + " (" + HubDataExtensions.UserId(target) + ") with " + WebhookHandler.GetDamageName(damageHandler).TrimEnd(new char[1] { '.' }), WebhookEventLog.PlayerKill);
			}
			else
			{
				SendEvent("\ud83e\udea6 [" + HubRoleExtensions.RoleId(target).ToString().SpaceByPascalCase() + "] " + HubDataExtensions.Nick(target) + " (" + HubDataExtensions.UserId(target) + ") killed himself using " + WebhookHandler.GetDamageName(damageHandler).TrimEnd(new char[1] { '.' }) + ".", WebhookEventLog.PlayerSelfDamage);
			}
		}
		catch (Exception ex)
		{
			FLog.Error("OnDeath caught exception: " + ex.Message);
		}
	}

	[Event]
	private static void OnJoined(PlayerJoinedEvent ev)
	{
		PlayerJoinedEvent ev2 = ev;
		Calls.Delay(0.5f, delegate
		{
			SendEvent("➡\ufe0f **" + ev2.Player.Nickname + " (" + ev2.Player.UserId + ") joined from " + ev2.Player.IpAddress + "** (assigned unique ID: **" + HubDataExtensions.UniqueId(ev2.Player.ReferenceHub) + "**)", WebhookEventLog.PlayerJoined);
		});
	}

	private static void OnLeaving(NetPeer peer, ReferenceHub player, DisconnectReason reason, SocketError error)
	{
		ReferenceHub player2 = player;
		if (reason == DisconnectReason.Timeout)
		{
			FLog.Warn($"Player '{HubDataExtensions.GetLogName(player2, includeIp: true)}' crashed! ({reason} / {error})");
			SendEvent("⬅\ufe0f  ⚠\ufe0f **[" + HubRoleExtensions.RoleName(player2) + "] " + HubDataExtensions.Nick(player2) + " (" + HubDataExtensions.UserId(player2) + ") left from " + HubDataExtensions.Ip(player2) + "** | **__CRASH__** | (assigned unique ID: **" + HubDataExtensions.UniqueId(player2) + "**)", WebhookEventLog.PlayerLeft);
			if (player2.IsSCP())
			{
				Hub.Hubs.ForEach(delegate(ReferenceHub hub)
				{
					HubWorldExtensions.Hint(hub, "<b><color=#FF0000>[SCP ODPOJENÍ]</color></b>\nHráči <b><color=#90FF33>" + HubDataExtensions.Nick(player2) + "</color></b> (<color=" + HubRoleExtensions.GetRoleColorHexPrefixed(player2) + ">" + HubRoleExtensions.RoleName(player2) + "</color>) spadla hra!");
				}, (ReferenceHub hub) => hub.serverRoles.RemoteAdmin);
			}
		}
		else
		{
			SendEvent("⬅\ufe0f **[" + HubRoleExtensions.RoleName(player2) + "] " + HubDataExtensions.Nick(player2) + " (" + HubDataExtensions.UserId(player2) + ") left from " + HubDataExtensions.Ip(player2) + "** (assigned unique ID: **" + HubDataExtensions.UniqueId(player2) + "**)", WebhookEventLog.PlayerLeft);
		}
	}

	[Event]
	private static void OnCuffed(PlayerHandcuffEvent ev)
	{
		SendEvent("\ud83d\udd12 [" + ev.Player.Role.ToString().SpaceByPascalCase() + "] " + ev.Player.Nickname + " (" + ev.Player.UserId + ") cuffed player " + ev.Target.Role.ToString().SpaceByPascalCase() + " " + ev.Target.Nickname + " (" + ev.Target.UserId + ") with " + (ev.Player.CurrentItem?.ItemTypeId.ToString().SpaceByPascalCase() ?? "Unknown"), WebhookEventLog.PlayerCuff);
	}

	[Event]
	private static void OnUncuffed(PlayerRemoveHandcuffsEvent ev)
	{
		SendEvent("\ud83d\udd13 [" + ev.Player.Role.ToString().SpaceByPascalCase() + "] " + ev.Player.Nickname + " (" + ev.Player.UserId + ") uncuffed player " + ev.Target.Role.ToString().SpaceByPascalCase() + " " + ev.Target.Nickname + " (" + ev.Target.UserId + ")", WebhookEventLog.PlayerUncuff);
	}

	[Event]
	private static void OnThrownGrenade(PlayerThrowProjectileEvent ev)
	{
		if (ev.Item.Category == ItemCategory.Grenade)
		{
			SendEvent("\ud83d\udd53 [" + ev.Thrower.Role.ToString().SpaceByPascalCase() + "] " + ev.Thrower.Nickname + " (" + ev.Thrower.UserId + ") threw their " + ev.Item.ItemTypeId.ToString().SpaceByPascalCase(), WebhookEventLog.GrenadeThrown);
		}
	}

	[Event]
	private static void OnGrenadeExploded(GrenadeExplodedEvent ev)
	{
		if ((object)ev.Grenade != null && ev.Grenade.PreviousOwner.Role.GetFaction() != 0)
		{
			SendEvent("\ud83d\udca5 [" + ev.Grenade.PreviousOwner.Role.ToString().SpaceByPascalCase() + "] " + ev.Grenade.PreviousOwner.Nickname + " (" + ev.Grenade.PreviousOwner.LogUserID + ")'s " + ev.Grenade.Info.ItemId.ToString().SpaceByPascalCase().Remove("Grenade")
				.Trim() + " grenade exploded.", WebhookEventLog.GrenadeExploded);
		}
	}

	[Event]
	private static void OnBanned(BanIssuedEvent ev)
	{
		if (ev.BanType != 0)
		{
			return;
		}
		DateTime dateTime = new DateTime(ev.BanDetails.IssuanceTime);
		DateTime dateTime2 = new DateTime(ev.BanDetails.Expires);
		TimeSpan span = TimeSpan.FromTicks((dateTime2 - dateTime).Ticks);
		string text = "Unknown Nick";
		string text2 = ev.BanDetails.Id;
		string text3 = ev.BanDetails.Id;
		string text4 = "Issuer Nick";
		string text5 = ev.BanDetails.Issuer;
		try
		{
			if (PlayerDataRecorder.TryQuery(ev.BanDetails.Id, queryNick: false, out var record))
			{
				text = record.NameTracking.LastValue;
				text2 = record.UserId;
				text3 = record.Ip;
			}
		}
		catch (Exception message)
		{
			FLog.Error("Caught an exception when trying to parse ban details!");
			FLog.Error(message);
		}
		try
		{
			int num = ev.BanDetails.Issuer.LastIndexOf('(');
			int num2 = ev.BanDetails.Issuer.LastIndexOf(')');
			if (num < 1 || num2 < 1)
			{
				text4 = ev.BanDetails.Issuer;
				text5 = "Unknown ID";
			}
			else
			{
				text4 = ev.BanDetails.Issuer.GetBeforeIndex(num).Trim();
				text5 = ev.BanDetails.Issuer.Between(num - 1, num2 + 2).Trim();
			}
		}
		catch (Exception message2)
		{
			FLog.Error("Caught an exception when trying to parse ban details!");
			FLog.Error(message2);
		}
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook.Type == WebhookLog.BanPrivate)
			{
				Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField = default(Compendium.Webhooks.Discord.DiscordEmbedField);
				Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField2 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
				Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField3 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
				Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField4 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
				DiscordEmbedFooter value = default(DiscordEmbedFooter);
				DiscordEmbedAuthor value2 = default(DiscordEmbedAuthor);
				Compendium.Webhooks.Discord.DiscordEmbed value3 = default(Compendium.Webhooks.Discord.DiscordEmbed);
				string text6 = (string.IsNullOrWhiteSpace(ev.BanDetails.Reason) ? "No reason provided." : ev.BanDetails.Reason);
				value2.WithName(World.CurrentClearOrAlternativeServerName);
				discordEmbedField4.WithName("❔ Reason");
				discordEmbedField4.WithValue("```" + text6 + "```", inline: false);
				discordEmbedField.WithName("\ud83d\udd17 Issuer");
				discordEmbedField2.WithName("\ud83d\udd17 Player");
				discordEmbedField3.WithName("\ud83d\udd52 Duration");
				discordEmbedField3.WithValue(span.UserFriendlySpan() ?? "", inline: false);
				if (WebhookHandler.PrivateBansIncludeIp)
				{
					discordEmbedField.WithValue("**Username**: " + text4 + "\n**User ID**: " + text5, inline: false);
					discordEmbedField2.WithValue("**Username**: " + text + "\n**User ID**: " + text2 + "\n**User IP**: " + text3, inline: false);
				}
				else
				{
					discordEmbedField.WithValue("**Username**: " + text4 + "\n**User ID**: " + text5, inline: false);
					discordEmbedField2.WithValue("**Username**: " + text + "\n**User ID**: " + text2 + "\n", inline: false);
				}
				value.WithText("\ud83d\udd52 Banned at: " + dateTime.ToLocalTime().ToString("G") + "\n\ud83d\udd52 Expires at: " + dateTime2.ToLocalTime().ToString("G"));
				value3.WithAuthor(value2);
				value3.WithColor(System.Drawing.Color.Red);
				value3.WithFields(discordEmbedField, discordEmbedField2, discordEmbedField3, discordEmbedField4);
				value3.WithFooter(value);
				value3.WithTitle("⚠\ufe0f Private Ban Log");
				webhook.Send(null, value3);
			}
			else if (webhook.Type == WebhookLog.BanPublic)
			{
				Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField5 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
				Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField6 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
				Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField7 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
				Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField8 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
				DiscordEmbedFooter value4 = default(DiscordEmbedFooter);
				DiscordEmbedAuthor value5 = default(DiscordEmbedAuthor);
				Compendium.Webhooks.Discord.DiscordEmbed value6 = default(Compendium.Webhooks.Discord.DiscordEmbed);
				string text7 = (string.IsNullOrWhiteSpace(ev.BanDetails.Reason) ? "No reason provided." : ev.BanDetails.Reason);
				value5.WithName(World.CurrentClearOrAlternativeServerName);
				discordEmbedField7.WithName("❔ Reason");
				discordEmbedField7.WithValue("```" + text7 + "```", inline: false);
				discordEmbedField5.WithName("\ud83d\udd17 Issuer");
				discordEmbedField6.WithName("\ud83d\udd17 Player");
				discordEmbedField8.WithName("\ud83d\udd52 Duration");
				discordEmbedField8.WithValue(span.UserFriendlySpan() ?? "", inline: false);
				discordEmbedField5.WithValue("**" + text4 + "**", inline: false);
				discordEmbedField6.WithValue("**" + text + "**", inline: false);
				value4.WithText("\ud83d\udd52 Banned at: " + dateTime.ToLocalTime().ToString("G") + "\n\ud83d\udd52 Expires at: " + dateTime2.ToLocalTime().ToString("G"));
				value6.WithAuthor(value5);
				value6.WithColor(System.Drawing.Color.Red);
				value6.WithFields(discordEmbedField5, discordEmbedField6, discordEmbedField8, discordEmbedField7);
				value6.WithFooter(value4);
				value6.WithTitle("⚠\ufe0f Public Ban Log");
				webhook.Send(null, value6);
			}
		}
	}

	[Event]
	private static void OnReport(PlayerReportEvent ev)
	{
		PlayerReportEvent ev2 = ev;
		if (WebhookHandler.AnnounceReportsInGame)
		{
			Hub.Hubs.ForEach(delegate(ReferenceHub h)
			{
				if (HubDataExtensions.IsStaff(h, countNwStaff: false))
				{
					HubWorldExtensions.Hint(h, "<b><color=#FF0000>[REPORT]</color></b>\nHráč <b><color=#90FF33>" + ev2.Player.Nickname + "</color></b> nahlásil hráče <b><color=#90FF33>" + ev2.Target.Nickname + "</color></b> za:\n<b><color=#33FFA5>" + ev2.Reason + "</color></b>", 10f);
				}
			});
		}
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField2 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField3 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		DiscordEmbedFooter value = default(DiscordEmbedFooter);
		DiscordEmbedAuthor value2 = default(DiscordEmbedAuthor);
		Compendium.Webhooks.Discord.DiscordEmbed value3 = default(Compendium.Webhooks.Discord.DiscordEmbed);
		value2.WithName(World.CurrentClearOrAlternativeServerName);
		discordEmbedField3.WithName("❔ Reason");
		discordEmbedField3.WithValue("```" + ev2.Reason + "```", inline: false);
		discordEmbedField.WithName("\ud83d\udd17 Reporting Player");
		discordEmbedField2.WithName("\ud83d\udd17 Reported Player");
		if (WebhookHandler.ReportsIncludeIp)
		{
			discordEmbedField.WithValue("**Username**: " + ev2.Player.Nickname + "\n**User ID**: " + ev2.Player.UserId + "\n**Player IP**: " + HubDataExtensions.Ip(ev2.Player.ReferenceHub) + "\n" + $"**Player ID**: {ev2.Player.PlayerId}\n" + "**Player Role**: " + ev2.Player.Role.ToString().SpaceByPascalCase(), inline: false);
			discordEmbedField2.WithValue("**Username**: " + ev2.Target.Nickname + "\n**User ID**: " + ev2.Target.UserId + "\n**Player IP**: " + HubDataExtensions.Ip(ev2.Target.ReferenceHub) + "\n" + $"**Player ID**: {ev2.Target.PlayerId}\n" + "**Player Role**: " + ev2.Target.Role.ToString().SpaceByPascalCase(), inline: false);
		}
		else
		{
			discordEmbedField.WithValue("**Username**: " + ev2.Player.Nickname + "\n**User ID**: " + ev2.Player.UserId + "\n" + $"**Player ID**: {ev2.Player.PlayerId}\n" + "**Player Role**: " + ev2.Player.Role.ToString().SpaceByPascalCase(), inline: false);
			discordEmbedField2.WithValue("**Username**: " + ev2.Target.Nickname + "\n**User ID**: " + ev2.Target.UserId + "\n" + $"**Player ID**: {ev2.Target.PlayerId}\n" + "**Player Role**: " + ev2.Target.Role.ToString().SpaceByPascalCase(), inline: false);
		}
		value.WithText("\ud83d\udd52 Reported at: " + DateTime.Now.ToLocalTime().ToString("G"));
		value3.WithAuthor(value2);
		value3.WithColor(System.Drawing.Color.Orange);
		value3.WithFields(discordEmbedField, discordEmbedField2, discordEmbedField3);
		value3.WithFooter(value);
		value3.WithTitle("⚠\ufe0f Player Report");
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook.Type == WebhookLog.Report)
			{
				webhook.Send(null, value3);
			}
		}
	}

	[Event]
	private static void OnCheaterReport(PlayerCheaterReportEvent ev)
	{
		PlayerCheaterReportEvent ev2 = ev;
		if (WebhookHandler.AnnounceReportsInGame)
		{
			Hub.Hubs.ForEach(delegate(ReferenceHub h)
			{
				if (HubDataExtensions.IsStaff(h))
				{
					HubWorldExtensions.Hint(h, "<b><color=#FF0000>[CHEATER REPORT]</color></b>\nHráč <b><color=#90FF33>" + ev2.Player.Nickname + "</color></b> nahlásil hráče <b><color=#90FF33>" + ev2.Target.Nickname + "</color></b> za:\n<b><color=#33FFA5>" + ev2.Reason + "</color></b>", 10f);
				}
			});
		}
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField2 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField3 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		DiscordEmbedFooter value = default(DiscordEmbedFooter);
		DiscordEmbedAuthor value2 = default(DiscordEmbedAuthor);
		Compendium.Webhooks.Discord.DiscordEmbed value3 = default(Compendium.Webhooks.Discord.DiscordEmbed);
		value2.WithName(World.CurrentClearOrAlternativeServerName);
		discordEmbedField3.WithName("❔ Reason");
		discordEmbedField3.WithValue("```" + ev2.Reason + "```");
		discordEmbedField.WithName("\ud83d\udd17 Reporting Player");
		discordEmbedField2.WithName("\ud83d\udd17 Reported Player");
		if (WebhookHandler.ReportsIncludeIp)
		{
			discordEmbedField.WithValue("**Username**: " + ev2.Player.Nickname + "\n**User ID**: " + ev2.Player.UserId + "\n**Player IP**: " + HubDataExtensions.Ip(ev2.Player.ReferenceHub) + "\n" + $"**Player ID**: {ev2.Player.PlayerId}\n" + "**Player Role**: " + ev2.Player.Role.ToString().SpaceByPascalCase());
			discordEmbedField2.WithValue("**Username**: " + ev2.Target.Nickname + "\n**User ID**: " + ev2.Target.UserId + "\n**Player IP**: " + HubDataExtensions.Ip(ev2.Target.ReferenceHub) + "\n" + $"**Player ID**: {ev2.Target.PlayerId}\n" + "**Player Role**: " + ev2.Target.Role.ToString().SpaceByPascalCase());
		}
		else
		{
			discordEmbedField.WithValue("**Username**: " + ev2.Player.Nickname + "\n**User ID**: " + ev2.Player.UserId + "\n" + $"**Player ID**: {ev2.Player.PlayerId}\n" + "**Player Role**: " + ev2.Player.Role.ToString().SpaceByPascalCase());
			discordEmbedField2.WithValue("**Username**: " + ev2.Target.Nickname + "\n**User ID**: " + ev2.Target.UserId + "\n" + $"**Player ID**: {ev2.Target.PlayerId}\n" + "**Player Role**: " + ev2.Target.Role.ToString().SpaceByPascalCase());
		}
		value.WithText("\ud83d\udd52 Reported at: " + DateTime.Now.ToLocalTime().ToString("G"));
		value3.WithAuthor(value2);
		value3.WithColor(System.Drawing.Color.Red);
		value3.WithFields(discordEmbedField, discordEmbedField2, discordEmbedField3);
		value3.WithFooter(value);
		value3.WithTitle("\ud83d\udeab Cheater Report");
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook.Type == WebhookLog.Report)
			{
				webhook.Send(null, value3);
			}
		}
	}

	[Event]
	private static void OnPlayerCommand(PlayerGameConsoleCommandExecutedEvent ev)
	{
		ReferenceHub referenceHub = null;
		referenceHub = ((ev.Player != null) ? ev.Player.ReferenceHub : ReferenceHub.HostHub);
		if ((object)referenceHub == null)
		{
			return;
		}
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField2 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField3 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		DiscordEmbedAuthor value = default(DiscordEmbedAuthor);
		Compendium.Webhooks.Discord.DiscordEmbed value2 = default(Compendium.Webhooks.Discord.DiscordEmbed);
		string[] value3;
		StaffGroup value4;
		StaffGroup[] array = ((StaffHandler.Members.TryGetValue(HubDataExtensions.UserId(referenceHub), out value3) && value3 != null && value3.Length != 0) ? (from g in value3
			select (!StaffHandler.Groups.TryGetValue(g, out value4)) ? null : value4 into g
			where g != null
			select g into c
			orderby c.Permissions.Count
			select c).ToArray() : Array.Empty<StaffGroup>());
		value.WithName(World.CurrentClearOrAlternativeServerName);
		discordEmbedField.WithName("❓ Sender");
		discordEmbedField.WithValue("**" + HubDataExtensions.Nick(referenceHub) + "** (" + (HubDataExtensions.IsServer(referenceHub) ? "Server" : HubDataExtensions.ParsedUserId(referenceHub).ClearId) + ")" + ((array.Length != 0) ? (" (" + string.Join(" | ", array.Select((StaffGroup g) => g.Text))) : "") + ")", inline: false);
		discordEmbedField2.WithName("\ud83d\uddd2\ufe0f Command");
		discordEmbedField2.WithValue("```" + ev.Command + " '" + string.Join(" ", ev.Arguments) + "'```", inline: false);
		discordEmbedField3.WithName("➡\ufe0f Response");
		discordEmbedField3.WithValue("```" + ev.Response.Remove(ev.Command.ToUpperInvariant() + "#") + "```", inline: false);
		value2.WithTitle((ev.Result ? "✅" : "⛔") + " Player Console Command Executed");
		value2.WithAuthor(value);
		value2.WithFields(discordEmbedField, discordEmbedField2, discordEmbedField3);
		value2.WithColor(ev.Result ? System.Drawing.Color.Green : System.Drawing.Color.Red);
		value2.WithFooter("\ud83d\udd57 " + DateTime.Now.ToString("G"));
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook.Type == WebhookLog.GameCommands)
			{
				webhook.Send(null, value2);
			}
		}
	}

	[Event]
	private static void OnConsoleCommmand(ConsoleCommandExecutedEvent ev)
	{
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField2 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		DiscordEmbedAuthor value = default(DiscordEmbedAuthor);
		Compendium.Webhooks.Discord.DiscordEmbed value2 = default(Compendium.Webhooks.Discord.DiscordEmbed);
		value.WithName(World.CurrentClearOrAlternativeServerName);
		discordEmbedField.WithName("\ud83d\uddd2\ufe0f Command");
		discordEmbedField.WithValue("```" + ev.Command + " '" + string.Join(" ", ev.Arguments) + "'```", inline: false);
		discordEmbedField2.WithName("➡\ufe0f Response");
		discordEmbedField2.WithValue("```" + ev.Response.Remove(ev.Command.ToUpperInvariant() + "#") + "```", inline: false);
		value2.WithTitle((ev.Result ? "✅" : "⛔") + " Console Command Executed");
		value2.WithAuthor(value);
		value2.WithFields(discordEmbedField, discordEmbedField2);
		value2.WithColor(ev.Result ? System.Drawing.Color.Green : System.Drawing.Color.Red);
		value2.WithFooter("\ud83d\udd57 " + DateTime.Now.ToString("G"));
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook.Type == WebhookLog.ConsoleCommands)
			{
				webhook.Send(null, value2);
			}
		}
	}

	[Event]
	private static void OnRemoteCommand(RemoteAdminCommandExecutedEvent ev)
	{
		ReferenceHub referenceHub = null;
		referenceHub = (Player.TryGet(ev.Sender, out var player) ? player.ReferenceHub : ReferenceHub.HostHub);
		if ((object)referenceHub == null)
		{
			return;
		}
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField2 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		Compendium.Webhooks.Discord.DiscordEmbedField discordEmbedField3 = default(Compendium.Webhooks.Discord.DiscordEmbedField);
		DiscordEmbedAuthor value = default(DiscordEmbedAuthor);
		Compendium.Webhooks.Discord.DiscordEmbed value2 = default(Compendium.Webhooks.Discord.DiscordEmbed);
		string[] value3;
		StaffGroup value4;
		StaffGroup[] array = ((StaffHandler.Members.TryGetValue(HubDataExtensions.UserId(referenceHub), out value3) && value3 != null && value3.Length != 0) ? (from g in value3
			select (!StaffHandler.Groups.TryGetValue(g, out value4)) ? null : value4 into g
			where g != null
			select g into c
			orderby c.Permissions.Count
			select c).ToArray() : Array.Empty<StaffGroup>());
		value.WithName(World.CurrentClearOrAlternativeServerName);
		discordEmbedField.WithName("❓ Sender");
		discordEmbedField.WithValue("**" + HubDataExtensions.Nick(referenceHub) + "** (" + (HubDataExtensions.IsServer(referenceHub) ? "Server" : HubDataExtensions.ParsedUserId(referenceHub).ClearId) + ")" + ((array.Length != 0) ? (" (" + string.Join(" | ", array.Select((StaffGroup g) => g.Text))) : "") + ")", inline: false);
		discordEmbedField2.WithName("\ud83d\uddd2\ufe0f Command");
		discordEmbedField2.WithValue("```" + ev.Command + " '" + string.Join(" ", ev.Arguments) + "'```", inline: false);
		discordEmbedField3.WithName("➡\ufe0f Response");
		discordEmbedField3.WithValue("```" + ev.Response.Remove(ev.Command.ToUpperInvariant() + "#") + "```", inline: false);
		value2.WithTitle((ev.Result ? "✅" : "⛔") + " Remote Admin Command Executed");
		value2.WithAuthor(value);
		value2.WithFields(discordEmbedField, discordEmbedField2, discordEmbedField3);
		value2.WithColor(ev.Result ? System.Drawing.Color.Green : System.Drawing.Color.Red);
		value2.WithFooter("\ud83d\udd57 " + DateTime.Now.ToString("G"));
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook.Type == WebhookLog.RaCommands)
			{
				webhook.Send(null, value2);
			}
		}
	}
}
