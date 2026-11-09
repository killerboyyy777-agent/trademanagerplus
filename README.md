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

- **Item-based rules**: match trades by AppID, item type, rarity and total item amount, separately for the items you receive and the items you give.
- **Accept and Decline**: the first matching rule decides what happens to the offer.
- **Safe by default**: if no rule matches, ASF handles the offer exactly as it would without the plugin.
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
            "Types": ["TradingCard"],
            "MinAmount": 1,
            "MaxAmount": 1
          },
          "MyItems": {
            "Types": ["TradingCard"],
            "MinAmount": 1,
            "MaxAmount": 1
          }
        },
        "ConditionLogic": "All",
        "Description": "Accept 1:1 Steam trading card swaps."
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

### Item conditions

`TheirItems` and `MyItems` accept the same properties. Every property is optional; omitted properties do not restrict matching.

| Property | Description |
| --- | --- |
| `AppIDs` | List of real AppIDs the items must belong to, for example `[753]` for Steam inventory items. |
| `Types` | List of item types. Matching is case-insensitive and ignores spaces, hyphens and underscores, so both `TradingCard` and `Trading Card` work. |
| `Rarities` | List of item rarities, same lenient naming. |
| `MinAmount` | Minimum total quantity of items on this side. `0` means no minimum. |
| `MaxAmount` | Maximum total quantity of items on this side. Omit for no maximum. |

**How a side is evaluated:**

1. If the side has no properties at all, it matches anything, including an empty side.
2. `MinAmount`/`MaxAmount` are checked against the total quantity of items on that side.
3. If item filters (`AppIDs`, `Types`, `Rarities`) are present, `ConditionLogic` decides whether every item (`All`) or at least one item (`Any`) must satisfy all of them.

A rule matches only when **both** `TheirItems` and `MyItems` match.

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
| `!TMP RULES` | List the loaded rules (up to 10) with their conditions. |
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
