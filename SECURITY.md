# Security Policy

## Supported versions

Only the latest release is supported. If you report a problem, first check that it still happens with the newest plugin version and a supported ArchiSteamFarm version (6.3.11.7 or 7.0.0.7).

## Reporting a vulnerability

Do not open a public issue for a security problem. Use GitHub's [private vulnerability reporting](https://github.com/killerboyyy777/trademanagerplus/security/advisories/new) instead.

Please include:

- the plugin version and the ArchiSteamFarm version you run,
- what an attacker can achieve, and what they need to do it,
- the smallest configuration and set of trade items that demonstrates it,
- the log lines the plugin printed, if any.

A rule that accepted an offer you did not want is not a security vulnerability unless it can be triggered by someone who does not already have the ability to send you trade offers. Report those as a normal [bug report](https://github.com/killerboyyy777/trademanagerplus/issues/new?template=bug_report.md), with the offer and the configuration.

## Scope

The plugin runs inside ArchiSteamFarm and uses the Steam session ASF already has. It does not store credentials, and it never overrides ASF's blacklist. Problems in ArchiSteamFarm itself belong in the [ArchiSteamFarm repository](https://github.com/JustArchiNET/ArchiSteamFarm/security/policy).
