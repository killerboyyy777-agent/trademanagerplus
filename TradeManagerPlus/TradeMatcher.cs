using System;
using System.Collections.Generic;
using System.Linq;
using ArchiSteamFarm.Steam.Data;

namespace TradeManagerPlus;

/// <summary>
///     Pure matching logic, intentionally free of ASF runtime objects so that it can be unit-tested.
/// </summary>
internal static class TradeMatcher {
	internal static bool IsMatch(CompiledTradeRule rule, IReadOnlyCollection<Asset> itemsToReceive, IReadOnlyCollection<Asset> itemsToGive, IReadOnlyDictionary<uint, byte>? cardSetSizes = null, Func<Asset, ulong?>? gooValue = null, Func<Asset, MarketQuote?>? marketPrice = null) =>
		IsSideMatch(rule.TheirItems, itemsToReceive, rule.Logic, cardSetSizes)
		&& IsSideMatch(rule.MyItems, itemsToGive, rule.Logic, cardSetSizes)
		&& IsValueSatisfied(rule, itemsToReceive, itemsToGive, cardSetSizes, gooValue)
		&& IsMarketProfitSatisfied(rule, itemsToReceive, itemsToGive, marketPrice);

	/// <summary>
	///     True when matching the rule requires Steam Community market prices, which the caller has to prefetch.
	/// </summary>
	internal static bool NeedsMarketPrices(CompiledTradeRule rule) => rule.HasMarketRequirement;

	/// <summary>
	///     True when the rule constrains card set sizes, in which case the caller has to resolve them first.
	/// </summary>
	internal static bool NeedsCardSetSizes(CompiledTradeRule rule) =>
		(rule.GemPrices.Count > 0)
		|| rule.TheirItems.Any(static group => group.SetSizes.Count > 0)
		|| rule.MyItems.Any(static group => group.SetSizes.Count > 0);

	/// <summary>
	///     True when matching the rule requires Steam's own gem values, which the caller has to prefetch.
	/// </summary>
	internal static bool NeedsGooValues(CompiledTradeRule rule) => rule.GooAllTypes || (rule.GooTypes.Count > 0);

	internal static bool UsesCardSetSizes(EAssetType type) => type is EAssetType.TradingCard or EAssetType.FoilTradingCard or EAssetType.BoosterPack;

	/// <summary>
	///     Compares the gem value of both sides. Every item on both sides has to have a known value, because a rule that
	///     prices a trade cannot tell whether an unpriced item changes the outcome. Better offers are always accepted.
	/// </summary>
	private static bool IsValueSatisfied(CompiledTradeRule rule, IReadOnlyCollection<Asset> itemsToReceive, IReadOnlyCollection<Asset> itemsToGive, IReadOnlyDictionary<uint, byte>? cardSetSizes, Func<Asset, ulong?>? gooValue) {
		if (!rule.HasValueRequirement) {
			return true;
		}

		ulong? receiveValue = SumValue(rule, itemsToReceive, cardSetSizes, gooValue);
		ulong? giveValue = SumValue(rule, itemsToGive, cardSetSizes, gooValue);

		if (!receiveValue.HasValue || !giveValue.HasValue) {
			return false;
		}

		// A priced trade always moves items in both directions. Without this check an offer that gives nothing back
		// prices its empty side at zero and passes every comparison below.
		if ((itemsToReceive.Count == 0) || (itemsToGive.Count == 0)) {
			return false;
		}

		// A rule built on Steam's own values accepts only offers that are actually worth more, while a configured price is
		// paid to the gem.
		if (rule.ValueBuy) {
			// The bot pays for what it receives, so the value it gives has to cover the price of the value it receives.
			decimal price = (receiveValue.Value * rule.ValueFactor) + rule.ValueOffset;

			return rule.ValueStrict ? giveValue.Value > price : price <= giveValue.Value;
		}

		// The bot is paid for what it gives, so the value it receives has to cover the value it gives.
		decimal requiredValue = (giveValue.Value * rule.ValueFactor) + rule.ValueOffset;

		return rule.ValueStrict ? receiveValue.Value > requiredValue : requiredValue <= receiveValue.Value;
	}

	/// <summary>
	///     Requires the trade to be profitable on the Steam Community market by both a flat and a percentage margin.
	/// </summary>
	/// <remarks>
	///     Every item on both sides has to have a price that passes the rule's trust checks. An offer that contains
	///     anything this plugin cannot price is rejected, because ringing in one unpriced item is exactly how a bad trade
	///     would be dressed up as a good one.
	/// </remarks>
	private static bool IsMarketProfitSatisfied(CompiledTradeRule rule, IReadOnlyCollection<Asset> itemsToReceive, IReadOnlyCollection<Asset> itemsToGive, Func<Asset, MarketQuote?>? marketPrice) {
		if (!rule.HasMarketRequirement) {
			return true;
		}

		if (marketPrice == null) {
			return false;
		}

		if (!TrySumMarketValue(rule, itemsToReceive, marketPrice, out decimal receiveValue) || !TrySumMarketValue(rule, itemsToGive, marketPrice, out decimal giveValue)) {
			return false;
		}

		decimal percentProfit = giveValue * rule.MarketMinProfitPercent / 100m;
		decimal requiredProfit = rule.MarketMinProfit > percentProfit ? rule.MarketMinProfit : percentProfit;

		return (receiveValue - giveValue) >= requiredProfit;
	}

	private static bool TrySumMarketValue(CompiledTradeRule rule, IReadOnlyCollection<Asset> items, Func<Asset, MarketQuote?> marketPrice, out decimal total) {
		total = 0;

		foreach (Asset item in items) {
			if (marketPrice(item) is not { } quote) {
				return false;
			}

			// A price that Steam reports for something barely trading is a price somebody can move on their own, and a
			// price above the configured cap is how a single item is used to make a terrible trade look great.
			if ((quote.Price <= 0) || (quote.Volume < rule.MarketMinVolume)) {
				return false;
			}

			if ((rule.MarketMaxUnitPrice > 0) && (quote.Price > rule.MarketMaxUnitPrice)) {
				return false;
			}

			total += quote.Price * item.Amount;
		}

		return true;
	}

	private static ulong? SumValue(CompiledTradeRule rule, IReadOnlyCollection<Asset> items, IReadOnlyDictionary<uint, byte>? cardSetSizes, Func<Asset, ulong?>? gooValue) {
		ulong total = 0;

		foreach (Asset item in items) {
			if (!TryGetUnitValue(rule, item, cardSetSizes, gooValue, out ulong unitValue)) {
				return null;
			}

			if ((unitValue > 0) && (item.Amount > (ulong.MaxValue / unitValue))) {
				return null;
			}

			ulong itemValue = unitValue * item.Amount;

			if (total > (ulong.MaxValue - itemValue)) {
				return null;
			}

			total += itemValue;
		}

		return total;
	}

	private static bool TryGetUnitValue(CompiledTradeRule rule, Asset item, IReadOnlyDictionary<uint, byte>? cardSetSizes, Func<Asset, ulong?>? gooValue, out ulong value) {
		value = 0;

		// A gem stack carries its gem count in Amount, so one unit of it is worth exactly one gem.
		if (item.Type == EAssetType.SteamGems) {
			value = 1;

			return true;
		}

		foreach (CompiledItemPrice price in rule.ItemPrices) {
			if (price.Matches(item)) {
				value = price.Gems;

				return true;
			}
		}

		if (TryGetCardPrice(rule, item, cardSetSizes, out value)) {
			return true;
		}

		if (gooValue != null && (rule.GooAllTypes || rule.GooTypes.Contains(item.Type))) {
			ulong? goo = gooValue(item);

			if (goo.HasValue && (goo.Value > 0)) {
				value = goo.Value;

				return true;
			}
		}

		return false;
	}

	private static bool TryGetCardPrice(CompiledTradeRule rule, Asset item, IReadOnlyDictionary<uint, byte>? cardSetSizes, out ulong price) {
		price = 0;

		uint cards;

		if (!rule.CardEquivalents.TryGetValue(item.Type, out cards)) {
			if (!UsesCardSetSizes(item.Type)) {
				return false;
			}

			cards = 1;
		}

		uint pricePerCard;

		if (rule.GemPrices.Count > 0) {
			// Pricing by card set size is only possible when the size of the item's set is known.
			if ((cardSetSizes == null) || !cardSetSizes.TryGetValue(item.RealAppID, out byte setSize)) {
				return false;
			}

			pricePerCard = GetPriceForSetSize(rule.GemPrices, setSize);
		} else {
			pricePerCard = rule.GemsPerCard;
		}

		if (pricePerCard == 0) {
			return false;
		}

		if (cards > (ulong.MaxValue / pricePerCard)) {
			return false;
		}

		price = (ulong) pricePerCard * cards;

		return true;
	}

	/// <summary>
	///     Finds the price for a card set size, using the closest configured size that is not larger. Sizes below the
	///     table use the smallest configured price and sizes above it use the largest one.
	/// </summary>
	private static uint GetPriceForSetSize(IReadOnlyList<KeyValuePair<byte, uint>> prices, byte setSize) {
		uint result = prices[0].Value;

		foreach (KeyValuePair<byte, uint> price in prices) {
			if (price.Key > setSize) {
				break;
			}

			result = price.Value;
		}

		return result;
	}

	/// <summary>
	///     Matches one side of the trade against its condition groups.
	/// </summary>
	private static bool IsSideMatch(IReadOnlyList<CompiledItemConditions> conditions, IReadOnlyCollection<Asset> items, TradeRuleLogic logic, IReadOnlyDictionary<uint, byte>? cardSetSizes) {
		if (conditions.Count == 0) {
			return true;
		}

		// A side written as one condition keeps its original meaning exactly, including the amount window covering the whole
		// side, so no configuration written before baskets existed can change behaviour.
		if (conditions.Count == 1) {
			return IsSingleConditionMatch(conditions[0], items, logic, cardSetSizes);
		}

		// 'Any' means the side has to look like at least one of the configured shapes.
		if (logic == TradeRuleLogic.Any) {
			return conditions.Any(group => IsGroupMatch(group, items, cardSetSizes));
		}

		// 'All' means every shape has to be present in its own amount, and every item has to belong to some shape, so a
		// trade cannot smuggle in an item that no group asked for.
		foreach (CompiledItemConditions group in conditions) {
			if (!IsGroupMatch(group, items, cardSetSizes)) {
				return false;
			}
		}

		foreach (Asset item in items) {
			if (!conditions.Any(group => group.MatchesItem(item, cardSetSizes))) {
				return false;
			}
		}

		return true;
	}

	/// <summary>
	///     Matches a side that has only one condition, with the amount window counted over every item on that side. This is
	///     the behaviour the plugin had before condition lists existed.
	/// </summary>
	private static bool IsSingleConditionMatch(CompiledItemConditions conditions, IReadOnlyCollection<Asset> items, TradeRuleLogic logic, IReadOnlyDictionary<uint, byte>? cardSetSizes) {
		if (conditions.IsEmpty) {
			return true;
		}

		ulong totalAmount = 0;

		foreach (Asset item in items) {
			totalAmount += item.Amount;
		}

		if ((totalAmount < conditions.MinAmount) || (totalAmount > conditions.MaxAmount)) {
			return false;
		}

		// Amount-only conditions are fully handled above, including the case of an empty side with MinAmount 0.
		if (conditions.HasNoItemFilters) {
			return true;
		}

		if (items.Count == 0) {
			return false;
		}

		if (logic == TradeRuleLogic.All) {
			foreach (Asset item in items) {
				if (!conditions.MatchesItem(item, cardSetSizes)) {
					return false;
				}
			}

			return true;
		}

		foreach (Asset item in items) {
			if (conditions.MatchesItem(item, cardSetSizes)) {
				return true;
			}
		}

		return false;
	}

	/// <summary>
	///     Matches a single condition group against a side. The amount window is checked over the items that belong to
	///     this group only, so a basket can ask for one key and two cards from separate groups.
	/// </summary>
	private static bool IsGroupMatch(CompiledItemConditions group, IReadOnlyCollection<Asset> items, IReadOnlyDictionary<uint, byte>? cardSetSizes) {
		if (group.IsEmpty) {
			return true;
		}

		ulong matchedAmount = 0;
		bool anyItem = false;

		foreach (Asset item in items) {
			if (!group.HasNoItemFilters && !group.MatchesItem(item, cardSetSizes)) {
				continue;
			}

			anyItem = true;
			matchedAmount += item.Amount;
		}

		// A group that filters items needs at least one item of that shape. An amount-only group is satisfied by an empty
		// side as long as its window allows zero.
		if (!anyItem && !group.HasNoItemFilters) {
			return false;
		}

		return (matchedAmount >= group.MinAmount) && (matchedAmount <= group.MaxAmount);
	}
}
