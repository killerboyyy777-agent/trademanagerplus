using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace TradeManagerPlus;

/// <summary>
///     A cache whose entries expire after a configurable lifetime. Expired entries are removed by a sweep that runs at
///     most once every <see cref="SweepInterval" />, so lookups stay cheap even when the cache is large.
/// </summary>
internal sealed class ExpiringCache<TKey, TValue> where TKey : notnull where TValue : struct {
	private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

	private readonly ConcurrentDictionary<TKey, Entry> _entries = new();
	private long _nextSweepUtcTicks;

	/// <summary>
	///     Number of entries currently held, including any that have expired but have not been swept yet.
	/// </summary>
	internal int Count => _entries.Count;

	/// <summary>
	///     Returns a live value for <paramref name="key" />, or null when it is missing or expired.
	/// </summary>
	internal bool TryGet(TKey key, out TValue? value) {
		DateTime now = DateTime.UtcNow;
		SweepIfDue(now);

		if (_entries.TryGetValue(key, out Entry entry) && (entry.ExpiresUtc > now)) {
			value = entry.Value;

			return true;
		}

		value = null;

		return false;
	}

	/// <summary>
	///     Stores a value with a lifetime measured from now.
	/// </summary>
	internal void Set(TKey key, TValue? value, TimeSpan lifetime) => _entries[key] = new Entry(value, DateTime.UtcNow + lifetime);

	/// <summary>
	///     Removes every entry that has expired by <paramref name="now" /> and returns how many were removed.
	/// </summary>
	internal int PruneExpired(DateTime now) {
		int removed = 0;

		foreach (KeyValuePair<TKey, Entry> pair in _entries) {
			if ((pair.Value.ExpiresUtc <= now) && _entries.TryRemove(pair)) {
				removed++;
			}
		}

		return removed;
	}

	private void SweepIfDue(DateTime now) {
		long nextSweep = Interlocked.Read(ref _nextSweepUtcTicks);

		if (now.Ticks < nextSweep) {
			return;
		}

		long newSweep = now.Add(SweepInterval).Ticks;

		if (Interlocked.CompareExchange(ref _nextSweepUtcTicks, newSweep, nextSweep) == nextSweep) {
			PruneExpired(now);
		}
	}

	private readonly record struct Entry(TValue? Value, DateTime ExpiresUtc);
}
