using System;
using CentralAuth;
using helpers.Patching;
using RemoteAdmin;

namespace Compendium.Webhooks;

public static class WebhookPatches
{
	[Patch(typeof(ServerConsole), "AddLog", PatchType.Prefix, new Type[] { })]
	public static bool ConsolePrefix(string q, ConsoleColor color = ConsoleColor.Gray)
	{
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook.Type == WebhookLog.Console)
			{
				webhook.Send("**[" + DateTime.Now.ToString("G") + "]** ```ansi\n" + q + "\n```");
			}
		}
		ServerConsole.PrintOnOutputs(q, color);
		ServerConsole.PrintFormattedString(q, color);
		return false;
	}

	[Patch(typeof(ServerLogs), "AddLog", PatchType.Prefix, new Type[] { })]
	public static bool ServerPrefix(ServerLogs.Modules module, string msg, ServerLogs.ServerLogType type, bool init = false)
	{
		string text = TimeBehaviour.Rfc3339Time();
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook.Type == WebhookLog.Server)
			{
				webhook.Send($"**[{text}]** <{module} : {type}> `{msg}`");
			}
		}
		object lockObject = ServerLogs.LockObject;
		lock (lockObject)
		{
			ServerLogs.Queue.Enqueue(new ServerLogs.ServerLog(msg, ServerLogs.Txt[(uint)type], ServerLogs.Modulestxt[(uint)module], text));
		}
		if (init)
		{
			return false;
		}
		ServerLogs._state = ServerLogs.LoggingState.Write;
		return false;
	}

	[Patch(typeof(CommandProcessor), "ProcessAdminChat", PatchType.Prefix, new Type[] { })]
	public static bool AdminChatPrefix(string q, CommandSender sender)
	{
		if (!CommandProcessor.CheckPermissions(sender, "Admin Chat", PlayerPermissions.AdminChat, string.Empty))
		{
			if (sender is PlayerCommandSender playerCommandSender)
			{
				playerCommandSender.ReferenceHub.gameConsoleTransmission.SendToClient("You don't have permissions to access Admin Chat!", "red");
				playerCommandSender.RaReply("You don't have permissions to access Admin Chat!", success: false, logToConsole: true, "");
			}
			return false;
		}
		uint num = 0u;
		if (sender is PlayerCommandSender playerCommandSender2)
		{
			num = playerCommandSender2.ReferenceHub.netId;
		}
		q = Misc.SanitizeRichText(q.Replace("~", "-"), "[", "]");
		if (string.IsNullOrWhiteSpace(q.Replace("@", string.Empty)))
		{
			return false;
		}
		if (q.Length > 2000)
		{
			string text = q;
			int length = 2000;
			q = text.Substring(0, length) + "...";
		}
		string content = num + "!" + q;
		if (ServerStatic.IsDedicated)
		{
			ServerConsole.AddLog("[AC " + sender.LogName + "] " + q, ConsoleColor.DarkYellow);
		}
		ServerLogs.AddLog(ServerLogs.Modules.Administrative, "[" + sender.LogName + "] " + q, ServerLogs.ServerLogType.AdminChat);
		foreach (ReferenceHub allHub in ReferenceHub.AllHubs)
		{
			ClientInstanceMode mode = allHub.Mode;
			if (mode != 0 && mode != ClientInstanceMode.DedicatedServer && allHub.serverRoles.AdminChatPerms)
			{
				allHub.encryptedChannelManager.TrySendMessageToClient(content, EncryptedChannelManager.EncryptedChannel.AdminChat);
			}
		}
		foreach (WebhookData webhook in WebhookHandler.Webhooks)
		{
			if (webhook.Type == WebhookLog.StaffChat)
			{
				webhook.Send("**[" + DateTime.Now.ToString("g") + "]** **" + sender.Nickname + "**: " + q);
			}
		}
		return false;
	}
}
