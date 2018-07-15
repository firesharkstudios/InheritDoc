using System;
using System.IO;

namespace InheritDocLib {
    public static class FileX {
        public static string MakeRelativePath(string rawBasePath, string file) {
            var fileWithRoot = GetPathWithRoot(file);
            Uri fileUri = new Uri(fileWithRoot);

            var rawBasePathWithRoot = GetPathWithRoot(rawBasePath);
            string basePath = rawBasePathWithRoot.EndsWith(Path.DirectorySeparatorChar.ToString()) ? rawBasePathWithRoot : rawBasePathWithRoot + Path.DirectorySeparatorChar;
            Uri basePathUri = new Uri(basePath);

            if (fileUri.Scheme != basePathUri.Scheme) { return basePath; } // path can't be made relative.

            Uri relativeUri = basePathUri.MakeRelativeUri(fileUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (basePathUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase)) {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        public static string GetPathWithRoot(string path) {
            if (path.Contains(":")) {
                return path;
            }
            else {
                var pathRoot = Path.GetPathRoot(System.Environment.CurrentDirectory);
                if (path.StartsWith(@"\")) {
                    return pathRoot + path.Substring(1);
                }
                else {
                    return Path.Combine(System.Environment.CurrentDirectory, path);
                }
            }
        }
    }
}
