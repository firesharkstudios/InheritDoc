/*
 * Copyright 2017 Fireshark Studios, LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

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

            InheritDocUtil.Run(options.Key, options.BasePath, options.XmlDocFileNamePatterns, options.ExcludeTypeNamePatterns, overwriteExisting: options.OverwriteExisting, logger: Logger);
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
        [Option('k', "key", Required = false, HelpText = InheritDocUtil.LICENSE_KEY_HELP)]
        public string Key { get; set; }

        [Option('b', "base", Required = false, HelpText = "Base path to look for XML document files (omit for current directory)")]
        public string BasePath { get; set; }

        [Option('f', "xml-doc-file-name-patterns", Required = false, HelpText = InheritDocUtil.XML_DOC_FILE_NAME_PATTERNS_HELP)]
        public string XmlDocFileNamePatterns { get; set; }

        [Option('x', "exclude-type-name-patterns", Required = false, DefaultValue = "System.*", HelpText = InheritDocUtil.EXCLUDE_TYPE_NAME_PATTERNS_HELP)]
        public string ExcludeTypeNamePatterns { get; set; }

        [Option('o', "overwrite", HelpText = "Include to overwrite existing xml files. Omit to create new files with '.new.xml' suffix.")]
        public bool OverwriteExisting { get; set; }
    }

}
