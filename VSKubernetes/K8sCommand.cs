//------------------------------------------------------------------------------
// <copyright file="K8sCommand.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using EnvDTE;
using EnvDTE100;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSKubernetes
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class K8sCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /*
        public static readonly Guid projectTypeCSharpCore = new Guid("9A19103F-16F7-4668-BE54-9A1E7A4F7556");
        public static readonly Guid projectTypeCSharp = new Guid("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC");
        public static readonly Guid projectTypeVBNet = new Guid("F184B08F-C81C-45F6-A57F-5ABD9991F28F");
        public static readonly Guid projectTypeVCpp = new Guid("8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942");
        public static readonly Guid projectTypeDockerCompose = new Guid("E53339B2-1760-4266-BCC7-CA923CBCF16C");
        */

        public const string dockerFileName = "Dockerfile";
        public const string kubernetesProjectName = "Kubernetes";

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="K8sCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private K8sCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(KubernetesPackageGuids.guidCommandSet, CommandId);
                var menuItem = new OleMenuCommand(this.MenuItemCallback, menuCommandID);
                menuItem.BeforeQueryStatus += new EventHandler(OnBeforeQueryStatus);
                commandService.AddCommand(menuItem);
            }
        }

        private Project GetCurrentProject()
        {
            DTE service = (DTE)this.ServiceProvider.GetService(typeof(DTE));
            var activeSolutionProjects = (Array)service.ActiveSolutionProjects;
            return (Project)activeSolutionProjects.GetValue(0);
        }

        private IVsOutputWindowPane GetOutputPane(Guid paneGuid, string title, bool visible, bool clearWithSolution)
        {
            IVsOutputWindow output = (IVsOutputWindow)ServiceProvider.GetService(typeof(SVsOutputWindow)); IVsOutputWindowPane pane;
            //output.CreatePane(ref paneGuid, title, Convert.ToInt32(visible), Convert.ToInt32(clearWithSolution));
            var hr = output.GetPane(ref paneGuid, out pane);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
            return pane;
        }

        private void WriteToOutputWindow(string message)
        {
            var paneGuid = Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.DebugPane_guid;
            var pane = GetOutputPane(paneGuid, "Kubernetes", true, true);
            pane.Activate();
            pane.OutputString(message + "\n");
        }

        private bool projectHasDockerFile(Project project)
        {
            foreach (ProjectItem p in project.ProjectItems)
                if (p.Name == dockerFileName && VSConstants.GUID_ItemType_PhysicalFile == new Guid(p.Kind))
                    return true;
            return false;
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand item = (OleMenuCommand)sender;
            var project = this.GetCurrentProject();

            item.Visible = projectHasDockerFile(project);
            item.Enabled = item.Visible;

            this.WriteToOutputWindow(project.Kind);

            //var outputType = project.Properties.Item("OutputType").Value;
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static K8sCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new K8sCommand(package);
        }

        private void CreateProjectFromTemplate(Solution4 solution, string templateName, string language, string projectName, bool createProjectDir=true)
        {
            var solutionDir = System.IO.Path.GetDirectoryName(solution.FileName);

            var projectDir = solutionDir;
            if (createProjectDir)
                projectDir = System.IO.Path.Combine(solutionDir, projectName);
            if (!projectDir.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                projectDir += System.IO.Path.DirectorySeparatorChar;

            var template = GetVSExtensionFilePath("ProjectTemplates\\KubernetesProject\\KubernetesProject.vstemplate");

            //var template = solution.GetProjectTemplate(templateName, language);
            solution.AddFromTemplate(template, projectDir, projectName, false);
        }

        private string GetVSExtensionFilePath(string relPath)
        {
            var extensionPath = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            var extensionDir = System.IO.Path.GetDirectoryName(extensionPath);
            return System.IO.Path.Combine(extensionDir, relPath);
        }

        private void CreateProject(Solution4 solution, string projectName, bool createProjectDir = true)
        {
            var solutionDir = System.IO.Path.GetDirectoryName(solution.FileName);

            var projectTemplatePath = GetVSExtensionFilePath("Templates\\Projects\\KubernetesProject\\KubernetesProject.k8sproj");
            var projectFilePath = System.IO.Path.Combine(solutionDir, projectName + ".k8sproj");
            System.IO.File.Copy(projectTemplatePath, projectFilePath, true);
            solution.AddFromFile(projectFilePath, false);
        }

        private bool projectExists(Solution solution, string projectName)
        {
            foreach (Project p in solution.Projects)
                if (p.Name == kubernetesProjectName)
                    return true;
            return false;
        }

        private void MenuItemCallback(object sender, EventArgs e)
        {
            try
            {
                DTE service = (DTE)this.ServiceProvider.GetService(typeof(DTE));
                var solution = service.Solution;
                if(!projectExists(solution, kubernetesProjectName))
                    //CreateProject((Solution4)solution, kubernetesProjectName, false);
                    CreateProjectFromTemplate((Solution4)solution, "ConsoleApplication.zip", "Yaml", kubernetesProjectName, true);
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                string title = "Kubernetes for Visual Studio";
                VsShellUtilities.ShowMessageBox(
                    this.ServiceProvider,
                    message,
                    title,
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}
