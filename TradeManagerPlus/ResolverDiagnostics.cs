using System;
using System.Threading;

namespace TradeManagerPlus;

/// <summary>
///     Identifies which Steam lookup resolver hit its per-offer budget.
/// </summary>
internal enum ResolverKind : byte {
	CardSetSizes,
	GooValues,
	MarketPrices
}

/// <summary>
///     Keeps budget-exhaustion warnings rate-limited so a busy bot does not write one line per offer.
/// </summary>
internal static class ResolverDiagnostics {
	private const long WarningIntervalTicks = TimeSpan.TicksPerMinute;

	private static long _lastCardSetWarningTicks;
	private static long _lastGooWarningTicks;
	private static long _lastMarketWarningTicks;

	/// <summary>
	///     Returns true at most once per minute per resolver, so the caller can log a warning about a skipped lookup.
	/// </summary>
	internal static bool ShouldLogBudgetWarning(ResolverKind kind, DateTime now) => kind switch {
		ResolverKind.CardSetSizes => ShouldLog(ref _lastCardSetWarningTicks, now),
		ResolverKind.GooValues => ShouldLog(ref _lastGooWarningTicks, now),
		ResolverKind.MarketPrices => ShouldLog(ref _lastMarketWarningTicks, now),
		_ => false
	};

	private static bool ShouldLog(ref long lastTicks, DateTime now) {
		long ticks = now.Ticks;
		long last = Interlocked.Read(ref lastTicks);

		if ((ticks - last) < WarningIntervalTicks) {
			return false;
		}

		return Interlocked.CompareExchange(ref lastTicks, ticks, last) == last;
	}
}
