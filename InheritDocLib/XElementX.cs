using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace InheritDocLib {
    public static class XElementX {

        public static string GetPath(this XElement me, string delim = "/", XElement stop = null) {
            StringBuilder sb = new StringBuilder();
            XElement current = me;
            while (current != null && current != stop) {
                if (current != me) sb.Insert(0, delim);
                sb.Insert(0, current.Name.LocalName);
                current = current.Parent;
            }
            if (sb.Length == 0) return null;
            else return sb.ToString();
        }

        public static IEnumerable<XElement> Select(this XElement me, string[] pathParts) {
            var current = me;
            if (pathParts != null) {
                for (int i = 0; i < pathParts.Length; i++) {
                    var childElements = current.Elements(pathParts[i]);
                    if (i==pathParts.Length-1) {
                        return childElements;
                    }
                    else {
                        current = childElements.SingleOrDefault();
                        if (current == null) return null;
                    }
                }
            }
            return new XElement[] { current };
        }

        public static void CopyFrom(this XElement target, XElement source, string[] pathParts) {
            var targetCurrent = target;
            var sourceCurrent = source;
            if (pathParts != null) {
                foreach (var pathPart in pathParts) {
                    var targetChildElements = targetCurrent.Elements(pathPart);
                    var sourceChildElements = sourceCurrent.Elements(pathPart);
                    if (targetChildElements.Count()==0) {
                        var newTarget = new XElement(pathPart);
                        targetCurrent.Add(newTarget);
                        targetCurrent = newTarget;
                    }
                    else if (targetChildElements.Count()==1) {
                        targetCurrent = targetChildElements.Single();
                    }
                    else {
                        throw new System.Exception($"Found multiple target elements '{pathPart}'");
                    }
                    sourceCurrent = sourceChildElements.Single();
                }
            }

            targetCurrent.RemoveNodes();
            targetCurrent.Add(sourceCurrent.Nodes());
        }

        /*
        public static void RemoveRecurseUp(this XElement me, XElement stop) {
            var current = me;
            while (current != null && current != stop) {
                var parent = current.Parent;
                current.Remove();
                if (!parent.IsEmpty) break;
                current = parent;
            }
        }
        */
        public static void CleanRemove(this XElement me) {
            XElement parent = me.Parent;
            me.Remove();
            if (parent!=null && parent.Elements().Count()==0 && parent.Value.Trim()=="") {
                parent.RemoveNodes();
            }
        }
    }
}
