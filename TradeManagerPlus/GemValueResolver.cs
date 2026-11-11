using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Data;
using ArchiSteamFarm.Steam.Integration;
using ArchiSteamFarm.Web.Responses;

namespace TradeManagerPlus;

/// <summary>
///     Resolves the gem ("goo") value Steam itself gives an item, which is what <c>Value.GooTypes</c> rules compare.
/// </summary>
/// <remarks>
///     The value only depends on the item's type, AppID and foil border, so one lookup covers every item of that kind and
///     the result is cached. A failed lookup is cached briefly and never matches, which keeps a broken lookup from
///     accepting a trade by accident. The lookup count per offer is bounded because the endpoint is rate-limited.
/// </remarks>
internal static class GemValueResolver {
	private const int MaxLookupsPerOffer = 25;
	private static readonly ConcurrentDictionary<GooKey, CacheEntry> Cache = new();
	private static readonly SemaphoreSlim LookupSemaphore = new(1, 1);
	private static readonly TimeSpan SuccessTTL = TimeSpan.FromDays(7);
	private static readonly TimeSpan FailureTTL = TimeSpan.FromMinutes(30);

	/// <summary>
	///     Looks up the gem value of the given items and returns a lookup for the matcher, or null when there is nothing
	///     worth looking up. Items without a known value resolve to null.
	/// </summary>
	internal static async Task<Func<Asset, ulong?>?> ResolveAsync(Bot bot, IEnumerable<Asset> items, CancellationToken cancellationToken = default) {
		Dictionary<GooKey, uint> values = [];
		int lookups = 0;

		foreach (Asset item in items) {
			if (!TryGetGooKey(item, out GooKey key) || values.ContainsKey(key)) {
				continue;
			}

			if (lookups >= MaxLookupsPerOffer) {
				break;
			}

			lookups++;

			uint? value = await GetGooValueAsync(bot, key, cancellationToken).ConfigureAwait(false);

			if (value.HasValue) {
				values[key] = value.Value;
			}
		}

		if (values.Count == 0) {
			return null;
		}

		return asset => {
			if (!TryGetGooKey(asset, out GooKey key) || !values.TryGetValue(key, out uint value)) {
				return null;
			}

			return value;
		};
	}

	/// <summary>
	///     Maps an item type to the <c>item_type</c> and <c>border_color</c> Steam uses for its gem values, which are the
	///     numeric parts of the item's <c>item_class_N</c> and <c>cardborder_N</c> tags.
	/// </summary>
	internal static bool TryGetGooKind(EAssetType type, out byte itemType, out byte border) {
		border = 0;

		switch (type) {
			case EAssetType.TradingCard:
				itemType = 2;

				return true;
			case EAssetType.FoilTradingCard:
				itemType = 2;
				border = 1;

				return true;
			case EAssetType.ProfileBackground:
				itemType = 3;

				return true;
			case EAssetType.Emoticon:
				itemType = 4;

				return true;
			case EAssetType.BoosterPack:
				itemType = 5;

				return true;
			case EAssetType.SteamGems:
				itemType = 7;

				return true;
			case EAssetType.Sticker:
				itemType = 11;

				return true;
			case EAssetType.ChatEffect:
				itemType = 12;

				return true;
			case EAssetType.MiniProfileBackground:
				itemType = 13;

				return true;
			case EAssetType.AvatarProfileFrame:
				itemType = 14;

				return true;
			case EAssetType.AnimatedAvatar:
				itemType = 15;

				return true;
			case EAssetType.KeyboardSkin:
				itemType = 16;

				return true;
			case EAssetType.StartupVideo:
				itemType = 17;

				return true;
			default:
				itemType = 0;

				return false;
		}
	}

	private static bool TryGetGooKey(Asset item, out GooKey key) {
		key = default;

		if ((item.RealAppID == 0) || !TryGetGooKind(item.Type, out byte itemType, out byte border)) {
			return false;
		}

		key = new GooKey(item.RealAppID, itemType, border);

		return true;
	}

	private static async Task<uint?> GetGooValueAsync(Bot bot, GooKey key, CancellationToken cancellationToken) {
		if (Cache.TryGetValue(key, out CacheEntry cached) && (cached.ExpiresUtc > DateTime.UtcNow)) {
			return cached.Value;
		}

		await LookupSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

		try {
			// Another offer may have resolved this item kind while we were waiting for the semaphore.
			if (Cache.TryGetValue(key, out cached) && (cached.ExpiresUtc > DateTime.UtcNow)) {
				return cached.Value;
			}

			uint? value = await FetchGooValueAsync(bot, key, cancellationToken).ConfigureAwait(false);

			Cache[key] = new CacheEntry(value, DateTime.UtcNow + (value.HasValue ? SuccessTTL : FailureTTL));

			return value;
		} finally {
			LookupSemaphore.Release();
		}
	}

	private static async Task<uint?> FetchGooValueAsync(Bot bot, GooKey key, CancellationToken cancellationToken) {
		Uri request = new(ArchiWebHandler.SteamCommunityURL, $"/auction/ajaxgetgoovalueforitemtype/?appid={key.AppID}&item_type={key.ItemType}&border_color={key.Border}");

		ObjectResponse<GooValueResponse>? response;

		try {
			response = await bot.ArchiWebHandler.UrlGetToJsonObjectWithSession<GooValueResponse>(request, checkSessionPreemptively: false, cancellationToken: cancellationToken).ConfigureAwait(false);
		} catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested) {
			bot.ArchiLogger.LogGenericException(e);

			return null;
		}

		GooValueResponse? content = response?.Content;

		if ((content != null) && (content.Success == 1) && uint.TryParse(content.GooValue, out uint value) && (value > 0)) {
			return value;
		}

		bot.ArchiLogger.LogGenericWarning($"[TradeManagerPlus] Unable to read the gem value of item type {key.ItemType}/{key.Border} of app {key.AppID}; rules that use Steam values will not accept it.");

		return null;
	}

	private readonly record struct GooKey(uint AppID, byte ItemType, byte Border);

	private readonly record struct CacheEntry(uint? Value, DateTime ExpiresUtc);

	private sealed class GooValueResponse {
		[JsonPropertyName("success")]
		public int Success { get; init; }

		[JsonPropertyName("goo_value")]
		public string? GooValue { get; init; }
	}
}
