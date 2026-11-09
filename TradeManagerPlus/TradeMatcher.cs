using System.Collections.Generic;
using ArchiSteamFarm.Steam.Data;

namespace TradeManagerPlus;

/// <summary>
///     Pure matching logic, intentionally free of ASF runtime objects so that it can be unit-tested.
/// </summary>
internal static class TradeMatcher {
	internal static bool IsMatch(CompiledTradeRule rule, IReadOnlyCollection<Asset> itemsToReceive, IReadOnlyCollection<Asset> itemsToGive) => IsSideMatch(rule.TheirItems, itemsToReceive, rule.Logic) && IsSideMatch(rule.MyItems, itemsToGive, rule.Logic);

	private static bool IsSideMatch(CompiledItemConditions conditions, IReadOnlyCollection<Asset> items, TradeRuleLogic logic) {
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
				if (!conditions.MatchesItem(item)) {
					return false;
				}
			}

			return true;
		}

		foreach (Asset item in items) {
			if (conditions.MatchesItem(item)) {
				return true;
			}
		}

		return false;
	}
}
