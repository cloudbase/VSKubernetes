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
using System.Diagnostics;

namespace VSKubernetes
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class KubernetesCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        private const int K8sAddSupportCommandId = 0x0100;
        private const int K8sDeployCommandId = 0x0101;

        /*
        public static readonly Guid projectTypeCSharp = new Guid("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC");
        public static readonly Guid projectTypeVBNet = new Guid("F184B08F-C81C-45F6-A57F-5ABD9991F28F");
        public static readonly Guid projectTypeVCpp = new Guid("8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942");
        public static readonly Guid projectTypeDockerCompose = new Guid("E53339B2-1760-4266-BCC7-CA923CBCF16C");
        */

        private static readonly Guid projectTypeCSharpCore = new Guid("9A19103F-16F7-4668-BE54-9A1E7A4F7556");
        //private static readonly Guid projectTypeNodeJs = new Guid("3AF33F2E-1136-4D97-BBB7-1795711AC8B8");
        private static readonly Guid projectTypeNodeJs = new Guid("9092AA53-FB77-4645-B42D-1CCCA6BD08BD");

        public const string dockerFileName = "Dockerfile";
        public const string kubernetesProjectName = "Kubernetes";

        private static readonly Guid kubernetesPaneGuid = new Guid("2BE8BB60-E918-4C59-8717-B078A6927D34");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="KubernetesCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private KubernetesCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(Constants.guidCommandSet, K8sAddSupportCommandId);
                var menuItem = new OleMenuCommand(this.MenuItemCallbackK8sAddSupport, menuCommandID);
                menuItem.BeforeQueryStatus += new EventHandler(this.OnBeforeQueryStatusK8sAddSupport);
                commandService.AddCommand(menuItem);

                menuCommandID = new CommandID(Constants.guidCommandSet, K8sDeployCommandId);
                menuItem = new OleMenuCommand(this.MenuItemCallbackK8sDeploy, menuCommandID);
                menuItem.BeforeQueryStatus += new EventHandler(this.OnBeforeQueryStatusK8sDeploy);
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
            output.CreatePane(ref paneGuid, title, Convert.ToInt32(visible), Convert.ToInt32(clearWithSolution));
            var hr = output.GetPane(ref paneGuid, out pane);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
            return pane;
        }

        private void WriteToOutputWindow(string message)
        {
            //var paneGuid = Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.DebugPane_guid;
            var paneGuid = kubernetesPaneGuid;
            var pane = GetOutputPane(paneGuid, "Kubernetes", true, false);
            pane.Activate();
            pane.OutputString(message + "\n");
        }

        private bool ProjectHasDockerFile(Project project)
        {
            var item = GetProjectItem(project, dockerFileName);
            return (item != null && VSConstants.GUID_ItemType_PhysicalFile == new Guid(item.Kind));
        }

        private ProjectItem GetProjectItem(Project project, string name)
        {
            foreach (ProjectItem p in project.ProjectItems)
                if (p.Name == name)
                    return p;
            return null;
        }

        private void OnBeforeQueryStatusK8sAddSupport(object sender, EventArgs e)
        {
            OleMenuCommand item = (OleMenuCommand)sender;
            var project = this.GetCurrentProject();

            item.Visible = true;
            item.Enabled = !ProjectHasDockerFile(project);
        }

        private void OnBeforeQueryStatusK8sDeploy(object sender, EventArgs e)
        {
            OleMenuCommand item = (OleMenuCommand)sender;
            var project = this.GetCurrentProject();

            item.Visible =true ;
            item.Enabled = ProjectHasDockerFile(project);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static KubernetesCommand Instance
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
            Instance = new KubernetesCommand(package);
        }

        private void CreateProjectFromTemplate(Solution4 solution, string templateName, string language, string projectName, bool createProjectDir=true)
        {
            var solutionDir = System.IO.Path.GetDirectoryName(solution.FileName);

            var projectDir = solutionDir;
            if (createProjectDir)
                projectDir = System.IO.Path.Combine(solutionDir, projectName);
            if (!projectDir.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                projectDir += System.IO.Path.DirectorySeparatorChar;

            var template = GetVSExtensionFilePath("ProjectTemplates\\Kubernetes\\1033\\KubernetesProjectTemplate\\KubernetesProjectTemplate.vstemplate");

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

        private int RunProcess(string path, string arguments="", string workingDirectory="", bool wait=false, EventHandler onExit=null)
        {
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.FileName = path;
            p.StartInfo.WorkingDirectory = workingDirectory;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.OutputDataReceived += Process_OutputDataReceived;
            p.ErrorDataReceived += Process_ErrorDataReceived;
            if (onExit != null)
            {
                p.EnableRaisingEvents = true;
                p.Exited += onExit;
            }

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            if (wait)
            {
                p.WaitForExit();
                return p.ExitCode;
            }
            return -1;
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                WriteToOutputWindow(e.Data);
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                WriteToOutputWindow(e.Data);
            }
        }

        private bool ProjectExists(Solution solution, string projectName)
        {
            foreach (Project p in solution.Projects)
                if (p.Name == kubernetesProjectName)
                    return true;
            return false;
        }

        private IVsStatusbar GetStatusBar()
        {
            return this.ServiceProvider.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
        }

        private void MenuItemCallbackK8sDeploy(object sender, EventArgs e)
        {
            try
            {
                var project = GetCurrentProject();
                var projectDir = System.IO.Path.GetDirectoryName(project.FullName);

                var bar = GetStatusBar();
                int frozen;
                bar.IsFrozen(out frozen);

                if (frozen != 0)
                {
                    bar.FreezeOutput(0);
                }

                //object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_Deploy;
                //bar.Animation(1, ref icon);
                bar.SetText("Deploying Kubernetes Helm Chart...");
                bar.FreezeOutput(1);

                RunProcess("draft.exe", "up .", projectDir, false, (s, e2) => {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate {
                        // Switch to main thread
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        try
                        {
                            bar.FreezeOutput(0);
                            bar.Clear();
                            //bar.Animation(0, ref icon);
                            var p = (System.Diagnostics.Process)s;
                            if (p.ExitCode != 0)
                            {
                                WriteToOutputWindow("draft up failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            showWarningMessageBox(ex.Message);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                showWarningMessageBox(ex.Message);
            }
        }

        private void DisableDraftWatch(string projectDir)
        {
            var draftTomlFileName = "draft.toml";
            var path = System.IO.Path.Combine(projectDir, draftTomlFileName);
            string text = System.IO.File.ReadAllText(path, System.Text.Encoding.ASCII);
            text = text.Replace("watch = true", "watch = false");
            System.IO.File.WriteAllText(path, text, System.Text.Encoding.ASCII);
        }

        private void showWarningMessageBox(string message)
        {
            string title = "Kubernetes for Visual Studio";
            VsShellUtilities.ShowMessageBox(
                this.ServiceProvider,
                message,
                title,
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private void MenuItemCallbackK8sAddSupport(object sender, EventArgs e)
        {
            try
            {
                var project = GetCurrentProject();
                var projectDir = System.IO.Path.GetDirectoryName(project.FullName);

                string packName = null;
                var projectKindGuid = new Guid(project.Kind);
                if (projectKindGuid == projectTypeCSharpCore)
                    packName = "dotnetcore";
                else if (projectKindGuid == projectTypeNodeJs)
                    packName = "node";
                else
                    throw new Exception("Unsupported project type");

                //String.Format("create . -a \"{0}\"", project.Name.ToLower());
                RunProcess("draft.exe", String.Format("create . --pack {0}", packName), projectDir, false, (s, e2) => {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate {
                        // Switch to main thread
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        try
                        {
                            var p = (System.Diagnostics.Process)s;
                            if (p.ExitCode != 0)
                            {
                                WriteToOutputWindow("draft create failed");
                                //throw new Exception("draft create failed");
                            }
                            else
                            {
                                DisableDraftWatch(projectDir);
                                foreach (var name in new[] { "Dockerfile", "draft.toml", ".draftignore", "chart" })
                                {
                                    if (GetProjectItem(project, name) == null)
                                    {
                                        var path = System.IO.Path.Combine(projectDir, name);
                                        if (System.IO.File.Exists(path))
                                            project.ProjectItems.AddFromFile(path);
                                        else if (System.IO.Directory.Exists(path))
                                            project.ProjectItems.AddFromDirectory(path);
                                    }
                                }

                                //DTE dte = (DTE)this.ServiceProvider.GetService(typeof(DTE));
                                //dte.ExecuteCommand("View.ServerExplorer");
                                //dte.ExecuteCommand("View.Refresh");
                            }
                        }
                        catch (Exception ex)
                        {
                            showWarningMessageBox(ex.Message);
                        }
                    });
                });

                //if(!ProjectExists(solution, kubernetesProjectName))
                //    //CreateProject((Solution4)solution, kubernetesProjectName, false);
                //    CreateProjectFromTemplate((Solution4)solution, "KubernetesProjectTemplate.zip", "Yaml", kubernetesProjectName, true);
            }
            catch (Exception ex)
            {
                showWarningMessageBox(ex.Message);
            }
        }
    }
}
