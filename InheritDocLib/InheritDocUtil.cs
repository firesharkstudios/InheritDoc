using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using Mono.Cecil;

namespace InheritDocLib {

    public enum LogLevel {
        Trace,
        Debug,
        Info,
        Warn,
        Error
    }

    // TODO: Add support for parameterized XML comments
    public static class InheritDocUtil {
        public const string XML_DOC_FILE_NAME_PATTERNS_HELP = "Set to a comma delimited list of XML documentation file names to process (may use wild cards like 'Butterfly.*', leave blank to scan for assemblies in project files, do not include paths). Example: 'Butterfly.Database.xml,Butterfly.Channel.*'";
        public const string GLOBAL_SOURCE_XML_FILES_HELP = @"Set to a comma delimited list of xml files to search for source xml comments. Example: 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.X\mscorlib.xml";
        public const string EXCLUDE_TYPES_HELP = "Set to a comma delimited list of types to exclude from comments. Example: 'System.Object'";

        const string TRIAL_TEXT = "running in free edition (only inheriting comments on types one level deep in type hierarchy from non-interface types), upgrade at https://www.inheritdoc.io/";

        /// <summary>
        /// Call to post process XML documentation files to replace &lt;inheritdoc/&gt; tags in XML comments (see https://www.inheritdoc.io)
        /// </summary>
        /// <param name="basePath">Leave blank to use current directory. Will scan for any project files under base path, determine assemblies from project files, and determine XML documentation files from project assemblies.</param>
        /// <param name="xmlDocFileNamePatterns">Set to a comma delimited list of XML documentation file names to process (may use wild cards like 'Butterfly.*', leave blank to scan for assemblies in project files, do not include paths). Example: 'Butterfly.Database.xml,Butterfly.Channel.*'</param>
        /// <param name="overwriteExisting">Set to false to create new .new.xml files.  Set to true to replace the existing .xml files.</param>
        /// <param name="logger">Set to optional callback to receive log messages.</param>
        /// <returns>List of XML documentation files modified (relative to base path).</returns>
        public static ICollection<string> Run(string basePath = null, string xmlDocFileNamePatterns = null, string globalSourceXmlFiles = null, string excludeTypes = null, bool overwriteExisting = false, Action<LogLevel, string> logger = null) {
            if (logger != null) logger(LogLevel.Info, $"InheritDoc(v{Assembly.GetExecutingAssembly().GetName().Version}).Run():basePath={basePath},xmlDocFileNamePatterns={xmlDocFileNamePatterns},globalSourceXmlFiles={globalSourceXmlFiles},excludeTypes={excludeTypes},overwriteExisting ={overwriteExisting}");

            string newBasePath = string.IsNullOrEmpty(basePath) ? Environment.CurrentDirectory : basePath;
            
            var assemblyFiles = GetAssemblyFiles(newBasePath, xmlDocFileNamePatterns, logger);
            var assemblyDocuments = LoadAssemblyDocuments(assemblyFiles, logger);

            var globalAssemblyDocuments = LoadGlobalAssemblyDocuments(globalSourceXmlFiles, logger);

            var allAssemblyDocuments = assemblyDocuments.Concat(globalAssemblyDocuments).ToArray();
            var typeDocByName = Compile(allAssemblyDocuments, excludeTypes, logger);
            var sortedTypeNames = Sort(typeDocByName);
            var count = ReplaceInheritDocs(assemblyDocuments, typeDocByName, sortedTypeNames, logger);
            if (count == 0) {
                if (logger != null) logger(LogLevel.Info, $"No <inheritdoc/> tags replaced (if you've used <inhertidoc/> tags, ensure you've enabled XML documentation files in your build settings)");
                return new string[] { };
            }
            else {
                var newXmlDocFiles = WriteNewAssemblyDocuments(assemblyDocuments, typeDocByName, overwriteExisting, logger);
                var relativeXmlDocFiles = newXmlDocFiles.Select(x => FileX.MakeRelativePath(newBasePath, x));
                if (logger != null) logger(LogLevel.Info, $"{count} <inheritdoc/> tag(s) replaced in {newXmlDocFiles.Count} XML documentation file(s) ({string.Join(",", relativeXmlDocFiles)})");
                return newXmlDocFiles;
            }
        }

        static ICollection<string> GetAssemblyFiles(string basePath, string xmlDocFileNamePatterns, Action<LogLevel, string> logger) {
            // Find all candidate xml files that meet filter
            List<string> filteredXmlFiles = new List<string>();
            var allXmlFiles = Directory.GetFiles(basePath, "*.xml", SearchOption.AllDirectories);
            var filterRegexes = string.IsNullOrEmpty(xmlDocFileNamePatterns) ? null : xmlDocFileNamePatterns.Split(',').Select(x => StringX.WildCardToRegex(x.Trim()));
            foreach (var xmlFile in allXmlFiles) {
                string xmlFileName = Path.GetFileName(xmlFile);
                if (filterRegexes==null || filterRegexes.Any(x => x.IsMatch(xmlFileName))) {
                    filteredXmlFiles.Add(xmlFile);
                }
            }

            // Get assembly for each xml file
            List<string> assemblyFiles = new List<string>();
            foreach (var xmlFile in filteredXmlFiles) {
                var directoryName = Path.GetDirectoryName(xmlFile);
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(xmlFile);

                var matchingAssemblyFiles = new List<string>();
                matchingAssemblyFiles.AddRange(Directory.GetFiles(directoryName, $"{fileNameWithoutExtension}.exe"));
                matchingAssemblyFiles.AddRange(Directory.GetFiles(directoryName, $"{fileNameWithoutExtension}.dll"));
                if (matchingAssemblyFiles.Count() == 0) {
                    if (logger!=null) logger(LogLevel.Trace, $"GetAssemblyFiles():Could not find assembly for xml file {xmlFile} ({string.Join(",", matchingAssemblyFiles)})");
                }
                else if (matchingAssemblyFiles.Count() == 1) {
                    assemblyFiles.Add(matchingAssemblyFiles.First());
                }
                else if (matchingAssemblyFiles.Count() > 1) {
                    if (logger!=null) logger(LogLevel.Warn, $"GetAssemblyFiles():Found too many assemblies for xml file {xmlFile} ({string.Join(",", matchingAssemblyFiles)})");
                }
            }

            return assemblyFiles;
        }

        static ICollection<AssemblyDocument> LoadAssemblyDocuments(ICollection<string> assemblyFiles, Action<LogLevel, string> logger) {
            if (logger != null) logger(LogLevel.Debug, $"LoadAssemblyDocuments():assemblyFiles={string.Join(",", assemblyFiles)}");

            Dictionary<string, AssemblyDocument> assemblyDocumentByAssemblyName = new Dictionary<string, AssemblyDocument>();

            foreach (var assemblyFile in assemblyFiles) {
                string assemblyName = null;
                try {
                    assemblyName = AssemblyName.GetAssemblyName(assemblyFile).ToString();
                }
                catch (Exception e) {
                    if (logger != null) logger(LogLevel.Warn, e.Message);
                    continue;
                }
                var xmlDocFile = Path.Combine(Path.GetDirectoryName(assemblyFile), $"{Path.GetFileNameWithoutExtension(assemblyFile)}.xml");
                if (logger != null) logger(LogLevel.Trace, $"LoadAssemblyDocuments():assemblyFile={assemblyFile},assemblyName={assemblyName},xmlDocFile={xmlDocFile}");
                if (assemblyDocumentByAssemblyName.TryGetValue(assemblyName, out AssemblyDocument existingAssemblyDocument)) {
                    if (logger != null) logger(LogLevel.Trace, $"LoadAssemblyDocuments():Already loaded assembly {assemblyName}");
                    existingAssemblyDocument.xmlDocFiles.Add(xmlDocFile);
                }
                else {
                    var document = ReadXDocument(xmlDocFile, logger);
                    if (document != null && document.Root.Name.LocalName=="doc") {
                        if (logger != null) logger(LogLevel.Trace, $"LoadAssemblyDocuments():Loading assembly {assemblyName}");
                        var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyFile);
                        var assemblyDocument = new AssemblyDocument(assemblyDefinition, document);
                        assemblyDocument.xmlDocFiles.Add(xmlDocFile);
                        assemblyDocumentByAssemblyName[assemblyName] = assemblyDocument;
                    }
                }
            }
            return assemblyDocumentByAssemblyName.Values;
        }

        static ICollection<AssemblyDocument> LoadGlobalAssemblyDocuments(string globalSourceXmlFiles, Action<LogLevel, string> logger) {
            if (string.IsNullOrEmpty(globalSourceXmlFiles)) return new AssemblyDocument[] { };

            var assemblyResolver = new DefaultAssemblyResolver();

            List<AssemblyDocument> assemblyDocuments = new List<AssemblyDocument>();
            foreach (var xmlFile in globalSourceXmlFiles.Split(',')) {
                var document = ReadXDocument(xmlFile, logger);
                string assemblyName = document.Root.Element("assembly").Element("name").Value;
                var assemblyDefinition = assemblyResolver.Resolve(assemblyName);
                var assemblyDocument = new AssemblyDocument(assemblyDefinition, document);
                assemblyDocuments.Add(assemblyDocument);
            }

            return assemblyDocuments;
        }

        static XDocument ReadXDocument(string xmlDocFile, Action<LogLevel, string> logger) {
            using (var streamReader = new StreamReader(xmlDocFile)) {
                try {
                    return XDocument.Load(streamReader);
                }
                catch (Exception e) {
                    if (logger!=null) logger(LogLevel.Warn, $"Unable to parse {xmlDocFile} due to error {e.Message} (skipping)");
                    return null;
                }
            }
        }

        // Compile assemblies into dictionary of types
        static IDictionary<string, TypeDoc> Compile(ICollection<AssemblyDocument> assemblyDocuments, string excludeTypesText, Action<LogLevel, string> logger) {
            var excludeTypes = string.IsNullOrWhiteSpace(excludeTypesText) ? null : excludeTypesText.Split(',').Select(x => x.Trim()).Select(x => x.EndsWith(".*") ? x.Replace(".*", "") : x).ToHashSet();

            var result = new Dictionary<string, TypeDoc>();
            foreach (var assemblyDocument in assemblyDocuments) {
                var memberElements = assemblyDocument.xDocument.Descendants("member");
                foreach (var memberElement in memberElements) {
                    var memberElementName = MemberElementName.Parse(memberElement);
                    if (!assemblyDocument.typeDataByName.TryGetValue(memberElementName.typeName, out TypeData typeData)) {
                        if (logger != null) logger(LogLevel.Warn, $"Could not find type '{memberElementName.typeName}'");
                        continue;
                    }
                    else if (excludeTypes != null && excludeTypes.Contains(memberElementName.typeName)) {
                        if (logger != null) logger(LogLevel.Info, $"Excluded type '{memberElementName.typeName}'");
                        continue;
                    }

                    if (!result.TryGetValue(typeData.name, out TypeDoc typeDoc)) {
                        var baseTypes = GetBaseTypeDatas(assemblyDocuments, typeData, logger);
                        typeDoc = new TypeDoc(baseTypes);
                        result[typeData.name] = typeDoc;
                    }

                    if (memberElementName.group == "T") {
                        typeDoc.rootElement.Add(memberElement.Elements());
                    }
                    else {
                        typeDoc.memberElements.Add(memberElement);
                    }
                }
            }
            return result;
        }

        // Sort dictionary by dependencies
        static List<string> Sort(IDictionary<string, TypeDoc> typeDocByName) {
            var unsortedTypeNames = typeDocByName.Keys.ToList();
            var result = new List<string>();
            while (unsortedTypeNames.Count>0) {
                int lastCount = unsortedTypeNames.Count;
                for (int i=unsortedTypeNames.Count-1; i>=0; i--) {
                    bool canAddToSorted = true;
                    var unsortedTypeName = unsortedTypeNames[i];
                    var baseTypeDatas = typeDocByName[unsortedTypeName].baseTypeDatas;
                    foreach (var baseTypeData in baseTypeDatas) {
                        if (baseTypeData.name!=unsortedTypeName && typeDocByName.ContainsKey(baseTypeData.name) && !result.Contains(baseTypeData.name)) {
                            canAddToSorted = false;
                            break;
                        }
                    }
                    if (canAddToSorted) {
                        result.Add(unsortedTypeName);
                        unsortedTypeNames.RemoveAt(i);
                    }
                }
                if (unsortedTypeNames.Count == lastCount) throw new Exception("Could not sort types (circular type dependency)");
            }
            return result;
        }

        static int ReplaceInheritDocs(ICollection<AssemblyDocument> assemblyDocuments, IDictionary<string, TypeDoc> typeDocByName, ICollection<string> sortedTypeNames, Action<LogLevel, string> logger) {
            int count = 0;
            foreach (var typeName in sortedTypeNames) {
                TypeDoc typeDoc = typeDocByName[typeName];
                while (true) {
                    var inheritDoc = typeDoc.rootElement.Descendants("inheritdoc").FirstOrDefault();
                    if (inheritDoc==null) break;

                    var cref = inheritDoc.Attribute("cref")?.Value;
                    string path = inheritDoc.Parent.GetPath(stop: typeDoc.rootElement);
                    //inheritDoc.RemoveRecurseUp(stop: typeDoc.rootElement);
                    inheritDoc.CleanRemove();
                    MergeChildElements(assemblyDocuments, typeDocByName, typeName, typeDoc, cref: cref, path: path, logger: logger);
                    if (path==null) {
                        MergeMemberElements(assemblyDocuments, typeDocByName, typeName, typeDoc, cref: cref, logger: logger);
                    }
                    count++;
                }

                foreach (var memberElement in typeDoc.memberElements.ToArray()) {
                    while (true) {
                        var inheritDoc = memberElement.Descendants("inheritdoc").FirstOrDefault();
                        if (inheritDoc == null) break;

                        var cref = inheritDoc.Attribute("cref")?.Value;
                        string path = inheritDoc.Parent.GetPath(stop: memberElement);
                        //inheritDoc.RemoveRecurseUp(stop: memberElement);
                        inheritDoc.CleanRemove();
                        MergeMemberElements(assemblyDocuments, typeDocByName, typeName, typeDoc, cref: cref, memberElementName: MemberElementName.Parse(memberElement), path: path, logger: logger);
                        count++;
                    }
                }
            }
            return count;
        }

        static void MergeChildElements(ICollection<AssemblyDocument> assemblyDocuments, IDictionary<string, TypeDoc> typeDocByName, string typeName, TypeDoc typeDoc, string cref = null, string path = null, Action<LogLevel, string> logger = null) {
            if (logger!=null) logger(LogLevel.Trace, $"MergeChildElements():typeName={typeName}");

            var crefMemberElementName = string.IsNullOrEmpty(cref) ? null : MemberElementName.Parse(cref);
            var baseTypeDatas = OverrideBaseTypeDatas(assemblyDocuments, typeDoc, crefMemberElementName, logger);
            var pathParts = path?.Split('/');

            foreach (var baseTypeData in baseTypeDatas.Reverse()) {
                if (typeDocByName.TryGetValue(baseTypeData.name, out TypeDoc baseTypeDoc)) {
                    var baseTargetElements = baseTypeDoc.rootElement.Select(pathParts);
                    if (baseTargetElements.Count() > 0) {
                        typeDoc.rootElement.CopyFrom(baseTypeDoc.rootElement, pathParts);
                        typeDoc.Changed = true;
                        break;
                    }
                }
            }
        }

        static void MergeMemberElements(ICollection<AssemblyDocument> assemblyDocuments, IDictionary<string, TypeDoc> typeDocByName, string typeName, TypeDoc typeDoc, string cref = null, MemberElementName memberElementName = null, string path = null, Action<LogLevel, string> logger = null) {
            if (logger!=null) logger(LogLevel.Trace, $"MergeMemberElements():typeName={typeName}");

            var crefMemberElementName = string.IsNullOrEmpty(cref) ? null : MemberElementName.Parse(cref);
            var baseTypeDatas = OverrideBaseTypeDatas(assemblyDocuments, typeDoc, crefMemberElementName, logger);
            var pathParts = path?.Split('/');

            foreach (var baseTypeData in baseTypeDatas.Reverse()) {
                if (typeDocByName.TryGetValue(baseTypeData.name, out TypeDoc baseTypeDoc)) {
                    foreach (var baseMemberElement in baseTypeDoc.memberElements) {
                        var baseMemberElementName = MemberElementName.Parse(baseMemberElement);
                        if (memberElementName == null || (crefMemberElementName!=null && crefMemberElementName.Matches(baseMemberElementName)) || (baseMemberElementName.group==memberElementName.group && baseMemberElementName.memberName==memberElementName.memberName)) {
                            var baseTargetElements = baseMemberElement.Select(pathParts);
                            if (baseTargetElements != null && baseTargetElements.Count() > 0) {
                                var newMemberElementName = new MemberElementName(baseMemberElementName.group, typeName, memberElementName!=null ? memberElementName.memberName : baseMemberElementName.memberName);
                                var newMemberElementNameText = newMemberElementName.ToString();

                                XElement matchingMemberElement = null;
                                var matchingMemberElements = typeDoc.memberElements.Where(x => x.Attribute("name").Value == newMemberElementNameText);
                                if (matchingMemberElements.Count() == 0) {
                                    matchingMemberElement = new XElement(baseMemberElement);
                                    matchingMemberElement.SetAttributeValue("name", newMemberElementNameText);
                                    typeDoc.memberElements.Add(matchingMemberElement);
                                }
                                else {
                                    var first = matchingMemberElements.First();
                                    var matchingTargetElements = first.Select(pathParts);
                                    if (matchingTargetElements == null || matchingTargetElements.Count() == 0 || (matchingTargetElements.Count() == 1 && matchingTargetElements.Single().IsEmpty)) {
                                        matchingMemberElement = first;
                                    }
                                    if (matchingMemberElements.Count() > 1) {
                                        logger(LogLevel.Warn, $"Found multiple matching elements where name='{newMemberElementNameText}'");
                                    }
                                }

                                if (matchingMemberElement != null) {
                                    matchingMemberElement.CopyFrom(baseMemberElement, pathParts);
                                    typeDoc.Changed = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        static ICollection<TypeData> OverrideBaseTypeDatas(ICollection<AssemblyDocument> assemblyDocuments, TypeDoc typeDoc, MemberElementName crefMemberElementName, Action<LogLevel, string> logger) {
            ICollection<TypeData> baseTypes = null;
            if (crefMemberElementName!=null) {
                foreach (var assemblyDocument in assemblyDocuments) {
                    if (assemblyDocument.typeDataByName.TryGetValue(crefMemberElementName.typeName, out TypeData crefTypeData)) {
                        baseTypes = new TypeData[] { crefTypeData };
                        break;
                    }
                }
            }
            if (baseTypes == null) {
                baseTypes = typeDoc.baseTypeDatas;
            }
            return baseTypes;
        }

        static ICollection<string> WriteNewAssemblyDocuments(ICollection<AssemblyDocument> assemblyDocuments, IDictionary<string, TypeDoc> typeDocByName, bool overwriteExisting, Action<LogLevel, string> logger) {
            List<string> result = new List<string>();
            foreach (var assemblyDocument in assemblyDocuments) {
                var assemblyTypeDocByType = typeDocByName.Where(pair => assemblyDocument.typeDataByName.ContainsKey(pair.Key));
                if (assemblyTypeDocByType.Where(x => x.Value.Changed).Count()>0) {
                    var membersElement = assemblyDocument.xDocument.Descendants("members").First();
                    membersElement.Nodes().Remove();
                    foreach ((string typeName, TypeDoc typeDoc) in assemblyTypeDocByType) {
                        // Add root member
                        var rootMember = new XElement(XName.Get("member"));
                        rootMember.SetAttributeValue("name", new MemberElementName("T", typeName, null));
                        rootMember.Add(typeDoc.rootElement.Elements());
                        membersElement.Add(rootMember);

                        // Add other members
                        membersElement.Add(typeDoc.memberElements);
                    }

                    foreach (var fileName in assemblyDocument.xmlDocFiles) {
                        string newFileName = overwriteExisting ? fileName : fileName.Replace(".xml", ".new.xml");
                        using (var writer = new XmlTextWriter(newFileName, new UTF8Encoding(false))) {
                            writer.Formatting = Formatting.Indented;
                            writer.Indentation = 4;
                            assemblyDocument.xDocument.Save(writer);
                        }
                        result.Add(newFileName);
                    }
                }
            }
            return result;
        }

        static ICollection<TypeData> GetBaseTypeDatas(ICollection<AssemblyDocument> assemblyDocuments, TypeData typeData, Action<LogLevel, string> logger) {
            List<TypeData> result = new List<TypeData>();
            string currentTypeName = typeData.name;
            while (currentTypeName != null) {
                if (!SearchAllAssemblyDocuments(assemblyDocuments, currentTypeName, out TypeData currentTypeData)) break;

                var batch = new List<TypeData>();
                foreach (var interfaceTypeName in currentTypeData.interfaceTypeNames) {
                    if (SearchAllAssemblyDocuments(assemblyDocuments, interfaceTypeName, out TypeData interfaceTypeData)) {
                        batch.Add(interfaceTypeData);
                    }
                }
                if (currentTypeData.name != typeData.name) {
                    batch.Add(currentTypeData);
                }
                result.InsertRange(0, batch);

                currentTypeName = currentTypeData.baseTypeName;
            }
            return result;
        }

        static bool SearchAllAssemblyDocuments(ICollection<AssemblyDocument> assemblyDocuments, string typeName, out TypeData typeData) {
            typeData = null;
            foreach (var assemblyDocument in assemblyDocuments) {
                if (assemblyDocument.typeDataByName.TryGetValue(typeName, out typeData)) return true;
            }
            return false;
        }

        static IEnumerable<Type> GetImmediateInterfaces(this Type type) {
            if (type.BaseType == null) return type.GetInterfaces();
            else return type.GetInterfaces().Except(type.BaseType.GetInterfaces());
        }

    }

    [Serializable]
    public class TypeData {
        public string name;
        public string baseTypeName;
        public string[] interfaceTypeNames;
    }

    public class AssemblyDocument {
        public readonly Dictionary<string, TypeData> typeDataByName = new Dictionary<string, TypeData>();

        public readonly XDocument xDocument;
        public readonly List<string> xmlDocFiles = new List<string>();

        public AssemblyDocument(AssemblyDefinition assemblyDefinition, XDocument xDocument) {
            this.typeDataByName = GetTypeByName(assemblyDefinition);
            this.xDocument = xDocument;
        }

        static Dictionary<string, TypeData> GetTypeByName(AssemblyDefinition assemblyDefinition) {
            var typeDatas = assemblyDefinition.MainModule.Types.Select(x => {
                return new TypeData {
                    name = x.FullName,
                    interfaceTypeNames = x.Interfaces == null ? new string[] { } : x.Interfaces.Select(y => y.FullName).ToArray(),
                    baseTypeName = x.BaseType?.FullName
                };
            });
            return typeDatas.ToDictionary(x => x.name);
        }
    }

    // Create this for each member starting with T:
    public class TypeDoc {
        // Like <summary/> and <remarks/> on class itself
        public readonly XElement rootElement = new XElement("root");

        // Like <member/> tags for properties, methods, etc on class
        public readonly List<XElement> memberElements = new List<XElement>();

        // All the recursive base types and interfaces
        public readonly ICollection<TypeData> baseTypeDatas;

        public TypeDoc(ICollection<TypeData> baseTypeDatas) {
            this.baseTypeDatas = baseTypeDatas;
        }

        public bool Changed {
            get;
            set;
        }
    }

    public class MemberElementName {
        public readonly string group;
        public readonly string typeName;
        public readonly string memberName;

        public MemberElementName(string group, string typeName, string memberName) {
            this.group = group;
            this.typeName = typeName;
            this.memberName = memberName;
        }

        public static MemberElementName Parse(XElement memberElement) {
            var text = memberElement.Attribute("name").Value;
            return Parse(text);
        }

        public static MemberElementName Parse(string text) {
            int colonPos = text.IndexOf(':');
            string group = text.Substring(0, colonPos);

            string typeName;
            string memberName;
            if (group == "T") {
                typeName = text.Substring(colonPos + 1);
                memberName = null;
            }
            else {
                int leftParenPos = text.IndexOf('(');
                int lastDot = text.LastIndexOf('.', leftParenPos == -1 ? text.Length - 1 : leftParenPos);
                typeName = text.Substring(colonPos + 1, lastDot - colonPos - 1);
                memberName = text.Substring(lastDot + 1);
            }
            return new MemberElementName(group, typeName, memberName);
        }

        public  bool Matches(MemberElementName other) {
            return other.group == this.group && other.typeName == this.typeName && other.memberName == this.memberName;
        }

        public override string ToString() {
            if (string.IsNullOrEmpty(this.memberName)) {
                return $"{this.group}:{this.typeName}";
            }
            else {
                return $"{this.group}:{this.typeName}.{this.memberName}";
            }
        }
    }

}
