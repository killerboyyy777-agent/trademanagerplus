### New features

- **Market profit in your wallet's currency.** `MarketProfit.Currency` accepts `"Wallet"`, so prices are compared in whatever currency the bot's own Steam account is charged in instead of a fixed currency. Numeric Steam currency IDs still work as before.
- **Steam Community rarities, plus your own.** `Rarities` now accepts only the tiers Steam Community items actually have: `Common`, `Uncommon` and `Rare`. A new `CustomRarities` property takes tiers from other inventories, so TF2 or CS:GO items can still be matched.
- **Trade hold conditions.** A rule can require or forbid an escrow hold with `TradeHold: { "MinDays": 0, "MaxDays": 0 }`. A hold Steam does not report never satisfies a rule that asks for one.

### Breaking change

`Rarities` no longer accepts `Mythical`, `Legendary`, `Ancient`, `Immortal`, `Arcana` or `Unusual`. Move those entries to `CustomRarities`. A config that still lists them in `Rarities` fails validation, the reason is logged, and the bot stays without rules until it is fixed, so it will not accept trades you did not intend.

### Upgrade notes

- `MarketProfit.Currency` can stay numeric. Use `"Wallet"` if you want the prices to follow the account rather than a fixed currency.
- A wallet rule only matches once Steam has reported a wallet currency for that bot. If it never matches, check the log for the warning naming the rule.
- `!TMP RULES` now shows the trade hold range and the currency of each market rule.

### Compatibility

| ArchiSteamFarm | Plugin asset | Target framework |
| --- | --- | --- |
| ASF 6.3.11.7 | `TradeManagerPlus.zip` | `net10.0` |
| ASF 7.0.0.7 (pre-release) | `TradeManagerPlus-V7.zip` | `net11.0` |

ASF 7 is still a pre-release and needs the .NET 11 runtime. Support for it may change before ASF 7 goes stable. ASF 6 remains supported.

Verified with 103 tests on both build targets.
