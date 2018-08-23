using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE;
using EnvDTE80;

using InheritDocLib;
using Microsoft.VisualStudio;
using System.ComponentModel.Design;

namespace InheritDocVsix {
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(InheritDocPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideOptionPage(typeof(OptionPageGrid), "InheritDoc", "General", 0, 0, true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class InheritDocPackage : Package {
        /// <summary>
        /// InheritDocPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "4427af26-3969-4753-9204-3c8f5c6290df";

        readonly BuildEventProxy buildEventProxy;

        /// <summary>
        /// Initializes a new instance of the <see cref="InheritDocPackage"/> class.
        /// </summary>
        public InheritDocPackage() {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.

            OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
            this.buildEventProxy = new BuildEventProxy(this, () => {
                if (page.RunWhen == OptionRunWhen.Automatic) {
                    Run(page);
                }
            });
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize() {
            base.Initialize();

            this.buildEventProxy.Initialize();
            RunInheritDoc.Initialize(this);

        }

        public static void Run(OptionPageGrid page) {
            var basePath = GetSolutionPath();
            var xmlDocFileNames = string.IsNullOrEmpty(page.XmlDocFileNames) ? GetProjectXmlDocFileNames() : page.XmlDocFileNames;
            var globalSourceXmlFiles = page.GlobalSourceXmlFiles;
            var excludeTypes = page.ExcludeTypes;
            var buildOutputPane = GetBuildOutputPane();
            buildOutputPane.Activate();
            InheritDocUtil.Run(basePath: basePath, xmlDocFileNamePatterns: xmlDocFileNames, globalSourceXmlFiles: globalSourceXmlFiles, excludeTypes: excludeTypes, overwriteExisting: page.OverwriteExisting, logger: (logLevel, message) => {
                if ((int)logLevel >= (int)page.LogLevel) {
                    buildOutputPane.OutputString($"InheritDoc.{logLevel}:{message}\r\n");
                }
            });
        }

        static string GetSolutionPath() {
            DTE2 dte = (DTE2)Package.GetGlobalService(typeof(SDTE));
            return Path.GetDirectoryName(dte.Solution.FullName);
        }

        static string GetProjectXmlDocFileNames() {
            DTE2 dte = (DTE2)Package.GetGlobalService(typeof(SDTE));
            List<string> result = new List<string>();
            foreach (Project project in dte.Solution.Projects) {
                var configurationManager = project.ConfigurationManager;
                if (configurationManager != null) {
                    var activeConfiguration = configurationManager.ActiveConfiguration;
                    if (activeConfiguration != null) {
                        foreach (OutputGroup outputGroup in activeConfiguration.OutputGroups) {
                            if (outputGroup.CanonicalName == "Documentation") {
                                foreach (string fileName in outputGroup.FileNames as object[]) {
                                    result.Add(fileName);
                                }
                            }
                        }
                    }
                }
            }
            return string.Join(",", result);
        }

        static IVsOutputWindowPane GetBuildOutputPane() {
            IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            Guid guid = VSConstants.GUID_BuildOutputWindowPane; //.GUID_OutWindowDebugPane;
            IVsOutputWindowPane buildOutputPane;
            outWindow.GetPane(ref guid, out buildOutputPane);
            return buildOutputPane;
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            this.buildEventProxy.Dispose();
        }

        #endregion
    }

    public class BuildEventProxy : IDisposable {
        protected readonly Package package;
        protected readonly Action onSuccessfulSolutionBuild;

        protected bool solutionBuildSuccess;

        public BuildEventProxy(Package package, Action onSuccessfulSolutionBuild) {
            this.package = package;
            this.onSuccessfulSolutionBuild = onSuccessfulSolutionBuild;
        }

        public void Initialize() {
            // Listen to the necessary build events.
            DTE2 dte = (DTE2)Package.GetGlobalService(typeof(SDTE));
            dte.Events.BuildEvents.OnBuildBegin += this.BuildEvents_OnBuildBegin;
            dte.Events.BuildEvents.OnBuildDone += BuildEvents_OnBuildDone;
            dte.Events.BuildEvents.OnBuildProjConfigDone += BuildEvents_OnBuildProjConfigDone;

        }

        private void BuildEvents_OnBuildBegin(EnvDTE.vsBuildScope scope, EnvDTE.vsBuildAction action) {
            if (scope==EnvDTE.vsBuildScope.vsBuildScopeSolution) {
                // Reset the solution build success to true and let build failures set it to false
                this.solutionBuildSuccess = true;
            }
        }

        private void BuildEvents_OnBuildProjConfigDone(string project, string projectConfig, string platform, string solutionConfig, bool success) {
            if (!success) {
                this.solutionBuildSuccess = false;
            }
        }

        private void BuildEvents_OnBuildDone(EnvDTE.vsBuildScope scope, EnvDTE.vsBuildAction action) {
            if (scope==EnvDTE.vsBuildScope.vsBuildScopeSolution && this.solutionBuildSuccess) {
                this.onSuccessfulSolutionBuild();
            } 
        }

        public void Dispose() {
            DTE2 dte = (DTE2)Package.GetGlobalService(typeof(SDTE));
            dte.Events.BuildEvents.OnBuildBegin -= this.BuildEvents_OnBuildBegin;
            dte.Events.BuildEvents.OnBuildDone -= BuildEvents_OnBuildDone;
            dte.Events.BuildEvents.OnBuildProjConfigDone -= BuildEvents_OnBuildProjConfigDone;
        }
    }

    public enum OptionLogLevel {
        Trace,
        Debug,
        Info,
        Warn,
        Error
    }

    public enum OptionRunWhen {
        Manual,
        Automatic
    }

    public class OptionPageGrid : DialogPage {

        private OptionRunWhen runWhen = OptionRunWhen.Manual;
        private string xmlDocFileNames = null;
        private string globalSourceXmlFiles = null;
        private string excludeTypes = "System.Object";
        private bool overwriteExisting = true;
        private OptionLogLevel logLevel = OptionLogLevel.Info;

        [Category("InheritDoc")]
        [DisplayName("Run When")]
        [Description("Choose Manual to only run InheritDoc from the 'Tools > Run InheritDoc now' menu in Visual Studio. Choose Automatic to automatically run InheritDoc after building the solution.")]
        public OptionRunWhen RunWhen {
            get => this.runWhen;
            set => this.runWhen = value;
        }

        [Category("InheritDoc")]
        [DisplayName("Xml Doc File Names")]
        [Description(InheritDocUtil.XML_DOC_FILE_NAME_PATTERNS_HELP)]
        public string XmlDocFileNames {
            get => this.xmlDocFileNames;
            set => this.xmlDocFileNames = value;
        }

        [Category("InheritDoc")]
        [DisplayName("Global Source Xml Files")]
        [Description(InheritDocUtil.GLOBAL_SOURCE_XML_FILES_HELP)]
        public string GlobalSourceXmlFiles {
            get => this.globalSourceXmlFiles;
            set => this.globalSourceXmlFiles = value;
        }

        [Category("InheritDoc")]
        [DisplayName("Exclude Types")]
        [Description(InheritDocUtil.EXCLUDE_TYPES_HELP)]
        public string ExcludeTypes {
            get => this.excludeTypes;
            set => this.excludeTypes = value;
        }

        [Category("InheritDoc")]
        [DisplayName("Overwrite Existing")]
        [Description("Set to true to overwrite existing xml files. Set to false to create new files with '.new.xml' suffix.")]
        public bool OverwriteExisting {
            get => this.overwriteExisting;
            set => this.overwriteExisting = value;
        }

        [Category("InheritDoc")]
        [DisplayName("Log Level")]
        [Description("Minimum log level (Trace, Debug, Info, Warn, Error) for InheritDoc messages. Log messages sent to Output window.")]
        public OptionLogLevel LogLevel {
            get => this.logLevel;
            set => this.logLevel = value;
        }
    }
}
