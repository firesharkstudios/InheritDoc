/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;

namespace InheritDocLib {
    public static class DictionaryX {

        public static V GetAs<T, U, V>(this Dictionary<T, U> me, T key, V defaultValue) {
            if (me.TryGetValue(key, out U value)) {
                return (V)Convert.ChangeType(value, typeof(V));
            }
            else {
                return defaultValue;
            }
        }

        public static void Deconstruct<T1, T2>(this KeyValuePair<T1, T2> tuple, out T1 key, out T2 value) {
            key = tuple.Key;
            value = tuple.Value;
        }
    }
}
