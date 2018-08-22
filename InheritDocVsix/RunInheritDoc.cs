using System;
using System.ComponentModel.Design;

using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace InheritDocVsix {
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class RunInheritDoc {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("1273a65a-7139-4c2d-ab42-c7768054b166");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="RunInheritDoc"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private RunInheritDoc(Package package) {
            if (package == null) {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            if (this.ServiceProvider.GetService(typeof(IMenuCommandService)) is OleMenuCommandService commandService) {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new OleMenuCommand(this.OnClick, menuCommandID);
                menuItem.BeforeQueryStatus += (sender, evt) => {
                    DTE service = (DTE)ServiceProvider.GetService(typeof(DTE));
                    Solution solution = service.Solution;
                    menuItem.Enabled = solution.IsOpen;
                };
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static RunInheritDoc Instance {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider {
            get {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package) {
            Instance = new RunInheritDoc(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void OnClick(object sender, EventArgs e) {
            OptionPageGrid page = (OptionPageGrid)this.package.GetDialogPage(typeof(OptionPageGrid));
            InheritDocPackage.Run(page);
        }
    }
}
