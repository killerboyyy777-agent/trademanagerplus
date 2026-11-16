### Bug fix

- **A value rule can no longer break trade evaluation.** Comparing both sides of an offer multiplied the value by `Value.Factor` and added `Value.Offset`. A rule with an extreme factor, or an offer with very large amounts, could make that calculation overflow while a trade was being decided. The overflow is now caught and treated as "this offer cannot be priced safely", so the rule does not match and the offer is left to ASF instead of the plugin throwing.

Nothing about how rules are written changed in this release, so existing configurations keep working unchanged. This is the same fail-closed behaviour market rules already had: an offer that cannot be valued never matches.

### Compatibility

| ArchiSteamFarm | Plugin asset | Target framework |
| --- | --- | --- |
| ASF 6.3.11.7 | `TradeManagerPlus.zip` | `net10.0` |
| ASF 7.0.0.7 (pre-release) | `TradeManagerPlus-V7.zip` | `net11.0` |

ASF 7 is still a pre-release and needs the .NET 11 runtime. Support for it may change before ASF 7 goes stable. ASF 6 remains supported.

Verified with 105 tests on both build targets.
