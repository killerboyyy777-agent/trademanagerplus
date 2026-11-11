# Contributing to TradeManagerPlus

Thanks for taking the time to improve the plugin. Bug reports and rule ideas are as welcome as code.

## Reporting a problem

Open an [issue](https://github.com/killerboyyy777/trademanagerplus/issues/new/choose) and pick the template that fits. Include the plugin version, the ArchiSteamFarm version, the rules involved and the log lines the plugin printed for the offer. A wrong decision is much easier to fix when the trade's items and the matching rule are both visible.

## Sending a change

1. Fork the repository and create a branch for your change.
2. Make the change, with a test for any new behaviour.
3. Open a pull request against `main`. The CI matrix builds and tests both ASF 6.3.11.7 and ASF 7.0.0.7 on Windows, Linux and macOS, in Debug and Release.
4. Keep the pull request focused. Unrelated cleanups belong in their own pull request.

Please do not push to `main` directly.

## Building and testing

ASF 6 (the default, .NET 10 SDK):

```sh
git clone --recurse-submodules https://github.com/killerboyyy777/trademanagerplus.git
cd trademanagerplus
dotnet build trademanagerplus.sln -c Release
dotnet test TradeManagerPlus.Tests -c Release
```

ASF 7 (.NET 11 SDK), from the same checkout:

```sh
git -C ArchiSteamFarm fetch origin tag 7.0.0.7 --no-tags
git -C ArchiSteamFarm checkout 7.0.0.7
git -C ArchiSteamFarm submodule update --init --recursive
dotnet build trademanagerplus.sln -c Release -p:AsfMajor=7
dotnet test TradeManagerPlus.Tests -c Release -p:AsfMajor=7
```

Return to the pinned ASF 6 version with `git submodule update --init --recursive` from the repository root.

## Code style

The repository follows ArchiSteamFarm's conventions: tabs, file-scoped namespaces, `internal sealed` types, XML documentation on configuration members and `ArgumentNullException.ThrowIfNull` for arguments. `.editorconfig` holds the enforced rules, and Release builds treat analyzer warnings as errors, so a local `dotnet build -c Release` is the fastest way to see what CI will reject.

## Changing the configuration

A new or changed property needs three things:

- the property, with a `[JsonPropertyName]` attribute and a doc comment, in `TradeManagerPlus/PluginConfig.cs`,
- validation with a readable error message in `RuleCompiler.TryCompile`, so a broken rule is reported instead of silently never matching,
- a row or paragraph in the matching part of `README.md`, plus a rule in one of the README's ```json blocks, which the test suite compiles.
