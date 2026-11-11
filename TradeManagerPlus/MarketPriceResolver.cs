using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Data;
using ArchiSteamFarm.Steam.Integration;
using ArchiSteamFarm.Web.Responses;

namespace TradeManagerPlus;

/// <summary>
///     A trusted Steam Community market price for a single unit of an item, together with how many units were sold in the
///     last 24 hours when the price was read.
/// </summary>
internal readonly record struct MarketQuote(decimal Price, uint Volume);

/// <summary>
///     Resolves Steam Community market prices, which is what <c>MarketProfit</c> rules compare.
/// </summary>
/// <remarks>
///     A market price is only worth trusting when it is not easy to push around, so this resolver is deliberately
///     conservative: it reads the median of recent sales instead of the cheapest listing whenever Steam reports one,
///     keeps the volume so the rule can refuse thin markets, and treats anything it cannot read as unknown rather than
///     as zero. Unknown prices never match, so every failure of this resolver makes a rule reject a trade instead of
///     accepting it.
/// </remarks>
internal static class MarketPriceResolver {
	private const int MaxLookupsPerOffer = 10;
	private static readonly ConcurrentDictionary<PriceKey, CacheEntry> Cache = new();
	private static readonly SemaphoreSlim LookupSemaphore = new(1, 1);
	private static readonly TimeSpan SuccessTTL = TimeSpan.FromHours(1);
	private static readonly TimeSpan FailureTTL = TimeSpan.FromMinutes(30);

	/// <summary>
	///     Reads the market prices of the given items in the given Steam currency and returns a lookup for the matcher,
	///     or null when no item could be priced. Items without a market name resolve to null.
	/// </summary>
	internal static async Task<Func<Asset, MarketQuote?>?> ResolveAsync(Bot bot, IEnumerable<Asset> items, int currency, CancellationToken cancellationToken = default) {
		ArgumentNullException.ThrowIfNull(bot);
		ArgumentNullException.ThrowIfNull(items);

		Dictionary<PriceKey, MarketQuote?> quotes = [];

		foreach (Asset item in items) {
			if (quotes.Count >= MaxLookupsPerOffer) {
				break;
			}

			if (!TryGetPriceKey(item, currency, out PriceKey key) || quotes.ContainsKey(key)) {
				continue;
			}

			quotes[key] = await GetQuoteAsync(bot, key, cancellationToken).ConfigureAwait(false);
		}

		if (quotes.Count == 0) {
			return null;
		}

		return item => TryGetPriceKey(item, currency, out PriceKey key) && quotes.TryGetValue(key, out MarketQuote? quote) ? quote : null;
	}

	/// <summary>
	///     Parses a Steam money string such as <c>$1.23</c>, <c>1,23€</c> or <c>$1,234.56</c>. Returns false for anything
	///     that does not look like a positive price.
	/// </summary>
	internal static bool TryParsePrice(string? text, out decimal value) {
		value = 0;

		if (string.IsNullOrWhiteSpace(text)) {
			return false;
		}

		StringBuilder cleaned = new(text.Length);

		foreach (char character in text) {
			if (char.IsAsciiDigit(character) || (character == ',') || (character == '.')) {
				cleaned.Append(character);
			}
		}

		if (cleaned.Length == 0) {
			return false;
		}

		string cleanedText = cleaned.ToString();
		int lastSeparator = -1;

		for (int i = cleanedText.Length - 1; i >= 0; i--) {
			if (cleanedText[i] is ',' or '.') {
				lastSeparator = i;

				break;
			}
		}

		if (lastSeparator < 0) {
			return decimal.TryParse(cleanedText, NumberStyles.None, CultureInfo.InvariantCulture, out value) && (value > 0);
		}

		int digitsAfterSeparator = cleanedText.Length - lastSeparator - 1;

		// One or two digits after the last separator are the cents. Anything else is a thousands group, which Steam only
		// uses in front of the cents, so the whole part is an integer.
		if (digitsAfterSeparator is not (1 or 2)) {
			string digitsOnly = new(cleanedText.Where(char.IsAsciiDigit).ToArray());

			return decimal.TryParse(digitsOnly, NumberStyles.None, CultureInfo.InvariantCulture, out value) && (value > 0);
		}

		StringBuilder normalized = new(cleanedText.Length);

		for (int i = 0; i < lastSeparator; i++) {
			if (char.IsAsciiDigit(cleanedText[i])) {
				normalized.Append(cleanedText[i]);
			}
		}

		normalized.Append('.').Append(cleanedText, lastSeparator + 1, digitsAfterSeparator);

		return decimal.TryParse(normalized.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && (value > 0);
	}

	/// <summary>
	///     Parses Steam's sold-units string such as <c>15,462</c>. A missing volume counts as zero, which is untrusted.
	/// </summary>
	internal static uint ParseVolume(string? text) {
		if (string.IsNullOrWhiteSpace(text)) {
			return 0;
		}

		string digitsOnly = new(text.Where(char.IsAsciiDigit).ToArray());

		return uint.TryParse(digitsOnly, NumberStyles.None, CultureInfo.InvariantCulture, out uint volume) ? volume : 0;
	}

	private static async Task<MarketQuote?> GetQuoteAsync(Bot bot, PriceKey key, CancellationToken cancellationToken) {
		DateTime now = DateTime.UtcNow;

		if (Cache.TryGetValue(key, out CacheEntry cached) && (cached.ExpiresUtc > now)) {
			return cached.Quote;
		}

		await LookupSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

		try {
			now = DateTime.UtcNow;

			if (Cache.TryGetValue(key, out cached) && (cached.ExpiresUtc > now)) {
				return cached.Quote;
			}

			MarketQuote? quote = await FetchQuoteAsync(bot, key, cancellationToken).ConfigureAwait(false);

			// A price is only cached for an hour, so a spike that was real for a moment cannot keep a rule accepting for days.
			Cache[key] = new CacheEntry(quote, now + (quote.HasValue ? SuccessTTL : FailureTTL));

			return quote;
		} finally {
			LookupSemaphore.Release();
		}
	}

	private static async Task<MarketQuote?> FetchQuoteAsync(Bot bot, PriceKey key, CancellationToken cancellationToken) {
		Uri request = new(ArchiWebHandler.SteamCommunityURL, $"/market/priceoverview/?appid={key.AppID.ToString(CultureInfo.InvariantCulture)}&currency={key.Currency.ToString(CultureInfo.InvariantCulture)}&market_hash_name={Uri.EscapeDataString(key.MarketHashName)}");

		ObjectResponse<PriceOverviewResponse>? response;

		try {
			response = await bot.ArchiWebHandler.UrlGetToJsonObjectWithSession<PriceOverviewResponse>(request, checkSessionPreemptively: false, cancellationToken: cancellationToken).ConfigureAwait(false);
		} catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested) {
			bot.ArchiLogger.LogGenericWarning($"[{nameof(MarketPriceResolver)}] Failed to read the market price of {key.MarketHashName}: {e.Message}");

			return null;
		}

		PriceOverviewResponse? content = response?.Content;

		if ((content == null) || !content.Success) {
			return null;
		}

		// The median of recent sales cannot be moved by listing a single expensive item, so it is preferred over the
		// cheapest listing, which is what a price manipulator would touch.
		string? priceText = !string.IsNullOrWhiteSpace(content.MedianPrice) ? content.MedianPrice : content.LowestPrice;

		if (!TryParsePrice(priceText, out decimal price)) {
			bot.ArchiLogger.LogGenericWarning($"[{nameof(MarketPriceResolver)}] Unable to read a market price for {key.MarketHashName}; rules that use market prices will not accept it.");

			return null;
		}

		return new MarketQuote(price, ParseVolume(content.Volume));
	}

	private static bool TryGetPriceKey(Asset item, int currency, out PriceKey key) {
		key = default;

		// The asset AppID is the market AppID: 753 for cards, backgrounds and the like, 440 for TF2, 730 for CS.
		if (item.AppID == 0) {
			return false;
		}

		string? marketHashName = item.Description?.MarketHashName;

		if (string.IsNullOrWhiteSpace(marketHashName)) {
			return false;
		}

		key = new PriceKey(item.AppID, marketHashName, currency);

		return true;
	}

	private readonly record struct PriceKey(uint AppID, string MarketHashName, int Currency);

	private readonly record struct CacheEntry(MarketQuote? Quote, DateTime ExpiresUtc);

	// ReSharper disable once ClassNeverInstantiated.Local
#pragma warning disable CA1812 // Instantiated through JSON deserialization
	private sealed class PriceOverviewResponse {
		[JsonPropertyName("success")]
		public bool Success { get; init; }

		[JsonPropertyName("lowest_price")]
		public string? LowestPrice { get; init; }

		[JsonPropertyName("median_price")]
		public string? MedianPrice { get; init; }

		[JsonPropertyName("volume")]
		public string? Volume { get; init; }
	}
#pragma warning restore CA1812
}
