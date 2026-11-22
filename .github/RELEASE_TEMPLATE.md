### Improvements

- **The plugin is now actually discovered by ASF.** The plugin class is exported with `[Export(typeof(IPlugin))]`, so ASF's composition can instantiate it. Without this, a correctly configured plugin could simply never load.
- **Rule validation is stricter.** `CardEquivalents` can no longer override the built-in value of trading cards or foils, and `CustomRarities` rejects `Unknown` instead of silently accepting it.
- **`!TMP RULES` no longer crashes on extreme offsets.** A `Value.Offset` at `decimal.MinValue` is now formatted as text instead of being negated.
- **ASF 7 target updated to 7.0.0.9 with the final .NET 11 SDK.** The ASF 7 build and release asset now track that version.

### Upgrade notes

- No rule syntax changed; existing configurations keep working.
- ASF 7 users need ASF 7.0.0.9 for the `TradeManagerPlus-V7.zip` asset. ASF 6 users keep using `TradeManagerPlus.zip`.
- Verified with 109 tests on both build targets.

### Compatibility

| ArchiSteamFarm | Plugin asset | Target framework |
| --- | --- | --- |
| ASF 6.3.11.7 | `TradeManagerPlus.zip` | `net10.0` |
| ASF 7.0.0.9 (release candidate) | `TradeManagerPlus-V7.zip` | `net11.0` |

ASF 7 is still a release candidate and needs the .NET 11 runtime. Support for it may change before ASF 7 goes stable. ASF 6 remains supported.
