using System;
using System.ComponentModel.Design;
using EnvDTE;
using EnvDTE100;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics;
using System.Collections.Generic;

namespace VSKubernetes
{
    internal sealed class KubernetesCommand
    {
        private const int K8sAddSupportCommandId = 0x0100;
        private const int K8sDeployCommandId = 0x0101;
        private const int K8sDeployMinikubeCommandId = 0x0102;

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

                menuCommandID = new CommandID(Constants.guidCommandSet, K8sDeployMinikubeCommandId);
                menuItem = new OleMenuCommand(this.MenuItemCallbackK8sMinikubeDeploy, menuCommandID);
                menuItem.BeforeQueryStatus += new EventHandler(this.OnBeforeQueryStatusK8sMinikubeDeploy);
                commandService.AddCommand(menuItem);
            }
        }

        private bool ProjectHasDockerFile(Project project)
        {
            var item = Utils.GetProjectItem(project, dockerFileName);
            return (item != null && VSConstants.GUID_ItemType_PhysicalFile == new Guid(item.Kind));
        }

        private void OnBeforeQueryStatusK8sAddSupport(object sender, EventArgs e)
        {
            OleMenuCommand item = (OleMenuCommand)sender;
            var project = Utils.GetCurrentProject(this.ServiceProvider);

            item.Visible = true;
            item.Enabled = !ProjectHasDockerFile(project);
        }

        private void OnBeforeQueryStatusK8sMinikubeDeploy(object sender, EventArgs e)
        {
            OleMenuCommand item = (OleMenuCommand)sender;
            item.Visible = true;
            item.Enabled = true;
        }

        private void OnBeforeQueryStatusK8sDeploy(object sender, EventArgs e)
        {
            OleMenuCommand item = (OleMenuCommand)sender;
            var project = Utils.GetCurrentProject(this.ServiceProvider);

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

            var template = Utils.GetVSExtensionFilePath("ProjectTemplates\\Kubernetes\\1033\\KubernetesProjectTemplate\\KubernetesProjectTemplate.vstemplate");

            //var template = solution.GetProjectTemplate(templateName, language);
            solution.AddFromTemplate(template, projectDir, projectName, false);
        }

        private void CreateProject(Solution4 solution, string projectName, bool createProjectDir = true)
        {
            var solutionDir = System.IO.Path.GetDirectoryName(solution.FileName);

            var projectTemplatePath = Utils.GetVSExtensionFilePath("Templates\\Projects\\KubernetesProject\\KubernetesProject.k8sproj");
            var projectFilePath = System.IO.Path.Combine(solutionDir, projectName + ".k8sproj");
            System.IO.File.Copy(projectTemplatePath, projectFilePath, true);
            solution.AddFromFile(projectFilePath, false);
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Utils.WriteToOutputWindow(this.ServiceProvider, e.Data);
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Utils.WriteToOutputWindow(this.ServiceProvider, e.Data);
            }
        }

        private IVsStatusbar GetStatusBar()
        {
            return this.ServiceProvider.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
        }

        private void MenuItemCallbackK8sMinikubeDeploy(object sender, EventArgs e)
        {
            OleMenuCommand menuCommand = (OleMenuCommand)sender;

            var baseDir = Utils.GetBinariesDir();
            var ps1Path = System.IO.Path.Combine(baseDir, "DeployMinikube.ps1");

            var bar = GetStatusBar();
            int frozen;
            bar.IsFrozen(out frozen);

            if (frozen != 0)
            {
                bar.FreezeOutput(0);
            }
            bar.SetText("Deploying Minikube...");
            bar.FreezeOutput(1);

            menuCommand.Enabled = false;

            Utils.RunProcess("powershell.exe", string.Format("-NonInteractive -NoLogo -ExecutionPolicy RemoteSigned -File \"{0}\"", ps1Path),
                       baseDir, false, Process_OutputDataReceived, Process_ErrorDataReceived, (s, e2) => {
                ThreadHelper.JoinableTaskFactory.Run(async delegate {
                    // Switch to main thread
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    try
                    {
                        menuCommand.Enabled = true;

                        bar.FreezeOutput(0);
                        bar.Clear();

                        var p = (System.Diagnostics.Process)s;
                        if (p.ExitCode != 0)
                        {
                            throw new Exception("Minikube deployment failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.ShowWarningMessageBox(this.ServiceProvider, ex.Message);
                    }
                });
            });
        }

        private void MenuItemCallbackK8sDeploy(object sender, EventArgs e)
        {
            try
            {
                var project = Utils.GetCurrentProject(this.ServiceProvider);
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

                var draftPath = System.IO.Path.Combine(Utils.GetBinariesDir(), "draft.exe");
                Utils.RunProcess(draftPath, "up .", projectDir, false, Process_OutputDataReceived, Process_ErrorDataReceived, (s, e2) => {
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
                                Utils.WriteToOutputWindow(this.ServiceProvider, "draft up failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            Utils.ShowWarningMessageBox(this.ServiceProvider, ex.Message);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Utils.ShowWarningMessageBox(this.ServiceProvider, ex.Message);
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

        private void AddItemsToProject(ProjectItems projectItems, IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                try
                {

                    var itemName = System.IO.Path.GetFileName(path);
                    if (System.IO.File.Exists(path))
                    {
                        if (Utils.GetProjectItem(projectItems, itemName) == null)
                            projectItems.AddFromFile(path);
                    }
                    else if (System.IO.Directory.Exists(path))
                    {
                        var childProjectItem = Utils.GetProjectItem(projectItems, itemName);
                        if (childProjectItem == null)
                            childProjectItem = projectItems.AddFromDirectory(path);
                        var childPaths = System.IO.Directory.GetFileSystemEntries(path);
                        AddItemsToProject(childProjectItem.ProjectItems, childPaths);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Item exists, ignore exception
                }
            }
        }

        private void MenuItemCallbackK8sAddSupport(object sender, EventArgs e)
        {
            try
            {
                var project = Utils.GetCurrentProject(this.ServiceProvider);
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
                var draftPath = System.IO.Path.Combine(Utils.GetBinariesDir(), "draft.exe");
                Utils.RunProcess(draftPath, String.Format("create . --pack {0}", packName), projectDir, false,
                                 Process_OutputDataReceived, Process_ErrorDataReceived, (s, e2) => {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate {
                        // Switch to main thread
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        try
                        {
                            var p = (System.Diagnostics.Process)s;
                            if (p.ExitCode != 0)
                            {
                                Utils.WriteToOutputWindow(this.ServiceProvider, "draft create failed");
                                //throw new Exception("draft create failed");
                            }
                            else
                            {
                                DisableDraftWatch(projectDir);
                                var paths = new List<string>();
                                foreach (var name in new[] { "Dockerfile", "draft.toml", ".draftignore", "chart" })
                                    paths.Add(System.IO.Path.Combine(projectDir, name));
                                AddItemsToProject(project.ProjectItems, paths);

                                //DTE dte = (DTE)this.ServiceProvider.GetService(typeof(DTE));
                                //dte.ExecuteCommand("View.ServerExplorer");
                                //dte.ExecuteCommand("View.Refresh");
                            }
                        }
                        catch (Exception ex)
                        {
                            Utils.ShowWarningMessageBox(this.ServiceProvider, ex.Message);
                        }
                    });
                });

                //if(!ProjectExists(solution, kubernetesProjectName))
                //    //CreateProject((Solution4)solution, kubernetesProjectName, false);
                //    CreateProjectFromTemplate((Solution4)solution, "KubernetesProjectTemplate.zip", "Yaml", kubernetesProjectName, true);
            }
            catch (Exception ex)
            {
                Utils.ShowWarningMessageBox(this.ServiceProvider, ex.Message);
            }
        }
    }
}
