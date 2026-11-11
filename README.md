# TradeManagerPlus for ArchiSteamFarm

[![Steam donate](https://img.shields.io/badge/Steam-donate-000000.svg?logo=steam)](https://steamcommunity.com/tradeoffer/new/?partner=1211192445&token=T9Hiu3Oz)

[![BTC donate](https://img.shields.io/badge/BTC-donate-f7931a.svg?logo=bitcoin)](https://www.blockchain.com/explorer/addresses/btc/bc1qmkv939k2wqsej657cxj25ppwqdh65y2umnv3gg)
[![LTC donate](https://img.shields.io/badge/LTC-donate-a6a9aa.svg?logo=litecoin)](https://live.blockcypher.com/ltc/address/ltc1qjr49nr028mcajlt7prmmnqnjh0552qjj90zdq4)
[![XMR donate](https://img.shields.io/badge/XMR-donate-FF6600.svg?logo=monero)](monero:82oJRDdiSWWbem3HiYx7ZdDdiPkYQAW4LaGNHpNcJ9DCendQ3XcxHNYQiRMtfghYtSMmARPGqKe2ddSrhtjviTraEyGwgZ2)

---

TradeManagerPlus is a plugin for **[ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm)** that lets you accept or decline incoming Steam trade offers with your own rules, based on the items in the offer.

> [!WARNING]
> **Beta / test build, not finished.** The current releases are early builds for testing. They have not been validated against live Steam trades, and the ASF 7 build depends on a pre-release ASF version. Do not use them on a bot that holds items you cannot afford to lose. The ASF 6 build is the more tested one.

## Features

- **Item-based rules**: match trades by AppID, item type, rarity, item name, card set size and total item amount, separately for the items you receive and the items you give.
- **Accept and Decline**: the first matching rule decides what happens to the offer.
- **Gem pricing**: price cards by how many cards their set has, so a 5-card game and a 15-card game get different prices.
- **Value comparison**: compare the gem value of both sides, using configured prices, Steam's own gem ("goo") values for backgrounds, emoticons and similar items, or a mix of both.
- **Market profit**: accept trades where the Steam Community market value of what you receive beats what you give by a flat and/or percentage margin.
- **Safe by default**: if no rule matches, ASF handles the offer exactly as it would without the plugin. Anything the plugin cannot price is treated as "unknown", never as zero, so an unpriceable item can never make a bad trade look good.
- **Validation**: malformed rules are reported in the ASF log and skipped instead of breaking the plugin.
- **Inspection**: `TMP STATUS` and `TMP RULES` commands show the loaded configuration.
- **Automatic updates**: the plugin uses ASF's GitHub plugin update mechanism. Beta releases are intentionally published as pre-releases, so ASF does not offer them through the automatic update channel.

## How it interacts with ASF

ASF calls this plugin only for trade offers that ASF itself is **not** willing to accept. That is a limitation of ASF's trade-offer interface (`IBotTradeOffer2` on ASF 6, `IBotTradeOffer` on ASF 7), not of this plugin:

- An **Accept** rule can rescue an offer that ASF would otherwise ignore or reject. Blacklisted offers are the exception: the plugin never overrides ASF's blacklist, and ASF declines those offers.
- A **Decline** rule explicitly declines an offer that ASF would otherwise leave untouched (ASF-declined offers are declined by ASF itself).
- Offers that ASF accepts on its own (for example donations or items matching your `LootableTypes`) are **not** passed to the plugin and cannot be vetoed by it.

If you need tighter control over all offers, combine this plugin with ASF's `TradingPreferences`, `LootableTypes` and `BotBehaviour` settings.

## Installation

1. Download the release asset that matches your ASF version from the **[beta releases page](https://github.com/killerboyyy777-agent/trademanagerplus/releases)**:
   - ASF 6.3.11.7: `TradeManagerPlus.zip`
   - ASF 7.0.0.7: `TradeManagerPlus-V7.zip`
2. Extract the archive into `plugins/TradeManagerPlus` inside your ASF directory, so that `TradeManagerPlus.dll` ends up in that folder.
3. Restart ASF.

The ASF 6 asset requires ASF 6.3.11.7 and .NET 10. The ASF 7 asset requires the ASF 7 pre-release (7.0.0.7) and .NET 11. ASF 7 support may change before ASF 7 goes stable.

> Beta releases are published from the fork for testing and are not offered through ASF's automatic update channel. Stable releases will be published on the [main releases page](https://github.com/killerboyyy777/trademanagerplus/releases) once they are ready.

## Configuration

Add a top-level `TradeManagerPlus` property to your bot config (for example `config/MyBot.json`), next to the other ASF properties:

```json
{
  "Enabled": true,
  "SteamLogin": "your_login",
  "TradeManagerPlus": {
    "Enabled": true,
    "TradeRules": [
      {
        "Type": "Accept",
        "Conditions": {
          "TheirItems": {
            "Types": ["TradingCard", "FoilTradingCard", "BoosterPack"],
            "ExcludedAppIDs": [730]
          },
          "MyItems": {
            "Types": ["SteamGems"]
          }
        },
        "GemPrices": [
          { "SetSize": 5, "Gems": 70 },
          { "SetSize": 6, "Gems": 58 },
          { "SetSize": 7, "Gems": 50 },
          { "SetSize": 8, "Gems": 43 },
          { "SetSize": 9, "Gems": 38 },
          { "SetSize": 10, "Gems": 35 },
          { "SetSize": 11, "Gems": 31 },
          { "SetSize": 12, "Gems": 29 },
          { "SetSize": 13, "Gems": 26 },
          { "SetSize": 14, "Gems": 25 },
          { "SetSize": 15, "Gems": 23 }
        ],
        "CardEquivalents": [
          { "Type": "BoosterPack", "Cards": 3 }
        ],
        "ConditionLogic": "All",
        "Description": "Buy any card at the price for its card set size, or better. Booster packs count as 3 cards."
      },
      {
        "Type": "Accept",
        "Conditions": {
          "TheirItems": {
            "Types": ["SteamGems"],
            "MinAmount": 1650
          },
          "MyItems": {
            "Names": ["Mann Co. Supply Crate Key"],
            "MinAmount": 1,
            "MaxAmount": 1
          }
        },
        "ItemPrices": [
          {
            "Type": "Unknown",
            "AppIDs": [440],
            "Names": ["Mann Co. Supply Crate Key"],
            "Gems": 1500
          }
        ],
        "Value": { "Direction": "Sell", "Factor": 1.1 },
        "ConditionLogic": "All",
        "Description": "Sell a TF2 key for at least 10% over its gem value."
      },
      {
        "Type": "Accept",
        "Conditions": {
          "TheirItems": {
            "Types": ["FoilTradingCard"],
            "MinAmount": 1,
            "MaxAmount": 1
          },
          "MyItems": {
            "Types": ["TradingCard"],
            "MinAmount": 3,
            "MaxAmount": 3
          }
        },
        "ConditionLogic": "All",
        "Description": "Swap 3 normal cards for 1 foil card."
      },
      {
        "Type": "Accept",
        "Conditions": {
          "TheirItems": {
            "Types": ["SteamGems"]
          },
          "MyItems": {
            "Types": ["ProfileBackground", "Emoticon"]
          }
        },
        "Value": {
          "Direction": "Sell",
          "GooTypes": ["ProfileBackground", "Emoticon"]
        },
        "ConditionLogic": "All",
        "Description": "Sell backgrounds and emotes for more than their Steam gem value."
      },
      {
        "Type": "Accept",
        "Conditions": {
          "TheirItems": {
            "AppIDs": [753]
          }
        },
        "MarketProfit": {
          "Currency": 1,
          "MinProfit": 0.05,
          "MinProfitPercent": 10,
          "MinVolume": 10
        },
        "Description": "Accept any trade worth at least 10% and 0.05 USD more than it costs."
      },
      {
        "Type": "Decline",
        "Conditions": {
          "MyItems": {
            "Rarities": ["Rare", "Mythical", "Legendary", "Ancient", "Immortal", "Arcana", "Unusual"]
          }
        },
        "Description": "Never give away rare or better items."
      }
    ]
  }
}
```

> Older versions of this README used a `"Plugins": { "TradeManagerPlus": { ... } }` wrapper. That layout is still recognized for backwards compatibility, but ASF passes custom bot properties to plugins at the top level, as shown above.

### Rule properties

| Property | Default | Description |
| --- | --- | --- |
| `Enabled` | `true` | Set to `false` to disable the plugin for this bot without deleting the rules. |
| `TradeRules` | `[]` | Ordered list of rules. The first rule that matches wins. |
| `TradeRules[].Type` | required | `Accept` or `Decline`. |
| `TradeRules[].Conditions` | empty | Conditions for `TheirItems` (you receive) and `MyItems` (you give). |
| `TradeRules[].ConditionLogic` | `All` | `All`: every item on that side must match the filters. `Any`: at least one item must match. |
| `TradeRules[].Description` | empty | Free-form text shown in the ASF log and in `TMP RULES`. |
| `TradeRules[].GemsPerCard` | `0` | Flat price in Steam gems per card received. `0` disables pricing. Offers that pay more are accepted. Cannot be combined with `GemPrices`. |
| `TradeRules[].GemPrices` | `[]` | Gem price per card, keyed by card set size: `[{ "SetSize": 8, "Gems": 43 }]`. See [Pricing cards](#pricing-cards). |
| `TradeRules[].CardEquivalents` | `[]` | How many cards an item type counts as for pricing: `[{ "Type": "BoosterPack", "Cards": 3 }]`. |
| `TradeRules[].ItemPrices` | `[]` | Gem value of one unit of an item, for things that are not cards or gems: `[{ "Type": "ProfileBackground", "Gems": 3 }]`. |
| `TradeRules[].Value` | disabled | Compares the gem value of both sides. See [Comparing value](#comparing-value-with-value). |
| `TradeRules[].MarketProfit` | disabled | Accepts trades whose market value beats the cost by a margin. See [Market profit](#market-profit-with-marketprofit). |

### Item conditions

`TheirItems` and `MyItems` accept the same properties. Every property is optional; omitted properties do not restrict matching.

| Property | Description |
| --- | --- |
| `AppIDs` | List of real AppIDs the items must belong to, for example `[753]` for Steam inventory items. |
| `ExcludedAppIDs` | List of real AppIDs that must never appear on this side, for example `[730]` to refuse CS items. |
| `Types` | List of item types. Matching is case-insensitive and ignores spaces, hyphens and underscores, so both `TradingCard` and `Trading Card` work. |
| `Rarities` | List of item rarities, same lenient naming. |
| `Names` | List of exact item names, matched against both the market hash name and the plain name, ignoring case. Use it for items that type and AppID cannot tell apart, for example `Mann Co. Supply Crate Key`. |
| `SetSizes` | List of Steam card set sizes the items must belong to, for example `[8]` for games whose set has 8 cards. See below. |
| `MinAmount` | Minimum total quantity of items on this side. `0` means no minimum. |
| `MaxAmount` | Maximum total quantity of items on this side. Omit for no maximum. |

**How a side is evaluated:**

1. If the side has no properties at all, it matches anything, including an empty side.
2. `MinAmount`/`MaxAmount` are checked against the total quantity of items on that side.
3. If item filters (`AppIDs`, `ExcludedAppIDs`, `Types`, `Rarities`, `Names`, `SetSizes`) are present, `ConditionLogic` decides whether every item (`All`) or at least one item (`Any`) must satisfy all of them.

A rule matches only when **both** `TheirItems` and `MyItems` match.

**About `SetSizes`:**

`SetSizes` filters by how many cards a game's Steam card set contains — not by how many cards are in the trade. The plugin reads that number from the game's card set page (`steamcommunity.com/my/gamecards/<AppID>`), exactly like ASF does, and caches it per AppID for 7 days. A failed lookup is retried after 30 minutes.

Because a trade can only be matched when the set size is known, items from a game whose set size could not be read **never match a rule that uses `SetSizes`** — the rule stays silent instead of accepting a trade it cannot verify. Set sizes apply to card sets, so combine `SetSizes` with `Types: ["TradingCard"]` (or `FoilTradingCard`) and, if you want whole sets only, a matching `MinAmount`.

### Pricing cards

`GemPrices` gives every card a price that depends on how many cards its game's set has, which is how most card shops work: a card from a 5-card set is worth more than a card from a 15-card set. Each entry is `{ "SetSize": 8, "Gems": 43 }`, and the price of a card is looked up by the set size of the game it belongs to.

- Cards and foil cards of the same set size share the same price. Add `CardEquivalents` when an item is worth several cards — a booster pack is usually 3 cards.
- A set size that is not in the table uses the price of the largest listed set size below it, and a size below the whole table uses the smallest listed price. Add more entries to avoid gaps.
- If the set size of a game cannot be read from Steam, the card cannot be priced and the rule does not match.
- `GemsPerCard` is the simpler variant for a flat price per card and cannot be combined with `GemPrices`.

`SteamGems` are worth exactly one gem per unit, and a gem stack already carries its amount, so a sack of 1000 gems counts as 1000. With `Value.Direction` left at its `Buy` default, the rule accepts an offer when the value of what you give covers the value of the cards you receive. That makes the price a minimum: a 3-card offer priced at 129 gems is accepted for 129 gems, for 500 gems, or anything in between, but not for 128.

Payment does not have to be gems. Because the same comparison prices both sides, a card-for-card or key-for-card trade works too, as long as every item in the offer can be priced. If you want gems only, restrict the giving side with `"MyItems": { "Types": ["SteamGems"] }`, as the example above does.

The example rule at the top of this section buys any card at its set-size price, counts booster packs as 3 cards and refuses CS items:

```json
"GemPrices": [
  { "SetSize": 8, "Gems": 43 },
  { "SetSize": 15, "Gems": 23 }
],
"CardEquivalents": [{ "Type": "BoosterPack", "Cards": 3 }]
```

### Comparing value with `Value`

Once a rule prices anything (`GemsPerCard`, `GemPrices` or `ItemPrices`), it compares the gem value of the two sides instead of only counting gems. The side that pays can be anything with a price, which is what makes trades like gems for a TF2 key or normal cards for a foil possible.

| Property | Default | Description |
| --- | --- | --- |
| `Value.Direction` | `Buy` | `Buy`: the bot receives goods and pays, so the payment has to be worth at least the goods. `Sell`: the bot gives goods and is paid, so the payment it receives has to be worth at least the goods. |
| `Value.Factor` | `1` | Multiplier on the goods value, for example `1.1` to demand a 10% margin. |
| `Value.Offset` | `0` | Flat value added to the goods value before comparing. A negative value accepts slightly worse offers. |
| `Value.GooTypes` | none | Item types whose Steam gem value ("goo") is read from steamcommunity.com and used as their price, for example `["ProfileBackground", "Emoticon"]`. `["All"]` allows every type. |
| `Value.Strict` | `true` with goo, `false` otherwise | When true the payment has to be strictly greater than the goods value, so an equal-value offer is rejected. |

**Every item on both sides has to have a price** when the rule prices anything. An item the plugin cannot price makes the rule not match, so a trade cannot be made to look good by adding an unpriced extra. That is deliberate: unknown is never treated as zero.

Steam's gem values are read per item type and community AppID from `steamcommunity.com/auction/ajaxgetgoovalueforitemtype`, which is the same value Steam uses when you turn an item into gems. They are cached for 7 days, and a failed lookup is retried after 30 minutes.

`ItemPrices` covers everything that is neither a card nor a gem. Leave `Type` out — or set it to `Unknown` — and filter by `Names` for items ASF cannot classify, such as TF2 keys:

```json
"ItemPrices": [
  { "Type": "Unknown", "AppIDs": [440], "Names": ["Mann Co. Supply Crate Key"], "Gems": 1500 }
]
```

### Market profit with `MarketProfit`

`MarketProfit` accepts an offer when the Steam Community market value of what the bot receives is higher than the value of what it gives. It is independent of the gem engine, so it can be combined with conditions to only buy certain items, or used on its own to catch anything profitable.

| Property | Default | Description |
| --- | --- | --- |
| `MarketProfit.Currency` | `1` | Steam currency ID for the price lookups, `1` for USD, `2` for GBP, `3` for EUR. |
| `MarketProfit.MinProfit` | `0` | Minimum profit in that currency, for example `0.05`. |
| `MarketProfit.MinProfitPercent` | `0` | Minimum profit as a percentage of the market value given, for example `10`. |
| `MarketProfit.MinVolume` | `10` | Minimum units sold in the last 24 hours before a price is trusted. |
| `MarketProfit.MaxUnitPrice` | none | Highest price per unit that is still trusted. |

When both `MinProfit` and `MinProfitPercent` are set, the larger of the two is required, so `MinProfit: 0.05` with `MinProfitPercent: 10` means "at least 10%, but never less than 5 cents".

**Anti-manipulation:** market prices are easy to fake, so the plugin is deliberately conservative.

- It uses the median sale price, not the lowest listing. The lowest listing is a single number that anyone can move with one cheap item.
- It refuses to trust a price for an item that sold fewer than `MinVolume` times in the last 24 hours. Thin markets are exactly where a fake price survives.
- `MaxUnitPrice` rejects any single item priced above a cap, which stops one absurdly expensive item from making a bad trade look profitable.
- Prices are only cached for an hour, so an artificial spike cannot be reused for long.
- Anything it cannot price makes the rule not match. It never guesses, and it never treats an unknown item as worth zero.

Steam's market feed is not an auction house with guaranteed liquidity, so treat this rule as a filter, not as a guarantee. If you trade in illiquid items, raise `MinVolume` and set a `MaxUnitPrice` that fits them.

A rule with pricing or a market requirement but nothing to receive never matches, so value cannot leave your inventory for an empty trade.

### Item types

`BoosterPack`, `Emoticon`, `FoilTradingCard`, `ProfileBackground`, `TradingCard`, `SteamGems`, `SaleItem`, `Consumable`, `ProfileModifier`, `Sticker`, `ChatEffect`, `MiniProfileBackground`, `AvatarProfileFrame`, `AnimatedAvatar`, `KeyboardSkin`, `StartupVideo`, `Unknown`.

### Rarities

`Common`, `Uncommon`, `Rare`, `Mythical` (`Epic`), `Legendary` (`Exotic`), `Ancient` (`Extraordinary`), `Immortal` (`Contraband`), `Arcana`, `Unusual`, `Unknown`.

> Steam only exposes the full rarity information for some item types. Cards usually only have `Common`, `Uncommon` or `Rare`.

## Commands

The commands require ASF `Master` access or higher and are scoped to the bot that receives them.

| Command | Description |
| --- | --- |
| `!TMP` or `!TMP STATUS` | Show whether the plugin is enabled and how many rules are loaded. |
| `!TMP RULES` | List the loaded rules (up to 10) with their conditions, prices and value or market requirements. |
| `!TEST` | Alias of `!TMP STATUS`, kept for backwards compatibility. |

Example: send `!TMP RULES` to the bot in Steam chat.

## Logging

Every accepted or declined offer that matched a rule is logged with the offer ID and the rule description. Invalid rules are logged as errors during bot initialization, including the reason and the list of valid types/rarities.

## Building from source

ASF 6 (default, .NET 10 SDK):

```sh
git clone --recurse-submodules https://github.com/killerboyyy777/trademanagerplus.git
cd trademanagerplus
dotnet build -c Release
```

The plugin is copied to `ArchiSteamFarm/ArchiSteamFarm/bin/Release/net10.0/plugins/TradeManagerPlus/`.

ASF 7 (.NET 11 SDK):

```sh
git -C ArchiSteamFarm fetch origin tag 7.0.0.7 --no-tags
git -C ArchiSteamFarm checkout 7.0.0.7
git -C ArchiSteamFarm submodule update --init --recursive
dotnet build -c Release -p:AsfMajor=7
```

The plugin is copied to `ArchiSteamFarm/ArchiSteamFarm/bin/Release/net11.0/plugins/TradeManagerPlus/`. To return to the pinned ASF 6 version, run `git submodule update --init --recursive` from the repository root.

Run the tests with:

```sh
dotnet test TradeManagerPlus.Tests/TradeManagerPlus.Tests.csproj -c Release
```

For ASF 7, add `-p:AsfMajor=7` to the test command as well.

## Contact

- **Steam:** [steamcommunity.com/id/klb777](https://steamcommunity.com/id/klb777)
- **Discord:** killerboyyy777

## License

Apache-2.0. See [LICENSE.txt](LICENSE.txt).
