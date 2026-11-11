### Safety fixes

This release closes a case where a priced `Accept` rule could match a trade that sent you **nothing**.

- **An offer that receives no items can no longer match a rule that prices anything.** Before this release, the received side of such an offer was valued at 0 gems, so `0 <= gems given` passed and the bot would have paid for an empty trade. A priced rule, or one with a `MarketProfit` requirement, now refuses any offer with an empty side.
- **Typos in the config are now an error instead of a wider rule.** A misspelled property such as `MyItem` used to be ignored, leaving that side unconstrained and letting the rule accept far more than intended. Unknown properties now stop the config from loading for that bot, and the reason is logged. Check the log after upgrading if a bot reports no rules.
- **Rules that could never match are rejected at load.** `Value` without any price, `CardEquivalents` on a non-card type with `GemPrices`, and `Value.GooTypes` naming a type that has no Steam gem value are now compile errors with an explanation, instead of silently doing nothing or never matching.
- **Lookups are bounded per offer.** Card set sizes now have a per-offer budget, and cached gem values no longer consume it, so a large offer cannot stall the trade handler for minutes.
- **`!TMP RULES`** now says the plugin is disabled instead of claiming there are no rules.

Market prices are buyer prices and include Steam's fee, which the bot does not receive when it sells. `MinProfitPercent` below about 15 can still be a net loss; the README now says so.

### Upgrade notes

- If a bot logs a configuration error after upgrading, an unknown property or an out-of-range value is present in its `TradeManagerPlus` section. The plugin stays inactive for that bot until it is fixed, so it will not accept surprise trades in the meantime.
- Verified with 92 tests on both build targets.

### Compatibility

| ArchiSteamFarm | Plugin asset | Target framework |
| --- | --- | --- |
| ASF 6.3.11.7 | `TradeManagerPlus.zip` | `net10.0` |
| ASF 7.0.0.7 (pre-release) | `TradeManagerPlus-V7.zip` | `net11.0` |

ASF 7 is still a pre-release and needs the .NET 11 runtime. Support for it may change before ASF 7 goes stable. ASF 6 remains supported.
