### Bug fix

- **A market price can no longer break trade evaluation.** Summing market values multiplied a price from Steam by the quantity in the offer, and an extreme `median_price` could make that calculation overflow while a trade was being decided. The overflow is now caught and treated as an unpriced item, so a market rule simply does not match instead of the plugin throwing. The profit margin calculation is guarded the same way.

Nothing about how rules are written changed in this release, so existing configurations keep working unchanged. If a market rule silently stops matching a trade with an absurd price, that is the guard doing its job, and the offer is left to ASF as before.

### Compatibility

| ArchiSteamFarm | Plugin asset | Target framework |
| --- | --- | --- |
| ASF 6.3.11.7 | `TradeManagerPlus.zip` | `net10.0` |
| ASF 7.0.0.7 (pre-release) | `TradeManagerPlus-V7.zip` | `net11.0` |

ASF 7 is still a pre-release and needs the .NET 11 runtime. Support for it may change before ASF 7 goes stable. ASF 6 remains supported.

Verified with 104 tests on both build targets.
