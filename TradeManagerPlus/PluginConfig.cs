using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TradeManagerPlus;

#pragma warning disable CA1812 // ASF uses these classes during runtime through JSON deserialization

/// <summary>
///     Configuration of the plugin, read from the <c>TradeManagerPlus</c> section of the bot's <c>Plugins</c> config.
/// </summary>
internal sealed class PluginConfig {
	/// <summary>
	///     Allows to disable the plugin for a particular bot without removing the configuration.
	/// </summary>
	[JsonPropertyName("Enabled")]
	public bool Enabled { get; init; } = true;

	/// <summary>
	///     Ordered list of rules. The first rule that matches a trade offer wins.
	/// </summary>
	[JsonPropertyName("TradeRules")]
	public IReadOnlyList<TradeRuleConfig>? TradeRules { get; init; }
}

internal sealed class TradeRuleConfig {
	/// <summary>
	///     Action to take when the rule matches: <c>Accept</c> or <c>Decline</c>.
	/// </summary>
	[JsonPropertyName("Type")]
	public string? Type { get; init; }

	/// <summary>
	///     Conditions that need to be met for this rule to match.
	/// </summary>
	[JsonPropertyName("Conditions")]
	public ConditionsConfig? Conditions { get; init; }

	/// <summary>
	///     How the item conditions are evaluated: <c>All</c> (default) or <c>Any</c>.
	/// </summary>
	[JsonPropertyName("ConditionLogic")]
	public string? ConditionLogic { get; init; }

	/// <summary>
	///     Optional human-readable description, used in logs and command output.
	/// </summary>
	[JsonPropertyName("Description")]
	public string? Description { get; init; }
}

internal sealed class ConditionsConfig {
	/// <summary>
	///     Conditions for the items the bot receives.
	/// </summary>
	[JsonPropertyName("TheirItems")]
	public ItemConditionsConfig? TheirItems { get; init; }

	/// <summary>
	///     Conditions for the items the bot gives away.
	/// </summary>
	[JsonPropertyName("MyItems")]
	public ItemConditionsConfig? MyItems { get; init; }
}

internal sealed class ItemConditionsConfig {
	/// <summary>
	///     Allowed real AppIDs. Empty means "any AppID".
	/// </summary>
	[JsonPropertyName("AppIDs")]
	public IReadOnlyList<uint>? AppIDs { get; init; }

	/// <summary>
	///     Allowed item types, for example <c>TradingCard</c> or <c>Trading Card</c>.
	/// </summary>
	[JsonPropertyName("Types")]
	public IReadOnlyList<string>? Types { get; init; }

	/// <summary>
	///     Allowed item rarities, for example <c>Rare</c> or <c>Common</c>.
	/// </summary>
	[JsonPropertyName("Rarities")]
	public IReadOnlyList<string>? Rarities { get; init; }

	/// <summary>
	///     Minimum total amount of items on this side of the trade. Absent or zero means no minimum.
	/// </summary>
	[JsonPropertyName("MinAmount")]
	public uint? MinAmount { get; init; }

	/// <summary>
	///     Maximum total amount of items on this side of the trade. Absent means no maximum.
	/// </summary>
	[JsonPropertyName("MaxAmount")]
	public uint? MaxAmount { get; init; }
}

#pragma warning restore CA1812 // ASF uses these classes during runtime through JSON deserialization
