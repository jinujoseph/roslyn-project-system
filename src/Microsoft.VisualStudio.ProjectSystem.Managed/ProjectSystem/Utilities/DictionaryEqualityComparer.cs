﻿//-----------------------------------------------------------------------
// NOTE that this was taken from CPS along with a couple of internal methods
// from CommonProjectSystemTools.cs in cps
//
// <copyright file="DictionaryEqualityComparer.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.VisualStudio.ProjectSystem.Utilities
{
    using System.Collections.Generic;
    using System.Collections.Immutable;

    /// <summary>
    /// Provides simple dictionary equality checks.
    /// </summary>
    /// <typeparam name="TKey">The type of key in the dictionaries to compare.</typeparam>
    /// <typeparam name="TValue">The type of value in the dictionaries to compare.</typeparam>
    internal class DictionaryEqualityComparer<TKey, TValue> : IEqualityComparer<IImmutableDictionary<TKey, TValue>>
    {
        /// <summary>
        /// Backing field for the <see cref="Instance"/> static property.
        /// </summary>
        private static DictionaryEqualityComparer<TKey, TValue> defaultInstance = new DictionaryEqualityComparer<TKey, TValue>();

        /// <summary>
        /// Initializes a new instance of the DictionaryEqualityComparer class.
        /// </summary>
        private DictionaryEqualityComparer()
        {
        }

        /// <summary>
        /// Gets a dictionary equality comparer instance appropriate for dictionaries that use the default key comparer for the <typeparamref name="TKey"/> type.
        /// </summary>
        internal static IEqualityComparer<IImmutableDictionary<TKey, TValue>> Instance
        {
            get { return defaultInstance; }
        }

        /// <summary>
        /// Checks two dictionaries for equality.
        /// </summary>
        public bool Equals(IImmutableDictionary<TKey, TValue> x, IImmutableDictionary<TKey, TValue> y)
        {
            return AreEquivalent(x, y);
        }

        /// <summary>
        /// Calculates a hash code for a dictionary.
        /// </summary>
        public int GetHashCode(IImmutableDictionary<TKey, TValue> obj)
        {
            int hashCode = 0;

            var concreteDictionary1 = obj as ImmutableDictionary<TKey, TValue>;
            IEqualityComparer<TKey> keyComparer = concreteDictionary1 != null ? concreteDictionary1.KeyComparer : EqualityComparer<TKey>.Default;
            IEqualityComparer<TValue> valueComparer = concreteDictionary1 != null ? concreteDictionary1.ValueComparer : EqualityComparer<TValue>.Default;
            if (obj != null)
            {
                foreach (var pair in obj)
                {
                    hashCode += keyComparer.GetHashCode(pair.Key) + valueComparer.GetHashCode(pair.Value);
                }
            }

            return hashCode;
        }
        /// <summary>
        /// Tests two dictionaries to see if their contents are identical.
        /// </summary>
        /// <typeparam name="TKey">The type of key used in the dictionary.  May be null.</typeparam>
        /// <typeparam name="TValue">The type of value used in the dictionary.  May be null.</typeparam>
        /// <param name="dictionary1">One dictionary to compare.</param>
        /// <param name="dictionary2">The other dictionary to compare.</param>
        /// <returns><c>true</c> if the dictionaries' contents are equivalent.  <c>false</c> otherwise.</returns>
        private static bool AreEquivalent(IImmutableDictionary<TKey, TValue> dictionary1, IImmutableDictionary<TKey, TValue> dictionary2)
        {
            Requires.NotNull(dictionary1, "dictionary1");
            if (dictionary1 == dictionary2)
            {
                return true;
            }

            var concreteDictionary1 = dictionary1 as ImmutableDictionary<TKey, TValue>;
            IEqualityComparer<TValue> valueComparer = concreteDictionary1 != null ? concreteDictionary1.ValueComparer : EqualityComparer<TValue>.Default;
            return AreEquivalent((IReadOnlyDictionary<TKey, TValue>)dictionary1, (IReadOnlyDictionary<TKey, TValue>)dictionary2, valueComparer);
        }

        /// <summary>
        /// Tests two dictionaries to see if their contents are identical.
        /// </summary>
        /// <typeparam name="TKey">The type of key used in the dictionary.  May be null.</typeparam>
        /// <typeparam name="TValue">The type of value used in the dictionary.  May be null.</typeparam>
        /// <param name="dictionary1">One dictionary to compare.</param>
        /// <param name="dictionary2">The other dictionary to compare.</param>
        /// <param name="valueComparer">The comparer to use to determine equivalence of the dictionary values.</param>
        /// <returns><c>true</c> if the dictionaries' contents are equivalent.  <c>false</c> otherwise.</returns>
        private static bool AreEquivalent(IReadOnlyDictionary<TKey, TValue> dictionary1, IReadOnlyDictionary<TKey, TValue> dictionary2, IEqualityComparer<TValue> valueComparer)
        {
            Requires.NotNull(valueComparer, "valueComparer");

            if (dictionary1 == dictionary2)
            {
                return true;
            }

            if ((dictionary1 == null) ^ (dictionary2 == null)) // XOR
            {
                return false;
            }

            if (dictionary1.Count != dictionary2.Count)
            {
                return false;
            }

            if (dictionary1.Count == 0)
            {
                // both dictionaries are empty, so bail out early to avoid
                // allocating an IEnumerator.
                return true;
            }

            foreach (KeyValuePair<TKey, TValue> pair in dictionary1)
            {
                TValue value;
                if (!dictionary2.TryGetValue(pair.Key, out value) || !valueComparer.Equals(value, pair.Value))
                {
                    return false;
                }
            }

            return true;
        }

    }
}
