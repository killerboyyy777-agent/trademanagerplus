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

	/// <summary>
	///     Flat price in Steam gems that the other side has to pay for every card received, for example <c>10</c> for "10
	///     gems per card". Use <see cref="GemPrices" /> instead when the price depends on the card set size. Absent or zero
	///     disables pricing, so the rule only uses the explicit <c>MinAmount</c> limits.
	/// </summary>
	[JsonPropertyName("GemsPerCard")]
	public uint? GemsPerCard { get; init; }

	/// <summary>
	///     Gem price per card, keyed by card set size. Cannot be combined with <see cref="GemsPerCard" />.
	/// </summary>
	[JsonPropertyName("GemPrices")]
	public IReadOnlyList<GemPriceConfig>? GemPrices { get; init; }

	/// <summary>
	///     How many cards an item type counts as for pricing. Trading cards and foil cards always count as one card;
	///     use this for item types that are worth more, for example booster packs.
	/// </summary>
	[JsonPropertyName("CardEquivalents")]
	public IReadOnlyList<CardEquivalentConfig>? CardEquivalents { get; init; }

	/// <summary>
	///     Gem value of a single unit of an item, for items that are neither cards nor gems, for example TF2 keys,
	///     backgrounds or emoticons. The first entry that matches an item wins.
	/// </summary>
	[JsonPropertyName("ItemPrices")]
	public IReadOnlyList<ItemPriceConfig>? ItemPrices { get; init; }

	/// <summary>
	///     Compares the gem value of both sides of the trade instead of only counting gems. When the rule prices
	///     anything, every item on both sides has to have a known value, otherwise the rule does not match.
	/// </summary>
	[JsonPropertyName("Value")]
	public ValueConditionConfig? Value { get; init; }

	/// <summary>
	///     Accepts the trade when the Steam Community market value of the items received exceeds the market value of the
	///     items given by both a flat and a percentage margin. Every item on both sides has to have a known market price,
	///     otherwise the rule does not match. Can be combined with item conditions, for example to only buy cards.
	/// </summary>
	[JsonPropertyName("MarketProfit")]
	public MarketProfitConfig? MarketProfit { get; init; }
}

internal sealed class MarketProfitConfig {
	/// <summary>
	///     Steam currency ID used for the price lookups, for example <c>1</c> for USD, <c>2</c> for GBP or <c>3</c> for
	///     EUR. Defaults to <c>1</c>.
	/// </summary>
	[JsonPropertyName("Currency")]
	public int? Currency { get; init; }

	/// <summary>
	///     Minimum profit in that currency that the trade has to be worth, for example <c>0.05</c>. Leave out for no flat
	///     minimum.
	/// </summary>
	[JsonPropertyName("MinProfit")]
	public decimal? MinProfit { get; init; }

	/// <summary>
	///     Minimum profit as a percentage of the market value given, for example <c>10</c> for 10%. Leave out for no
	///     percentage minimum.
	/// </summary>
	[JsonPropertyName("MinProfitPercent")]
	public decimal? MinProfitPercent { get; init; }

	/// <summary>
	///     Minimum number of units sold in the last 24 hours before a price is trusted. Prices for items that trade less
	///     than this are easy to manipulate and are ignored, making the rule not match. Defaults to <c>10</c>; set to
	///     <c>0</c> to trust any price with a volume.
	/// </summary>
	[JsonPropertyName("MinVolume")]
	public uint? MinVolume { get; init; }

	/// <summary>
	///     Highest price per unit that is still trusted. Anything priced above this is treated as untrusted, which stops a
	///     single absurdly priced item from making a bad trade look profitable. Leave out for no cap.
	/// </summary>
	[JsonPropertyName("MaxUnitPrice")]
	public decimal? MaxUnitPrice { get; init; }
}

internal sealed class GemPriceConfig {
	/// <summary>
	///     Number of cards in the game's Steam card set, for example <c>8</c>.
	/// </summary>
	[JsonPropertyName("SetSize")]
	public byte SetSize { get; init; }

	/// <summary>
	///     Gems paid per card from a set of that size. Must be greater than zero.
	/// </summary>
	[JsonPropertyName("Gems")]
	public uint Gems { get; init; }
}

internal sealed class CardEquivalentConfig {
	/// <summary>
	///     Item type, for example <c>BoosterPack</c>. Same lenient naming as <c>Types</c>.
	/// </summary>
	[JsonPropertyName("Type")]
	public string? Type { get; init; }

	/// <summary>
	///     Number of cards this item type counts as. Defaults to <c>1</c>, which is what cards themselves count as.
	/// </summary>
	[JsonPropertyName("Cards")]
	public uint Cards { get; init; } = 1;
}

internal sealed class ItemPriceConfig {
	/// <summary>
	///     Item type, for example <c>ProfileBackground</c> or <c>Emoticon</c>. Same lenient naming as <c>Types</c>.
	///     Leave out to match any type, for example for TF2 keys, which ASF reports as <c>Unknown</c>.
	/// </summary>
	[JsonPropertyName("Type")]
	public string? Type { get; init; }

	/// <summary>
	///     Real AppIDs this price applies to. Leave out to match any AppID.
	/// </summary>
	[JsonPropertyName("AppIDs")]
	public IReadOnlyList<uint>? AppIDs { get; init; }

	/// <summary>
	///     Exact item names this price applies to, matched against both the market hash name and the plain name,
	///     ignoring case. Use this for items that cannot be told apart by type and AppID, for example
	///     <c>Mann Co. Supply Crate Key</c>.
	/// </summary>
	[JsonPropertyName("Names")]
	public IReadOnlyList<string>? Names { get; init; }

	/// <summary>
	///     Gem value of a single unit of the item. Must be greater than zero.
	/// </summary>
	[JsonPropertyName("Gems")]
	public uint Gems { get; init; }
}

internal sealed class ValueConditionConfig {
	/// <summary>
	///     Multiplier applied to the value of the items the bot gives, defaulting to <c>1</c>. Use <c>1.1</c> to require
	///     receiving at least a tenth more value than giving, which is how a key or gem exchange is configured.
	/// </summary>
	[JsonPropertyName("Factor")]
	public decimal? Factor { get; init; }

	/// <summary>
	///     Value added to the items the bot gives before the comparison, defaulting to <c>0</c>. Negative values make
	///     the rule accept slightly worse offers.
	/// </summary>
	[JsonPropertyName("Offset")]
	public decimal? Offset { get; init; }

	/// <summary>
	///     Item types whose Steam gem ("goo") value is read from steamcommunity.com and used as their price, for example
	///     <c>ProfileBackground</c> or <c>Emoticon</c>. Use <c>All</c> to allow every item type. Absent or empty means
	///     Steam's values are never used, so only the configured prices apply.
	/// </summary>
	[JsonPropertyName("GooTypes")]
	public IReadOnlyList<string>? GooTypes { get; init; }

	/// <summary>
	///     When true, the value received has to be strictly greater than the value the bot gives, so an offer that is only
	///     worth the same as what it costs is rejected. Defaults to true for rules that use <see cref="GooTypes" /> and to
	///     false otherwise, because a plain gem price is paid exactly.
	/// </summary>
	[JsonPropertyName("Strict")]
	public bool? Strict { get; init; }

	/// <summary>
	///     Which side is the one being paid. <c>Buy</c> (the default) means the bot pays for what it receives, so the
	///     items given have to be worth at least the value of the items received, multiplied by <see cref="Factor" />.
	///     <c>Sell</c> means the bot is paid for what it gives, so the items received have to be worth at least the value
	///     of the items given, multiplied by <see cref="Factor" />.
	/// </summary>
	[JsonPropertyName("Direction")]
	public string? Direction { get; init; }
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
	///     Real AppIDs that must never appear on this side, for example <c>[730]</c> to refuse CS items.
	///     The check applies to every item on the side when <c>ConditionLogic</c> is <c>All</c>.
	/// </summary>
	[JsonPropertyName("ExcludedAppIDs")]
	public IReadOnlyList<uint>? ExcludedAppIDs { get; init; }

	/// <summary>
	///     Allowed card set sizes, meaning the number of cards in the game's Steam card set, for example <c>8</c>.
	///     Absent or empty means "any card set size". Items whose card set size is unknown never match.
	/// </summary>
	[JsonPropertyName("SetSizes")]
	public IReadOnlyList<byte>? SetSizes { get; init; }

	/// <summary>
	///     Allowed item names, matched against both the market hash name and the plain name, ignoring case. Use this for
	///     items that cannot be told apart by type and AppID, for example <c>Mann Co. Supply Crate Key</c>.
	/// </summary>
	[JsonPropertyName("Names")]
	public IReadOnlyList<string>? Names { get; init; }

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
