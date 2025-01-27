namespace Compendium.Webhooks.Discord;

public struct DiscordMessageAllowedMentionsType
{
	private readonly string _value;

	public static DiscordMessageAllowedMentionsType Role { get; } = new DiscordMessageAllowedMentionsType("Role");


	public static DiscordMessageAllowedMentionsType User { get; } = new DiscordMessageAllowedMentionsType("Users");


	public static DiscordMessageAllowedMentionsType Everyone { get; } = new DiscordMessageAllowedMentionsType("Everyone");


	private DiscordMessageAllowedMentionsType(string value)
	{
		_value = value;
	}

	public override string ToString()
	{
		return _value;
	}
}
