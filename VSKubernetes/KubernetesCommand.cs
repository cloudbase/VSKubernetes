using System;
using System.ComponentModel.Design;
using EnvDTE;
using EnvDTE100;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VSKubernetes
{
    internal sealed class KubernetesCommand
    {
        private const int K8sAddSupportCommandId = 0x0100;
        private const int K8sDeployCommandId = 0x0101;
        private const int K8sDeployMinikubeCommandId = 0x0102;
        private const int K8sDebugCommandId = 0x0103;
        private const uint cmdidMyDropDownCombo = 0x101;
        private const uint cmdidMyDropDownComboGetList = 0x102;

        /*
        public static readonly Guid projectTypeCSharp = new Guid("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC");
        public static readonly Guid projectTypeVBNet = new Guid("F184B08F-C81C-45F6-A57F-5ABD9991F28F");
        public static readonly Guid projectTypeVCpp = new Guid("8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942");
        public static readonly Guid projectTypeDockerCompose = new Guid("E53339B2-1760-4266-BCC7-CA923CBCF16C");
        */

        private static readonly Guid projectTypeCSharpCore = new Guid("9A19103F-16F7-4668-BE54-9A1E7A4F7556");
        private static readonly Guid projectTypeNodeJs = new Guid("9092AA53-FB77-4645-B42D-1CCCA6BD08BD");

        private static IDictionary<Guid, string> vsProjectTypeToPackNameMap = new Dictionary <Guid, string> {
            { projectTypeCSharpCore, @"github.com\Azure\draft\packs\csharp" },
            { projectTypeNodeJs, @"github.com\Azure\draft\packs\javascript" }
        };

        public const string kubernetesProjectName = "Kubernetes";

        private readonly Package package;

        private volatile bool minikubeDeploymentRunning = false;
        private volatile bool draftCreateRunning = false;
        private volatile bool draftUpRunning = false;
        private volatile bool draftConnectRunning = false;

        private string currentKubernetesContext = null;

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

                menuCommandID = new CommandID(Constants.guidCommandSet, K8sDebugCommandId);
                menuItem = new OleMenuCommand(this.MenuItemCallbackK8sDebug, menuCommandID);
                menuItem.BeforeQueryStatus += new EventHandler(this.OnBeforeQueryStatusK8sDebug);
                commandService.AddCommand(menuItem);

                CommandID menuMyDropDownComboCommandID = new CommandID(Constants.guidComboBoxCmdSet, (int)cmdidMyDropDownCombo);
                OleMenuCommand menuMyDropDownComboCommand = new OleMenuCommand(new EventHandler(OnMenuMyDropDownCombo), menuMyDropDownComboCommandID);
                commandService.AddCommand(menuMyDropDownComboCommand);

                CommandID menuMyDropDownComboGetListCommandID = new CommandID(Constants.guidComboBoxCmdSet, (int)cmdidMyDropDownComboGetList);
                MenuCommand menuMyDropDownComboGetListCommand = new OleMenuCommand(new EventHandler(OnMenuMyDropDownComboGetList), menuMyDropDownComboGetListCommandID);
                commandService.AddCommand(menuMyDropDownComboGetListCommand);
            }
        }

        private void OnMenuMyDropDownComboGetList(object sender, EventArgs e)
        {
            OleMenuCmdEventArgs eventArgs = e as OleMenuCmdEventArgs;

            if (eventArgs != null)
            {
                object inParam = eventArgs.InValue;
                IntPtr vOut = eventArgs.OutValue;

                if (inParam != null)
                {
                    throw (new ArgumentException());
                }
                else if (vOut != IntPtr.Zero)
                {
                    var contextNames = Kubernetes.GetContextNames();
                    Marshal.GetNativeVariantForObject(contextNames, vOut);
                }
                else
                {
                    throw (new ArgumentException());
                }
            }
        }

        private void OnMenuMyDropDownCombo(object sender, EventArgs e)
        {
            OleMenuCmdEventArgs eventArgs = e as OleMenuCmdEventArgs;

            if (eventArgs != null)
            {
                string newChoice = eventArgs.InValue as string;
                IntPtr vOut = eventArgs.OutValue;

                if (vOut != IntPtr.Zero)
                {
                    if(currentKubernetesContext == null)
                        currentKubernetesContext = Kubernetes.GetCurrentContext();

                    // when vOut is non-NULL, the IDE is requesting the current value for the combo
                    Marshal.GetNativeVariantForObject(currentKubernetesContext, vOut);
                }

                else if (newChoice != null)
                {
                    var contextNames = Kubernetes.GetContextNames();

                    // new value was selected or typed in
                    // see if it is one of our items
                    bool validInput = false;
                    int indexInput = -1;
                    for (indexInput = 0; indexInput < contextNames.Length; indexInput++)
                    {
                        if (string.Compare(contextNames[indexInput], newChoice, StringComparison.CurrentCultureIgnoreCase) == 0)
                        {
                            validInput = true;
                            break;
                        }
                    }

                    if (validInput)
                    {
                        var newCurrentContext = contextNames[indexInput];
                        Kubernetes.SetCurrentContext(newCurrentContext);
                        currentKubernetesContext = newCurrentContext;
                    }
                    else
                    {
                        throw (new ArgumentException()); // force an exception to be thrown
                    }
                }
            }
            else
            {
                // We should never get here; EventArgs are required.
                throw (new ArgumentException()); // force an exception to be thrown
            }
        }

        private bool ProjectHasHelmChart(Project project)
        {
            var item = Utils.GetProjectItem(project, "charts");
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
            item.Enabled = ProjectHasHelmChart(project) && !draftConnectRunning && !draftUpRunning &&
                !draftCreateRunning && !minikubeDeploymentRunning;
        }

        private void OnBeforeQueryStatusK8sDebug(object sender, EventArgs e)
        {
            OleMenuCommand item = (OleMenuCommand)sender;
            var project = Utils.GetCurrentProject(this.ServiceProvider);

            // Only CSharpCore is supported for the moment
            var debugSupported = new Guid(project.Kind) == projectTypeCSharpCore;

            item.Visible = true;
            item.Enabled = debugSupported && ProjectHasHelmChart(project) && !draftConnectRunning && !draftUpRunning &&
                !draftCreateRunning && !minikubeDeploymentRunning;
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

        private IVsStatusbar GetStatusBar(bool unfreeze = true)
        {
            var bar = this.ServiceProvider.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            if (unfreeze)
            {
                int frozen;
                bar.IsFrozen(out frozen);
                if (frozen != 0)
                {
                    bar.FreezeOutput(0);
                }
            }
            return bar;
        }

        private void MenuItemCallbackK8sMinikubeDeploy(object sender, EventArgs e)
        {
            var bar = GetStatusBar();
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
                var minikubeDockerEnv = true;

                var bar = GetStatusBar();

                //object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_Deploy;
                //bar.Animation(1, ref icon);
                bar.SetText("Deploying Kubernetes Helm Chart...");
                bar.FreezeOutput(1);

                if (new Guid(project.Kind) == projectTypeCSharpCore)
                {
                    // TODO: add abstraction :)
                    Utils.WriteToOutputWindow(this.ServiceProvider, "Generating SSH key...");
                    var sshKeyPath = System.IO.Path.Combine(projectDir, "id_rsa_vscode");
                    Utils.GenerateSSHKeypair(sshKeyPath, Process_OutputDataReceived, Process_ErrorDataReceived);
                }

                Utils.WriteToOutputWindow(this.ServiceProvider, "Starting draft up...");

                draftCreateRunning = true;

                Kubernetes.DraftUp(projectDir, minikubeDockerEnv, Process_OutputDataReceived, Process_ErrorDataReceived, (s, e2) => {
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

        private string NormalizeAppName(string name)
        {
            return name.Replace(" ", "_").ToLower();
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

                var appName = NormalizeAppName(project.Name);
                draftUpRunning = true;

                Kubernetes.DraftCreate(projectDir, packName, appName, Process_OutputDataReceived, Process_ErrorDataReceived, (s, e2) => {
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
                                foreach (var name in new[] { "Dockerfile", "draft.toml", ".draftignore", "charts" })
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

        private void MenuItemCallbackK8sDebug(object sender, EventArgs e)
        {
            var bar = GetStatusBar();
            bar.SetText("Connecting to your Kubernetes deployment...");
            bar.FreezeOutput(1);

            // TODO: check if port is available
            int port = new Random().Next(10000, 20000);
            var project = Utils.GetCurrentProject(this.ServiceProvider);
            var projectDir = System.IO.Path.GetDirectoryName(project.FullName);

            var portMappings = new List<KeyValuePair<int, int>>();
            portMappings.Add(new KeyValuePair<int, int>(port, 22));
            // TODO: make configurable
            portMappings.Add(new KeyValuePair<int, int>(8080, 80));

            draftConnectRunning = true;
            var connectProcess = Kubernetes.DraftConnect(projectDir, portMappings, Process_OutputDataReceived, Process_ErrorDataReceived);
            try
            {
                bar.FreezeOutput(0);
                var dbg = new DotNetCoreDebug(this.ServiceProvider, "localhost", port, () => {
                    if (!connectProcess.HasExited)
                        connectProcess.Kill();
                    draftConnectRunning = false;
                });
                dbg.StartDebugging();
            }
            catch (Exception ex)
            {
                bar.FreezeOutput(0);
                if (!connectProcess.HasExited)
                    connectProcess.Kill();
                draftConnectRunning = false;
                Utils.ShowWarningMessageBox(this.ServiceProvider, ex.Message);
            }
        }
    }
}
