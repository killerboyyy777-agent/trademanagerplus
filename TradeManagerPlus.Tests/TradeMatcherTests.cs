using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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

	private const string AcceptCardSetJson =
		"""
		{
			"Type": "Accept",
			"Conditions": {
				"TheirItems": { "Types": ["TradingCard"], "SetSizes": [8], "MinAmount": 8 },
				"MyItems": { "Types": ["SteamGems"], "MinAmount": 600 }
			},
			"Description": "Accept an 8-card set for at least 600 gems."
		}
		""";

	private const string AcceptPricedCardSetJson =
		"""
		{
			"Type": "Accept",
			"Conditions": {
				"TheirItems": { "Types": ["TradingCard"], "SetSizes": [8] },
				"MyItems": { "Types": ["SteamGems"] }
			},
			"GemsPerCard": 10,
			"Description": "Buy any card from an 8-card set at 10 gems each, or better."
		}
		""";

	private const string GemPriceTableJson =
		"""
		{
			"Type": "Accept",
			"Conditions": {
				"TheirItems": { "Types": ["TradingCard"] },
				"MyItems": { "Types": ["SteamGems"] }
			},
			"GemPrices": [
				{ "SetSize": 5, "Gems": 70 },
				{ "SetSize": 6, "Gems": 58 },
				{ "SetSize": 8, "Gems": 43 },
				{ "SetSize": 15, "Gems": 23 }
			],
			"Description": "Buy cards at set-based gem prices."
		}
		""";

	private const string GemPriceTableWithBoosterJson =
		"""
		{
			"Type": "Accept",
			"Conditions": {
				"TheirItems": { "Types": ["TradingCard", "BoosterPack"] },
				"MyItems": { "Types": ["SteamGems"] }
			},
			"GemPrices": [
				{ "SetSize": 5, "Gems": 70 },
				{ "SetSize": 8, "Gems": 43 }
			],
			"CardEquivalents": [ { "Type": "BoosterPack", "Cards": 3 } ],
			"Description": "Buy cards and booster packs at set-based gem prices."
		}
		""";

	private const string SellBackgroundsForGooJson =
		"""
		{
			"Type": "Accept",
			"Conditions": {
				"TheirItems": { "Types": ["SteamGems"] },
				"MyItems": { "Types": ["ProfileBackground"] }
			},
			"Value": { "GooTypes": ["ProfileBackground"], "Direction": "Sell" },
			"Description": "Sell backgrounds for more than their Steam gem value."
		}
		""";

	private const string ItemPriceJson =
		"""
		{
			"Type": "Accept",
			"Conditions": {
				"TheirItems": { "Types": ["ProfileBackground"] },
				"MyItems": { "Types": ["SteamGems"] }
			},
			"ItemPrices": [ { "Type": "ProfileBackground", "Gems": 3 } ],
			"Description": "Buy backgrounds at 3 gems each."
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

		Assert.Contains(EAssetRarity.Mythical, rule.TheirItems[0].Rarities);
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

	[TestMethod]
	public void CardSetOfRequestedSizeMatches() {
		CompiledTradeRule rule = CompileSingle(AcceptCardSetJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 8, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 600);
		Dictionary<uint, byte> setSizes = new() { [440] = 8 };

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [cards], [gems], setSizes));
	}

	[TestMethod]
	public void CardSetOfDifferentSizeDoesNotMatch() {
		CompiledTradeRule rule = CompileSingle(AcceptCardSetJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 8, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 600);
		Dictionary<uint, byte> setSizes = new() { [440] = 5 };

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [cards], [gems], setSizes));
	}

	[TestMethod]
	public void UnknownCardSetSizeDoesNotMatch() {
		CompiledTradeRule rule = CompileSingle(AcceptCardSetJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 8, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 600);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [cards], [gems], new Dictionary<uint, byte>()));
		Assert.IsFalse(TradeMatcher.IsMatch(rule, [cards], [gems]));
	}

	[TestMethod]
	public void TooFewGemsDoNotMatch() {
		CompiledTradeRule rule = CompileSingle(AcceptCardSetJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 8, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 599);
		Dictionary<uint, byte> setSizes = new() { [440] = 8 };

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [cards], [gems], setSizes));
	}

	[TestMethod]
	public void CardSetSizeZeroIsRejected() => AssertCompileFails(
		"""{ "Type": "Accept", "Conditions": { "TheirItems": { "SetSizes": [0] } } }""",
		"SetSizes"
	);

	[TestMethod]
	public void PricedCardsMatchAtExactPrice() {
		CompiledTradeRule rule = CompileSingle(AcceptPricedCardSetJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 3, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 30);
		Dictionary<uint, byte> setSizes = new() { [440] = 8 };

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [cards], [gems], setSizes));
	}

	[TestMethod]
	public void PricedCardsAcceptBetterOffers() {
		CompiledTradeRule rule = CompileSingle(AcceptPricedCardSetJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 3, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 250);
		Dictionary<uint, byte> setSizes = new() { [440] = 8 };

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [cards], [gems], setSizes));
	}

	[TestMethod]
	public void PricedCardsRejectLowerOffers() {
		CompiledTradeRule rule = CompileSingle(AcceptPricedCardSetJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 3, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 29);
		Dictionary<uint, byte> setSizes = new() { [440] = 8 };

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [cards], [gems], setSizes));
	}

	[TestMethod]
	public void PricedCardsRequirePriceablePayment() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": { "TheirItems": { "Types": ["TradingCard"] } },
				"GemsPerCard": 10
			}
			"""
		);

		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 2);
		Asset unpriceable = CreateAsset(EAssetType.Emoticon, amount: 1000);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [cards], [unpriceable]));
	}

	[TestMethod]
	public void PricedCardsAcceptOtherCardsAsPayment() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": { "TheirItems": { "Types": ["TradingCard"] } },
				"GemsPerCard": 10
			}
			"""
		);

		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 2);
		Asset payment = CreateAsset(EAssetType.TradingCard, amount: 1000);

		// A card is worth a card, so this pays far more than the price of two cards.
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [cards], [payment]));
	}

	[TestMethod]
	public void PricedCardsRequireSomethingToSell() {
		CompiledTradeRule rule = CompileSingle(AcceptPricedCardSetJson);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [], [CreateAsset(EAssetType.SteamGems, amount: 1000)]));
	}

	[TestMethod]
	public void SetPriceTableMatchesAtExactPrice() {
		CompiledTradeRule rule = CompileSingle(GemPriceTableJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 2, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 86);
		Dictionary<uint, byte> setSizes = new() { [440] = 8 };

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [cards], [gems], setSizes));
	}

	[TestMethod]
	public void SetPriceTableRejectsLowerOffer() {
		CompiledTradeRule rule = CompileSingle(GemPriceTableJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 2, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 85);
		Dictionary<uint, byte> setSizes = new() { [440] = 8 };

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [cards], [gems], setSizes));
	}

	[TestMethod]
	public void SetPriceTableAcceptsBetterOffer() {
		CompiledTradeRule rule = CompileSingle(GemPriceTableJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 2, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 500);
		Dictionary<uint, byte> setSizes = new() { [440] = 8 };

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [cards], [gems], setSizes));
	}

	[TestMethod]
	public void SetSmallerThanPriceTableUsesSmallestSetPrice() {
		CompiledTradeRule rule = CompileSingle(GemPriceTableJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 1, realAppID: 440);
		Dictionary<uint, byte> setSizes = new() { [440] = 3 };

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [cards], [CreateAsset(EAssetType.SteamGems, amount: 69)], setSizes));
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [cards], [CreateAsset(EAssetType.SteamGems, amount: 70)], setSizes));
	}

	[TestMethod]
	public void SetLargerThanPriceTableUsesLargestSetPrice() {
		CompiledTradeRule rule = CompileSingle(GemPriceTableJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 1, realAppID: 440);
		Dictionary<uint, byte> setSizes = new() { [440] = 20 };

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [cards], [CreateAsset(EAssetType.SteamGems, amount: 22)], setSizes));
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [cards], [CreateAsset(EAssetType.SteamGems, amount: 23)], setSizes));
	}

	[TestMethod]
	public void SetSizeWithoutPriceDoesNotMatch() {
		CompiledTradeRule rule = CompileSingle(GemPriceTableJson);
		Asset cards = CreateAsset(EAssetType.TradingCard, amount: 1, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 1000);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [cards], [gems], new Dictionary<uint, byte>()));
	}

	[TestMethod]
	public void BoosterPackCountsAsItsConfiguredCards() {
		CompiledTradeRule rule = CompileSingle(GemPriceTableWithBoosterJson);
		Asset boosterPack = CreateAsset(EAssetType.BoosterPack, amount: 1, realAppID: 440);
		Dictionary<uint, byte> setSizes = new() { [440] = 8 };

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [boosterPack], [CreateAsset(EAssetType.SteamGems, amount: 128)], setSizes));
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [boosterPack], [CreateAsset(EAssetType.SteamGems, amount: 129)], setSizes));
	}

	[TestMethod]
	public void ItemWithoutPriceDoesNotMatch() {
		CompiledTradeRule rule = CompileSingle(GemPriceTableJson);
		Asset emoticon = CreateAsset(EAssetType.Emoticon, amount: 1, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 10000);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [emoticon], [gems], new Dictionary<uint, byte> { [440] = 8 }));
	}

	[TestMethod]
	public void ExcludedAppIDDoesNotMatch() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": {
					"TheirItems": { "Types": ["TradingCard"], "ExcludedAppIDs": [730] },
					"MyItems": { "Types": ["SteamGems"] }
				}
			}
			"""
		);

		Asset csCard = CreateAsset(EAssetType.TradingCard, realAppID: 730);
		Asset otherCard = CreateAsset(EAssetType.TradingCard, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 100);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [csCard], [gems]));
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [otherCard], [gems]));
	}

	[TestMethod]
	public void DuplicateGemPriceSetSizeIsRejected() => AssertCompileFails(
		"""
		{
			"Type": "Accept",
			"GemPrices": [ { "SetSize": 8, "Gems": 43 }, { "SetSize": 8, "Gems": 40 } ]
		}
		""",
		"more than one price for card set size 8"
	);

	[TestMethod]
	public void ZeroGemPriceIsRejected() => AssertCompileFails(
		"""
		{
			"Type": "Accept",
			"GemPrices": [ { "SetSize": 8, "Gems": 0 } ]
		}
		""",
		"price of 0 gems"
	);

	[TestMethod]
	public void FlatPriceAndSetPriceTableCannotBeCombined() => AssertCompileFails(
		"""
		{
			"Type": "Accept",
			"GemsPerCard": 10,
			"GemPrices": [ { "SetSize": 8, "Gems": 43 } ]
		}
		""",
		"uses both"
	);

	[TestMethod]
	public void ZeroCardEquivalentIsRejected() => AssertCompileFails(
		"""
		{
			"Type": "Accept",
			"CardEquivalents": [ { "Type": "BoosterPack", "Cards": 0 } ]
		}
		""",
		"card count of 0"
	);

	[TestMethod]
	public void GooValueSellAcceptsOfferAboveValue() {
		CompiledTradeRule rule = CompileSingle(SellBackgroundsForGooJson);
		Asset background = CreateAsset(EAssetType.ProfileBackground, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 41);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [gems], [background], gooValue: BackgroundGoo(40)));
	}

	[TestMethod]
	public void GooValueSellRejectsOfferAtValue() {
		CompiledTradeRule rule = CompileSingle(SellBackgroundsForGooJson);
		Asset background = CreateAsset(EAssetType.ProfileBackground, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 40);

		// Steam values accept only strictly better offers unless the rule asks otherwise.
		Assert.IsFalse(TradeMatcher.IsMatch(rule, [gems], [background], gooValue: BackgroundGoo(40)));
	}

	[TestMethod]
	public void GooValueSellAcceptsOfferAtValueWhenNotStrict() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": {
					"TheirItems": { "Types": ["SteamGems"] },
					"MyItems": { "Types": ["ProfileBackground"] }
				},
				"Value": { "GooTypes": ["ProfileBackground"], "Direction": "Sell", "Strict": false }
			}
			"""
		);

		Asset background = CreateAsset(EAssetType.ProfileBackground, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 40);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [gems], [background], gooValue: BackgroundGoo(40)));
	}

	[TestMethod]
	public void GooValueOnlyAppliesToConfiguredTypes() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Value": { "GooTypes": ["ProfileBackground"], "Direction": "Sell" }
			}
			"""
		);

		Asset background = CreateAsset(EAssetType.ProfileBackground, realAppID: 440);
		Asset emoticon = CreateAsset(EAssetType.Emoticon, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 1000);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [gems], [background], gooValue: BackgroundGoo(40)));
		Assert.IsFalse(TradeMatcher.IsMatch(rule, [gems], [emoticon], gooValue: BackgroundGoo(40)));
	}

	[TestMethod]
	public void GooValueAllCoversEveryItemType() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Value": { "GooTypes": ["All"], "Direction": "Sell" }
			}
			"""
		);

		Asset emoticon = CreateAsset(EAssetType.Emoticon, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 41);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [gems], [emoticon], gooValue: _ => 40ul));
	}

	[TestMethod]
	public void UnknownGooValueDoesNotMatch() {
		CompiledTradeRule rule = CompileSingle(SellBackgroundsForGooJson);
		Asset background = CreateAsset(EAssetType.ProfileBackground, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 1000);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [gems], [background], gooValue: _ => null));
	}

	[TestMethod]
	public void ItemPricesMatchAtExactValue() {
		CompiledTradeRule rule = CompileSingle(ItemPriceJson);
		Asset backgrounds = CreateAsset(EAssetType.ProfileBackground, amount: 10, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 30);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [backgrounds], [gems]));
	}

	[TestMethod]
	public void ItemPricesAcceptBetterOffers() {
		CompiledTradeRule rule = CompileSingle(ItemPriceJson);
		Asset backgrounds = CreateAsset(EAssetType.ProfileBackground, amount: 10, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 100);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [backgrounds], [gems]));
	}

	[TestMethod]
	public void ItemPricesRejectLowerOffers() {
		CompiledTradeRule rule = CompileSingle(ItemPriceJson);
		Asset backgrounds = CreateAsset(EAssetType.ProfileBackground, amount: 10, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 29);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [backgrounds], [gems]));
	}

	[TestMethod]
	public void ItemPriceAppIDFilterNarrowsThePrice() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"ItemPrices": [ { "Type": "TradingCard", "AppIDs": [440], "Gems": 5 } ]
			}
			"""
		);

		Asset tf2Cards = CreateAsset(EAssetType.TradingCard, amount: 4, realAppID: 440);
		Asset csCards = CreateAsset(EAssetType.TradingCard, amount: 4, realAppID: 730);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 20);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [tf2Cards], [gems]));
		Assert.IsFalse(TradeMatcher.IsMatch(rule, [csCards], [gems]));
	}

	[TestMethod]
	public void ValueFactorRaisesTheRequiredValue() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": {
					"TheirItems": { "Types": ["ProfileBackground"] },
					"MyItems": { "Types": ["SteamGems"] }
				},
				"ItemPrices": [ { "Type": "ProfileBackground", "Gems": 3 } ],
				"Value": { "Factor": 2 }
			}
			"""
		);

		Asset backgrounds = CreateAsset(EAssetType.ProfileBackground, amount: 10, realAppID: 440);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [backgrounds], [CreateAsset(EAssetType.SteamGems, amount: 59)]));
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [backgrounds], [CreateAsset(EAssetType.SteamGems, amount: 60)]));
	}

	[TestMethod]
	public void ValueOffsetRaisesTheRequiredValue() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": {
					"TheirItems": { "Types": ["ProfileBackground"] },
					"MyItems": { "Types": ["SteamGems"] }
				},
				"ItemPrices": [ { "Type": "ProfileBackground", "Gems": 3 } ],
				"Value": { "Offset": 5 }
			}
			"""
		);

		Asset backgrounds = CreateAsset(EAssetType.ProfileBackground, amount: 10, realAppID: 440);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [backgrounds], [CreateAsset(EAssetType.SteamGems, amount: 34)]));
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [backgrounds], [CreateAsset(EAssetType.SteamGems, amount: 35)]));
	}

	[TestMethod]
	public void SellDirectionRequiresPaymentForWhatIsGiven() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": {
					"TheirItems": { "Types": ["SteamGems"] },
					"MyItems": { "Types": ["ProfileBackground"] }
				},
				"ItemPrices": [ { "Type": "ProfileBackground", "Gems": 3 } ],
				"Value": { "Direction": "Sell" }
			}
			"""
		);

		Asset backgrounds = CreateAsset(EAssetType.ProfileBackground, amount: 10, realAppID: 440);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [CreateAsset(EAssetType.SteamGems, amount: 29)], [backgrounds]));
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [CreateAsset(EAssetType.SteamGems, amount: 30)], [backgrounds]));
	}

	[TestMethod]
	public void UnpriceableExtrasMakeTheOfferNotMatch() {
		CompiledTradeRule rule = CompileSingle(ItemPriceJson);
		Asset backgrounds = CreateAsset(EAssetType.ProfileBackground, amount: 10, realAppID: 440);
		Asset gems = CreateAsset(EAssetType.SteamGems, amount: 30);
		Asset emoticon = CreateAsset(EAssetType.Emoticon);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [backgrounds], [gems, emoticon]));
	}

	[TestMethod]
	public void MarketProfitFlatMarginIsSatisfied() {
		CompiledTradeRule rule = CompileSingle("""{ "Type": "Accept", "MarketProfit": { "MinProfit": 5 } }""");
		Asset received = CreateAsset(EAssetType.TradingCard);
		Asset given = CreateAsset(EAssetType.BoosterPack);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [received], [given], marketPrice: MarketPrices(received, 10m, given, 4m)));
	}

	[TestMethod]
	public void MarketProfitFlatMarginRejectsThinProfit() {
		CompiledTradeRule rule = CompileSingle("""{ "Type": "Accept", "MarketProfit": { "MinProfit": 5 } }""");
		Asset received = CreateAsset(EAssetType.TradingCard);
		Asset given = CreateAsset(EAssetType.BoosterPack);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [received], [given], marketPrice: MarketPrices(received, 10m, given, 6m)));
	}

	[TestMethod]
	public void MarketProfitPercentMarginIsSatisfied() {
		CompiledTradeRule rule = CompileSingle("""{ "Type": "Accept", "MarketProfit": { "MinProfitPercent": 10 } }""");
		Asset received = CreateAsset(EAssetType.TradingCard);
		Asset given = CreateAsset(EAssetType.BoosterPack);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [received], [given], marketPrice: MarketPrices(received, 110m, given, 100m)));
		Assert.IsFalse(TradeMatcher.IsMatch(rule, [received], [given], marketPrice: MarketPrices(received, 109m, given, 100m)));
	}

	[TestMethod]
	public void MarketProfitRequiresBothMargins() {
		CompiledTradeRule rule = CompileSingle("""{ "Type": "Accept", "MarketProfit": { "MinProfit": 5, "MinProfitPercent": 50 } }""");
		Asset received = CreateAsset(EAssetType.TradingCard);
		Asset given = CreateAsset(EAssetType.BoosterPack);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [received], [given], marketPrice: MarketPrices(received, 149m, given, 100m)));
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [received], [given], marketPrice: MarketPrices(received, 150m, given, 100m)));
	}

	[TestMethod]
	public void MarketProfitRejectsThinMarkets() {
		CompiledTradeRule rule = CompileSingle("""{ "Type": "Accept", "MarketProfit": { "MinProfit": 1 } }""");
		Asset received = CreateAsset(EAssetType.TradingCard);
		Asset given = CreateAsset(EAssetType.BoosterPack);

		Func<Asset, MarketQuote?> prices = item => item == received ? new MarketQuote(100m, 5) : new MarketQuote(1m, 5);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [received], [given], marketPrice: prices));
	}

	[TestMethod]
	public void MarketProfitRejectsPricesAboveTheUnitCap() {
		CompiledTradeRule rule = CompileSingle("""{ "Type": "Accept", "MarketProfit": { "MinProfit": 1, "MaxUnitPrice": 100 } }""");
		Asset received = CreateAsset(EAssetType.TradingCard);
		Asset given = CreateAsset(EAssetType.BoosterPack);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [received], [given], marketPrice: MarketPrices(received, 500m, given, 1m)));
	}

	[TestMethod]
	public void MarketProfitRejectsItemsWithoutAPrice() {
		CompiledTradeRule rule = CompileSingle("""{ "Type": "Accept", "MarketProfit": { "MinProfit": 1 } }""");
		Asset received = CreateAsset(EAssetType.TradingCard);

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [received], [], marketPrice: _ => null));
	}

	[TestMethod]
	public void MarketProfitWithoutAPriceLookupDoesNotMatch() {
		CompiledTradeRule rule = CompileSingle("""{ "Type": "Accept", "MarketProfit": { "MinProfit": 1 } }""");

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [CreateAsset(EAssetType.TradingCard)], []));
	}

	[TestMethod]
	public void MarketPriceTextIsParsed() {
		Assert.IsTrue(MarketPriceResolver.TryParsePrice("$1.23", out decimal dollars));
		Assert.AreEqual(1.23m, dollars);

		Assert.IsTrue(MarketPriceResolver.TryParsePrice("1,23€", out decimal euros));
		Assert.AreEqual(1.23m, euros);

		Assert.IsTrue(MarketPriceResolver.TryParsePrice("$1,234.56", out decimal thousands));
		Assert.AreEqual(1234.56m, thousands);

		Assert.IsTrue(MarketPriceResolver.TryParsePrice("$0.03", out decimal cents));
		Assert.AreEqual(0.03m, cents);

		Assert.IsFalse(MarketPriceResolver.TryParsePrice("free", out _));
		Assert.IsFalse(MarketPriceResolver.TryParsePrice(null, out _));
	}

	[TestMethod]
	public void MarketVolumeTextIsParsed() {
		Assert.AreEqual(15462u, MarketPriceResolver.ParseVolume("15,462"));
		Assert.AreEqual(0u, MarketPriceResolver.ParseVolume(null));
	}

	[TestMethod]
	public void UnknownValueDirectionIsRejected() => AssertCompileFails(
		"""{ "Type": "Accept", "Value": { "Direction": "Give" } }""",
		"'Value.Direction' contains unknown value"
	);

	[TestMethod]
	public void NegativeMarketMarginIsRejected() => AssertCompileFails(
		"""{ "Type": "Accept", "MarketProfit": { "MinProfit": -1 } }""",
		"'MarketProfit.MinProfit' must not be negative"
	);

	[TestMethod]
	public void MarketProfitWithoutMarginIsRejected() => AssertCompileFails(
		"""{ "Type": "Accept", "MarketProfit": { "MinVolume": 5 } }""",
		"'MarketProfit' needs 'MinProfit' or 'MinProfitPercent' greater than zero"
	);

	[TestMethod]
	public void InvalidMarketCurrencyIsRejected() => AssertCompileFails(
		"""{ "Type": "Accept", "MarketProfit": { "MinProfit": 1, "Currency": 0 } }""",
		"'MarketProfit.Currency' must be greater than zero"
	);

	[TestMethod]
	public void UnknownGooItemTypeIsRejected() => AssertCompileFails(
		"""{ "Type": "Accept", "Value": { "GooTypes": ["Gizmo"] } }""",
		"'Value.GooTypes' contains unknown item type"
	);

	[TestMethod]
	public void DuplicateGooItemTypeIsRejected() => AssertCompileFails(
		"""{ "Type": "Accept", "Value": { "GooTypes": ["ProfileBackground", "profile background"] } }""",
		"'Value.GooTypes' contains more than one entry"
	);

	[TestMethod]
	public void NonPositiveValueFactorIsRejected() => AssertCompileFails(
		"""{ "Type": "Accept", "Value": { "Factor": 0 } }""",
		"'Value.Factor' must be greater than zero"
	);

	[TestMethod]
	public void SingleConditionWithAnyCountsTheWholeSide() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"ConditionLogic": "Any",
				"Conditions": {
					"TheirItems": { "Types": ["TradingCard"], "MinAmount": 3 }
				}
			}
			"""
		);

		Asset card = CreateAsset(EAssetType.TradingCard);
		Asset booster = CreateAsset(EAssetType.BoosterPack);

		// A single condition keeps its original meaning: the amount window covers the whole side, so two boosters plus one
		// card reach the minimum even though only the card passes the filter.
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [card, booster, booster], []));
	}

	[TestMethod]
	public void EmptyConditionListIsRejected() {
		AssertCompileFails(
			"""
			{
				"Type": "Accept",
				"Conditions": { "TheirItems": [] }
			}
			""",
			"is an empty list"
		);
	}

	[TestMethod]
	public void EmptyConditionInsideABasketIsRejected() {
		AssertCompileFails(
			"""
			{
				"Type": "Accept",
				"Conditions": {
					"TheirItems": [ { "AppIDs": [440], "MinAmount": 1 }, {} ]
				}
			}
			""",
			"has no conditions"
		);
	}

	[TestMethod]
	public void ReadmeExampleRulesCompile() {
		string? readmePath = FindReadme();

		if (readmePath == null) {
			Assert.Inconclusive("README.md could not be found next to the test binaries.");

			return;
		}

		string[] lines = File.ReadAllLines(readmePath);
		int compiledRules = 0;

		for (int start = 0; start < lines.Length; start++) {
			if (lines[start].Trim() != "```json") {
				continue;
			}

			int end = Array.FindIndex(lines, start + 1, static line => line.Trim() == "```");

			Assert.IsGreaterThan(start, end, "A json code block in README.md is not closed.");

			string json = string.Join('\n', lines[(start + 1)..end]);
			compiledRules += CompileReadmeCodeBlock(json, start + 1);
			start = end;
		}

		Assert.IsGreaterThan(0, compiledRules, "README.md does not contain a compilable rule.");
	}

	/// <summary>
	///     Compiles every rule of one README json block, which is either a full config or a single rule. Blocks that are
	///     only a fragment of a rule, such as a lone property, are skipped because they are not valid JSON on their own.
	/// </summary>
	private static int CompileReadmeCodeBlock(string json, int lineNumber) {
		JsonDocument document;

		try {
			document = JsonDocument.Parse(json);
		} catch (JsonException) {
			return 0;
		}

		using (document) {
			JsonElement root = document.RootElement;

			if (root.ValueKind != JsonValueKind.Object) {
				return 0;
			}

			if (!root.TryGetProperty("TradeManagerPlus", out JsonElement pluginConfig)) {
				if (!root.TryGetProperty("Type", out _)) {
					return 0;
				}

				TradeRuleConfig? singleRule = root.ToJsonObject<TradeRuleConfig>();

				Assert.IsNotNull(singleRule);
				Assert.IsTrue(RuleCompiler.TryCompile(singleRule, out _, out string? singleRuleError), $"The README rule at line {lineNumber} \"{singleRule.Description}\" does not compile: {singleRuleError}");

				return 1;
			}

			PluginConfig? config = pluginConfig.ToJsonObject<PluginConfig>();

			Assert.IsNotNull(config);
			Assert.IsNotNull(config.TradeRules);

			foreach (TradeRuleConfig ruleConfig in config.TradeRules) {
				Assert.IsTrue(RuleCompiler.TryCompile(ruleConfig, out _, out string? ruleError), $"The README rule at line {lineNumber} \"{ruleConfig.Description}\" does not compile: {ruleError}");
			}

			return config.TradeRules.Count;
		}
	}

	private static string? FindReadme() {
		DirectoryInfo? directory = new(AppContext.BaseDirectory);

		while (directory != null) {
			string candidate = Path.Combine(directory.FullName, "README.md");

			if (File.Exists(candidate)) {
				return candidate;
			}

			directory = directory.Parent;
		}

		return null;
	}

	private const string BasketJson =
		"""
		{
			"Type": "Accept",
			"Conditions": {
				"TheirItems": [
					{ "AppIDs": [440], "MinAmount": 1, "MaxAmount": 1 },
					{ "Types": ["TradingCard"], "SetSizes": [5], "MinAmount": 1, "MaxAmount": 1 },
					{ "Types": ["TradingCard"], "SetSizes": [8], "MinAmount": 1, "MaxAmount": 1 }
				],
				"MyItems": { "Types": ["SteamGems"] }
			},
			"ItemPrices": [
				{ "Type": "Unknown", "AppIDs": [440], "Gems": 10 },
				{ "Type": "Sticker", "Gems": 1 }
			],
			"GemPrices": [
				{ "SetSize": 5, "Gems": 7 },
				{ "SetSize": 8, "Gems": 5 }
			],
			"Description": "One key plus one card from a set of 5 and one from a set of 8, paid in gems."
		}
		""";

	private const string BasketAnyJson =
		"""
		{
			"Type": "Accept",
			"Conditions": {
				"TheirItems": [
					{ "AppIDs": [440], "MinAmount": 1, "MaxAmount": 1 },
					{ "Types": ["TradingCard"], "SetSizes": [5], "MinAmount": 1, "MaxAmount": 1 }
				],
				"MyItems": { "Types": ["SteamGems"] }
			},
			"ItemPrices": [ { "Type": "Unknown", "AppIDs": [440], "Gems": 10 } ],
			"GemPrices": [ { "SetSize": 5, "Gems": 7 } ],
			"ConditionLogic": "Any",
			"Description": "A key or a card from a set of 5, paid in gems."
		}
		""";

	private const string BasketAmountJson =
		"""
		{
			"Type": "Accept",
			"Conditions": {
				"TheirItems": [ { "Types": ["TradingCard"], "SetSizes": [5], "MinAmount": 1, "MaxAmount": 1 } ],
				"MyItems": { "Types": ["SteamGems"] }
			},
			"Description": "Exactly one card from a set of 5."
		}
		""";

	[TestMethod]
	public void BasketMatchesWhenEveryShapeIsPresentAndPaid() {
		CompiledTradeRule rule = CompileSingle(BasketJson);
		Dictionary<uint, byte> setSizes = new() { [111] = 5, [222] = 8 };

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [Key(), Card(111), Card(222)], Gems(22), setSizes));
	}

	[TestMethod]
	public void BasketRejectsOneGemShort() {
		CompiledTradeRule rule = CompileSingle(BasketJson);
		Dictionary<uint, byte> setSizes = new() { [111] = 5, [222] = 8 };

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [Key(), Card(111), Card(222)], Gems(21), setSizes));
	}

	[TestMethod]
	public void BasketRejectsAMissingShape() {
		CompiledTradeRule rule = CompileSingle(BasketJson);
		Dictionary<uint, byte> setSizes = new() { [111] = 5, [222] = 8 };

		Assert.IsFalse(TradeMatcher.IsMatch(rule, [Key(), Card(111)], Gems(22), setSizes));
	}

	[TestMethod]
	public void BasketRejectsAnItemNoShapeAskedFor() {
		CompiledTradeRule rule = CompileSingle(BasketJson);
		Dictionary<uint, byte> setSizes = new() { [111] = 5, [222] = 8 };
		Asset sticker = CreateAsset(EAssetType.Sticker, realAppID: 999);

		// The sticker has a price, so the gem value is covered - the basket must still refuse it because no group asks
		// for it.
		Assert.IsFalse(TradeMatcher.IsMatch(rule, [Key(), Card(111), Card(222), sticker], Gems(23), setSizes));
	}

	[TestMethod]
	public void BasketAnyLogicAcceptsOneSatisfiedShape() {
		CompiledTradeRule rule = CompileSingle(BasketAnyJson);
		Dictionary<uint, byte> setSizes = new() { [111] = 5 };

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [Card(111)], Gems(7), setSizes));
		Assert.IsTrue(TradeMatcher.IsMatch(rule, [Key()], Gems(10), setSizes));
	}

	[TestMethod]
	public void BasketGroupAmountsAreCountedPerShape() {
		CompiledTradeRule rule = CompileSingle(BasketAmountJson);
		Dictionary<uint, byte> setSizes = new() { [111] = 5 };

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [Card(111)], Gems(100), setSizes));
		Assert.IsFalse(TradeMatcher.IsMatch(rule, [Card(111), Card(111)], Gems(100), setSizes));
	}

	[TestMethod]
	public void EmptyConditionGroupMatchesAnything() {
		CompiledTradeRule rule = CompileSingle(
			"""
			{
				"Type": "Accept",
				"Conditions": { "TheirItems": [ {} ] }
			}
			"""
		);

		Assert.IsTrue(TradeMatcher.IsMatch(rule, [CreateAsset(EAssetType.Emoticon)], []));
	}

	[TestMethod]
	public void ConditionGroupListReportsTheFailingEntryPath() {
		AssertCompileFails(
			"""
			{
				"Type": "Accept",
				"Conditions": { "TheirItems": [ { "MinAmount": 2, "MaxAmount": 1 } ] }
			}
			""",
			"TheirItems[0].MinAmount"
		);
	}

	private static Asset Key() => CreateAsset(EAssetType.Unknown, realAppID: 440);

	private static Asset Card(uint appID) => CreateAsset(EAssetType.TradingCard, realAppID: appID);

	private static Asset[] Gems(uint amount) => [CreateAsset(EAssetType.SteamGems, amount: amount)];

	private static Func<Asset, ulong?> BackgroundGoo(ulong value) => item => item.Type == EAssetType.ProfileBackground ? value : null;

	private static Func<Asset, MarketQuote?> MarketPrices(Asset received, decimal receivedPrice, Asset given, decimal givenPrice) => item => {
		if (item == received) {
			return new MarketQuote(receivedPrice, 50);
		}

		return item == given ? new MarketQuote(givenPrice, 50) : null;
	};

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

	private static Asset CreateAsset(EAssetType type, EAssetRarity rarity = EAssetRarity.Common, uint amount = 1, uint realAppID = Asset.SteamAppID) {
		InventoryDescription description = new(appID: Asset.SteamAppID, classID: 1, realAppID: realAppID, type: type, rarity: rarity);

		return new Asset(Asset.SteamAppID, Asset.SteamCommunityContextID, classID: 1, amount, description);
	}
}
