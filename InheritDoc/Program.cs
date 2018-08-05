using System;

using CommandLine;
using NLog;

using InheritDocLib;

namespace InheritDoc {
    // Allows using <inheritdoc/> tags in XML comments that copy/extend the comments from base classes and interfaces
    class Program {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args) {
            // Parse options
            var options = new Options();
            var isValid = CommandLine.Parser.Default.ParseArgumentsStrict(args, options);
            if (!isValid) throw new Exception("Invalid command line");

            InheritDocUtil.Run(options.BasePath, options.XmlDocFileNamePatterns, options.GlobalSourceXmlFiles, options.ExcludeTypes, overwriteExisting: options.OverwriteExisting, logger: Logger);
        }

        static void Logger(InheritDocLib.LogLevel logLevel, string message) {
            switch (logLevel) {
                case InheritDocLib.LogLevel.Trace:
                    logger.Trace(message);
                    break;
                case InheritDocLib.LogLevel.Debug:
                    logger.Debug(message);
                    break;
                case InheritDocLib.LogLevel.Info:
                    logger.Info(message);
                    break;
                case InheritDocLib.LogLevel.Warn:
                    logger.Warn(message);
                    break;
                case InheritDocLib.LogLevel.Error:
                    logger.Error(message);
                    break;
            }
        }
    }

    public class Options {
        [Option('b', "base", Required = false, HelpText = "Base path to look for XML document files (omit for current directory)")]
        public string BasePath { get; set; }

        [Option('f', "xml-doc-file-name-patterns", Required = false, HelpText = InheritDocUtil.XML_DOC_FILE_NAME_PATTERNS_HELP)]
        public string XmlDocFileNamePatterns { get; set; }

        [Option('g', "global-source-xml-files", Required = false, DefaultValue = null, HelpText = InheritDocUtil.GLOBAL_SOURCE_XML_FILES_HELP)]
        public string GlobalSourceXmlFiles { get; set; }

        [Option('x', "exclude-types", Required = false, DefaultValue = "System.Object", HelpText = InheritDocUtil.EXCLUDE_TYPES_HELP)]
        public string ExcludeTypes { get; set; }

        [Option('o', "overwrite", HelpText = "Include to overwrite existing xml files. Omit to create new files with '.new.xml' suffix.")]
        public bool OverwriteExisting { get; set; }
    }

}
