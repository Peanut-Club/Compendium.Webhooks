using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Compendium.Webhooks.Discord;

public struct DiscordMessageAllowedMentions
{
	[JsonPropertyName("parse")]
	public string[] Parsable { get; set; }

	[JsonPropertyName("roles")]
	public ulong[] RoleIds { get; set; }

	[JsonPropertyName("users")]
	public ulong[] UserIds { get; set; }

	[JsonPropertyName("replied_user")]
	public bool MentionRepliedUser { get; set; }

	public DiscordMessageAllowedMentions WithRoles(params ulong[] roles)
	{
		if (RoleIds != null && RoleIds.Any())
		{
			List<ulong> list = new List<ulong>(RoleIds);
			list.AddRange(roles);
			RoleIds = list.ToArray();
			return this;
		}
		RoleIds = roles;
		return this;
	}

	public DiscordMessageAllowedMentions WithUsers(params ulong[] users)
	{
		if (UserIds != null && UserIds.Any())
		{
			List<ulong> list = new List<ulong>(UserIds);
			list.AddRange(users);
			RoleIds = list.ToArray();
			return this;
		}
		UserIds = users;
		return this;
	}

	public DiscordMessageAllowedMentions WithRepliedUser(bool repliedUser = true)
	{
		MentionRepliedUser = repliedUser;
		return this;
	}

	public DiscordMessageAllowedMentions WithParsable(DiscordMessageAllowedMentionsType discordMessageAllowedMentionsType)
	{
		List<string> list = null;
		list = ((Parsable == null || !Parsable.Any()) ? new List<string>() : new List<string>(Parsable));
		list.Add(discordMessageAllowedMentionsType.ToString());
		Parsable = list.ToArray();
		return this;
	}

	public DiscordMessageAllowedMentions ClearParsable()
	{
		Parsable = Array.Empty<string>();
		return this;
	}

	public DiscordMessageAllowedMentions ClearUsers()
	{
		UserIds = Array.Empty<ulong>();
		return this;
	}

	public DiscordMessageAllowedMentions ClearRoles()
	{
		RoleIds = Array.Empty<ulong>();
		return this;
	}
}
