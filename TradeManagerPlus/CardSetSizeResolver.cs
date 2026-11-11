using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Integration;
using ArchiSteamFarm.Web.Responses;

namespace TradeManagerPlus;

/// <summary>
///     Resolves how many cards a game's Steam card set contains, which is what <c>SetSizes</c> conditions are compared
///     against.
/// </summary>
/// <remarks>
///     ASF knows this value internally (<c>ArchiWebHandler.GetCardCountForGame</c>) but does not expose it to plugins, so
///     the same community page is read here and cached per AppID. A failed lookup is cached briefly and never matches,
///     which keeps a broken lookup from accepting a trade by accident.
/// </remarks>
internal static class CardSetSizeResolver {
	private static readonly ConcurrentDictionary<uint, CacheEntry> Cache = new();
	private static readonly SemaphoreSlim LookupSemaphore = new(1, 1);
	private static readonly TimeSpan SuccessTTL = TimeSpan.FromDays(7);
	private static readonly TimeSpan FailureTTL = TimeSpan.FromMinutes(30);

	internal static async Task<IReadOnlyDictionary<uint, byte>> ResolveAsync(Bot bot, IEnumerable<uint> appIDs, CancellationToken cancellationToken = default) {
		Dictionary<uint, byte> result = [];

		foreach (uint appID in appIDs) {
			byte? setSize = await GetCardSetSizeAsync(bot, appID, cancellationToken).ConfigureAwait(false);

			if (setSize.HasValue) {
				result[appID] = setSize.Value;
			}
		}

		return result;
	}

	private static async Task<byte?> GetCardSetSizeAsync(Bot bot, uint appID, CancellationToken cancellationToken) {
		if (Cache.TryGetValue(appID, out CacheEntry cached) && (cached.ExpiresUtc > DateTime.UtcNow)) {
			return cached.Size;
		}

		await LookupSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

		try {
			// Another offer may have resolved this AppID while we were waiting for the semaphore.
			if (Cache.TryGetValue(appID, out cached) && (cached.ExpiresUtc > DateTime.UtcNow)) {
				return cached.Size;
			}

			byte? setSize = await FetchCardSetSizeAsync(bot, appID, cancellationToken).ConfigureAwait(false);

			Cache[appID] = new CacheEntry(setSize, DateTime.UtcNow + (setSize.HasValue ? SuccessTTL : FailureTTL));

			return setSize;
		} finally {
			LookupSemaphore.Release();
		}
	}

	private static async Task<byte?> FetchCardSetSizeAsync(Bot bot, uint appID, CancellationToken cancellationToken) {
		Uri request = new(ArchiWebHandler.SteamCommunityURL, $"/my/gamecards/{appID}?l=english");

		HtmlDocumentResponse? response;

		try {
			response = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(request, checkSessionPreemptively: false, cancellationToken: cancellationToken).ConfigureAwait(false);
		} catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested) {
			bot.ArchiLogger.LogGenericException(e);

			return null;
		}

		using (response) {
			IDocument? document = response?.Content;

			if (document == null) {
				bot.ArchiLogger.LogGenericWarning($"[TradeManagerPlus] Unable to read the card set page of app {appID}; rules that use SetSizes will not accept items from it.");

				return null;
			}

			int cardCount = document.QuerySelectorAll("div[class='badge_card_set_cards'] > div[class^='badge_card_set_card']").Length;

			if ((cardCount <= 0) || (cardCount > byte.MaxValue)) {
				bot.ArchiLogger.LogGenericWarning($"[TradeManagerPlus] App {appID} has no readable card set; rules that use SetSizes will not accept items from it.");

				return null;
			}

			return (byte) cardCount;
		}
	}

	private readonly record struct CacheEntry(byte? Size, DateTime ExpiresUtc);
}
