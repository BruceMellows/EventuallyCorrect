// MIT License
//
// Copyright (c) 2026 BruceMellows
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.Concurrent;

namespace BruceMellows.EventuallyCorrect;

/// <summary>
/// A lock free cache for a Key/Value lookup where the values that are derived from the key may be lost but will be populated again later upon demand.<br/>
/// The objective is to create the fastest lookup possible once the cache is complete at the expense of performance while the cache is being populated.<br/><br/>
/// NOTES:<br/>
/// If you need to ensure that you only call the value factory once for each key, DO NOT use this class.<br/>
/// If you are not prepared to sacrifice startup time for execution time, DO NOT use this class.
/// </summary>
/// <typeparam name="TKey">The key for the lookup.</typeparam>
/// <typeparam name="TValue">The value from the lookup.</typeparam>
/// <param name="identityFactory">The converter from key to string that is used in debug build to ensure that keys are unique.</param>
/// <param name="valueFactory">The converter from key to value that is the source of the value that is returned by the GetValue method.</param>
/// <param name="comparer">The comparer of keys that is used to return a previously derived value from the key.</param>
public sealed class EventuallyCorrectCache<TKey, TIdentity, TValue>(Func<TKey, TValue> valueFactory, IEqualityComparer<TKey> comparer, Func<TKey, TIdentity>? identityFactory = default)
	: IEventuallyCorrectCache<TKey, string, TValue>
	where TKey : notnull
	where TIdentity : notnull
{
	public TValue GetValue(TKey newKey)
	{
		var snapshot = cache;
		if (!snapshot.TryGetValue(newKey, out var value))
		{
#if DEBUG
			if (identityFactory is not null)
			{
				_ = identityUniqueness.AddOrUpdate(
					identityFactory(newKey),
					_ => newKey,
					(identity, existingKey) =>
					{
						// Check reference equality of the expression object
						if (comparer.Equals(newKey, existingKey))
						{
							return newKey;
						}

						throw IdentityReuseException.Create<TKey, TIdentity, TValue>();
					});
			}
#endif

			value = valueFactory(newKey);
			if (ReferenceEquals(snapshot, cache))
			{
				cache = new Dictionary<TKey, TValue>(snapshot, snapshot.Comparer)
				{
					[newKey] = value
				};
			}
		}

		return value;
	}

	readonly Func<TKey, TValue> valueFactory = valueFactory;
	readonly Func<TKey, TIdentity>? identityFactory = identityFactory;

	Dictionary<TKey, TValue> cache = new(comparer);

#if DEBUG
	// Debug-only dictionary mapping expression identity to PropertyInfo (for reuse check)
	static readonly ConcurrentDictionary<TIdentity, TKey> identityUniqueness = new();
#endif
}
