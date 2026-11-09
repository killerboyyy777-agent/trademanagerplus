using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Helpers.Json;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Data;
using ArchiSteamFarm.Steam.Exchange;
using ArchiSteamFarm.Steam.Storage;
using JetBrains.Annotations;

#if ASF7
using BotUserAccess = ArchiSteamFarm.Steam.EBotUserAccess;
using TradeBehaviour = ArchiSteamFarm.Steam.Storage.EBotBehaviour;
using TradeResult = ArchiSteamFarm.Steam.Exchange.ETradeResult;
#else
using BotUserAccess = ArchiSteamFarm.Steam.EAccess;
using TradeBehaviour = ArchiSteamFarm.Steam.Storage.BotConfig.EBotBehaviour;
using TradeResult = ArchiSteamFarm.Steam.Exchange.ParseTradeResult.EResult;
#endif

namespace TradeManagerPlus;

internal sealed class BotRuleSet {
	internal bool Enabled { get; init; } = true;
	internal IReadOnlyList<CompiledTradeRule> Rules { get; init; } = [];
}

#pragma warning disable CA1812 // ASF instantiates this class during runtime
[UsedImplicitly]
#if ASF7
internal sealed class TradeManagerPlusPlugin : IGitHubPluginUpdates, IBot, IBotCommand, IBotModules, IBotTradeOffer {
#else
internal sealed class TradeManagerPlusPlugin : IGitHubPluginUpdates, IBot, IBotCommand2, IBotModules, IBotTradeOffer2 {
#endif
	private const string PluginName = "TradeManagerPlus";
	private const string Repository = "killerboyyy777/trademanagerplus";
	private const int MaxRulesShownInCommand = 10;

	private readonly ConcurrentDictionary<string, BotRuleSet> BotRules = new(StringComparer.Ordinal);

	public string Name => PluginName;

	public string RepositoryName => Repository;

	public Version Version => typeof(TradeManagerPlusPlugin).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

#if ASF7
	public Task OnLoaded(CancellationToken cancellationToken = default) {
#else
	public Task OnLoaded() {
#endif
		ASF.ArchiLogger.LogGenericInfo($"{PluginName} v{Version} loaded. Configure rules in the '{PluginName}' section of your bot config.");

		return Task.CompletedTask;
	}

#if ASF7
	public Task OnBotInit(Bot bot, CancellationToken cancellationToken = default) {
#else
	public Task OnBotInit(Bot bot) {
#endif
		ArgumentNullException.ThrowIfNull(bot);

		BotRules.TryRemove(bot.BotName, out _);

		return Task.CompletedTask;
	}

#if ASF7
	public Task OnBotDestroy(Bot bot, CancellationToken cancellationToken = default) {
#else
	public Task OnBotDestroy(Bot bot) {
#endif
		ArgumentNullException.ThrowIfNull(bot);

		BotRules.TryRemove(bot.BotName, out _);

		return Task.CompletedTask;
	}

#if ASF7
	public Task OnBotInitModules(Bot bot, IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null, CancellationToken cancellationToken = default) {
#else
	public Task OnBotInitModules(Bot bot, IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
#endif
		ArgumentNullException.ThrowIfNull(bot);

		JsonElement? configElement = GetPluginConfigElement(additionalConfigProperties);

		if (configElement == null) {
			BotRules.TryRemove(bot.BotName, out _);

			return Task.CompletedTask;
		}

		PluginConfig? config;

		try {
			config = configElement.Value.ToJsonObject<PluginConfig>();
		} catch (Exception e) when (e is JsonException or NotSupportedException) {
			bot.ArchiLogger.LogGenericError($"[{PluginName}] Failed to parse config: {e.Message}");

			BotRules.TryRemove(bot.BotName, out _);

			return Task.CompletedTask;
		}

		if (config == null) {
			BotRules.TryRemove(bot.BotName, out _);

			return Task.CompletedTask;
		}

		if (!config.Enabled) {
			BotRules[bot.BotName] = new BotRuleSet { Enabled = false };
			bot.ArchiLogger.LogGenericInfo($"[{PluginName}] Disabled by config for {bot.BotName}.");

			return Task.CompletedTask;
		}

		List<CompiledTradeRule> rules = [];
		int ruleNumber = 0;

		foreach (TradeRuleConfig ruleConfig in config.TradeRules ?? []) {
			ruleNumber++;

			if (ruleConfig == null) {
				bot.ArchiLogger.LogGenericWarning($"[{PluginName}] Rule #{ruleNumber} is null and has been skipped.");

				continue;
			}

			if (RuleCompiler.TryCompile(ruleConfig, out CompiledTradeRule? rule, out string? error)) {
				rules.Add(rule!);

				continue;
			}

			bot.ArchiLogger.LogGenericError($"[{PluginName}] Rule #{ruleNumber} is invalid and has been skipped: {error}.");
		}

		BotRules[bot.BotName] = new BotRuleSet { Rules = rules };

		int acceptRules = rules.Count(static rule => rule.Action == TradeRuleAction.Accept);
		int declineRules = rules.Count - acceptRules;

		bot.ArchiLogger.LogGenericInfo($"[{PluginName}] Loaded {rules.Count} valid rule(s) for {bot.BotName}: {acceptRules} accept, {declineRules} decline.");

		return Task.CompletedTask;
	}

#if ASF7
	public Task<string?> OnBotCommand(Bot bot, BotUserAccess access, string message, string[] args, ulong steamID = 0, CancellationToken cancellationToken = default) {
#else
	public Task<string?> OnBotCommand(Bot bot, BotUserAccess access, string message, string[] args, ulong steamID = 0) {
#endif
		ArgumentNullException.ThrowIfNull(bot);

		if ((args == null) || (args.Length == 0)) {
			return Task.FromResult<string?>(null);
		}

		string command = args[0].ToUpperInvariant();

		if ((command != "TMP") && (command != "TEST")) {
			return Task.FromResult<string?>(null);
		}

		if (access < BotUserAccess.Master) {
			return Task.FromResult(access > BotUserAccess.None ? bot.Commands.FormatBotResponse("Access denied.") : null);
		}

		string subcommand = args.Length > 1 ? args[1].ToUpperInvariant() : "STATUS";

		BotRules.TryGetValue(bot.BotName, out BotRuleSet? ruleSet);

		string response = subcommand switch {
			"STATUS" => FormatStatus(bot, ruleSet),
			"RULES" => FormatRules(bot, ruleSet),
			_ => FormatHelp()
		};

		return Task.FromResult<string?>(bot.Commands.FormatBotResponse(response));
	}

#if ASF7
	public async Task<bool> OnBotTradeOffer(Bot bot, TradeOffer tradeOffer, TradeResult asfResult, CancellationToken cancellationToken = default) {
#else
	public async Task<bool> OnBotTradeOffer(Bot bot, TradeOffer tradeOffer, TradeResult asfResult) {
#endif
		ArgumentNullException.ThrowIfNull(bot);
		ArgumentNullException.ThrowIfNull(tradeOffer);

		if (!BotRules.TryGetValue(bot.BotName, out BotRuleSet? ruleSet) || !ruleSet.Enabled || (ruleSet.Rules.Count == 0)) {
			return false;
		}

		// ASF blacklists trades from untrusted bots and bad actors for a reason. Never override that decision.
		if (asfResult == TradeResult.Blacklisted) {
			bot.ArchiLogger.LogGenericWarning($"[{PluginName}] Not accepting blacklisted trade offer {tradeOffer.TradeOfferID}; ASF will decline it.");

			return false;
		}

		IReadOnlyCollection<Asset> itemsToReceive = tradeOffer.ItemsToReceiveReadOnly;
		IReadOnlyCollection<Asset> itemsToGive = tradeOffer.ItemsToGiveReadOnly;

		foreach (CompiledTradeRule rule in ruleSet.Rules) {
			if (!TradeMatcher.IsMatch(rule, itemsToReceive, itemsToGive)) {
				continue;
			}

			string description = string.IsNullOrWhiteSpace(rule.Description) ? "(no description)" : rule.Description;

			switch (rule.Action) {
				case TradeRuleAction.Accept:
					bot.ArchiLogger.LogGenericInfo($"[{PluginName}] Accepting trade offer {tradeOffer.TradeOfferID} due to rule \"{description}\".");

					return true;
				case TradeRuleAction.Decline:
					if (AsfWillDeclineOffer(bot, asfResult)) {
						bot.ArchiLogger.LogGenericInfo($"[{PluginName}] Trade offer {tradeOffer.TradeOfferID} matched decline rule \"{description}\"; ASF will decline it.");

						return false;
					}

					bot.ArchiLogger.LogGenericInfo($"[{PluginName}] Declining trade offer {tradeOffer.TradeOfferID} due to rule \"{description}\".");

#if ASF7
					if (!await bot.ArchiWebHandler.DeclineTradeOffer(tradeOffer.TradeOfferID, cancellationToken).ConfigureAwait(false)) {
#else
					if (!await bot.ArchiWebHandler.DeclineTradeOffer(tradeOffer.TradeOfferID).ConfigureAwait(false)) {
#endif
						bot.ArchiLogger.LogGenericWarning($"[{PluginName}] Failed to decline trade offer {tradeOffer.TradeOfferID}.");
					}

					return false;
				default:
					return false;
			}
		}

		return false;
	}

	private static bool AsfWillDeclineOffer(Bot bot, TradeResult asfResult) => asfResult switch {
		TradeResult.Blacklisted => true,
		TradeResult.Rejected => bot.BotConfig.BotBehaviour.HasFlag(TradeBehaviour.RejectInvalidTrades),
		_ => false
	};

	internal static JsonElement? GetPluginConfigElement(IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties) {
		if (additionalConfigProperties == null) {
			return null;
		}

		// Standard ASF layout: a top-level property named after the plugin.
		foreach ((string key, JsonElement value) in additionalConfigProperties) {
			if (key.Equals(PluginName, StringComparison.OrdinalIgnoreCase)) {
				return value;
			}
		}

		// Legacy layout used by earlier versions of this plugin's README: "Plugins": { "TradeManagerPlus": { ... } }.
		foreach ((string key, JsonElement value) in additionalConfigProperties) {
			if (!key.Equals("Plugins", StringComparison.OrdinalIgnoreCase) || (value.ValueKind != JsonValueKind.Object)) {
				continue;
			}

			foreach (JsonProperty property in value.EnumerateObject()) {
				if (property.Name.Equals(PluginName, StringComparison.OrdinalIgnoreCase)) {
					return property.Value;
				}
			}
		}

		return null;
	}

	private static string FormatStatus(Bot bot, BotRuleSet? ruleSet) {
		if (ruleSet == null) {
			return $"{PluginName} is not configured for {bot.BotName}.";
		}

		if (!ruleSet.Enabled) {
			return $"{PluginName} is disabled for {bot.BotName}.";
		}

		int acceptRules = ruleSet.Rules.Count(static rule => rule.Action == TradeRuleAction.Accept);
		int declineRules = ruleSet.Rules.Count - acceptRules;

		return $"{PluginName} is enabled for {bot.BotName} with {ruleSet.Rules.Count} valid rule(s): {acceptRules} accept, {declineRules} decline.";
	}

	private static string FormatRules(Bot bot, BotRuleSet? ruleSet) {
		if (ruleSet == null) {
			return $"{PluginName} is not configured for {bot.BotName}.";
		}

		if (ruleSet.Rules.Count == 0) {
			return $"{PluginName} has no trade rules configured for {bot.BotName}.";
		}

		StringBuilder response = new();
		response.Append(PluginName).Append(" rules for ").Append(bot.BotName).Append(':');

		int shownRules = Math.Min(ruleSet.Rules.Count, MaxRulesShownInCommand);

		for (int i = 0; i < shownRules; i++) {
			CompiledTradeRule rule = ruleSet.Rules[i];

			response.Append(Environment.NewLine).Append(i + 1).Append(". ").Append(rule.Action.ToString()).Append(": ").Append(string.IsNullOrWhiteSpace(rule.Description) ? "(no description)" : rule.Description);
			response.Append(" [receive: ").Append(rule.TheirItems.Describe()).Append("; give: ").Append(rule.MyItems.Describe()).Append(']');
		}

		if (ruleSet.Rules.Count > shownRules) {
			response.Append(Environment.NewLine).Append("...and ").Append(ruleSet.Rules.Count - shownRules).Append(" more rule(s).");
		}

		return response.ToString();
	}

	private static string FormatHelp() => $"{PluginName} commands: TMP STATUS, TMP RULES. The old TEST command is kept as an alias of TMP STATUS.";
}
#pragma warning restore CA1812 // ASF instantiates this class during runtime
