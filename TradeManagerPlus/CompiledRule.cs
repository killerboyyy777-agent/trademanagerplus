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
	public HashSet<EAssetType> Types { get; } = [];
	public HashSet<EAssetRarity> Rarities { get; } = [];
	public uint MinAmount { get; init; }
	public uint MaxAmount { get; init; } = uint.MaxValue;

	/// <summary>
	///     True when this side has no constraints at all, in which case it matches anything.
	/// </summary>
	internal bool IsEmpty => (AppIDs.Count == 0) && (Types.Count == 0) && (Rarities.Count == 0) && (MinAmount == 0) && (MaxAmount == uint.MaxValue);

	/// <summary>
	///     True when this side has no per-item constraints, only an amount constraint.
	/// </summary>
	internal bool HasNoItemFilters => (AppIDs.Count == 0) && (Types.Count == 0) && (Rarities.Count == 0);

	internal bool MatchesItem(Asset item) => ((AppIDs.Count == 0) || AppIDs.Contains(item.RealAppID)) && ((Types.Count == 0) || Types.Contains(item.Type)) && ((Rarities.Count == 0) || Rarities.Contains(item.Rarity));

	internal string Describe() {
		if (IsEmpty) {
			return "anything";
		}

		List<string> parts = [];

		if (AppIDs.Count > 0) {
			parts.Add($"appIDs {string.Join(", ", AppIDs.Order())}");
		}

		if (Types.Count > 0) {
			parts.Add($"types {string.Join(", ", Types.Order().Select(static type => type.ToString()))}");
		}

		if (Rarities.Count > 0) {
			parts.Add($"rarities {string.Join(", ", Rarities.Order().Select(static rarity => rarity.ToString()))}");
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
	public required CompiledItemConditions TheirItems { get; init; }
	public required CompiledItemConditions MyItems { get; init; }
	public string Description { get; init; } = string.Empty;
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

		CompiledItemConditions? theirItems = CompileItemConditions(config.Conditions?.TheirItems, "TheirItems", out error);

		if (theirItems == null) {
			return false;
		}

		CompiledItemConditions? myItems = CompileItemConditions(config.Conditions?.MyItems, "MyItems", out error);

		if (myItems == null) {
			return false;
		}

		rule = new CompiledTradeRule {
			Action = action,
			Logic = logic,
			TheirItems = theirItems,
			MyItems = myItems,
			Description = config.Description?.Trim() ?? string.Empty
		};

		return true;
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
				if (!EnumNameParser.TryParseAssetRarity(rarityName, out EAssetRarity rarity)) {
					error = $"'{path}.Rarities' contains unknown item rarity '{rarityName}'. Valid values: {EnumNameParser.AssetRarityNames}";

					return null;
				}

				result.Rarities.Add(rarity);
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
