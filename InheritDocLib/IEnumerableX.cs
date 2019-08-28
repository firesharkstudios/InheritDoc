/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;

namespace InheritDocLib {
    public static class IEnumerableX {
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> me) {
            return new HashSet<T>(me);
        }
    }
}
