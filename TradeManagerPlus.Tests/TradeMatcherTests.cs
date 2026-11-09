using System;
using System.Linq;
using ArchiSteamFarm.Helpers.Json;
using ArchiSteamFarm.Steam.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TradeManagerPlus.Tests;

[TestClass]
public sealed class TradeMatcherTests {
	private const string AcceptCardsJson =
		"""
		{
			"Type": "Accept",
			"Conditions": {
				"TheirItems": { "Types": ["Trading Card"] },
				"MyItems": { "Types": ["Trading Card"] }
			},
			"Description": "1:1 trading cards"
		}
		""";

	[TestMethod]
	public void TradingCardNameWithSpaceMatches() {
		CompiledTradeRule rule = CompileSingle(AcceptCardsJson);
		Asset card = CreateAsset(EAssetType.TradingCard);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [card], [card]));
	}

	[TestMethod]
	public void WrongItemTypeDoesNotMatch() {
		CompiledTradeRule rule = CompileSingle(AcceptCardsJson);
		Asset card = CreateAsset(EAssetType.TradingCard);
		Asset boosterPack = CreateAsset(EAssetType.BoosterPack);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [boosterPack], [card]));
	}

	[TestMethod]
	public void AllLogicRequiresEveryItemToMatch() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": { "TheirItems": { "Types": ["Trading Card"] } },
				"ConditionLogic": "All"
			}
			"""
		);

		Asset card = CreateAsset(EAssetType.TradingCard);
		Asset boosterPack = CreateAsset(EAssetType.BoosterPack);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [card, boosterPack], []));
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [card], []));
	}

	[TestMethod]
	public void AnyLogicRequiresOnlyOneItemToMatch() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": { "TheirItems": { "Types": ["Trading Card"] } },
				"ConditionLogic": "any"
			}
			"""
		);

		Asset card = CreateAsset(EAssetType.TradingCard);
		Asset boosterPack = CreateAsset(EAssetType.BoosterPack);

		Assert.AreEqual(TradeRuleLogic.Any, rule.Logic);
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [boosterPack, card], []));
		Assert.IsFalse(TradeMatcher.IsMatch(rule, [boosterPack], []));
	}

	[TestMethod]
	public void EmptyConditionsMatchAnEmptyTrade() {
		CompiledTradeRule rule = CompileSingle("""{ "Type": "Accept", "Description": "anything" }""");

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [], []));
	}

	[TestMethod]
	public void AmountRangeIsRespected() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": { "TheirItems": { "MinAmount": 1, "MaxAmount": 2 } }
			}
			"""
		);

		Asset singleCard = CreateAsset(EAssetType.TradingCard);
		Asset twoCards = CreateAsset(EAssetType.TradingCard, amount: 2);
		Asset threeCards = CreateAsset(EAssetType.TradingCard, amount: 3);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [singleCard], []));
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [twoCards], []));
		Assert.IsFalse(TradeMatcher.IsMatch(rule, [threeCards], []));
		Assert.IsFalse(TradeMatcher.IsMatch(rule, [], []));
	}

	[TestMethod]
	public void RarityAliasIsCanonicalized() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": { "TheirItems": { "Rarities": ["Epic"] } }
			}
			"""
		);

		Assert.Contains(EAssetRarity.Mythical, rule.TheirItems.Rarities);
	}

	[TestMethod]
	public void RarityMatches() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": { "TheirItems": { "Rarities": ["Rare"] } }
			}
			"""
		);

		Asset rareItem = CreateAsset(EAssetType.Emoticon, EAssetRarity.Rare);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [rareItem], []));
	}

	[TestMethod]
	public void GivenItemsConditionsAreChecked() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": { "MyItems": { "Types": ["Trading Card"] } }
			}
			"""
		);

		Asset card = CreateAsset(EAssetType.TradingCard);
		Asset boosterPack = CreateAsset(EAssetType.BoosterPack);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [], [card]));
		Assert.IsFalse(TradeMatcher.IsMatch(rule, [], [boosterPack]));
	}

	[TestMethod]
	public void UnknownItemTypeIsRejected() => AssertCompileFails(
		"""{ "Type": "Accept", "Conditions": { "TheirItems": { "Types": ["NotARealType"] } } }""",
		"unknown item type"
	);

	[TestMethod]
	public void NumericItemTypeIsRejected() => AssertCompileFails(
		"""{ "Type": "Accept", "Conditions": { "TheirItems": { "Types": ["5"] } } }""",
		"unknown item type"
	);

	[TestMethod]
	public void MinAmountGreaterThanMaxAmountIsRejected() => AssertCompileFails(
		"""{ "Type": "Accept", "Conditions": { "TheirItems": { "MinAmount": 3, "MaxAmount": 1 } } }""",
		"greater than"
	);

	[TestMethod]
	public void UnsupportedActionIsRejected() => AssertCompileFails(
		"""{ "Type": "Ignore" }""",
		"unsupported 'Type'"
	);

	[TestMethod]
	public void ConditionLogicIsCaseInsensitive() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": { "TheirItems": { "Types": ["Trading Card"] } },
				"ConditionLogic": "aNy"
			}
			"""
		);

		Assert.AreEqual(TradeRuleLogic.Any, rule.Logic);
	}

	[TestMethod]
	public void RulesKeepConfiguredOrder() {
		PluginConfig config = ParseConfig(
			"""
			{ "Type": "Accept", "Description": "first" },
			{ "Type": "Decline", "Description": "second" }
			"""
		);

		CompiledTradeRule[] rules = config.TradeRules!.Select(static rule => {
			Assert.IsTrue(RuleCompiler.TryCompile(rule, out CompiledTradeRule? compiled, out string? error), error);

			return compiled!;
		}).ToArray();

		Assert.HasCount(2, rules);
		Assert.AreEqual(TradeRuleAction.Accept, rules[0].Action);
		Assert.AreEqual("first", rules[0].Description);
		Assert.AreEqual(TradeRuleAction.Decline, rules[1].Action);
		Assert.AreEqual("second", rules[1].Description);
	}

	[TestMethod]
	public void DisabledConfigIsRead() {
		PluginConfig? config = """{"Enabled": false, "TradeRules": []}""".ToJsonObject<PluginConfig>();

		Assert.IsNotNull(config);
		Assert.IsFalse(config!.Enabled);
	}

	private static void AssertCompileFails(string ruleJson, string expectedErrorFragment) {
		PluginConfig config = ParseConfig(ruleJson);
		TradeRuleConfig ruleConfig = config.TradeRules!.Single();

		bool compiled = RuleCompiler.TryCompile(ruleConfig, out _, out string? error);

		Assert.IsFalse(compiled);
		Assert.IsNotNull(error);
		StringAssert.Contains(error, expectedErrorFragment, StringComparison.Ordinal);
	}

	private static CompiledTradeRule CompileSingle(string ruleJson) {
		PluginConfig config = ParseConfig(ruleJson);
		TradeRuleConfig ruleConfig = config.TradeRules!.Single();

		Assert.IsTrue(RuleCompiler.TryCompile(ruleConfig, out CompiledTradeRule? rule, out string? error), error);

		return rule!;
	}

	private static PluginConfig ParseConfig(string ruleJson) {
		string json = $"{{\"TradeRules\":[{ruleJson}]}}";

		PluginConfig? config = json.ToJsonObject<PluginConfig>();

		Assert.IsNotNull(config);
		Assert.IsNotNull(config.TradeRules);

		return config!;
	}

	private static Asset CreateAsset(EAssetType type, EAssetRarity rarity = EAssetRarity.Common, uint amount = 1) {
		InventoryDescription description = new(appID: Asset.SteamAppID, classID: 1, realAppID: Asset.SteamAppID, type: type, rarity: rarity);

		return new Asset(Asset.SteamAppID, Asset.SteamCommunityContextID, classID: 1, amount, description);
	}
}
