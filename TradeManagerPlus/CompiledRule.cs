using System;
using System.Collections.Generic;
using System.Linq;
using ArchiSteamFarm.Steam.Data;

namespace TradeManagerPlus;

internal enum TradeRuleAction : byte {
	Accept,
	Decline
}

internal enum TradeRuleLogic : byte {
	All,
	Any
}

/// <summary>
///     Validated, ready-to-use representation of a single side's item conditions.
/// </summary>
internal sealed class CompiledItemConditions {
	public HashSet<uint> AppIDs { get; } = [];
	public HashSet<uint> ExcludedAppIDs { get; } = [];
	public HashSet<EAssetType> Types { get; } = [];
	public HashSet<EAssetRarity> Rarities { get; } = [];
	public HashSet<byte> SetSizes { get; } = [];
	public HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);
	public uint MinAmount { get; init; }
	public uint MaxAmount { get; init; } = uint.MaxValue;

	/// <summary>
	///     True when this side has no constraints at all, in which case it matches anything.
	/// </summary>
	internal bool IsEmpty => (AppIDs.Count == 0) && (ExcludedAppIDs.Count == 0) && (Types.Count == 0) && (Rarities.Count == 0) && (SetSizes.Count == 0) && (Names.Count == 0) && (MinAmount == 0) && (MaxAmount == uint.MaxValue);

	/// <summary>
	///     True when this side has no per-item constraints, only an amount constraint.
	/// </summary>
	internal bool HasNoItemFilters => (AppIDs.Count == 0) && (ExcludedAppIDs.Count == 0) && (Types.Count == 0) && (Rarities.Count == 0) && (SetSizes.Count == 0) && (Names.Count == 0);

	/// <summary>
	///     Matches a single item. <paramref name="cardSetSizes" /> maps a game's AppID to the number of cards in its Steam
	///     card set and is only required when this side constrains <see cref="SetSizes" />.
	/// </summary>
	internal bool MatchesItem(Asset item, IReadOnlyDictionary<uint, byte>? cardSetSizes) =>
		((AppIDs.Count == 0) || AppIDs.Contains(item.RealAppID))
		&& !ExcludedAppIDs.Contains(item.RealAppID)
		&& ((Types.Count == 0) || Types.Contains(item.Type))
		&& ((Rarities.Count == 0) || Rarities.Contains(item.Rarity))
		&& MatchesName(item)
		&& MatchesCardSetSize(item, cardSetSizes);

	private bool MatchesName(Asset item) {
		if (Names.Count == 0) {
			return true;
		}

		string? marketHashName = item.Description?.MarketHashName;
		string? name = item.Description?.Name;

		return ((marketHashName != null) && Names.Contains(marketHashName)) || ((name != null) && Names.Contains(name));
	}

	private bool MatchesCardSetSize(Asset item, IReadOnlyDictionary<uint, byte>? cardSetSizes) {
		if (SetSizes.Count == 0) {
			return true;
		}

		// An unknown card set size never matches: we cannot prove that the item comes from the requested set.
		return (cardSetSizes != null) && cardSetSizes.TryGetValue(item.RealAppID, out byte setSize) && SetSizes.Contains(setSize);
	}

	internal string Describe() {
		if (IsEmpty) {
			return "anything";
		}

		List<string> parts = [];

		if (AppIDs.Count > 0) {
			parts.Add($"appIDs {string.Join(", ", AppIDs.Order())}");
		}

		if (ExcludedAppIDs.Count > 0) {
			parts.Add($"excluded appIDs {string.Join(", ", ExcludedAppIDs.Order())}");
		}

		if (Types.Count > 0) {
			parts.Add($"types {string.Join(", ", Types.Order().Select(static type => type.ToString()))}");
		}

		if (Rarities.Count > 0) {
			parts.Add($"rarities {string.Join(", ", Rarities.Order().Select(static rarity => rarity.ToString()))}");
		}

		if (SetSizes.Count > 0) {
			parts.Add($"card set sizes {string.Join(", ", SetSizes.Order())}");
		}

		if (Names.Count > 0) {
			parts.Add($"names {string.Join(", ", Names.Order(StringComparer.OrdinalIgnoreCase))}");
		}

		if ((MinAmount > 0) || (MaxAmount < uint.MaxValue)) {
			parts.Add(MaxAmount < uint.MaxValue ? $"amount {MinAmount}-{MaxAmount}" : $"amount {MinAmount}+");
		}

		return parts.Count > 0 ? string.Join(", ", parts) : "anything";
	}
}

internal sealed class CompiledTradeRule {
	public required TradeRuleAction Action { get; init; }
	public required TradeRuleLogic Logic { get; init; }

	/// <summary>
	///     Condition groups for the items the bot receives. Empty means the side is not constrained.
	/// </summary>
	public required IReadOnlyList<CompiledItemConditions> TheirItems { get; init; }

	/// <summary>
	///     Condition groups for the items the bot gives away. Empty means the side is not constrained.
	/// </summary>
	public required IReadOnlyList<CompiledItemConditions> MyItems { get; init; }

	public string Description { get; init; } = string.Empty;

	/// <summary>
	///     Gems that have to be paid for every item received, or <c>0</c> when the rule does not price the trade.
	/// </summary>
	public uint GemsPerCard { get; init; }

	/// <summary>
	///     Gems per card keyed by card set size, sorted by set size. Empty when the rule does not price by set size.
	/// </summary>
	public IReadOnlyList<KeyValuePair<byte, uint>> GemPrices { get; init; } = [];

	/// <summary>
	///     How many cards an item type counts as. Card types are always worth one card, even when not listed here.
	/// </summary>
	public IReadOnlyDictionary<EAssetType, uint> CardEquivalents { get; init; } = new Dictionary<EAssetType, uint>();

	/// <summary>
	///     Gem value of a single unit of specific items, in the order they were configured. The first matching entry wins.
	/// </summary>
	public IReadOnlyList<CompiledItemPrice> ItemPrices { get; init; } = [];

	/// <summary>
	///     Multiplier applied to the value of the items the bot gives before it is compared with the value it receives.
	/// </summary>
	public decimal ValueFactor { get; init; } = 1;

	/// <summary>
	///     Value added to the items the bot gives before it is compared with the value it receives.
	/// </summary>
	public decimal ValueOffset { get; init; }

	/// <summary>
	///     True when Steam's own gem ("goo") value may be read for items without a configured price.
	/// </summary>
	public HashSet<EAssetType> GooTypes { get; init; } = [];

	/// <summary>
	///     True when Steam's own gem value may be read for every item type.
	/// </summary>
	public bool GooAllTypes { get; init; }

	/// <summary>
	///     True when the value received has to be strictly greater than the value the bot gives.
	/// </summary>
	public bool ValueStrict { get; init; }

	/// <summary>
	///     True when the bot pays for what it receives, so the value given has to cover the value received. False when the
	///     bot is paid for what it gives, so the value received has to cover the value given.
	/// </summary>
	public bool ValueBuy { get; init; } = true;

	/// <summary>
	///     True when the rule compares the value of both sides instead of only checking the item conditions.
	/// </summary>
	internal bool HasValueRequirement => (GemsPerCard > 0) || (GemPrices.Count > 0) || (ItemPrices.Count > 0) || GooAllTypes || (GooTypes.Count > 0);

	/// <summary>
	///     Flat market profit the trade has to be worth before the rule matches.
	/// </summary>
	public decimal MarketMinProfit { get; init; }

	/// <summary>
	///     Market profit as a percentage of the value given that the trade has to be worth before the rule matches.
	/// </summary>
	public decimal MarketMinProfitPercent { get; init; }

	/// <summary>
	///     Steam currency ID used for the market price lookups.
	/// </summary>
	public int MarketCurrency { get; init; } = 1;

	/// <summary>
	///     True when the market prices are compared in the currency of the bot's own Steam wallet instead of
	///     <see cref="MarketCurrency" />.
	/// </summary>
	public bool MarketCurrencyFollowsWallet { get; init; }

	/// <summary>
	///     Units sold in the last 24 hours before a market price is trusted.
	/// </summary>
	public uint MarketMinVolume { get; init; } = 10;

	/// <summary>
	///     Highest trusted market price per unit, or <c>0</c> when any price is trusted.
	/// </summary>
	public decimal MarketMaxUnitPrice { get; init; }

	/// <summary>
	///     Smallest trade hold in days this rule accepts.
	/// </summary>
	public uint TradeHoldMinDays { get; init; }

	/// <summary>
	///     Largest trade hold in days this rule accepts.
	/// </summary>
	public uint TradeHoldMaxDays { get; init; } = 15;

	/// <summary>
	///     True when the rule restricts the trade hold, in which case an unknown hold never matches.
	/// </summary>
	internal bool HasTradeHoldRequirement { get; init; }

	/// <summary>
	///     True when the rule requires the trade to be profitable on the Steam Community market.
	/// </summary>
	internal bool HasMarketRequirement => (MarketMinProfit > 0) || (MarketMinProfitPercent > 0);
}

/// <summary>
///     A configured gem value for one kind of item, optionally narrowed down by type, AppID and exact name.
/// </summary>
internal sealed class CompiledItemPrice {
	public EAssetType? Type { get; init; }
	public HashSet<uint> AppIDs { get; } = [];
	public HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);
	public uint Gems { get; init; }

	internal bool Matches(Asset item) {
		if (Type.HasValue && (item.Type != Type.Value)) {
			return false;
		}

		if ((AppIDs.Count > 0) && !AppIDs.Contains(item.RealAppID)) {
			return false;
		}

		if (Names.Count > 0) {
			string? marketHashName = item.Description?.MarketHashName;
			string? name = item.Description?.Name;

			if (((marketHashName == null) || !Names.Contains(marketHashName)) && ((name == null) || !Names.Contains(name))) {
				return false;
			}
		}

		return true;
	}

	internal string Describe() {
		List<string> filters = [];

		if (Type.HasValue) {
			filters.Add(Type.Value.ToString());
		}

		if (AppIDs.Count > 0) {
			filters.Add($"appIDs {string.Join(", ", AppIDs.Order())}");
		}

		if (Names.Count > 0) {
			filters.Add($"names {string.Join(", ", Names.Order(StringComparer.OrdinalIgnoreCase))}");
		}

		string target = filters.Count > 0 ? string.Join(" ", filters) : "anything";

		return $"{target} = {Gems} gem(s)";
	}
}

/// <summary>
///     Parses and validates the user-provided rule config. Invalid rules are rejected with a reason instead of being
///     silently ignored, so that configuration mistakes are visible in the ASF log.
/// </summary>
internal static class RuleCompiler {
	internal static bool TryCompile(TradeRuleConfig config, out CompiledTradeRule? rule, out string? error) {
		rule = null;
		error = null;

		TradeRuleAction action;

		switch (config.Type?.Trim().ToUpperInvariant()) {
			case "ACCEPT":
				action = TradeRuleAction.Accept;

				break;
			case "DECLINE":
				action = TradeRuleAction.Decline;

				break;
			case null or "":
				error = "missing required 'Type' property (expected 'Accept' or 'Decline')";

				return false;
			default:
				error = $"unsupported 'Type' value '{config.Type}' (expected 'Accept' or 'Decline')";

				return false;
		}

		TradeRuleLogic logic;

		switch (config.ConditionLogic?.Trim().ToUpperInvariant()) {
			case null or "" or "ALL":
				logic = TradeRuleLogic.All;

				break;
			case "ANY":
				logic = TradeRuleLogic.Any;

				break;
			default:
				error = $"unsupported 'ConditionLogic' value '{config.ConditionLogic}' (expected 'All' or 'Any')";

				return false;
		}

		IReadOnlyList<CompiledItemConditions>? theirItems = CompileItemConditions(config.Conditions?.TheirItems, "TheirItems", out error);

		if (theirItems == null) {
			return false;
		}

		IReadOnlyList<CompiledItemConditions>? myItems = CompileItemConditions(config.Conditions?.MyItems, "MyItems", out error);

		if (myItems == null) {
			return false;
		}

		List<KeyValuePair<byte, uint>> gemPrices = [];

		if (config.GemPrices != null) {
			foreach (GemPriceConfig gemPrice in config.GemPrices) {
				if (gemPrice.SetSize == 0) {
					error = "'GemPrices' contains an invalid card set size 0";

					return false;
				}

				if (gemPrice.Gems == 0) {
					error = $"'GemPrices' contains a price of 0 gems for card set size {gemPrice.SetSize}";

					return false;
				}

				if (gemPrices.Any(entry => entry.Key == gemPrice.SetSize)) {
					error = $"'GemPrices' contains more than one price for card set size {gemPrice.SetSize}";

					return false;
				}

				gemPrices.Add(new KeyValuePair<byte, uint>(gemPrice.SetSize, gemPrice.Gems));
			}

			gemPrices.Sort(static (left, right) => left.Key.CompareTo(right.Key));
		}

		if ((gemPrices.Count > 0) && ((config.GemsPerCard ?? 0) > 0)) {
			error = "uses both 'GemsPerCard' and 'GemPrices'; use 'GemPrices' alone for per-set-size pricing";

			return false;
		}

		Dictionary<EAssetType, uint> cardEquivalents = [];

		if (config.CardEquivalents != null) {
			foreach (CardEquivalentConfig equivalent in config.CardEquivalents) {
				if (!EnumNameParser.TryParseAssetType(equivalent.Type, out EAssetType type)) {
					error = $"'CardEquivalents' contains unknown item type '{equivalent.Type}'. Valid values: {EnumNameParser.AssetTypeNames}";

					return false;
				}

				if (equivalent.Cards == 0) {
					error = $"'CardEquivalents' contains a card count of 0 for '{equivalent.Type}'";

					return false;
				}

				// Pricing by card set size needs a set size, and only card item types have one. Allowing any other
				// type here would compile into a price that can never be resolved.
				if ((gemPrices.Count > 0) && !TradeMatcher.UsesCardSetSizes(type)) {
					error = $"'CardEquivalents' contains '{equivalent.Type}', which has no card set size; with 'GemPrices' only card item types can have a card count";

					return false;
				}

				if (!cardEquivalents.TryAdd(type, equivalent.Cards)) {
					error = $"'CardEquivalents' contains more than one entry for '{type}'";

					return false;
				}
			}
		}

		List<CompiledItemPrice> itemPrices = [];

		if (config.ItemPrices != null) {
			foreach (ItemPriceConfig itemPrice in config.ItemPrices) {
				if (itemPrice.Gems == 0) {
					error = "'ItemPrices' contains a price of 0 gems";

					return false;
				}

				EAssetType? type = null;

				if (!string.IsNullOrWhiteSpace(itemPrice.Type)) {
					if (!EnumNameParser.TryParseAssetType(itemPrice.Type, out EAssetType parsedType)) {
						error = $"'ItemPrices' contains unknown item type '{itemPrice.Type}'. Valid values: {EnumNameParser.AssetTypeNames}";

						return false;
					}

					type = parsedType;
				}

				CompiledItemPrice compiled = new() {
					Type = type,
					Gems = itemPrice.Gems
				};

				if (itemPrice.AppIDs != null) {
					foreach (uint appID in itemPrice.AppIDs) {
						if (appID == 0) {
							error = "'ItemPrices' contains an invalid AppID 0";

							return false;
						}

						compiled.AppIDs.Add(appID);
					}
				}

				if (itemPrice.Names != null) {
					foreach (string name in itemPrice.Names) {
						if (string.IsNullOrWhiteSpace(name)) {
							error = "'ItemPrices' contains an empty item name";

							return false;
						}

						compiled.Names.Add(name.Trim());
					}
				}

				// An entry without any filter would price every item in every trade, which is never what a user means.
				if (!compiled.Type.HasValue && (compiled.AppIDs.Count == 0) && (compiled.Names.Count == 0)) {
					error = "'ItemPrices' contains an entry without any 'Type', 'AppIDs' or 'Names'";

					return false;
				}

				itemPrices.Add(compiled);
			}
		}

		decimal valueFactor = config.Value?.Factor ?? 1;

		if (valueFactor <= 0) {
			error = $"'Value.Factor' must be greater than zero, but is {valueFactor}";

			return false;
		}

		HashSet<EAssetType> gooTypes = [];
		bool gooAllTypes = false;

		if (config.Value?.GooTypes != null) {
			foreach (string typeName in config.Value.GooTypes) {
				if (string.IsNullOrWhiteSpace(typeName)) {
					error = "'Value.GooTypes' contains an empty item type";

					return false;
				}

				if (string.Equals(typeName.Trim(), "All", StringComparison.OrdinalIgnoreCase)) {
					gooAllTypes = true;

					continue;
				}

				if (!EnumNameParser.TryParseAssetType(typeName, out EAssetType gooType)) {
					error = $"'Value.GooTypes' contains unknown item type '{typeName}'. Valid values: All, {EnumNameParser.AssetTypeNames}";

					return false;
				}

				// Types Steam does not convert into gems would silently never resolve, which would make the rule
				// never match at all.
				if (!GemValueResolver.TryGetGooKind(gooType, out _, out _)) {
					error = $"'Value.GooTypes' contains '{typeName}', which has no Steam gem value; use a type such as ProfileBackground or Emoticon";

					return false;
				}

				if (!gooTypes.Add(gooType)) {
					error = $"'Value.GooTypes' contains more than one entry for '{gooType}'";

					return false;
				}
			}
		}

		// Steam's value is a market estimate, so a rule that relies on it only takes a trade that is actually worth more.
		// Configured prices are exact prices instead, which are paid to the gem.
		bool valueStrict = config.Value?.Strict ?? (gooAllTypes || (gooTypes.Count > 0));

		bool valueBuy = true;

		if (!string.IsNullOrWhiteSpace(config.Value?.Direction)) {
			string direction = config.Value.Direction.Trim();

			if (string.Equals(direction, "Buy", StringComparison.OrdinalIgnoreCase)) {
				valueBuy = true;
			} else if (string.Equals(direction, "Sell", StringComparison.OrdinalIgnoreCase)) {
				valueBuy = false;
			} else {
				error = $"'Value.Direction' contains unknown value '{direction}'. Valid values: Buy, Sell";

				return false;
			}
		}

		uint tradeHoldMinDays = config.TradeHold?.MinDays ?? 0;
		uint tradeHoldMaxDays = config.TradeHold?.MaxDays ?? 15;
		bool hasTradeHoldRequirement = (config.TradeHold?.MinDays != null) || (config.TradeHold?.MaxDays != null);

		if (hasTradeHoldRequirement) {
			if (tradeHoldMaxDays > 15) {
				error = $"'TradeHold.MaxDays' must not be greater than 15, but is {tradeHoldMaxDays}";

				return false;
			}

			if (tradeHoldMinDays > tradeHoldMaxDays) {
				error = $"'TradeHold.MinDays' ({tradeHoldMinDays}) is greater than 'TradeHold.MaxDays' ({tradeHoldMaxDays})";

				return false;
			}
		}

		decimal marketMinProfit = config.MarketProfit?.MinProfit ?? 0;
		decimal marketMinProfitPercent = config.MarketProfit?.MinProfitPercent ?? 0;

		if (marketMinProfit < 0) {
			error = $"'MarketProfit.MinProfit' must not be negative, but is {marketMinProfit}";

			return false;
		}

		if (marketMinProfitPercent < 0) {
			error = $"'MarketProfit.MinProfitPercent' must not be negative, but is {marketMinProfitPercent}";

			return false;
		}

		// A 'Value' block without anything to price would compare nothing and match every trade, which is never what
		// the author meant.
		if ((config.Value != null) && (gemPrices.Count == 0) && ((config.GemsPerCard ?? 0) == 0) && (itemPrices.Count == 0) && !gooAllTypes && (gooTypes.Count == 0)) {
			error = "'Value' is set but the rule prices nothing; add 'GemsPerCard', 'GemPrices', 'ItemPrices' or 'Value.GooTypes'";

			return false;
		}

		// A market rule without a margin would accept every trade that is priced at all, including losing ones.
		if ((config.MarketProfit != null) && (marketMinProfit == 0) && (marketMinProfitPercent == 0)) {
			error = "'MarketProfit' needs 'MinProfit' or 'MinProfitPercent' greater than zero";

			return false;
		}

		if (!MarketCurrency.TryParse(config.MarketProfit?.Currency, out int marketCurrency, out bool marketCurrencyFollowsWallet, out string? currencyError)) {
			error = currencyError;

			return false;
		}

		uint marketMinVolume = config.MarketProfit?.MinVolume ?? 10;

		decimal marketMaxUnitPrice = config.MarketProfit?.MaxUnitPrice ?? 0;

		if (marketMaxUnitPrice < 0) {
			error = $"'MarketProfit.MaxUnitPrice' must not be negative, but is {marketMaxUnitPrice}";

			return false;
		}

		rule = new CompiledTradeRule {
			Action = action,
			Logic = logic,
			TheirItems = theirItems,
			MyItems = myItems,
			Description = config.Description?.Trim() ?? string.Empty,
			GemsPerCard = config.GemsPerCard ?? 0,
			GemPrices = gemPrices,
			CardEquivalents = cardEquivalents,
			ItemPrices = itemPrices,
			ValueFactor = valueFactor,
			ValueOffset = config.Value?.Offset ?? 0,
			GooTypes = gooTypes,
			GooAllTypes = gooAllTypes,
			ValueStrict = valueStrict,
			ValueBuy = valueBuy,
			MarketMinProfit = marketMinProfit,
			MarketMinProfitPercent = marketMinProfitPercent,
			MarketCurrency = marketCurrency,
			MarketCurrencyFollowsWallet = marketCurrencyFollowsWallet,
			MarketMinVolume = marketMinVolume,
			MarketMaxUnitPrice = marketMaxUnitPrice,
			TradeHoldMinDays = tradeHoldMinDays,
			TradeHoldMaxDays = tradeHoldMaxDays,
			HasTradeHoldRequirement = hasTradeHoldRequirement
		};

		return true;
	}

	private static List<CompiledItemConditions>? CompileItemConditions(IReadOnlyList<ItemConditionsConfig>? config, string path, out string? error) {
		error = null;

		if (config == null) {
			return [];
		}

		if (config.Count == 0) {
			error = $"'{path}' is an empty list; remove it to leave that side unconstrained.";

			return null;
		}

		List<CompiledItemConditions> result = [];

		for (int index = 0; index < config.Count; index++) {
			CompiledItemConditions? group = CompileItemConditions(config[index], $"{path}[{index}]", out string? groupError);

			if (group == null) {
				error = groupError;

				return null;
			}

			// An entry without conditions would match every item and silently void the coverage check of a basket, so a
			// list has to describe actual items in every entry. A single condition object is still allowed to be empty.
			if ((config.Count > 1) && group.IsEmpty) {
				error = $"'{path}[{index}]' has no conditions; every entry of a list has to describe some items.";

				return null;
			}

			result.Add(group);
		}

		return result;
	}

	private static CompiledItemConditions? CompileItemConditions(ItemConditionsConfig? config, string path, out string? error) {
		error = null;

		if (config == null) {
			return new CompiledItemConditions();
		}

		uint minAmount = config.MinAmount ?? 0;
		uint maxAmount = config.MaxAmount ?? uint.MaxValue;

		if (minAmount > maxAmount) {
			error = $"'{path}.MinAmount' ({minAmount}) is greater than '{path}.MaxAmount' ({maxAmount})";

			return null;
		}

		CompiledItemConditions result = new() {
			MinAmount = minAmount,
			MaxAmount = maxAmount
		};

		if (config.AppIDs != null) {
			foreach (uint appID in config.AppIDs) {
				if (appID == 0) {
					error = $"'{path}.AppIDs' contains an invalid AppID 0";

					return null;
				}

				result.AppIDs.Add(appID);
			}
		}

		if (config.ExcludedAppIDs != null) {
			foreach (uint appID in config.ExcludedAppIDs) {
				if (appID == 0) {
					error = $"'{path}.ExcludedAppIDs' contains an invalid AppID 0";

					return null;
				}

				result.ExcludedAppIDs.Add(appID);
			}
		}

		if (config.Types != null) {
			foreach (string typeName in config.Types) {
				if (!EnumNameParser.TryParseAssetType(typeName, out EAssetType type)) {
					error = $"'{path}.Types' contains unknown item type '{typeName}'. Valid values: {EnumNameParser.AssetTypeNames}";

					return null;
				}

				result.Types.Add(type);
			}
		}

		if (config.Rarities != null) {
			foreach (string rarityName in config.Rarities) {
				if (!EnumNameParser.TryParseCommunityRarity(rarityName, out EAssetRarity rarity)) {
					error = $"'{path}.Rarities' contains '{rarityName}', which is not a Steam Community item rarity. Valid values: {EnumNameParser.CommunityRarityNames}. Use 'CustomRarities' for tiers that belong to other inventories.";

					return null;
				}

				result.Rarities.Add(rarity);
			}
		}

		if (config.CustomRarities != null) {
			foreach (string rarityName in config.CustomRarities) {
				if (!EnumNameParser.TryParseCustomRarity(rarityName, out EAssetRarity rarity)) {
					error = $"'{path}.CustomRarities' contains unknown item rarity '{rarityName}'. Valid values: {EnumNameParser.CustomRarityNames}";

					return null;
				}

				result.Rarities.Add(rarity);
			}
		}

		if (config.SetSizes != null) {
			foreach (byte setSize in config.SetSizes) {
				if (setSize == 0) {
					error = $"'{path}.SetSizes' contains an invalid card set size 0";

					return null;
				}

				result.SetSizes.Add(setSize);
			}
		}

		if (config.Names != null) {
			foreach (string name in config.Names) {
				if (string.IsNullOrWhiteSpace(name)) {
					error = $"'{path}.Names' contains an empty item name";

					return null;
				}

				result.Names.Add(name.Trim());
			}
		}

		return result;
	}
}

/// <summary>
///     Parses enum names leniently enough for hand-written config: case, spaces, hyphens and underscores are
///     ignored, so both <c>TradingCard</c> and <c>Trading Card</c> work. Numeric values are rejected on purpose.
/// </summary>
internal static class EnumNameParser {
	internal static readonly string AssetTypeNames = string.Join(", ", Enum.GetNames<EAssetType>());
	internal static readonly string AssetRarityNames = string.Join(", ", Enum.GetNames<EAssetRarity>());
	internal const string CommunityRarityNames = "Common, Uncommon, Rare";
	internal const string CustomRarityNames = "Mythical, Legendary, Ancient, Immortal, Arcana, Unusual";

	internal static bool TryParseCommunityRarity(string? value, out EAssetRarity result) {
		result = default;

		return TryParseAssetRarity(value, out result) && (result is EAssetRarity.Common or EAssetRarity.Uncommon or EAssetRarity.Rare);
	}

	internal static bool TryParseCustomRarity(string? value, out EAssetRarity result) {
		result = default;

		return TryParseAssetRarity(value, out result) && (result is not (EAssetRarity.Common or EAssetRarity.Uncommon or EAssetRarity.Rare));
	}

	internal static bool TryParseAssetType(string? value, out EAssetType result) => TryParse(value, out result);

	internal static bool TryParseAssetRarity(string? value, out EAssetRarity result) => TryParse(value, out result);

	private static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum {
		result = default;

		if (string.IsNullOrWhiteSpace(value)) {
			return false;
		}

		string normalized = Normalize(value);

		// Enum.TryParse would happily accept "753" or "TradingCard,Emoticon"; those are not valid config values.
		if ((normalized.Length == 0) || normalized.Any(static character => !char.IsLetter(character))) {
			return false;
		}

		return Enum.TryParse(normalized, true, out result) && Enum.IsDefined(result);
	}

	private static string Normalize(string value) => value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
}
