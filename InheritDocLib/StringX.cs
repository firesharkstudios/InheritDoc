using System;
using System.Text.RegularExpressions;

namespace InheritDocLib {
    public static class StringX {
        public static Regex WildCardToRegex(String value) {
            return new Regex("^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$");
        }
    }
}
