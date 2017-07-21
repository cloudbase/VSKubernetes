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
        private static readonly Guid projectTypeNodeJs = new Guid("9092AA53-FB77-4645-B42D-1CCCA6BD08BD");

        private static IDictionary<Guid, string> vsProjectTypeToPackNameMap = new Dictionary <Guid, string> {
            { projectTypeCSharpCore, "dotnetcore" },
            { projectTypeNodeJs, "node" }
        };

        public const string kubernetesProjectName = "Kubernetes";

        private readonly Package package;

        private volatile bool minikubeDeploymentRunning = false;
        private volatile bool draftCreateRunning = false;
        private volatile bool draftUpRunning = false;

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

        private bool ProjectHasHelmChart(Project project)
        {
            var item = Utils.GetProjectItem(project, "chart");
            return (item != null && VSConstants.GUID_ItemType_PhysicalFolder == new Guid(item.Kind));
        }

        private void OnBeforeQueryStatusK8sAddSupport(object sender, EventArgs e)
        {
            OleMenuCommand item = (OleMenuCommand)sender;
            var project = Utils.GetCurrentProject(this.ServiceProvider);

            item.Visible = true;
            item.Enabled = !ProjectHasHelmChart(project) && !draftUpRunning && !draftCreateRunning;
        }

        private void OnBeforeQueryStatusK8sMinikubeDeploy(object sender, EventArgs e)
        {
            OleMenuCommand item = (OleMenuCommand)sender;
            item.Visible = true;
            item.Enabled = !minikubeDeploymentRunning && !draftCreateRunning;
        }

        private void OnBeforeQueryStatusK8sDeploy(object sender, EventArgs e)
        {
            OleMenuCommand item = (OleMenuCommand)sender;
            var project = Utils.GetCurrentProject(this.ServiceProvider);

            item.Visible = true;
            item.Enabled = ProjectHasHelmChart(project) && !draftUpRunning && !draftCreateRunning && !minikubeDeploymentRunning;
        }

        public static KubernetesCommand Instance
        {
            get;
            private set;
        }

        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        public static void Initialize(Package package)
        {
            Instance = new KubernetesCommand(package);
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
            var bar = GetStatusBar();
            int frozen;
            bar.IsFrozen(out frozen);

            if (frozen != 0)
            {
                bar.FreezeOutput(0);
            }
            bar.SetText("Deploying Minikube...");
            bar.FreezeOutput(1);

            minikubeDeploymentRunning = true;

            Kubernetes.DeployMinikube(Process_OutputDataReceived, Process_ErrorDataReceived, (s, e2) => {
                ThreadHelper.JoinableTaskFactory.Run(async delegate {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    try
                    {
                        minikubeDeploymentRunning = false;

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

                draftCreateRunning = true;

                Kubernetes.DraftUp(projectDir, Process_OutputDataReceived, Process_ErrorDataReceived, (s, e2) => {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        try
                        {
                            draftCreateRunning = false;

                            bar.FreezeOutput(0);
                            bar.Clear();
                            //bar.Animation(0, ref icon);
                            var p = (System.Diagnostics.Process)s;
                            if (p.ExitCode != 0)
                            {
                                var message = "draft up failed";
                                Utils.WriteToOutputWindow(this.ServiceProvider, message);
                                throw new Exception(message);
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

        private void MenuItemCallbackK8sAddSupport(object sender, EventArgs e)
        {
            try
            {
                var project = Utils.GetCurrentProject(this.ServiceProvider);
                var projectDir = System.IO.Path.GetDirectoryName(project.FullName);

                var projectKindGuid = new Guid(project.Kind);
                string packName = null;
                if (!vsProjectTypeToPackNameMap.TryGetValue(new Guid(project.Kind), out packName))
                {
                    throw new Exception("Unsupported project type. Only ASP.NET Core and Node.js projects are currently supported for now, more will be added soon!");
                }

                draftUpRunning = true;

                Kubernetes.DraftCreate(projectDir, packName, Process_OutputDataReceived, Process_ErrorDataReceived, (s, e2) => {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        try
                        {
                            draftUpRunning = false;

                            var p = (System.Diagnostics.Process)s;
                            if (p.ExitCode != 0)
                            {
                                var message = "draft create failed";
                                Utils.WriteToOutputWindow(this.ServiceProvider, message);
                                throw new Exception(message);
                            }
                            else
                            {
                                Kubernetes.DisableDraftWatch(projectDir);
                                var paths = new List<string>();
                                foreach (var name in new[] { "Dockerfile", "draft.toml", ".draftignore", "chart" })
                                    paths.Add(System.IO.Path.Combine(projectDir, name));
                                Utils.AddItemsToProject(project.ProjectItems, paths);
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
    }
}
