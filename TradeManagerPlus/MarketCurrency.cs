using System;
using System.Globalization;

namespace TradeManagerPlus;

/// <summary>
///     Parses and describes the <c>MarketProfit.Currency</c> setting. The value is either a Steam currency ID or the
///     special value <c>Wallet</c>, which follows the currency of the bot's own Steam wallet.
/// </summary>
internal static class MarketCurrency {
	/// <summary>
	///     Prevents a market rule from comparing prices in one currency while the account's wallet uses another.
	/// </summary>
	internal const string WalletName = "Wallet";

	/// <summary>
	///     Parses the configured value. An absent value keeps the historical default of USD.
	/// </summary>
	internal static bool TryParse(string? text, out int currency, out bool followsWallet, out string? error) {
		currency = 1;
		followsWallet = false;
		error = null;

		if (string.IsNullOrWhiteSpace(text)) {
			return true;
		}

		string trimmed = text.Trim();

		if (trimmed.Equals(WalletName, StringComparison.OrdinalIgnoreCase) || trimmed.Equals("Auto", StringComparison.OrdinalIgnoreCase)) {
			followsWallet = true;

			return true;
		}

		if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || (value <= 0)) {
			error = $"'MarketProfit.Currency' contains unknown currency '{text}'. Use a Steam currency ID such as 1 for USD or 3 for EUR, or 'Wallet' to follow the currency of the bot's Steam wallet.";

			return false;
		}

		currency = value;

		return true;
	}

	/// <summary>
	///     Renders the currency for the <c>!TMP RULES</c> command.
	/// </summary>
	internal static string Describe(int currency, bool followsWallet) => followsWallet ? "the bot's wallet currency" : currency.ToString(CultureInfo.InvariantCulture);
}
