using System.Collections.Generic;

namespace InheritDocLib {
    public static class IEnumerableX {
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> me) {
            return new HashSet<T>(me);
        }
    }
}
