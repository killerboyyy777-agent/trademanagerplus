### Improvements

- **Steam lookup caches now prune expired entries.** Market-price, gem-value and card-set-size results are kept in expiring caches. Expired entries are removed in the background instead of accumulating for the lifetime of a long-running bot (#15).
- **Lookup limits are now visible.** When an offer needs more Steam lookups than the per-offer budget allows, the plugin logs a rate-limited warning naming the limit, so a rule that does not match because of a budget is no longer silent (#16). The remaining items stay unknown and the rule simply does not match; no trade is accepted on a guess.
- **Release reruns no longer fail on an existing release.** The publish workflow can update an already-published release, so re-running a tag no longer leaves a red workflow run.

Steam lookups are cached and capped per offer: at most 10 market-price lookups, 25 gem-value lookups and 15 card-set-size lookups. Cached results do not count toward those limits. The README documents this under "Steam lookup limits".

### Upgrade notes

No rule syntax changes in this release. Existing configurations keep working unchanged.

### Compatibility

| ArchiSteamFarm | Plugin asset | Target framework |
| --- | --- | --- |
| ASF 6.3.11.7 | `TradeManagerPlus.zip` | `net10.0` |
| ASF 7.0.0.9 (pre-release) | `TradeManagerPlus-V7.zip` | `net11.0` |

ASF 7 is still a pre-release and needs the .NET 11 runtime. Support for it may change before ASF 7 goes stable. ASF 6 remains supported.

Verified with 112 tests on both build targets.
